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
    /// Manages ship travel between celestial bodies with SOI-aware frame selection
    /// and phased orbit insertion.
    ///
    /// Travel is two-phase:
    ///
    ///   Phase 1 — Main Travel (ShipState.Travelling):
    ///     Ship interpolates from origin to an approach point located
    ///     OrbitApproachMultiplier × orbit radius from the destination body.
    ///     Frame: GlobalParent (star frame) for interplanetary, LocalParent for same-SOI transfers.
    ///
    ///   Phase 2 — Orbit Insertion (ShipState.InsertingIntoOrbit):
    ///     Ship converges from approach point to final orbit radius in the LOCAL frame
    ///     of the arrival parent body. Uses smooth-step easing.
    ///     Duration: OrbitInsertionDuration sim-seconds.
    ///
    /// Frame selection (DetermineRouteFrame):
    ///   - Same SOI root (e.g. ship in Terra SOI → Luna) → LocalParent frame of Terra
    ///   - Different SOI roots (e.g. Terra → Ares) → Global (star) frame
    ///   - Implemented via parent hierarchy traversal; star bodies excluded from local frame
    ///
    /// Compatibility:
    ///   - NPCShipScheduler: InsertingIntoOrbit is treated as Travelling (skipped in scheduler)
    ///   - DockingSystem: unaffected (ApproachingStation is a separate flow)
    ///   - Economy/Cargo: unaffected (operations happen on Docked state)
    ///
    /// Pure C# — no UnityEngine dependency.
    /// </summary>
    public class ShipMovementSystem
    {
        private readonly WorldRegistry _registry;
        private readonly List<EntityId> _trackedShips = new List<EntityId>();

        // Default orbit radius/period for ships arriving at non-station bodies.
        private const double DefaultOrbitRadius = 3.0;
        private const double DefaultOrbitPeriod = 12.0;

        // Small orbit for ships arriving at orbital stations.
        private const double StationOrbitRadius = 0.5;
        private const double StationOrbitPeriod = 8.0;

        /// <summary>
        /// Ship arrives at this multiple of the destination orbit radius first (approach point).
        /// Then insertion phase converges from approach point to orbit radius.
        /// E.g. orbit radius = 3 Mm → approach point = 3 × 2.5 = 7.5 Mm from body center.
        /// </summary>
        public double OrbitApproachMultiplier { get; set; } = 2.5;

        /// <summary>
        /// Duration of the orbit insertion phase in sim-seconds.
        /// During insertion, ship moves from approach point to orbit radius in local frame.
        /// Set to 0 to skip insertion phase (instant orbit, legacy behavior).
        /// </summary>
        public double OrbitInsertionDuration { get; set; } = 1.5;

        /// <summary>
        /// Fired when a ship completes arrival and enters orbit.
        /// Args: shipId, original destinationId (may be surface station).
        /// </summary>
        public event Action<EntityId, EntityId> OnShipArrived;

        /// <summary>
        /// Optional: debug/logging callback for navigation events.
        /// Called from Simulation layer — subscriber routes to GameDebug if needed.
        /// Args: shipId, message.
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
        /// Builds a two-phase route: main travel to approach point, then orbit insertion.
        /// Returns true if route was started successfully.
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

            // Determine actual arrival parent body.
            // For surface stations: ship orbits the planet, not the station.
            EntityId arrivalParentId;
            double destOrbitRadius;
            double destOrbitPeriod;

            if (destination.BodyType == CelestialBodyType.Station
                && destination.StationInfo != null
                && destination.StationInfo.Kind == StationKind.Surface
                && destination.ParentId.IsValid)
            {
                var parentPlanet = _registry.GetCelestialBody(destination.ParentId);
                if (parentPlanet == null) return false;
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
                if (ship.Orbit != null)
                {
                    destOrbitRadius = ship.Orbit.SemiMajorAxis;
                    destOrbitPeriod = ship.Orbit.OrbitalPeriod;
                }
                else
                {
                    destOrbitRadius = DefaultOrbitRadius;
                    destOrbitPeriod = DefaultOrbitPeriod;
                }
            }

            var arrivalBody = _registry.GetCelestialBody(arrivalParentId);
            if (arrivalBody == null) return false;

            EntityId localFrameBodyId;
            RouteFrame frame = DetermineRouteFrame(origin, arrivalBody, out localFrameBodyId);

            ShipRoute route;
            if (frame == RouteFrame.LocalParent && positionResolver != null)
            {
                route = BuildLocalRoute(ship, origin, arrivalBody, localFrameBodyId,
                    currentSimTime, travelDuration, destOrbitRadius, destOrbitPeriod, positionResolver);
            }
            else
            {
                route = BuildGlobalRoute(ship, origin, arrivalBody,
                    currentSimTime, travelDuration, destOrbitRadius, destOrbitPeriod, positionResolver);
            }

            // Preserve original destination id (may be surface station) for event/scheduler.
            route.DestinationBodyId = destinationId;

            ship.ShipInfo.CurrentRoute = route;
            ship.ShipInfo.State = ShipState.Travelling;
            ship.ShipInfo.OverrideWorldPosition = route.StartWorldPosition;

            origin.RemoveChildId(shipId);
            ship.AttachmentMode = AttachmentMode.None;
            ship.Orbit = null;

            TrackShip(shipId);
            return true;
        }

        /// <summary>
        /// Update all tracked ships. Call once per simulation tick.
        /// </summary>
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
        // Frame determination (SOI-aware via parent hierarchy)
        // ---------------------------------------------------------------

        /// <summary>
        /// Determine the reference frame for a route.
        ///
        /// LocalParent frame is used when origin and destination share a common
        /// parent body that is NOT a star (same SOI context).
        /// This covers: Planet ↔ Moon, Ship ↔ Station-around-same-planet, etc.
        ///
        /// Global frame is used for interplanetary travel where the common ancestor
        /// is a star (or there is none).
        /// </summary>
        private RouteFrame DetermineRouteFrame(
            CelestialBody origin, CelestialBody destination, out EntityId localFrameBodyId)
        {
            localFrameBodyId = EntityId.None;

            // Case 1: destination is direct parent of origin (e.g. Moon → Planet).
            if (origin.ParentId == destination.Id)
            {
                if (destination.BodyType != CelestialBodyType.Star)
                {
                    localFrameBodyId = destination.Id;
                    return RouteFrame.LocalParent;
                }
            }

            // Case 2: origin is direct parent of destination (e.g. Planet → Moon).
            if (destination.ParentId == origin.Id)
            {
                if (origin.BodyType != CelestialBodyType.Star)
                {
                    localFrameBodyId = origin.Id;
                    return RouteFrame.LocalParent;
                }
            }

            // Case 3: siblings sharing the same non-star parent (e.g. Moon ↔ Station@Planet).
            if (origin.ParentId.IsValid && origin.ParentId == destination.ParentId)
            {
                var parent = _registry.GetCelestialBody(origin.ParentId);
                if (parent != null && parent.BodyType != CelestialBodyType.Star)
                {
                    localFrameBodyId = origin.ParentId;
                    return RouteFrame.LocalParent;
                }
            }

            // Case 4: destination is a station — check station's parent body.
            if (destination.BodyType == CelestialBodyType.Station && destination.ParentId.IsValid)
            {
                var stationParent = _registry.GetCelestialBody(destination.ParentId);
                if (stationParent != null)
                {
                    // Ship is at the station's parent (e.g. ship at Terra → station at Terra).
                    if (origin.Id == destination.ParentId && origin.BodyType != CelestialBodyType.Star)
                    {
                        localFrameBodyId = origin.Id;
                        return RouteFrame.LocalParent;
                    }
                    // Ship and station share grandparent (e.g. both children of Terra).
                    if (origin.ParentId == destination.ParentId
                        && stationParent.BodyType != CelestialBodyType.Star)
                    {
                        localFrameBodyId = destination.ParentId;
                        return RouteFrame.LocalParent;
                    }
                }
            }

            // Case 5: origin is a station — check station's parent body.
            if (origin.BodyType == CelestialBodyType.Station && origin.ParentId.IsValid)
            {
                var stationParent = _registry.GetCelestialBody(origin.ParentId);
                if (stationParent != null)
                {
                    if (destination.Id == origin.ParentId && destination.BodyType != CelestialBodyType.Star)
                    {
                        localFrameBodyId = destination.Id;
                        return RouteFrame.LocalParent;
                    }
                    if (origin.ParentId == destination.ParentId
                        && stationParent.BodyType != CelestialBodyType.Star)
                    {
                        localFrameBodyId = origin.ParentId;
                        return RouteFrame.LocalParent;
                    }
                }
            }

            // Default: global star frame (interplanetary).
            return RouteFrame.Global;
        }

        // ---------------------------------------------------------------
        // Route builders (Phase 1: travel to approach point)
        // ---------------------------------------------------------------

        /// <summary>
        /// Build a route in global (star) frame.
        /// Travel endpoint = approach point at OrbitApproachMultiplier × orbit radius from destination.
        /// </summary>
        private ShipRoute BuildGlobalRoute(
            CelestialBody ship, CelestialBody origin, CelestialBody destination,
            double currentSimTime, double travelDuration,
            double destOrbitRadius, double destOrbitPeriod,
            Func<EntityId, double, SimVec3> positionResolver)
        {
            SimVec3 startPos = ComputeShipWorldPosition(ship, currentSimTime, positionResolver);

            double arrivalTime = currentSimTime + travelDuration;
            SimVec3 destBodyPosAtArrival = positionResolver != null
                ? positionResolver(destination.Id, arrivalTime)
                : SimVec3.Zero;

            // Direction from destination toward ship at departure (near-side approach).
            SimVec3 fromDestToShip = startPos - destBodyPosAtArrival;
            double nearSideAngleRad = System.Math.Atan2(fromDestToShip.Z, fromDestToShip.X);
            double arrivalPhaseDeg = NormalizeDeg(nearSideAngleRad * 180.0 / System.Math.PI);

            // Approach point: OrbitApproachMultiplier × orbit radius from destination body.
            // Phase 2 (insertion) will converge from here to the orbit radius.
            double approachRadius = destOrbitRadius * OrbitApproachMultiplier;
            double approachX = approachRadius * System.Math.Cos(nearSideAngleRad);
            double approachZ = approachRadius * System.Math.Sin(nearSideAngleRad);
            SimVec3 approachPos = destBodyPosAtArrival + new SimVec3(approachX, 0.0, approachZ);

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
                ArrivalOrbitPhaseDeg = arrivalPhaseDeg
            };
        }

        /// <summary>
        /// Build a route in local parent frame.
        /// Travel endpoint = approach point at OrbitApproachMultiplier × orbit radius,
        /// expressed in local coordinates of the frame body.
        /// </summary>
        private ShipRoute BuildLocalRoute(
            CelestialBody ship, CelestialBody origin, CelestialBody destination,
            EntityId localFrameBodyId,
            double currentSimTime, double travelDuration,
            double destOrbitRadius, double destOrbitPeriod,
            Func<EntityId, double, SimVec3> positionResolver)
        {
            SimVec3 frameBodyPos = positionResolver(localFrameBodyId, currentSimTime);
            SimVec3 shipWorldPos = ComputeShipWorldPosition(ship, currentSimTime, positionResolver);
            SimVec3 startLocal = shipWorldPos - frameBodyPos;

            double arrivalTime = currentSimTime + travelDuration;
            SimVec3 frameBodyPosAtArrival = positionResolver(localFrameBodyId, arrivalTime);
            SimVec3 destBodyPosAtArrival = positionResolver(destination.Id, arrivalTime);
            // Destination position in local frame at arrival time.
            SimVec3 destLocalAtArrival = destBodyPosAtArrival - frameBodyPosAtArrival;

            // Approach direction: from destination toward ship start position (in local frame).
            SimVec3 fromDestToShipLocal = startLocal - destLocalAtArrival;
            double nearSideAngleRad = System.Math.Atan2(fromDestToShipLocal.Z, fromDestToShipLocal.X);
            double arrivalPhaseDeg = NormalizeDeg(nearSideAngleRad * 180.0 / System.Math.PI);

            // Approach point in local frame (further out than final orbit radius).
            double approachRadius = destOrbitRadius * OrbitApproachMultiplier;
            double approachX = approachRadius * System.Math.Cos(nearSideAngleRad);
            double approachZ = approachRadius * System.Math.Sin(nearSideAngleRad);
            SimVec3 approachLocalRelDest = new SimVec3(approachX, 0.0, approachZ);
            SimVec3 approachLocal = destLocalAtArrival + approachLocalRelDest;
            SimVec3 approachWorld = frameBodyPosAtArrival + approachLocal;

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
                ArrivalOrbitPhaseDeg = arrivalPhaseDeg
            };
        }

        // ---------------------------------------------------------------
        // Per-tick update
        // ---------------------------------------------------------------

        private void UpdateShip(CelestialBody ship, double currentSimTime,
            Func<EntityId, double, SimVec3> positionResolver)
        {
            var info = ship.ShipInfo;

            // Handle Phase 2: orbit insertion approach.
            if (info.State == ShipState.InsertingIntoOrbit)
            {
                UpdateInsertionPhase(ship, currentSimTime, positionResolver);
                return;
            }

            // Handle Phase 1: main travel.
            if (info.State != ShipState.Travelling || info.CurrentRoute == null)
                return;

            var route = info.CurrentRoute;
            double progress = route.GetProgress(currentSimTime);

            if (progress >= 1.0)
            {
                // Phase 1 complete — begin orbit insertion phase.
                StartInsertionPhase(ship, route, currentSimTime, positionResolver);
                return;
            }

            // Interpolate ship position during Phase 1.
            if (route.Frame == RouteFrame.LocalParent && route.LocalFrameBodyId.IsValid)
            {
                SimVec3 localPos = Lerp(route.StartLocalPosition, route.ArrivalLocalPosition, progress);
                SimVec3 frameBodyPos = positionResolver(route.LocalFrameBodyId, currentSimTime);
                info.OverrideWorldPosition = frameBodyPos + localPos;
            }
            else
            {
                info.OverrideWorldPosition = Lerp(route.StartWorldPosition, route.ArrivalWorldPosition, progress);
            }
        }

        // ---------------------------------------------------------------
        // Phase 2: Orbit insertion
        // ---------------------------------------------------------------

        /// <summary>
        /// Begin the orbit insertion phase after main travel completes.
        /// Computes the actual approach angle from real ship position,
        /// then smoothly converges to orbit radius in the arrival body's local frame.
        /// </summary>
        private void StartInsertionPhase(
            CelestialBody ship, ShipRoute route, double currentSimTime,
            Func<EntityId, double, SimVec3> positionResolver)
        {
            // If no position resolver or zero duration, skip straight to arrival.
            if (positionResolver == null || OrbitInsertionDuration < 0.001)
            {
                CompleteArrival(ship, route, currentSimTime, positionResolver);
                return;
            }

            var destination = _registry.GetCelestialBody(route.DestinationBodyId);
            if (destination == null)
            {
                CompleteArrival(ship, route, currentSimTime, positionResolver);
                return;
            }

            EntityId arrivalParentId = DetermineArrivalParent(destination);
            var arrivalParent = _registry.GetCelestialBody(arrivalParentId);
            if (arrivalParent == null)
            {
                CompleteArrival(ship, route, currentSimTime, positionResolver);
                return;
            }

            // Compute actual ship world position at end of Phase 1.
            SimVec3 shipWorldPos;
            if (route.Frame == RouteFrame.LocalParent && route.LocalFrameBodyId.IsValid)
            {
                SimVec3 framePos = positionResolver(route.LocalFrameBodyId, currentSimTime);
                shipWorldPos = framePos + route.ArrivalLocalPosition;
            }
            else
            {
                shipWorldPos = route.ArrivalWorldPosition;
            }

            // Arrival parent world position right now.
            SimVec3 parentWorldPos = positionResolver(arrivalParentId, currentSimTime);

            // Ship position in arrival parent's local frame.
            SimVec3 shipLocalPos = shipWorldPos - parentWorldPos;

            // Compute approach angle from parent to ship (in XZ plane).
            double angleRad = System.Math.Atan2(shipLocalPos.Z, shipLocalPos.X);
            double angleDeg = NormalizeDeg(angleRad * 180.0 / System.Math.PI);

            // Orbit insertion target: orbit radius in the same direction.
            double orbX = route.DestinationOrbitRadius * System.Math.Cos(angleRad);
            double orbZ = route.DestinationOrbitRadius * System.Math.Sin(angleRad);
            SimVec3 insertionTargetLocal = new SimVec3(orbX, 0.0, orbZ);

            // Write insertion phase data into the route.
            route.InsertionPhaseActive = true;
            route.InsertionStartTime = currentSimTime;
            route.InsertionFrameBodyId = arrivalParentId;
            route.InsertionStartLocalPos = shipLocalPos;
            route.InsertionTargetLocalPos = insertionTargetLocal;
            route.InsertionArrivalAngleDeg = angleDeg;

            ship.ShipInfo.State = ShipState.InsertingIntoOrbit;
            ship.ShipInfo.OverrideWorldPosition = shipWorldPos;

            OnNavEvent?.Invoke(ship.Id,
                $"[Nav] {ship.DisplayName}: inserting into orbit around {arrivalParent.DisplayName} " +
                $"(r={route.DestinationOrbitRadius:F1} Mm, angle={angleDeg:F0}°)");
        }

        /// <summary>
        /// Update ship position during orbit insertion (Phase 2).
        /// Interpolates in local frame of arrival body using smooth-step easing.
        /// </summary>
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
                ? System.Math.Min(elapsed / OrbitInsertionDuration, 1.0)
                : 1.0;

            if (t >= 1.0)
            {
                CompleteArrival(ship, route, currentSimTime, positionResolver);
                return;
            }

            // Smooth-step easing: slower start and end, faster middle.
            // Gives a more natural deceleration into orbit.
            double smoothT = t * t * (3.0 - 2.0 * t);

            if (route.InsertionFrameBodyId.IsValid && positionResolver != null)
            {
                SimVec3 parentWorldPos = positionResolver(route.InsertionFrameBodyId, currentSimTime);
                SimVec3 localPos = Lerp(route.InsertionStartLocalPos, route.InsertionTargetLocalPos, smoothT);
                ship.ShipInfo.OverrideWorldPosition = parentWorldPos + localPos;
            }
        }

        /// <summary>
        /// Complete arrival: set orbit, clear route, fire events.
        /// Called after insertion phase completes (or immediately if duration = 0).
        /// This is the final step — ship is now in orbit.
        /// </summary>
        private void CompleteArrival(
            CelestialBody ship, ShipRoute route, double currentSimTime,
            Func<EntityId, double, SimVec3> positionResolver)
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

            // Re-parent ship to arrival body.
            ship.ParentId = arrivalParentId;
            arrivalParent.AddChildId(ship.Id);
            ship.AttachmentMode = AttachmentMode.Orbit;

            // Use actual insertion angle (Phase 2) for orbit accuracy.
            // Falls back to predicted angle if insertion phase was skipped.
            double orbitAngleDeg = route.InsertionPhaseActive
                ? route.InsertionArrivalAngleDeg
                : route.ArrivalOrbitPhaseDeg;

            double period = route.DestinationOrbitPeriod;
            double meanAnomalyAtEpoch = orbitAngleDeg - 360.0 * currentSimTime / period;
            meanAnomalyAtEpoch = NormalizeDeg(meanAnomalyAtEpoch);

            ship.Orbit = new OrbitDefinition
            {
                SemiMajorAxis = route.DestinationOrbitRadius,
                Eccentricity = 0.0,
                InclinationDeg = 0.0,
                LongitudeOfAscendingNodeDeg = 0.0,
                ArgumentOfPeriapsisDeg = 0.0,
                MeanAnomalyAtEpochDeg = meanAnomalyAtEpoch,
                OrbitalPeriod = period,
                EpochTime = 0.0,
                IsPrograde = true
            };

            ship.ShipInfo.CurrentRoute = null;
            ship.ShipInfo.OverrideWorldPosition = null;
            ship.ShipInfo.State = ShipState.Orbiting;

            OnNavEvent?.Invoke(ship.Id,
                $"[Nav] {ship.DisplayName}: arrived, orbiting {arrivalParent.DisplayName} " +
                $"at r={route.DestinationOrbitRadius:F1} Mm");

            // Fire arrival event with ORIGINAL destination id.
            // NPCShipScheduler uses this to detect surface station arrivals.
            OnShipArrived?.Invoke(ship.Id, route.DestinationBodyId);
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// Determine the body a ship should orbit after arriving at the destination.
        /// Surface stations: ship orbits the station's parent body (planet), not the station.
        /// Everything else: ship orbits the destination directly.
        /// </summary>
        private static EntityId DetermineArrivalParent(CelestialBody destination)
        {
            if (destination.BodyType == CelestialBodyType.Station
                && destination.StationInfo != null
                && destination.StationInfo.Kind == StationKind.Surface
                && destination.ParentId.IsValid)
            {
                return destination.ParentId;
            }
            return destination.Id;
        }

        private SimVec3 ComputeShipWorldPosition(
            CelestialBody ship, double simTime, Func<EntityId, double, SimVec3> positionResolver)
        {
            if (positionResolver != null && ship.Orbit != null && ship.ParentId.IsValid)
            {
                SimVec3 parentPos = positionResolver(ship.ParentId, simTime);
                SimVec3 localPos = OrbitalPositionCalculator.CalculatePosition(ship.Orbit, simTime);
                return parentPos + localPos;
            }
            if (positionResolver != null && ship.ParentId.IsValid)
                return positionResolver(ship.ParentId, simTime);
            return SimVec3.Zero;
        }

        private static SimVec3 Lerp(SimVec3 a, SimVec3 b, double t)
        {
            return new SimVec3(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t,
                a.Z + (b.Z - a.Z) * t);
        }

        private static double NormalizeDeg(double deg)
        {
            deg = deg % 360.0;
            if (deg < 0) deg += 360.0;
            return deg;
        }
    }
}
