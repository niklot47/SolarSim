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
    /// Manages ship travel between celestial bodies with fully symmetric SOI-aware frame switching
    /// and phased orbit insertion.
    ///
    /// Travel is two-phase:
    ///
    ///   Phase 1 — Main Travel (ShipState.Travelling):
    ///     Ship interpolates toward an approach point at OrbitApproachMultiplier × orbit radius
    ///     from the destination body.
    ///     Frame: GlobalParent (star frame) for interplanetary, LocalParent for same-SOI transfers.
    ///
    ///     During Phase 1, SOI transitions trigger one of three cases:
    ///
    ///       Case 1 — Entered destination SOI:
    ///         StartInsertionPhase() immediately (early arrival).
    ///
    ///       Case 2 — Entered intermediate SOI relevant to destination (inward, Global frame):
    ///         ReframeRoute() — re-anchor into new body's local frame.
    ///
    ///       Case 3 — LEFT the active local frame body's SOI (outward):
    ///         ReframeRouteOutward() — re-anchor into the parent/global frame.
    ///
    ///   Phase 2 — Orbit Insertion (ShipState.InsertingIntoOrbit):
    ///     Smooth-step convergence to orbit radius in the arrival body's local frame.
    ///
    /// Anti-jitter:
    ///   _lastFrameSwitchTime prevents rapid back-and-forth reframes near SOI boundaries.
    ///   MinFrameSwitchInterval = 2.0 sim-seconds between frame switches per ship.
    ///   Additionally, StartRoute() clears the cooldown for the new route.
    ///
    /// Pure C# — no UnityEngine dependency.
    /// </summary>
    public class ShipMovementSystem
    {
        private readonly WorldRegistry _registry;
        private readonly List<EntityId> _trackedShips = new List<EntityId>();

        // Per-ship cooldown: sim time of last frame switch.
        private readonly Dictionary<EntityId, double> _lastFrameSwitchTime =
            new Dictionary<EntityId, double>();

        /// <summary>
        /// Minimum sim-seconds between consecutive frame switches on the same ship.
        /// Prevents ping-pong reframes when a ship oscillates near an SOI boundary.
        /// </summary>
        private const double MinFrameSwitchInterval = 2.0;

        private const double DefaultOrbitRadius = 3.0;
        private const double DefaultOrbitPeriod = 12.0;
        private const double StationOrbitRadius = 0.5;
        private const double StationOrbitPeriod = 8.0;

        /// <summary>
        /// Multiplier applied to destination orbit radius to set the Phase 1 approach endpoint.
        /// </summary>
        public double OrbitApproachMultiplier { get; set; } = 2.5;

        /// <summary>
        /// Duration of the orbit insertion phase in sim-seconds.
        /// Set to 0 to skip insertion (instant orbit).
        /// </summary>
        public double OrbitInsertionDuration { get; set; } = 1.5;

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

        /// <summary>Begin a travel route for a ship.</summary>
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

            EntityId localFrameBodyId;
            RouteFrame frame = DetermineRouteFrame(origin, arrivalBody, out localFrameBodyId);

            ShipRoute route = (frame == RouteFrame.LocalParent && positionResolver != null)
                ? BuildLocalRoute(ship, origin, arrivalBody, localFrameBodyId,
                    currentSimTime, travelDuration, destOrbitRadius, destOrbitPeriod, positionResolver)
                : BuildGlobalRoute(ship, origin, arrivalBody,
                    currentSimTime, travelDuration, destOrbitRadius, destOrbitPeriod, positionResolver);

            route.DestinationBodyId = destinationId;

            ship.ShipInfo.CurrentRoute = route;
            ship.ShipInfo.State = ShipState.Travelling;
            ship.ShipInfo.OverrideWorldPosition = route.StartWorldPosition;

            origin.RemoveChildId(shipId);
            ship.AttachmentMode = AttachmentMode.None;
            ship.Orbit = null;

            // Reset frame-switch cooldown for the new route.
            _lastFrameSwitchTime.Remove(shipId);

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
                UpdateShip(ship, currentSimTime, positionResolver);
            }
        }

        // ---------------------------------------------------------------
        // Symmetric SOI Frame Switching
        // ---------------------------------------------------------------

        /// <summary>
        /// Called by the coordinator when SOIResolver detects an SOI boundary crossing.
        ///
        /// Signature now receives BOTH previousSOIBodyId AND newSOIBodyId to support
        /// both inward (entry) and outward (exit) frame switching.
        ///
        ///   Case 1 — newSOI == destination:
        ///     Trigger orbit insertion immediately. Highest priority.
        ///
        ///   Case 2 — newSOI is an intermediate body, currently Global frame:
        ///     Reframe inward (re-anchor into child body's local frame).
        ///
        ///   Case 3 — previousSOI was our active LocalParent frame body:
        ///     Reframe outward (re-anchor into parent or global frame).
        /// </summary>
        public void HandleSOITransition(
            EntityId shipId,
            EntityId previousSOIBodyId,
            EntityId newSOIBodyId,
            double simTime,
            Func<EntityId, double, SimVec3> positionResolver)
        {
            if (positionResolver == null) return;

            var ship = _registry.GetCelestialBody(shipId);
            if (ship?.ShipInfo == null) return;
            if (ship.ShipInfo.State != ShipState.Travelling) return;

            var route = ship.ShipInfo.CurrentRoute;
            if (route == null) return;

            var destination = _registry.GetCelestialBody(route.DestinationBodyId);
            if (destination == null) return;

            EntityId arrivalParentId = DetermineArrivalParent(destination);

            // Anti-jitter: skip if we switched frames too recently on this ship.
            if (_lastFrameSwitchTime.TryGetValue(shipId, out double lastSwitch)
                && simTime - lastSwitch < MinFrameSwitchInterval)
            {
                return;
            }

            // -----------------------------------------------------------
            // Case 1: Entered the destination's SOI → early insertion.
            //         Highest priority; returns immediately after handling.
            // -----------------------------------------------------------
            if (newSOIBodyId.IsValid && newSOIBodyId == arrivalParentId)
            {
                double progress = route.GetProgress(simTime);
                if (progress < 0.95)
                {
                    var arrivalBody = _registry.GetCelestialBody(arrivalParentId);
                    OnNavEvent?.Invoke(shipId,
                        $"[Nav] {ship.DisplayName}: entered destination SOI " +
                        $"({arrivalBody?.DisplayName ?? arrivalParentId.ToString()}), " +
                        $"starting insertion early (progress={progress:P0})");

                    _lastFrameSwitchTime[shipId] = simTime;
                    StartInsertionPhase(ship, route, simTime, positionResolver);
                }
                return;
            }

            // -----------------------------------------------------------
            // Case 2: Entered an intermediate SOI relevant to the destination
            //         while in Global frame → reframe inward.
            // -----------------------------------------------------------
            if (newSOIBodyId.IsValid
                && route.Frame == RouteFrame.Global
                && IsBodyRelatedToDestination(newSOIBodyId, arrivalParentId))
            {
                double remaining = 1.0 - route.GetProgress(simTime);
                if (remaining >= 0.05)
                {
                    _lastFrameSwitchTime[shipId] = simTime;
                    ReframeRoute(ship, route, newSOIBodyId, arrivalParentId, simTime, positionResolver);
                }
                return;
            }

            // -----------------------------------------------------------
            // Case 3: Left the body whose SOI defined the active local frame
            //         → reframe outward into the parent or global frame.
            //
            // Condition: LocalParent frame AND the exited body == current frame body.
            // This ensures we only reframe outward when directly relevant;
            // crossing some unrelated SOI boundary never triggers this branch.
            // -----------------------------------------------------------
            if (previousSOIBodyId.IsValid
                && route.Frame == RouteFrame.LocalParent
                && route.LocalFrameBodyId == previousSOIBodyId)
            {
                double remaining = 1.0 - route.GetProgress(simTime);
                if (remaining >= 0.05)
                {
                    _lastFrameSwitchTime[shipId] = simTime;
                    ReframeRouteOutward(ship, route,
                        previousSOIBodyId, newSOIBodyId, arrivalParentId,
                        simTime, positionResolver);
                }
            }
        }

        // ---------------------------------------------------------------
        // Inward reframe (Case 2 — from Step 19)
        // ---------------------------------------------------------------

        private void ReframeRoute(
            CelestialBody ship, ShipRoute route,
            EntityId newFrameBodyId, EntityId arrivalParentId,
            double simTime, Func<EntityId, double, SimVec3> positionResolver)
        {
            SimVec3 currentWorldPos = ship.ShipInfo.OverrideWorldPosition
                ?? ComputeShipWorldPosition(ship, simTime, positionResolver);

            double remainingFraction = 1.0 - route.GetProgress(simTime);
            double remainingDuration = System.Math.Max(route.TravelDuration * remainingFraction, 0.5);
            double estimatedArrivalTime = simTime + remainingDuration;

            SimVec3 frameBodyPosNow = positionResolver(newFrameBodyId, simTime);
            SimVec3 frameBodyPosAtArrival = positionResolver(newFrameBodyId, estimatedArrivalTime);
            SimVec3 destWorldPosAtArrival = positionResolver(arrivalParentId, estimatedArrivalTime);

            SimVec3 fromDestToShip = currentWorldPos - destWorldPosAtArrival;
            if (fromDestToShip.Magnitude < 0.001)
            {
                StartInsertionPhase(ship, route, simTime, positionResolver);
                return;
            }

            double nearSideAngle = System.Math.Atan2(fromDestToShip.Z, fromDestToShip.X);
            double approachRadius = route.DestinationOrbitRadius * OrbitApproachMultiplier;
            SimVec3 approachWorld = destWorldPosAtArrival
                + new SimVec3(approachRadius * System.Math.Cos(nearSideAngle), 0.0,
                              approachRadius * System.Math.Sin(nearSideAngle));

            route.Frame = RouteFrame.LocalParent;
            route.LocalFrameBodyId = newFrameBodyId;
            route.StartLocalPosition = currentWorldPos - frameBodyPosNow;
            route.ArrivalLocalPosition = approachWorld - frameBodyPosAtArrival;
            route.StartWorldPosition = currentWorldPos;
            route.ArrivalWorldPosition = approachWorld;
            route.ArrivalOrbitPhaseDeg = NormalizeDeg(nearSideAngle * 180.0 / System.Math.PI);
            route.DepartureTime = simTime;
            route.TravelDuration = remainingDuration;
            ship.ShipInfo.OverrideWorldPosition = currentWorldPos;

            var frameBody = _registry.GetCelestialBody(newFrameBodyId);
            OnNavEvent?.Invoke(ship.Id,
                $"[Nav] {ship.DisplayName}: reframed route → " +
                $"{frameBody?.DisplayName ?? newFrameBodyId.ToString()} frame at t={simTime:F1}");
        }

        // ---------------------------------------------------------------
        // Outward reframe (Case 3 — new in Step 20)
        // ---------------------------------------------------------------

        /// <summary>
        /// Re-anchor the remaining route outward into the parent or global frame.
        ///
        /// New frame selection:
        ///   newSOIBodyId valid and not a star → LocalParent frame of newSOIBodyId.
        ///   newSOIBodyId is a star, or invalid (no containing SOI) → Global frame.
        ///
        /// Current world position is captured exactly before any changes are made,
        /// guaranteeing no visual discontinuity.
        /// </summary>
        private void ReframeRouteOutward(
            CelestialBody ship, ShipRoute route,
            EntityId exitedBodyId, EntityId newSOIBodyId, EntityId arrivalParentId,
            double simTime, Func<EntityId, double, SimVec3> positionResolver)
        {
            // Capture exact current position — must not change after this point.
            SimVec3 currentWorldPos = ship.ShipInfo.OverrideWorldPosition
                ?? ComputeShipWorldPosition(ship, simTime, positionResolver);

            double remainingFraction = 1.0 - route.GetProgress(simTime);
            double remainingDuration = System.Math.Max(route.TravelDuration * remainingFraction, 0.5);
            double estimatedArrivalTime = simTime + remainingDuration;

            // Recompute approach point from current position toward destination.
            SimVec3 destWorldPosAtArrival = positionResolver(arrivalParentId, estimatedArrivalTime);
            SimVec3 fromDestToShip = currentWorldPos - destWorldPosAtArrival;
            if (fromDestToShip.Magnitude < 0.001)
            {
                StartInsertionPhase(ship, route, simTime, positionResolver);
                return;
            }

            double nearSideAngle = System.Math.Atan2(fromDestToShip.Z, fromDestToShip.X);
            double approachRadius = route.DestinationOrbitRadius * OrbitApproachMultiplier;
            SimVec3 approachWorldAtArrival = destWorldPosAtArrival
                + new SimVec3(approachRadius * System.Math.Cos(nearSideAngle), 0.0,
                              approachRadius * System.Math.Sin(nearSideAngle));

            // Determine whether the new frame is a planet's local frame or global.
            bool useGlobal = !newSOIBodyId.IsValid;
            if (!useGlobal)
            {
                var candidate = _registry.GetCelestialBody(newSOIBodyId);
                if (candidate == null || candidate.BodyType == CelestialBodyType.Star)
                    useGlobal = true;
            }

            string newFrameName;

            if (useGlobal)
            {
                // Switch to global (world-coordinate) frame.
                route.Frame = RouteFrame.Global;
                route.LocalFrameBodyId = EntityId.None;
                route.StartWorldPosition = currentWorldPos;
                route.ArrivalWorldPosition = approachWorldAtArrival;
                route.StartLocalPosition = SimVec3.Zero;   // unused in Global frame
                route.ArrivalLocalPosition = SimVec3.Zero;
                newFrameName = "global frame";
            }
            else
            {
                // Switch to the new dominant body's local frame.
                SimVec3 frameBodyPosNow = positionResolver(newSOIBodyId, simTime);
                SimVec3 frameBodyPosAtArrival = positionResolver(newSOIBodyId, estimatedArrivalTime);

                route.Frame = RouteFrame.LocalParent;
                route.LocalFrameBodyId = newSOIBodyId;
                route.StartLocalPosition = currentWorldPos - frameBodyPosNow;
                route.ArrivalLocalPosition = approachWorldAtArrival - frameBodyPosAtArrival;
                route.StartWorldPosition = currentWorldPos;
                route.ArrivalWorldPosition = approachWorldAtArrival;

                var newFrameBody = _registry.GetCelestialBody(newSOIBodyId);
                newFrameName = $"{newFrameBody?.DisplayName ?? newSOIBodyId.ToString()} frame";
            }

            // Reset timing so progress runs 0 → 1 over the remaining duration.
            route.ArrivalOrbitPhaseDeg = NormalizeDeg(nearSideAngle * 180.0 / System.Math.PI);
            route.DepartureTime = simTime;
            route.TravelDuration = remainingDuration;

            // Preserve exact position — no jump.
            ship.ShipInfo.OverrideWorldPosition = currentWorldPos;

            var exitedBody = _registry.GetCelestialBody(exitedBodyId);
            OnNavEvent?.Invoke(ship.Id,
                $"[Nav] {ship.DisplayName}: left {exitedBody?.DisplayName ?? exitedBodyId.ToString()} SOI, " +
                $"reframing outward → {newFrameName} at t={simTime:F1}");
        }

        // ---------------------------------------------------------------
        // Relevance filter (unchanged)
        // ---------------------------------------------------------------

        /// <summary>
        /// Returns true if soiBodyId is an ancestor of destBodyId in the hierarchy.
        /// Stars are excluded — entering a stellar SOI is never an intermediate reframe.
        /// </summary>
        private bool IsBodyRelatedToDestination(EntityId soiBodyId, EntityId destBodyId)
        {
            var soiBody = _registry.GetCelestialBody(soiBodyId);
            if (soiBody == null || soiBody.BodyType == CelestialBodyType.Star) return false;

            var current = _registry.GetCelestialBody(destBodyId);
            int depth = 0;
            while (current != null && current.ParentId.IsValid && depth < 15)
            {
                if (current.ParentId == soiBodyId) return true;
                current = _registry.GetCelestialBody(current.ParentId);
                depth++;
            }
            return false;
        }

        // ---------------------------------------------------------------
        // Frame determination (unchanged)
        // ---------------------------------------------------------------

        private RouteFrame DetermineRouteFrame(
            CelestialBody origin, CelestialBody destination, out EntityId localFrameBodyId)
        {
            localFrameBodyId = EntityId.None;

            if (origin.ParentId == destination.Id && destination.BodyType != CelestialBodyType.Star)
            {
                localFrameBodyId = destination.Id;
                return RouteFrame.LocalParent;
            }

            if (destination.ParentId == origin.Id && origin.BodyType != CelestialBodyType.Star)
            {
                localFrameBodyId = origin.Id;
                return RouteFrame.LocalParent;
            }

            if (origin.ParentId.IsValid && origin.ParentId == destination.ParentId)
            {
                var parent = _registry.GetCelestialBody(origin.ParentId);
                if (parent != null && parent.BodyType != CelestialBodyType.Star)
                {
                    localFrameBodyId = origin.ParentId;
                    return RouteFrame.LocalParent;
                }
            }

            if (destination.BodyType == CelestialBodyType.Station && destination.ParentId.IsValid)
            {
                var sp = _registry.GetCelestialBody(destination.ParentId);
                if (sp != null)
                {
                    if (origin.Id == destination.ParentId && origin.BodyType != CelestialBodyType.Star)
                    {
                        localFrameBodyId = origin.Id;
                        return RouteFrame.LocalParent;
                    }
                    if (origin.ParentId == destination.ParentId && sp.BodyType != CelestialBodyType.Star)
                    {
                        localFrameBodyId = destination.ParentId;
                        return RouteFrame.LocalParent;
                    }
                }
            }

            if (origin.BodyType == CelestialBodyType.Station && origin.ParentId.IsValid)
            {
                var sp = _registry.GetCelestialBody(origin.ParentId);
                if (sp != null)
                {
                    if (destination.Id == origin.ParentId && destination.BodyType != CelestialBodyType.Star)
                    {
                        localFrameBodyId = destination.Id;
                        return RouteFrame.LocalParent;
                    }
                    if (origin.ParentId == destination.ParentId && sp.BodyType != CelestialBodyType.Star)
                    {
                        localFrameBodyId = origin.ParentId;
                        return RouteFrame.LocalParent;
                    }
                }
            }

            return RouteFrame.Global;
        }

        // ---------------------------------------------------------------
        // Route builders (unchanged)
        // ---------------------------------------------------------------

        private ShipRoute BuildGlobalRoute(
            CelestialBody ship, CelestialBody origin, CelestialBody destination,
            double currentSimTime, double travelDuration,
            double destOrbitRadius, double destOrbitPeriod,
            Func<EntityId, double, SimVec3> positionResolver)
        {
            SimVec3 startPos = ComputeShipWorldPosition(ship, currentSimTime, positionResolver);
            double arrivalTime = currentSimTime + travelDuration;
            SimVec3 destPosAtArrival = positionResolver != null
                ? positionResolver(destination.Id, arrivalTime) : SimVec3.Zero;

            SimVec3 dir = startPos - destPosAtArrival;
            double angle = System.Math.Atan2(dir.Z, dir.X);
            double approachRadius = destOrbitRadius * OrbitApproachMultiplier;
            SimVec3 approachPos = destPosAtArrival
                + new SimVec3(approachRadius * System.Math.Cos(angle), 0.0,
                              approachRadius * System.Math.Sin(angle));

            return new ShipRoute
            {
                OriginBodyId = origin.Id,
                DestinationBodyId = destination.Id,
                DepartureTime = currentSimTime,
                TravelDuration = travelDuration,
                Frame = RouteFrame.Global,
                LocalFrameBodyId = EntityId.None,
                StartWorldPosition = startPos,
                ArrivalWorldPosition = approachPos,
                DestinationOrbitRadius = destOrbitRadius,
                DestinationOrbitPeriod = destOrbitPeriod,
                ArrivalOrbitPhaseDeg = NormalizeDeg(angle * 180.0 / System.Math.PI)
            };
        }

        private ShipRoute BuildLocalRoute(
            CelestialBody ship, CelestialBody origin, CelestialBody destination,
            EntityId localFrameBodyId,
            double currentSimTime, double travelDuration,
            double destOrbitRadius, double destOrbitPeriod,
            Func<EntityId, double, SimVec3> positionResolver)
        {
            SimVec3 framePosNow = positionResolver(localFrameBodyId, currentSimTime);
            SimVec3 shipWorldPos = ComputeShipWorldPosition(ship, currentSimTime, positionResolver);
            SimVec3 startLocal = shipWorldPos - framePosNow;

            double arrivalTime = currentSimTime + travelDuration;
            SimVec3 framePosAtArrival = positionResolver(localFrameBodyId, arrivalTime);
            SimVec3 destLocalAtArrival = positionResolver(destination.Id, arrivalTime) - framePosAtArrival;

            SimVec3 dir = startLocal - destLocalAtArrival;
            double angle = System.Math.Atan2(dir.Z, dir.X);
            double approachRadius = destOrbitRadius * OrbitApproachMultiplier;
            SimVec3 approachLocal = destLocalAtArrival
                + new SimVec3(approachRadius * System.Math.Cos(angle), 0.0,
                              approachRadius * System.Math.Sin(angle));
            SimVec3 approachWorld = framePosAtArrival + approachLocal;

            return new ShipRoute
            {
                OriginBodyId = origin.Id,
                DestinationBodyId = destination.Id,
                DepartureTime = currentSimTime,
                TravelDuration = travelDuration,
                Frame = RouteFrame.LocalParent,
                LocalFrameBodyId = localFrameBodyId,
                StartWorldPosition = shipWorldPos,
                ArrivalWorldPosition = approachWorld,
                StartLocalPosition = startLocal,
                ArrivalLocalPosition = approachLocal,
                DestinationOrbitRadius = destOrbitRadius,
                DestinationOrbitPeriod = destOrbitPeriod,
                ArrivalOrbitPhaseDeg = NormalizeDeg(angle * 180.0 / System.Math.PI)
            };
        }

        // ---------------------------------------------------------------
        // Per-tick update
        // ---------------------------------------------------------------

        private void UpdateShip(CelestialBody ship, double currentSimTime,
            Func<EntityId, double, SimVec3> positionResolver)
        {
            var info = ship.ShipInfo;

            if (info.State == ShipState.InsertingIntoOrbit)
            {
                UpdateInsertionPhase(ship, currentSimTime, positionResolver);
                return;
            }

            if (info.State != ShipState.Travelling || info.CurrentRoute == null)
                return;

            var route = info.CurrentRoute;
            double progress = route.GetProgress(currentSimTime);

            if (progress >= 1.0)
            {
                StartInsertionPhase(ship, route, currentSimTime, positionResolver);
                return;
            }

            if (route.Frame == RouteFrame.LocalParent && route.LocalFrameBodyId.IsValid)
            {
                SimVec3 localPos = Lerp(route.StartLocalPosition, route.ArrivalLocalPosition, progress);
                info.OverrideWorldPosition = positionResolver(route.LocalFrameBodyId, currentSimTime) + localPos;
            }
            else
            {
                info.OverrideWorldPosition = Lerp(route.StartWorldPosition, route.ArrivalWorldPosition, progress);
            }
        }

        // ---------------------------------------------------------------
        // Phase 2: Orbit insertion
        // ---------------------------------------------------------------

        private void StartInsertionPhase(
            CelestialBody ship, ShipRoute route, double currentSimTime,
            Func<EntityId, double, SimVec3> positionResolver)
        {
            if (positionResolver == null || OrbitInsertionDuration < 0.001)
            {
                CompleteArrival(ship, route, currentSimTime, positionResolver);
                return;
            }

            var destination = _registry.GetCelestialBody(route.DestinationBodyId);
            if (destination == null) { CompleteArrival(ship, route, currentSimTime, positionResolver); return; }

            EntityId arrivalParentId = DetermineArrivalParent(destination);
            var arrivalParent = _registry.GetCelestialBody(arrivalParentId);
            if (arrivalParent == null) { CompleteArrival(ship, route, currentSimTime, positionResolver); return; }

            // Capture actual ship position at insertion start.
            SimVec3 shipWorldPos;
            if (route.Frame == RouteFrame.LocalParent && route.LocalFrameBodyId.IsValid)
            {
                SimVec3 framePos = positionResolver(route.LocalFrameBodyId, currentSimTime);
                double p = System.Math.Min(route.GetProgress(currentSimTime), 1.0);
                shipWorldPos = framePos + Lerp(route.StartLocalPosition, route.ArrivalLocalPosition, p);
            }
            else
            {
                shipWorldPos = ship.ShipInfo.OverrideWorldPosition ?? route.ArrivalWorldPosition;
            }

            SimVec3 parentWorldPos = positionResolver(arrivalParentId, currentSimTime);
            SimVec3 shipLocalPos = shipWorldPos - parentWorldPos;
            double angleRad = System.Math.Atan2(shipLocalPos.Z, shipLocalPos.X);
            double angleDeg = NormalizeDeg(angleRad * 180.0 / System.Math.PI);

            route.InsertionPhaseActive = true;
            route.InsertionStartTime = currentSimTime;
            route.InsertionFrameBodyId = arrivalParentId;
            route.InsertionStartLocalPos = shipLocalPos;
            route.InsertionTargetLocalPos = new SimVec3(
                route.DestinationOrbitRadius * System.Math.Cos(angleRad), 0.0,
                route.DestinationOrbitRadius * System.Math.Sin(angleRad));
            route.InsertionArrivalAngleDeg = angleDeg;

            ship.ShipInfo.State = ShipState.InsertingIntoOrbit;
            ship.ShipInfo.OverrideWorldPosition = shipWorldPos;

            OnNavEvent?.Invoke(ship.Id,
                $"[Nav] {ship.DisplayName}: inserting into orbit around {arrivalParent.DisplayName} " +
                $"(r={route.DestinationOrbitRadius:F1} Mm, angle={angleDeg:F0}°)");
        }

        private void UpdateInsertionPhase(
            CelestialBody ship, double currentSimTime,
            Func<EntityId, double, SimVec3> positionResolver)
        {
            var route = ship.ShipInfo?.CurrentRoute;
            if (route == null || !route.InsertionPhaseActive)
            {
                ship.ShipInfo.State = ShipState.Idle;
                return;
            }

            double elapsed = currentSimTime - route.InsertionStartTime;
            double t = OrbitInsertionDuration > 0.001
                ? System.Math.Min(elapsed / OrbitInsertionDuration, 1.0) : 1.0;

            if (t >= 1.0) { CompleteArrival(ship, route, currentSimTime, positionResolver); return; }

            double smoothT = t * t * (3.0 - 2.0 * t);
            if (route.InsertionFrameBodyId.IsValid && positionResolver != null)
            {
                SimVec3 parentWorldPos = positionResolver(route.InsertionFrameBodyId, currentSimTime);
                ship.ShipInfo.OverrideWorldPosition =
                    parentWorldPos + Lerp(route.InsertionStartLocalPos, route.InsertionTargetLocalPos, smoothT);
            }
        }

        private void CompleteArrival(
            CelestialBody ship, ShipRoute route, double currentSimTime,
            Func<EntityId, double, SimVec3> positionResolver)
        {
            var destination = _registry.GetCelestialBody(route.DestinationBodyId);
            if (destination == null) { ship.ShipInfo.State = ShipState.Idle; ship.ShipInfo.CurrentRoute = null; ship.ShipInfo.OverrideWorldPosition = null; return; }

            EntityId arrivalParentId = DetermineArrivalParent(destination);
            var arrivalParent = _registry.GetCelestialBody(arrivalParentId);
            if (arrivalParent == null) { ship.ShipInfo.State = ShipState.Idle; ship.ShipInfo.CurrentRoute = null; ship.ShipInfo.OverrideWorldPosition = null; return; }

            ship.ParentId = arrivalParentId;
            arrivalParent.AddChildId(ship.Id);
            ship.AttachmentMode = AttachmentMode.Orbit;

            double orbitAngleDeg = route.InsertionPhaseActive
                ? route.InsertionArrivalAngleDeg : route.ArrivalOrbitPhaseDeg;
            double period = route.DestinationOrbitPeriod;

            ship.Orbit = new OrbitDefinition
            {
                SemiMajorAxis = route.DestinationOrbitRadius,
                Eccentricity = 0.0,
                MeanAnomalyAtEpochDeg = NormalizeDeg(orbitAngleDeg - 360.0 * currentSimTime / period),
                OrbitalPeriod = period,
                EpochTime = 0.0,
                IsPrograde = true
            };

            ship.ShipInfo.CurrentRoute = null;
            ship.ShipInfo.OverrideWorldPosition = null;
            ship.ShipInfo.State = ShipState.Orbiting;

            // Clean up cooldown entry for this ship.
            _lastFrameSwitchTime.Remove(ship.Id);

            OnNavEvent?.Invoke(ship.Id,
                $"[Nav] {ship.DisplayName}: arrived, orbiting {arrivalParent.DisplayName} " +
                $"at r={route.DestinationOrbitRadius:F1} Mm");

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
    }
}
