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
    /// Manages ship travel using segmented patched-conics routes.
    /// Phase 26d: OrbitInsertion uses quadratic Bezier for smooth tangential approach.
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

        public double OrbitApproachMultiplier { get; set; } = 2.5;
        /// <summary>[DEPRECATED] Retained for Inspector. Not used by segmented routes.</summary>
        public double OrbitInsertionDuration { get; set; } = 1.5;

        public bool ImpactSafetyEnabled { get; set; } = true;
        public double ImpactSafetyMargin { get; set; } = 0.1;
        public int ImpactCheckSamples { get; set; } = 20;

        public event Action<EntityId, EntityId> OnShipArrived;
        public Action<EntityId, string> OnNavEvent;

        public ShipMovementSystem(WorldRegistry registry) { _registry = registry; }

        public void TrackShip(EntityId shipId) { if (!_trackedShips.Contains(shipId)) _trackedShips.Add(shipId); }
        public void UntrackShip(EntityId shipId) { _trackedShips.Remove(shipId); }
        public int TrackedCount => _trackedShips.Count;

        // ---------------------------------------------------------------
        // StartRoute
        // ---------------------------------------------------------------

        public bool StartRoute(
            EntityId shipId, EntityId destinationId,
            double currentSimTime, double travelDuration,
            Func<EntityId, double, SimVec3> positionResolver = null)
        {
            var ship = _registry.GetCelestialBody(shipId);
            if (ship == null || ship.ShipInfo == null) return false;
            if (!ship.ParentId.IsValid) return false;

            var origin = _registry.GetCelestialBody(ship.ParentId);
            var destination = _registry.GetCelestialBody(destinationId);
            if (origin == null || destination == null) return false;

            EntityId arrivalParentId;
            double destOrbitRadius, destOrbitPeriod;

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
                ? ComputeShipWorldPosition(ship, currentSimTime, positionResolver) : SimVec3.Zero;
            double approachRadius = destOrbitRadius * OrbitApproachMultiplier;

            // --- Maneuver planning ---
            ManeuverPlanner.ManeuverPlan mPlan;

            if (ImpactSafetyEnabled && positionResolver != null)
            {
                mPlan = ManeuverPlanner.Plan(
                    shipWorldPos, arrivalParentId, approachRadius,
                    currentSimTime, travelDuration, _registry, positionResolver,
                    origin.Id, ImpactSafetyMargin, ImpactCheckSamples);

                if (!mPlan.Success)
                {
                    if (mPlan.IsDelayed) ship.ShipInfo.PlannedDepartureTime = mPlan.DepartureTime;

                    if (mPlan.IsDelayed)
                    {
                        double w = mPlan.DepartureTime - currentSimTime;
                        if (mPlan.ImmediateWasAvailable)
                            OnNavEvent?.Invoke(shipId, $"[Nav] {ship.DisplayName}: poor alignment (direct {mPlan.ImmediateDirectScore:F2}), searching better window to {arrivalBody.DisplayName} ~{w:F0}s away");
                        else
                            OnNavEvent?.Invoke(shipId, $"[Nav] {ship.DisplayName}: waiting for better window to {arrivalBody.DisplayName}{(mPlan.DirectRouteBlocker != null ? $" (blocked by {mPlan.DirectRouteBlocker})" : "")}, delayed departure by {w:F0}s");
                    }
                    else
                    {
                        OnNavEvent?.Invoke(shipId, $"[Nav] {ship.DisplayName}: all maneuver plans failed to {arrivalBody.DisplayName}{(mPlan.DirectRouteBlocker != null ? $" (blocked by {mPlan.DirectRouteBlocker})" : "")}");
                    }
                    return false;
                }

                ship.ShipInfo.PlannedDepartureTime = 0.0;

                if (mPlan.StrategyType == ManeuverPlanner.StrategyOffset)
                    OnNavEvent?.Invoke(shipId, $"[Nav] {ship.DisplayName}: using offset variant #{mPlan.VariantIndex} to {arrivalBody.DisplayName} (approach {mPlan.ApproachAngleDeg:F0}°, score {mPlan.Score:F2}, direct {mPlan.DirectScore:F2}{(mPlan.DirectRouteBlocker != null ? $", blocked by {mPlan.DirectRouteBlocker}" : "")})");
                else
                    OnNavEvent?.Invoke(shipId, $"[Nav] {ship.DisplayName}: selected maneuver score {mPlan.Score:F2} (direct {mPlan.DirectScore:F2}) to {arrivalBody.DisplayName}");
            }
            else
            {
                if (positionResolver != null)
                {
                    SimVec3 destAtArr = positionResolver(arrivalParentId, currentSimTime + travelDuration);
                    SimVec3 dir = shipWorldPos - destAtArr;
                    mPlan = new ManeuverPlanner.ManeuverPlan { Success = true, DepartureTime = currentSimTime, ApproachAngleDeg = Math.Atan2(dir.Z, dir.X) * RadToDeg, ApproachRadius = approachRadius, StrategyType = ManeuverPlanner.StrategyDirect, VariantIndex = 0, Score = 0.0 };
                }
                else
                {
                    mPlan = new ManeuverPlanner.ManeuverPlan { Success = true, DepartureTime = currentSimTime, ApproachRadius = approachRadius, StrategyType = ManeuverPlanner.StrategyDirect };
                }
                ship.ShipInfo.PlannedDepartureTime = 0.0;
            }

            // --- Build segmented route ---
            SimVec3 approachWorldPos = mPlan.ApproachWorldPos;
            if (approachWorldPos == SimVec3.Zero && positionResolver != null)
            {
                SimVec3 destAtArr = positionResolver(arrivalParentId, currentSimTime + travelDuration);
                double aRad = mPlan.ApproachAngleDeg * DegToRad;
                approachWorldPos = new SimVec3(destAtArr.X + approachRadius * Math.Cos(aRad), destAtArr.Y, destAtArr.Z + approachRadius * Math.Sin(aRad));
            }

            var route = new ShipRoute
            {
                OriginBodyId = origin.Id, DestinationBodyId = destinationId,
                DepartureTime = currentSimTime, TravelDuration = travelDuration,
                StartWorldPosition = shipWorldPos, ArrivalWorldPosition = approachWorldPos,
                DestinationOrbitRadius = destOrbitRadius, DestinationOrbitPeriod = destOrbitPeriod,
                ArrivalOrbitPhaseDeg = NormalizeDeg(mPlan.ApproachAngleDeg)
            };

            if (positionResolver != null)
            {
                try
                {
                    RouteSegmentBuilder.BuildSegments(route, origin, arrivalBody, shipWorldPos, approachWorldPos, currentSimTime, travelDuration, _registry, positionResolver);
                }
                catch (Exception)
                {
                    OnNavEvent?.Invoke(shipId, $"[Nav] {ship.DisplayName}: segment build failed to {arrivalBody.DisplayName}");
                    return false;
                }
            }

            if (route.Segments == null || route.Segments.Count == 0)
            {
                OnNavEvent?.Invoke(shipId, $"[Nav] {ship.DisplayName}: no segments built to {arrivalBody.DisplayName}");
                return false;
            }

            route.UseSegmentedRoute = true;
            route.CurrentSegmentIndex = 0;

            OnNavEvent?.Invoke(shipId, $"[Nav] {ship.DisplayName}: built segmented route: {route.Segments.Count} segments ({DescribeSegmentTypes(route.Segments)})");

            ship.ShipInfo.CurrentRoute = route;
            ship.ShipInfo.State = ShipState.Travelling;
            ship.ShipInfo.OverrideWorldPosition = route.StartWorldPosition;
            origin.RemoveChildId(shipId);
            ship.AttachmentMode = AttachmentMode.None;
            ship.Orbit = null;
            TrackShip(shipId);
            return true;
        }

        // ---------------------------------------------------------------
        // Update
        // ---------------------------------------------------------------

        public void Update(double currentSimTime, Func<EntityId, double, SimVec3> positionResolver)
        {
            if (positionResolver == null) return;
            for (int i = _trackedShips.Count - 1; i >= 0; i--)
            {
                var shipId = _trackedShips[i];
                var ship = _registry.GetCelestialBody(shipId);
                if (ship?.ShipInfo == null) { _trackedShips.RemoveAt(i); continue; }
                if (ship.ShipInfo.State != ShipState.Travelling || ship.ShipInfo.CurrentRoute == null) continue;
                UpdateShipSegmented(ship, ship.ShipInfo.CurrentRoute, currentSimTime, positionResolver);
            }
        }

        /// <summary>Debug-only no-op. Coordinator compatibility.</summary>
        public void HandleSOITransition(EntityId shipId, EntityId prevSOI, EntityId newSOI, double simTime, Func<EntityId, double, SimVec3> pr) { }

        // ---------------------------------------------------------------
        // Segmented execution
        // ---------------------------------------------------------------

        private void UpdateShipSegmented(CelestialBody ship, ShipRoute route, double currentSimTime, Func<EntityId, double, SimVec3> positionResolver)
        {
            var seg = route.CurrentSegment;
            if (seg == null || route.AllSegmentsComplete) { CompleteArrival(ship, route, currentSimTime, positionResolver); return; }

            double progress = seg.GetProgress(currentSimTime);

            // Phase 26d: Bezier for OrbitInsertion, linear for everything else.
            if (seg.Type == SegmentType.OrbitInsertion && seg.UseCurvedInsertion)
                InterpolateBezier(ship, seg, progress, currentSimTime, positionResolver);
            else
                InterpolateLinear(ship, seg, progress, currentSimTime, positionResolver);

            if (progress >= 1.0)
            {
                OnNavEvent?.Invoke(ship.Id, $"[Nav] {ship.DisplayName}: segment complete: {seg.Type}");
                if (!route.AdvanceSegment()) { CompleteArrival(ship, route, currentSimTime, positionResolver); return; }
                var next = route.CurrentSegment;
                if (next != null) OnNavEvent?.Invoke(ship.Id, $"[Nav] {ship.DisplayName}: segment start: {next.Type}");
            }
        }

        private static void InterpolateLinear(CelestialBody ship, RouteSegment seg, double t, double simTime, Func<EntityId, double, SimVec3> pr)
        {
            if (seg.ReferenceBodyId.IsValid)
            {
                SimVec3 refPos = pr(seg.ReferenceBodyId, simTime);
                ship.ShipInfo.OverrideWorldPosition = refPos + Lerp(seg.StartLocalPosition, seg.EndLocalPosition, t);
            }
            else
            {
                ship.ShipInfo.OverrideWorldPosition = Lerp(seg.CachedWorldStart ?? SimVec3.Zero, seg.CachedWorldEnd ?? SimVec3.Zero, t);
            }
        }

        /// <summary>
        /// Quadratic Bezier: B(t) = (1-t)^2 * P0 + 2(1-t)t * C + t^2 * P1
        /// with smoothstep easing for natural deceleration into orbit.
        /// </summary>
        private static void InterpolateBezier(CelestialBody ship, RouteSegment seg, double progress, double simTime, Func<EntityId, double, SimVec3> pr)
        {
            double t = progress * progress * (3.0 - 2.0 * progress); // smoothstep

            SimVec3 p0 = seg.StartLocalPosition;
            SimVec3 c = seg.InsertionControlPoint;
            SimVec3 p1 = seg.EndLocalPosition;

            double omt = 1.0 - t;
            SimVec3 localPos = new SimVec3(
                omt * omt * p0.X + 2.0 * omt * t * c.X + t * t * p1.X,
                omt * omt * p0.Y + 2.0 * omt * t * c.Y + t * t * p1.Y,
                omt * omt * p0.Z + 2.0 * omt * t * c.Z + t * t * p1.Z);

            if (seg.ReferenceBodyId.IsValid)
                ship.ShipInfo.OverrideWorldPosition = pr(seg.ReferenceBodyId, simTime) + localPos;
            else
                ship.ShipInfo.OverrideWorldPosition = localPos;
        }

        // ---------------------------------------------------------------
        // Complete arrival
        // ---------------------------------------------------------------

        private void CompleteArrival(CelestialBody ship, ShipRoute route, double currentSimTime, Func<EntityId, double, SimVec3> positionResolver)
        {
            var destination = _registry.GetCelestialBody(route.DestinationBodyId);
            if (destination == null) { ship.ShipInfo.State = ShipState.Idle; ship.ShipInfo.CurrentRoute = null; ship.ShipInfo.OverrideWorldPosition = null; return; }

            EntityId arrivalParentId = DetermineArrivalParent(destination);
            var arrivalParent = _registry.GetCelestialBody(arrivalParentId);
            if (arrivalParent == null) { ship.ShipInfo.State = ShipState.Idle; ship.ShipInfo.CurrentRoute = null; ship.ShipInfo.OverrideWorldPosition = null; return; }

            double orbitRadius = route.DestinationOrbitRadius;
            double orbitPeriod = route.DestinationOrbitPeriod;

            if (route.Segments != null)
                for (int i = route.Segments.Count - 1; i >= 0; i--)
                {
                    var s = route.Segments[i];
                    if (s.Type == SegmentType.OrbitInsertion)
                    {
                        if (s.DestinationOrbitRadius > 0.0) orbitRadius = s.DestinationOrbitRadius;
                        if (s.DestinationOrbitPeriod > 0.0) orbitPeriod = s.DestinationOrbitPeriod;
                        break;
                    }
                }

            ship.ParentId = arrivalParentId;
            arrivalParent.AddChildId(ship.Id);
            ship.AttachmentMode = AttachmentMode.Orbit;

            double finalAngleDeg = route.ArrivalOrbitPhaseDeg;
            if (positionResolver != null)
            {
                SimVec3 parentPos = positionResolver(arrivalParentId, currentSimTime);
                SimVec3 shipPos = ship.ShipInfo.OverrideWorldPosition ?? parentPos;
                SimVec3 localPos = shipPos - parentPos;
                finalAngleDeg = NormalizeDeg(Math.Atan2(localPos.Z, localPos.X) * RadToDeg);
            }

            ship.Orbit = new OrbitDefinition
            {
                SemiMajorAxis = orbitRadius, Eccentricity = 0.0,
                MeanAnomalyAtEpochDeg = NormalizeDeg(finalAngleDeg - 360.0 * currentSimTime / orbitPeriod),
                OrbitalPeriod = orbitPeriod, EpochTime = 0.0, IsPrograde = true
            };

            ship.ShipInfo.CurrentRoute = null;
            ship.ShipInfo.OverrideWorldPosition = null;
            ship.ShipInfo.State = ShipState.Orbiting;

            OnNavEvent?.Invoke(ship.Id, $"[Nav] {ship.DisplayName}: arrived, orbiting {arrivalParent.DisplayName} at r={orbitRadius:F1} Mm");
            OnShipArrived?.Invoke(ship.Id, route.DestinationBodyId);
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static EntityId DetermineArrivalParent(CelestialBody dest)
        {
            if (dest.BodyType == CelestialBodyType.Station && dest.StationInfo?.Kind == StationKind.Surface && dest.ParentId.IsValid) return dest.ParentId;
            return dest.Id;
        }

        private SimVec3 ComputeShipWorldPosition(CelestialBody ship, double simTime, Func<EntityId, double, SimVec3> pr)
        {
            if (pr != null && ship.Orbit != null && ship.ParentId.IsValid)
                return pr(ship.ParentId, simTime) + OrbitalPositionCalculator.CalculatePosition(ship.Orbit, simTime);
            if (pr != null && ship.ParentId.IsValid) return pr(ship.ParentId, simTime);
            return SimVec3.Zero;
        }

        private static SimVec3 Lerp(SimVec3 a, SimVec3 b, double t) => new SimVec3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
        private static double NormalizeDeg(double d) { d %= 360.0; return d < 0 ? d + 360.0 : d; }

        private static string DescribeSegmentTypes(List<RouteSegment> segs)
        {
            if (segs == null || segs.Count == 0) return "empty";
            var n = new string[segs.Count];
            for (int i = 0; i < segs.Count; i++)
                n[i] = segs[i].Type switch { SegmentType.LocalOrbitDeparture => "Departure", SegmentType.SOIExit => "SOIExit", SegmentType.HeliocentricTransfer => "Helio", SegmentType.SOIEntry => "SOIEntry", SegmentType.LocalTransfer => "Local", SegmentType.OrbitInsertion => "Insertion", _ => "?" };
            return string.Join("\u2192", n);
        }
    }
}
