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
    public class ShipMovementSystem
    {
        private readonly WorldRegistry _registry;
        private readonly List<EntityId> _trackedShips = new List<EntityId>();

        private const double DefaultOrbitRadius = 3.0;
        private const double DefaultOrbitPeriod = 12.0;

        // Small orbit around stations for arriving ships.
        private const double StationOrbitRadius = 0.5;
        private const double StationOrbitPeriod = 8.0;

        public event Action<EntityId, EntityId> OnShipArrived;

        public ShipMovementSystem(WorldRegistry registry)
        {
            _registry = registry;
        }

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

            // Determine orbit radius/period at destination.
            // If destination is a station, use small station orbit.
            double destOrbitRadius;
            double destOrbitPeriod;

            if (destination.BodyType == CelestialBodyType.Station)
            {
                destOrbitRadius = StationOrbitRadius;
                destOrbitPeriod = StationOrbitPeriod;
            }
            else if (ship.Orbit != null)
            {
                destOrbitRadius = ship.Orbit.SemiMajorAxis;
                destOrbitPeriod = ship.Orbit.OrbitalPeriod;
            }
            else
            {
                destOrbitRadius = DefaultOrbitRadius;
                destOrbitPeriod = DefaultOrbitPeriod;
            }

            EntityId localFrameBodyId;
            RouteFrame frame = DetermineRouteFrame(origin, destination, out localFrameBodyId);

            ShipRoute route;
            if (frame == RouteFrame.LocalParent && positionResolver != null)
            {
                route = BuildLocalRoute(ship, origin, destination, localFrameBodyId,
                    currentSimTime, travelDuration, destOrbitRadius, destOrbitPeriod, positionResolver);
            }
            else
            {
                route = BuildGlobalRoute(ship, origin, destination,
                    currentSimTime, travelDuration, destOrbitRadius, destOrbitPeriod, positionResolver);
            }

            ship.ShipInfo.CurrentRoute = route;
            ship.ShipInfo.State = ShipState.Travelling;
            ship.ShipInfo.OverrideWorldPosition = route.StartWorldPosition;

            origin.RemoveChildId(shipId);
            ship.AttachmentMode = AttachmentMode.None;
            ship.Orbit = null;

            TrackShip(shipId);
            return true;
        }

        public bool StartRoute(EntityId shipId, EntityId destinationId, double currentSimTime, double travelDuration)
        {
            return StartRoute(shipId, destinationId, currentSimTime, travelDuration, null);
        }

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
        // Route frame determination
        // ---------------------------------------------------------------

        private RouteFrame DetermineRouteFrame(
            CelestialBody origin, CelestialBody destination, out EntityId localFrameBodyId)
        {
            localFrameBodyId = EntityId.None;

            // Case 1: destination is parent of origin (e.g. Moon -> Planet).
            if (origin.ParentId == destination.Id)
            {
                // Only use local frame if destination is NOT a star.
                if (destination.BodyType != CelestialBodyType.Star)
                {
                    localFrameBodyId = destination.Id;
                    return RouteFrame.LocalParent;
                }
            }

            // Case 2: origin is parent of destination (e.g. Planet -> Moon).
            if (destination.ParentId == origin.Id)
            {
                if (origin.BodyType != CelestialBodyType.Star)
                {
                    localFrameBodyId = origin.Id;
                    return RouteFrame.LocalParent;
                }
            }

            // Case 3: siblings sharing the same parent.
            // Only use local frame if parent is NOT a star.
            if (origin.ParentId.IsValid && origin.ParentId == destination.ParentId)
            {
                var parent = _registry.GetCelestialBody(origin.ParentId);
                if (parent != null && parent.BodyType != CelestialBodyType.Star)
                {
                    localFrameBodyId = origin.ParentId;
                    return RouteFrame.LocalParent;
                }
            }

            // Case 4: destination is a station — check if station's parent
            // creates a local-frame relationship with the ship's origin.
            if (destination.BodyType == CelestialBodyType.Station && destination.ParentId.IsValid)
            {
                var stationParent = _registry.GetCelestialBody(destination.ParentId);
                if (stationParent != null)
                {
                    // If ship's origin is the station's parent body (or vice versa),
                    // use the station's parent as local frame.
                    if (origin.Id == destination.ParentId && origin.BodyType != CelestialBodyType.Star)
                    {
                        localFrameBodyId = origin.Id;
                        return RouteFrame.LocalParent;
                    }
                    if (origin.ParentId == destination.ParentId && stationParent.BodyType != CelestialBodyType.Star)
                    {
                        localFrameBodyId = destination.ParentId;
                        return RouteFrame.LocalParent;
                    }
                }
            }

            // Case 5: origin is a station — similar logic.
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
                    if (origin.ParentId == destination.ParentId && stationParent.BodyType != CelestialBodyType.Star)
                    {
                        localFrameBodyId = origin.ParentId;
                        return RouteFrame.LocalParent;
                    }
                }
            }

            // Default: global frame (interplanetary).
            return RouteFrame.Global;
        }

        // ---------------------------------------------------------------
        // Route builders
        // ---------------------------------------------------------------

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

            SimVec3 fromDestToShip = startPos - destBodyPosAtArrival;
            double nearSideAngleRad = System.Math.Atan2(fromDestToShip.Z, fromDestToShip.X);
            double arrivalPhaseDeg = NormalizeDeg(nearSideAngleRad * 180.0 / System.Math.PI);

            double orbX = destOrbitRadius * System.Math.Cos(nearSideAngleRad);
            double orbZ = destOrbitRadius * System.Math.Sin(nearSideAngleRad);
            SimVec3 arrivalPos = destBodyPosAtArrival + new SimVec3(orbX, 0.0, orbZ);

            return new ShipRoute
            {
                OriginBodyId = origin.Id,
                DestinationBodyId = destination.Id,
                DepartureTime = currentSimTime,
                TravelDuration = travelDuration,
                Frame = RouteFrame.Global,
                LocalFrameBodyId = EntityId.None,
                StartWorldPosition = startPos,
                ArrivalWorldPosition = arrivalPos,
                DestinationOrbitRadius = destOrbitRadius,
                DestinationOrbitPeriod = destOrbitPeriod,
                ArrivalOrbitPhaseDeg = arrivalPhaseDeg
            };
        }

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
            SimVec3 destLocalAtArrival = destBodyPosAtArrival - frameBodyPosAtArrival;

            SimVec3 fromDestToShipLocal = startLocal - destLocalAtArrival;
            double nearSideAngleRad = System.Math.Atan2(fromDestToShipLocal.Z, fromDestToShipLocal.X);
            double arrivalPhaseDeg = NormalizeDeg(nearSideAngleRad * 180.0 / System.Math.PI);

            double orbX = destOrbitRadius * System.Math.Cos(nearSideAngleRad);
            double orbZ = destOrbitRadius * System.Math.Sin(nearSideAngleRad);
            SimVec3 arrivalLocal = destLocalAtArrival + new SimVec3(orbX, 0.0, orbZ);

            SimVec3 arrivalWorld = frameBodyPosAtArrival + arrivalLocal;

            return new ShipRoute
            {
                OriginBodyId = origin.Id,
                DestinationBodyId = destination.Id,
                DepartureTime = currentSimTime,
                TravelDuration = travelDuration,
                Frame = RouteFrame.LocalParent,
                LocalFrameBodyId = localFrameBodyId,
                StartWorldPosition = shipWorldPos,
                ArrivalWorldPosition = arrivalWorld,
                StartLocalPosition = startLocal,
                ArrivalLocalPosition = arrivalLocal,
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
            if (info.State != ShipState.Travelling || info.CurrentRoute == null)
                return;

            var route = info.CurrentRoute;
            double progress = route.GetProgress(currentSimTime);

            if (progress >= 1.0)
            {
                ArriveAtDestination(ship, route, currentSimTime);
                return;
            }

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
        // Arrival
        // ---------------------------------------------------------------

        private void ArriveAtDestination(CelestialBody ship, ShipRoute route, double currentSimTime)
        {
            var destination = _registry.GetCelestialBody(route.DestinationBodyId);
            if (destination == null)
            {
                ship.ShipInfo.State = ShipState.Idle;
                ship.ShipInfo.CurrentRoute = null;
                ship.ShipInfo.OverrideWorldPosition = null;
                return;
            }

            // Ship enters orbit around the destination (including stations — no docking).
            ship.ParentId = destination.Id;
            destination.AddChildId(ship.Id);
            ship.AttachmentMode = AttachmentMode.Orbit;

            double period = route.DestinationOrbitPeriod;
            double meanAnomalyAtEpoch = route.ArrivalOrbitPhaseDeg
                - 360.0 * currentSimTime / period;
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

            OnShipArrived?.Invoke(ship.Id, destination.Id);
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

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
