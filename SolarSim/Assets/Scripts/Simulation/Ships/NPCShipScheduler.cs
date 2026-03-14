using System;
using System.Collections.Generic;
using SpaceSim.Shared.Identifiers;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;

namespace SpaceSim.Simulation.Ships
{
    /// <summary>
    /// Automatically assigns new routes to NPC ships that have finished travelling.
    /// Runs each tick after ShipMovementSystem.
    /// Pure C# — no UnityEngine dependency.
    /// </summary>
    public class NPCShipScheduler
    {
        private readonly WorldRegistry _registry;
        private readonly ShipMovementSystem _movementSystem;
        private readonly Func<double> _getSimTime;

        // Configurable travel duration range (sim-seconds).
        private readonly double _minTravelDuration;
        private readonly double _maxTravelDuration;

        // Delay after arrival before scheduling next route (sim-seconds).
        private readonly double _idleDelay;

        // Track when each ship arrived so we can apply idle delay.
        private readonly Dictionary<EntityId, double> _arrivalTimes = new Dictionary<EntityId, double>();

        // Track last origin for patrol ping-pong behavior.
        private readonly Dictionary<EntityId, EntityId> _lastPatrolOrigin = new Dictionary<EntityId, EntityId>();

        // Simple deterministic RNG seeded per session.
        private readonly Random _rng;

        // Cached list of valid destination body ids (planets, moons — not stars, not ships).
        private readonly List<EntityId> _destinationCandidates = new List<EntityId>();
        private bool _candidatesDirty = true;

        // Callback invoked when scheduler starts a route.
        // Coordinator uses this to handle transit parenting and UI refresh.
        public event Action<EntityId> OnRouteScheduled;

        /// <summary>
        /// Creates an NPC ship scheduler.
        /// </summary>
        /// <param name="registry">World registry to query entities.</param>
        /// <param name="movementSystem">Ship movement system to start routes.</param>
        /// <param name="getSimTime">Function returning current simulation time.</param>
        /// <param name="minTravelDuration">Minimum travel duration in sim-seconds.</param>
        /// <param name="maxTravelDuration">Maximum travel duration in sim-seconds.</param>
        /// <param name="idleDelay">Delay before NPC departs again after arrival (sim-seconds).</param>
        /// <param name="seed">RNG seed for deterministic scheduling.</param>
        public NPCShipScheduler(
            WorldRegistry registry,
            ShipMovementSystem movementSystem,
            Func<double> getSimTime,
            double minTravelDuration = 40.0,
            double maxTravelDuration = 90.0,
            double idleDelay = 3.0,
            int seed = 42)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _movementSystem = movementSystem ?? throw new ArgumentNullException(nameof(movementSystem));
            _getSimTime = getSimTime ?? throw new ArgumentNullException(nameof(getSimTime));
            _minTravelDuration = minTravelDuration;
            _maxTravelDuration = maxTravelDuration;
            _idleDelay = idleDelay;
            _rng = new Random(seed);
        }

        /// <summary>
        /// Call once after star system is fully loaded to rebuild destination cache.
        /// Also call if celestial bodies are added/removed at runtime.
        /// </summary>
        public void InvalidateDestinationCache()
        {
            _candidatesDirty = true;
        }

        /// <summary>
        /// Main update. Call each tick after ShipMovementSystem.Update().
        /// </summary>
        public void Update()
        {
            if (_candidatesDirty)
            {
                RebuildDestinationCandidates();
            }

            if (_destinationCandidates.Count < 2)
            {
                // Not enough destinations to schedule routes.
                return;
            }

            double simTime = _getSimTime();

            foreach (var body in _registry.AllCelestialBodies)
            {
                if (body.BodyType != CelestialBodyType.Ship)
                    continue;

                if (body.ShipInfo == null)
                    continue;

                // Skip player ships.
                if (body.ShipInfo.Role == ShipRole.Player)
                    continue;

                // Only schedule ships that are currently orbiting (finished previous route).
                if (body.ShipInfo.State != ShipState.Orbiting)
                    continue;

                // Apply idle delay: record arrival time on first observation.
                if (!_arrivalTimes.ContainsKey(body.Id))
                {
                    _arrivalTimes[body.Id] = simTime;
                }

                double arrivedAt = _arrivalTimes[body.Id];
                if (simTime - arrivedAt < _idleDelay)
                {
                    // Still waiting at destination.
                    continue;
                }

                // Ready to depart — pick destination and start route.
                EntityId destination = PickDestination(body);
                if (destination == default)
                    continue;

                // Remember current parent before departure (for patrol ping-pong).
                EntityId currentParent = body.ParentId;

                double duration = _minTravelDuration
                    + _rng.NextDouble() * (_maxTravelDuration - _minTravelDuration);

                bool started = _movementSystem.StartRoute(body.Id, destination, simTime, duration);

                if (started)
                {
                    // Track patrol origin for ping-pong.
                    _lastPatrolOrigin[body.Id] = currentParent;

                    // Clear arrival tracking so next arrival gets a fresh timestamp.
                    _arrivalTimes.Remove(body.Id);

                    // Notify coordinator to handle transit parenting.
                    OnRouteScheduled?.Invoke(body.Id);
                }
            }
        }

        /// <summary>
        /// Picks destination based on ship role.
        /// </summary>
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

        /// <summary>
        /// Picks a random destination different from the current parent body.
        /// </summary>
        private EntityId PickRandomDestination(EntityId excludeBodyId)
        {
            // Build filtered list (exclude current location).
            var candidates = new List<EntityId>();
            for (int i = 0; i < _destinationCandidates.Count; i++)
            {
                if (_destinationCandidates[i] != excludeBodyId)
                {
                    candidates.Add(_destinationCandidates[i]);
                }
            }

            if (candidates.Count == 0)
                return default;

            return candidates[_rng.Next(candidates.Count)];
        }

        /// <summary>
        /// Patrol ships ping-pong: go back to where they came from.
        /// If no previous origin is known, fall back to random.
        /// </summary>
        private EntityId PickPatrolDestination(CelestialBody ship)
        {
            if (_lastPatrolOrigin.TryGetValue(ship.Id, out EntityId previousOrigin))
            {
                // Verify previous origin is still a valid destination.
                if (_destinationCandidates.Contains(previousOrigin) && previousOrigin != ship.ParentId)
                {
                    return previousOrigin;
                }
            }

            // Fallback: random destination (first patrol leg or invalid origin).
            return PickRandomDestination(ship.ParentId);
        }

        /// <summary>
        /// Rebuilds the cached list of valid destination bodies (planets and moons only).
        /// </summary>
        private void RebuildDestinationCandidates()
        {
            _destinationCandidates.Clear();

            foreach (var body in _registry.AllCelestialBodies)
            {
                // Valid destinations: planets, moons (not stars, ships, stations, asteroids).
                if (body.BodyType == CelestialBodyType.Planet ||
                    body.BodyType == CelestialBodyType.Moon)
                {
                    _destinationCandidates.Add(body.Id);
                }
            }

            _candidatesDirty = false;
        }

        /// <summary>
        /// Returns a short status summary for debug purposes.
        /// </summary>
        public string GetStatus()
        {
            int tracked = _arrivalTimes.Count;
            int destinations = _destinationCandidates.Count;
            return $"NPCShipScheduler: {destinations} destinations cached, {tracked} ships waiting to depart";
        }
    }
}
