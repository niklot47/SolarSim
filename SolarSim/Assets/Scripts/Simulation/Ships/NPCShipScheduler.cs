using System;
using System.Collections.Generic;
using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;

namespace SpaceSim.Simulation.Ships
{
    /// <summary>
    /// Automatically assigns new routes to NPC ships that have finished travelling.
    /// Runs each tick after ShipMovementSystem.Update().
    /// Pure C# — no UnityEngine dependency.
    /// </summary>
    public class NPCShipScheduler
    {
        private readonly WorldRegistry _registry;
        private readonly ShipMovementSystem _movementSystem;
        private readonly Func<double> _getSimTime;

        /// <summary>Travel speed in world units per sim-second (Mm/sim-s).</summary>
        public double TravelSpeed { get; set; }

        /// <summary>Minimum travel duration floor (sim-seconds).</summary>
        public double MinTravelDuration { get; set; }

        /// <summary>Delay after arrival before scheduling next route (sim-seconds).</summary>
        public double IdleDelay { get; set; }

        private readonly Dictionary<EntityId, double> _arrivalTimes = new Dictionary<EntityId, double>();
        private readonly Dictionary<EntityId, EntityId> _lastPatrolOrigin = new Dictionary<EntityId, EntityId>();
        private readonly Random _rng;
        private readonly List<EntityId> _destinationCandidates = new List<EntityId>();
        private bool _candidatesDirty = true;

        private Func<EntityId, double, SimVec3> _positionResolver;

        public event Action<EntityId> OnRouteScheduled;

        public NPCShipScheduler(
            WorldRegistry registry,
            ShipMovementSystem movementSystem,
            Func<double> getSimTime,
            double travelSpeed = 2.0,
            double minTravelDuration = 3.0,
            double idleDelay = 3.0,
            int seed = 42)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _movementSystem = movementSystem ?? throw new ArgumentNullException(nameof(movementSystem));
            _getSimTime = getSimTime ?? throw new ArgumentNullException(nameof(getSimTime));
            TravelSpeed = travelSpeed > 0.0 ? travelSpeed : 2.0;
            MinTravelDuration = minTravelDuration > 0.0 ? minTravelDuration : 3.0;
            IdleDelay = idleDelay;
            _rng = new Random(seed);
        }

        public void SetPositionResolver(Func<EntityId, double, SimVec3> resolver)
        {
            _positionResolver = resolver;
        }

        public void InvalidateDestinationCache()
        {
            _candidatesDirty = true;
        }

        public void Update()
        {
            if (_candidatesDirty)
                RebuildDestinationCandidates();

            if (_destinationCandidates.Count < 2)
                return;

            double simTime = _getSimTime();

            foreach (var body in _registry.AllCelestialBodies)
            {
                if (body.BodyType != CelestialBodyType.Ship)
                    continue;
                if (body.ShipInfo == null)
                    continue;
                if (body.ShipInfo.Role == ShipRole.Player)
                    continue;
                if (body.ShipInfo.State != ShipState.Orbiting)
                    continue;

                if (!_arrivalTimes.ContainsKey(body.Id))
                    _arrivalTimes[body.Id] = simTime;

                double arrivedAt = _arrivalTimes[body.Id];
                if (simTime - arrivedAt < IdleDelay)
                    continue;

                EntityId destination = PickDestination(body);
                if (!destination.IsValid)
                    continue;

                EntityId currentParent = body.ParentId;
                double duration = ComputeTravelDuration(body, destination, simTime);

                bool started = _movementSystem.StartRoute(
                    body.Id, destination, simTime, duration, _positionResolver);

                if (started)
                {
                    _lastPatrolOrigin[body.Id] = currentParent;
                    _arrivalTimes.Remove(body.Id);
                    OnRouteScheduled?.Invoke(body.Id);
                }
            }
        }

        /// <summary>
        /// Compute travel duration. Uses local-frame distance for local routes,
        /// global distance for interplanetary routes.
        /// </summary>
        private double ComputeTravelDuration(CelestialBody ship, EntityId destinationId, double simTime)
        {
            double speed = TravelSpeed > 0.0 ? TravelSpeed : 2.0;

            if (_positionResolver == null)
                return MinTravelDuration;

            var destination = _registry.GetCelestialBody(destinationId);
            if (destination == null)
                return MinTravelDuration;

            var origin = _registry.GetCelestialBody(ship.ParentId);
            if (origin == null)
                return MinTravelDuration;

            // Get ship's world position.
            SimVec3 shipPos = ComputeShipWorldPos(ship, simTime);

            // Determine if this is a local route.
            EntityId localFrameBodyId = EntityId.None;
            bool isLocal = false;

            // Case 1: destination is parent of origin (Moon -> Planet).
            if (origin.ParentId == destinationId)
            {
                localFrameBodyId = destinationId;
                isLocal = true;
            }
            // Case 2: origin is parent of destination (Planet -> Moon).
            else if (destination.ParentId == origin.Id)
            {
                localFrameBodyId = origin.Id;
                isLocal = true;
            }
            // Case 3: siblings (Moon1 -> Moon2).
            else if (origin.ParentId.IsValid && origin.ParentId == destination.ParentId)
            {
                localFrameBodyId = origin.ParentId;
                isLocal = true;
            }

            double distance;

            if (isLocal && localFrameBodyId.IsValid)
            {
                // Compute distance in local frame.
                SimVec3 framePos = _positionResolver(localFrameBodyId, simTime);
                SimVec3 shipLocal = shipPos - framePos;
                SimVec3 destPos = _positionResolver(destinationId, simTime);
                SimVec3 destLocal = destPos - framePos;
                distance = SimVec3.Distance(shipLocal, destLocal);
            }
            else
            {
                // Global distance.
                SimVec3 destPos = _positionResolver(destinationId, simTime);
                distance = SimVec3.Distance(shipPos, destPos);
            }

            double duration = distance / speed;
            if (duration < MinTravelDuration)
                duration = MinTravelDuration;

            return duration;
        }

        private SimVec3 ComputeShipWorldPos(CelestialBody ship, double simTime)
        {
            if (_positionResolver != null && ship.Orbit != null && ship.ParentId.IsValid)
            {
                SimVec3 parentPos = _positionResolver(ship.ParentId, simTime);
                SimVec3 localPos = Simulation.Orbits.OrbitalPositionCalculator
                    .CalculatePosition(ship.Orbit, simTime);
                return parentPos + localPos;
            }
            if (_positionResolver != null && ship.ParentId.IsValid)
            {
                return _positionResolver(ship.ParentId, simTime);
            }
            return SimVec3.Zero;
        }

        private EntityId PickDestination(CelestialBody ship)
        {
            switch (ship.ShipInfo.Role)
            {
                case ShipRole.Trader:
                    return PickRandomDestination(ship.ParentId);
                case ShipRole.Patrol:
                    return PickPatrolDestination(ship);
                case ShipRole.Civilian:
                    return PickRandomDestination(ship.ParentId);
                default:
                    return PickRandomDestination(ship.ParentId);
            }
        }

        private EntityId PickRandomDestination(EntityId excludeBodyId)
        {
            int count = 0;
            for (int i = 0; i < _destinationCandidates.Count; i++)
            {
                if (_destinationCandidates[i] != excludeBodyId)
                    count++;
            }
            if (count == 0) return EntityId.None;

            int pick = _rng.Next(count);
            int idx = 0;
            for (int i = 0; i < _destinationCandidates.Count; i++)
            {
                if (_destinationCandidates[i] == excludeBodyId)
                    continue;
                if (idx == pick)
                    return _destinationCandidates[i];
                idx++;
            }
            return EntityId.None;
        }

        private EntityId PickPatrolDestination(CelestialBody ship)
        {
            if (_lastPatrolOrigin.TryGetValue(ship.Id, out EntityId previousOrigin))
            {
                if (_destinationCandidates.Contains(previousOrigin) && previousOrigin != ship.ParentId)
                    return previousOrigin;
            }
            return PickRandomDestination(ship.ParentId);
        }

        private void RebuildDestinationCandidates()
        {
            _destinationCandidates.Clear();
            foreach (var body in _registry.AllCelestialBodies)
            {
                // Planets, moons, and stations are valid destinations.
                if (body.BodyType == CelestialBodyType.Planet ||
                    body.BodyType == CelestialBodyType.Moon ||
                    body.BodyType == CelestialBodyType.Station)
                {
                    _destinationCandidates.Add(body.Id);
                }
            }
            _candidatesDirty = false;
        }

        public string GetStatus()
        {
            return $"NPCShipScheduler: {_destinationCandidates.Count} destinations, " +
                   $"{_arrivalTimes.Count} ships waiting, speed={TravelSpeed:F1} Mm/s";
        }
    }
}
