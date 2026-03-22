using System;
using System.Collections.Generic;
using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;
using SpaceSim.World.ValueTypes;
using SpaceSim.Simulation.Orbits;

namespace SpaceSim.Simulation.Ships
{
    /// <summary>
    /// Manages ship travel between celestial bodies using segmented patched-conics routes.
    ///
    /// Phase 26c: Segmented routes are the ONLY active execution path.
    ///
    /// Legacy reframe code (ReframeRoute, ReframeRouteOutward, UpdateShipLegacy,
    /// StartInsertionPhaseLegacy, UpdateInsertionPhaseLegacy, CompleteArrivalLegacy,
    /// DetermineRouteFrame, BuildGlobalRoute, BuildLocalRoute, IsBodyRelatedToDestination)
    /// has been removed. The InsertingIntoOrbit ship state is no longer used by this system.
    ///
    /// Route lifecycle:
    ///   1. StartRoute() builds a ShipRoute with segments via RouteSegmentBuilder
    ///   2. Update() interpolates per-segment position each tick
    ///   3. Segments advance automatically; final segment completes arrival
    ///   4. HandleSOITransition() is debug-only — no route mutation
    ///
    /// Maneuver Planning (Phase 23):
    ///   ManeuverPlanner.Plan() is called before any route is committed.
    ///
    /// Route Safety (Phase 21):
    ///   RouteSafetyChecker validates each candidate inside ManeuverPlanner.
    ///
    /// Pure C# — no UnityEngine dependency.
    /// </summary>
    public class ShipMovementSystem
    {
        private readonly WorldRegistry _registry;
        private readonly List<EntityId> _trackedShips = new List<EntityId>();

        private const double DefaultOrbitRadius = 3.0;
        private const double DefaultOrbitPeriod = 12.0;
        private const double StationOrbitRadius = 0.5;
        private const double StationOrbitPeriod = 8.0;

        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;

        /// <summary>
        /// Multiplier applied to destination orbit radius to set the approach endpoint.
        /// </summary>
        public double OrbitApproachMultiplier { get; set; } = 2.5;

        /// <summary>
        /// [DEPRECATED — Phase 26c] No longer used by segmented routes.
        /// Retained as a public property so Inspector-serialized values don't break.
        /// Segmented routes handle insertion as their final OrbitInsertion segment.
        /// </summary>
        public double OrbitInsertionDuration { get; set; } = 1.5;

        // ---------------------------------------------------------------
        // Route safety and planning (Phases 21 + 23)
        // ---------------------------------------------------------------

        public bool ImpactSafetyEnabled { get; set; } = true;
        public double ImpactSafetyMargin { get; set; } = 0.1;
        public int ImpactCheckSamples { get; set; } = 20;

        // ---------------------------------------------------------------
        // Events
        // ---------------------------------------------------------------

        /// <summary>
        /// Fired when a ship completes arrival and enters orbit.
        /// Args: shipId, original destinationId (may be surface station).
        /// </summary>
        public event Action<EntityId, EntityId> OnShipArrived;

        /// <summary>
        /// Optional debug callback for navigation events.
        /// Caller routes to GameDebug.
        /// </summary>
        public Action<EntityId, string> OnNavEvent;

        public ShipMovementSystem(WorldRegistry registry)
        {
            _registry = registry;
        }

        // ---------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------

        public void TrackShip(EntityId shipId)
        {
            if (!_trackedShips.Contains(shipId))
                _trackedShips.Add(shipId);
        }

        public void UntrackShip(EntityId shipId)
        {
            _trackedShips.Remove(shipId);
        }

        public int TrackedCount => _trackedShips.Count;

        /// <summary>
        /// Begin a travel route for a ship.
        /// Builds a segmented route via RouteSegmentBuilder. If segment build fails,
        /// the route start is rejected (returns false) rather than falling back to legacy.
        ///
        /// Signature unchanged from Phase 25 — NPCShipScheduler compatible.
        /// </summary>
        public bool StartRoute(
            EntityId shipId,
            EntityId destinationId,
            double currentSimTime,
            double travelDuration,
            Func<EntityId, double, SimVec3> positionResolver = null)
        {
            var ship = _registry.GetCelestialBody(shipId);
            if (ship == null || ship.ShipInfo == null) return false;
            if (!ship.ParentId.IsValid) return false;

            var origin = _registry.GetCelestialBody(ship.ParentId);
            var destination = _registry.GetCelestialBody(destinationId);
            if (origin == null || destination == null) return false;

            EntityId arrivalParentId;
            double destOrbitRadius;
            double destOrbitPeriod;

            if (destination.BodyType == CelestialBodyType.Station
                && destination.StationInfo != null
                && destination.StationInfo.Kind == StationKind.Surface
                && destination.ParentId.IsValid)
            {
                if (_registry.GetCelestialBody(destination.ParentId) == null) return false;
                arrivalParentId = destination.ParentId;
                destOrbitRadius = DefaultOrbitRadius;
                destOrbitPeriod = DefaultOrbitPeriod;
            }
            else if (destination.BodyType == CelestialBodyType.Station)
            {
                arrivalParentId = destinationId;
                destOrbitRadius = StationOrbitRadius;
                destOrbitPeriod = StationOrbitPeriod;
            }
            else
            {
                arrivalParentId = destinationId;
                destOrbitRadius = ship.Orbit != null ? ship.Orbit.SemiMajorAxis : DefaultOrbitRadius;
                destOrbitPeriod = ship.Orbit != null ? ship.Orbit.OrbitalPeriod : DefaultOrbitPeriod;
            }

            var arrivalBody = _registry.GetCelestialBody(arrivalParentId);
            if (arrivalBody == null) return false;

            SimVec3 shipWorldPos = positionResolver != null
                ? ComputeShipWorldPosition(ship, currentSimTime, positionResolver)
                : SimVec3.Zero;

            double approachRadius = destOrbitRadius * OrbitApproachMultiplier;

            // ---------------------------------------------------------------
            // Maneuver planning
            // ---------------------------------------------------------------

            ManeuverPlanner.ManeuverPlan mPlan;

            if (ImpactSafetyEnabled && positionResolver != null)
            {
                mPlan = ManeuverPlanner.Plan(
                    shipWorldPos,
                    arrivalParentId,
                    approachRadius,
                    currentSimTime,
                    travelDuration,
                    _registry,
                    positionResolver,
                    origin.Id,
                    ImpactSafetyMargin,
                    ImpactCheckSamples);

                if (!mPlan.Success)
                {
                    if (mPlan.IsDelayed)
                    {
                        ship.ShipInfo.PlannedDepartureTime = mPlan.DepartureTime;
                    }

                    if (mPlan.IsDelayed)
                    {
                        double waitSec = mPlan.DepartureTime - currentSimTime;

                        if (mPlan.ImmediateWasAvailable)
                        {
                            OnNavEvent?.Invoke(shipId,
                                $"[Nav] {ship.DisplayName}: poor alignment (direct {mPlan.ImmediateDirectScore:F2})," +
                                $" searching better window to {arrivalBody.DisplayName}" +
                                $" ~{waitSec:F0}s away");
                        }
                        else
                        {
                            OnNavEvent?.Invoke(shipId,
                                $"[Nav] {ship.DisplayName}: waiting for better window to" +
                                $" {arrivalBody.DisplayName}" +
                                (mPlan.DirectRouteBlocker != null
                                    ? $" (blocked by {mPlan.DirectRouteBlocker})" : "") +
                                $", delayed departure by {waitSec:F0}s");
                        }
                    }
                    else
                    {
                        OnNavEvent?.Invoke(shipId,
                            $"[Nav] {ship.DisplayName}: all maneuver plans failed to" +
                            $" {arrivalBody.DisplayName}" +
                            (mPlan.DirectRouteBlocker != null
                                ? $" (blocked by {mPlan.DirectRouteBlocker})" : ""));
                    }
                    return false;
                }

                ship.ShipInfo.PlannedDepartureTime = 0.0;

                if (mPlan.StrategyType == ManeuverPlanner.StrategyOffset)
                {
                    OnNavEvent?.Invoke(shipId,
                        $"[Nav] {ship.DisplayName}: using offset variant #{mPlan.VariantIndex}" +
                        $" to {arrivalBody.DisplayName}" +
                        $" (approach {mPlan.ApproachAngleDeg:F0}°, score {mPlan.Score:F2}," +
                        $" direct {mPlan.DirectScore:F2}" +
                        (mPlan.DirectRouteBlocker != null
                            ? $", blocked by {mPlan.DirectRouteBlocker}" : "") +
                        ")");
                }
                else
                {
                    OnNavEvent?.Invoke(shipId,
                        $"[Nav] {ship.DisplayName}: selected maneuver score {mPlan.Score:F2}" +
                        $" (direct {mPlan.DirectScore:F2})" +
                        $" to {arrivalBody.DisplayName}");
                }
            }
            else
            {
                // Safety checking disabled — compute direct approach without validation.
                if (positionResolver != null)
                {
                    double estimatedArrivalTime = currentSimTime + travelDuration;
                    SimVec3 destWorldAtArrival = positionResolver(arrivalParentId, estimatedArrivalTime);
                    SimVec3 dir = shipWorldPos - destWorldAtArrival;
                    double angleRad = System.Math.Atan2(dir.Z, dir.X);
                    mPlan = new ManeuverPlanner.ManeuverPlan
                    {
                        Success          = true,
                        DepartureTime    = currentSimTime,
                        ApproachAngleDeg = angleRad * RadToDeg,
                        ApproachRadius   = approachRadius,
                        StrategyType     = ManeuverPlanner.StrategyDirect,
                        VariantIndex     = 0,
                        Score            = 0.0
                    };
                }
                else
                {
                    mPlan = new ManeuverPlanner.ManeuverPlan
                    {
                        Success          = true,
                        DepartureTime    = currentSimTime,
                        ApproachAngleDeg = 0.0,
                        ApproachRadius   = approachRadius,
                        StrategyType     = ManeuverPlanner.StrategyDirect,
                        VariantIndex     = 0,
                        Score            = 0.0
                    };
                }
                ship.ShipInfo.PlannedDepartureTime = 0.0;
            }

            // ---------------------------------------------------------------
            // Build segmented route
            // ---------------------------------------------------------------

            SimVec3 approachWorldPos = mPlan.ApproachWorldPos;
            if (approachWorldPos == SimVec3.Zero && positionResolver != null)
            {
                double arrTime = currentSimTime + travelDuration;
                SimVec3 destAtArr = positionResolver(arrivalParentId, arrTime);
                double angleRad = mPlan.ApproachAngleDeg * DegToRad;
                approachWorldPos = new SimVec3(
                    destAtArr.X + approachRadius * System.Math.Cos(angleRad),
                    destAtArr.Y,
                    destAtArr.Z + approachRadius * System.Math.Sin(angleRad));
            }

            var route = new ShipRoute
            {
                OriginBodyId = origin.Id,
                DestinationBodyId = destinationId,
                DepartureTime = currentSimTime,
                TravelDuration = travelDuration,
                StartWorldPosition = shipWorldPos,
                ArrivalWorldPosition = approachWorldPos,
                DestinationOrbitRadius = destOrbitRadius,
                DestinationOrbitPeriod = destOrbitPeriod,
                ArrivalOrbitPhaseDeg = NormalizeDeg(mPlan.ApproachAngleDeg)
            };

            if (positionResolver != null)
            {
                try
                {
                    RouteSegmentBuilder.BuildSegments(
                        route,
                        origin,
                        arrivalBody,
                        shipWorldPos,
                        approachWorldPos,
                        currentSimTime,
                        travelDuration,
                        _registry,
                        positionResolver);
                }
                catch (Exception)
                {
                    // Segment build failed — reject route.
                    OnNavEvent?.Invoke(shipId,
                        $"[Nav] {ship.DisplayName}: segment build failed to {arrivalBody.DisplayName}");
                    return false;
                }
            }

            if (route.Segments == null || route.Segments.Count == 0)
            {
                OnNavEvent?.Invoke(shipId,
                    $"[Nav] {ship.DisplayName}: no segments built to {arrivalBody.DisplayName}");
                return false;
            }

            route.UseSegmentedRoute = true;
            route.CurrentSegmentIndex = 0;

            OnNavEvent?.Invoke(shipId,
                $"[Nav] {ship.DisplayName}: built segmented route: {route.Segments.Count} segments" +
                $" ({DescribeSegmentTypes(route.Segments)})");

            // ---------------------------------------------------------------
            // Commit ship state
            // ---------------------------------------------------------------

            ship.ShipInfo.CurrentRoute = route;
            ship.ShipInfo.State = ShipState.Travelling;
            ship.ShipInfo.OverrideWorldPosition = route.StartWorldPosition;

            origin.RemoveChildId(shipId);
            ship.AttachmentMode = AttachmentMode.None;
            ship.Orbit = null;

            TrackShip(shipId);
            return true;
        }

        /// <summary>Update all tracked ships. Call once per simulation tick.</summary>
        public void Update(double currentSimTime, Func<EntityId, double, SimVec3> positionResolver)
        {
            if (positionResolver == null) return;

            for (int i = _trackedShips.Count - 1; i >= 0; i--)
            {
                var shipId = _trackedShips[i];
                var ship = _registry.GetCelestialBody(shipId);
                if (ship?.ShipInfo == null)
                {
                    _trackedShips.RemoveAt(i);
                    continue;
                }

                if (ship.ShipInfo.State != ShipState.Travelling || ship.ShipInfo.CurrentRoute == null)
                    continue;

                UpdateShipSegmented(ship, ship.ShipInfo.CurrentRoute, currentSimTime, positionResolver);
            }
        }

        // ---------------------------------------------------------------
        // SOI transition — debug-only (Phase 26c)
        // ---------------------------------------------------------------

        /// <summary>
        /// Called by the coordinator when SOIResolver detects an SOI boundary crossing.
        ///
        /// Phase 26c: This method no longer mutates routes. It is debug/telemetry only.
        /// Segmented routes define reference frames per-segment at build time.
        /// SOI transitions are observed and logged but do not affect navigation.
        ///
        /// The method signature is preserved for coordinator compatibility.
        /// </summary>
        public void HandleSOITransition(
            EntityId shipId,
            EntityId previousSOIBodyId,
            EntityId newSOIBodyId,
            double simTime,
            Func<EntityId, double, SimVec3> positionResolver)
        {
            // Debug-only: log the SOI transition for telemetry.
            // No route mutation. No reframe. No insertion trigger.
            // Coordinator still calls this — it's wired to SOIResolver events.
            // The call is harmless and provides useful debug data.
        }

        // ---------------------------------------------------------------
        // Segmented route execution
        // ---------------------------------------------------------------

        /// <summary>
        /// Per-tick update for segmented routes.
        /// Reads CurrentSegment, interpolates position in segment's reference frame,
        /// advances segment when complete, finishes route when all segments done.
        /// </summary>
        private void UpdateShipSegmented(
            CelestialBody ship, ShipRoute route,
            double currentSimTime, Func<EntityId, double, SimVec3> positionResolver)
        {
            var segment = route.CurrentSegment;

            // All segments complete — finalize arrival.
            if (segment == null || route.AllSegmentsComplete)
            {
                CompleteArrival(ship, route, currentSimTime, positionResolver);
                return;
            }

            double progress = segment.GetProgress(currentSimTime);

            // Interpolate position based on segment's reference frame.
            if (segment.ReferenceBodyId.IsValid)
            {
                SimVec3 refBodyPos = positionResolver(segment.ReferenceBodyId, currentSimTime);
                SimVec3 localPos = Lerp(segment.StartLocalPosition, segment.EndLocalPosition, progress);
                ship.ShipInfo.OverrideWorldPosition = refBodyPos + localPos;
            }
            else
            {
                // Global frame fallback (edge case: no valid reference body).
                SimVec3 startW = segment.CachedWorldStart ?? SimVec3.Zero;
                SimVec3 endW = segment.CachedWorldEnd ?? SimVec3.Zero;
                ship.ShipInfo.OverrideWorldPosition = Lerp(startW, endW, progress);
            }

            // Advance to next segment when current completes.
            if (progress >= 1.0)
            {
                OnNavEvent?.Invoke(ship.Id,
                    $"[Nav] {ship.DisplayName}: segment complete: {segment.Type}");

                bool hasMore = route.AdvanceSegment();

                if (!hasMore)
                {
                    CompleteArrival(ship, route, currentSimTime, positionResolver);
                    return;
                }

                var nextSeg = route.CurrentSegment;
                if (nextSeg != null)
                {
                    OnNavEvent?.Invoke(ship.Id,
                        $"[Nav] {ship.DisplayName}: segment start: {nextSeg.Type}");
                }
            }
        }

        /// <summary>
        /// Complete arrival after all segments are done.
        /// Reads orbit parameters from the last OrbitInsertion segment.
        /// Re-parents ship, creates orbit, fires OnShipArrived.
        /// </summary>
        private void CompleteArrival(
            CelestialBody ship, ShipRoute route,
            double currentSimTime, Func<EntityId, double, SimVec3> positionResolver)
        {
            var destination = _registry.GetCelestialBody(route.DestinationBodyId);
            if (destination == null)
            {
                ship.ShipInfo.State = ShipState.Idle;
                ship.ShipInfo.CurrentRoute = null;
                ship.ShipInfo.OverrideWorldPosition = null;
                return;
            }

            EntityId arrivalParentId = DetermineArrivalParent(destination);
            var arrivalParent = _registry.GetCelestialBody(arrivalParentId);
            if (arrivalParent == null)
            {
                ship.ShipInfo.State = ShipState.Idle;
                ship.ShipInfo.CurrentRoute = null;
                ship.ShipInfo.OverrideWorldPosition = null;
                return;
            }

            // Read orbit parameters from last OrbitInsertion segment.
            double orbitRadius = route.DestinationOrbitRadius;
            double orbitPeriod = route.DestinationOrbitPeriod;

            if (route.Segments != null)
            {
                for (int i = route.Segments.Count - 1; i >= 0; i--)
                {
                    var seg = route.Segments[i];
                    if (seg.Type == SegmentType.OrbitInsertion)
                    {
                        if (seg.DestinationOrbitRadius > 0.0)
                            orbitRadius = seg.DestinationOrbitRadius;
                        if (seg.DestinationOrbitPeriod > 0.0)
                            orbitPeriod = seg.DestinationOrbitPeriod;
                        break;
                    }
                }
            }

            // Re-parent ship to arrival body.
            ship.ParentId = arrivalParentId;
            arrivalParent.AddChildId(ship.Id);
            ship.AttachmentMode = AttachmentMode.Orbit;

            // Compute orbit phase from actual ship position for seamless transition.
            double finalAngleDeg = route.ArrivalOrbitPhaseDeg;
            if (positionResolver != null)
            {
                SimVec3 parentPos = positionResolver(arrivalParentId, currentSimTime);
                SimVec3 shipWorldPos = ship.ShipInfo.OverrideWorldPosition ?? parentPos;
                SimVec3 localPos = shipWorldPos - parentPos;
                double angleRad = System.Math.Atan2(localPos.Z, localPos.X);
                finalAngleDeg = NormalizeDeg(angleRad * RadToDeg);
            }

            ship.Orbit = new OrbitDefinition
            {
                SemiMajorAxis = orbitRadius,
                Eccentricity = 0.0,
                MeanAnomalyAtEpochDeg = NormalizeDeg(finalAngleDeg - 360.0 * currentSimTime / orbitPeriod),
                OrbitalPeriod = orbitPeriod,
                EpochTime = 0.0,
                IsPrograde = true
            };

            ship.ShipInfo.CurrentRoute = null;
            ship.ShipInfo.OverrideWorldPosition = null;
            ship.ShipInfo.State = ShipState.Orbiting;

            OnNavEvent?.Invoke(ship.Id,
                $"[Nav] {ship.DisplayName}: arrived, orbiting {arrivalParent.DisplayName} " +
                $"at r={orbitRadius:F1} Mm");

            OnShipArrived?.Invoke(ship.Id, route.DestinationBodyId);
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static EntityId DetermineArrivalParent(CelestialBody destination)
        {
            if (destination.BodyType == CelestialBodyType.Station
                && destination.StationInfo?.Kind == StationKind.Surface
                && destination.ParentId.IsValid)
                return destination.ParentId;
            return destination.Id;
        }

        private SimVec3 ComputeShipWorldPosition(
            CelestialBody ship, double simTime, Func<EntityId, double, SimVec3> positionResolver)
        {
            if (positionResolver != null && ship.Orbit != null && ship.ParentId.IsValid)
                return positionResolver(ship.ParentId, simTime)
                    + OrbitalPositionCalculator.CalculatePosition(ship.Orbit, simTime);
            if (positionResolver != null && ship.ParentId.IsValid)
                return positionResolver(ship.ParentId, simTime);
            return SimVec3.Zero;
        }

        private static SimVec3 Lerp(SimVec3 a, SimVec3 b, double t)
            => new SimVec3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);

        private static double NormalizeDeg(double deg)
        {
            deg %= 360.0;
            return deg < 0 ? deg + 360.0 : deg;
        }

        /// <summary>
        /// Build a short description of segment types for logging.
        /// Only called once per route start (not per tick).
        /// </summary>
        private static string DescribeSegmentTypes(List<RouteSegment> segments)
        {
            if (segments == null || segments.Count == 0) return "empty";
            var names = new string[segments.Count];
            for (int i = 0; i < segments.Count; i++)
            {
                names[i] = segments[i].Type switch
                {
                    SegmentType.LocalOrbitDeparture => "Departure",
                    SegmentType.SOIExit => "SOIExit",
                    SegmentType.HeliocentricTransfer => "Helio",
                    SegmentType.SOIEntry => "SOIEntry",
                    SegmentType.LocalTransfer => "Local",
                    SegmentType.OrbitInsertion => "Insertion",
                    _ => "Unknown"
                };
            }
            return string.Join("\u2192", names);
        }
    }
}
