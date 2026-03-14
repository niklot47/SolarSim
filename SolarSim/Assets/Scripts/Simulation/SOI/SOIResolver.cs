using System;
using System.Collections.Generic;
using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;
using SpaceSim.Simulation.Core;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;

namespace SpaceSim.Simulation.SOI
{
    /// <summary>
    /// Result of an SOI transition check.
    /// </summary>
    public struct SOITransition
    {
        /// <summary>EntityId of the ship that transitioned.</summary>
        public EntityId ShipId;

        /// <summary>Previous dominant body id.</summary>
        public EntityId PreviousBodyId;

        /// <summary>New dominant body id.</summary>
        public EntityId NewBodyId;

        /// <summary>Simulation time when transition was detected.</summary>
        public double SimTime;

        public override string ToString()
        {
            return $"SOITransition[ship={ShipId} {PreviousBodyId}->{NewBodyId} t={SimTime:F2}]";
        }
    }

    /// <summary>
    /// Resolves Sphere of Influence containment for positions and ships.
    /// Uses the hierarchy of bodies with valid SOI radii.
    /// Prefers the deepest (most specific) containing body.
    /// Pure C# — no UnityEngine dependency. Lives in Simulation layer.
    ///
    /// Dominance rules:
    /// 1. Only bodies with valid SOIRadius participate.
    /// 2. A position is "inside" a body's SOI if distance to body < SOIRadius.
    /// 3. Among all containing bodies, the one with the smallest SOIRadius wins (deepest/narrowest).
    /// 4. Root star is treated as fallback if it has SOI, or EntityId.None otherwise.
    /// </summary>
    public class SOIResolver
    {
        private readonly WorldRegistry _registry;
        private readonly WorldPositionResolver _positionResolver;

        // Cached list of bodies with valid SOI, rebuilt on demand.
        private readonly List<EntityId> _soiBodies = new List<EntityId>();
        private bool _dirty = true;

        public SOIResolver(WorldRegistry registry, WorldPositionResolver positionResolver)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _positionResolver = positionResolver ?? throw new ArgumentNullException(nameof(positionResolver));
        }

        /// <summary>
        /// Mark the SOI body cache as dirty. Call when bodies are added/removed.
        /// </summary>
        public void Invalidate()
        {
            _dirty = true;
        }

        /// <summary>
        /// Check if a world position is inside a specific body's SOI.
        /// </summary>
        public bool IsInsideSOI(EntityId bodyId, SimVec3 worldPosition, double simTime)
        {
            var body = _registry.GetCelestialBody(bodyId);
            if (body == null || !body.SOIRadius.HasValue) return false;

            SimVec3 bodyPos = _positionResolver.Resolve(bodyId, simTime);
            double distance = SimVec3.Distance(worldPosition, bodyPos);
            return distance < body.SOIRadius.Value;
        }

        /// <summary>
        /// Resolve which body's SOI dominates a given world position.
        /// Returns the deepest (smallest SOI) containing body.
        /// Returns EntityId.None if no body's SOI contains the position.
        /// </summary>
        public EntityId ResolveDominantBody(SimVec3 worldPosition, double simTime)
        {
            EnsureCache();

            EntityId bestId = EntityId.None;
            double bestSOI = double.MaxValue;

            for (int i = 0; i < _soiBodies.Count; i++)
            {
                var bodyId = _soiBodies[i];
                var body = _registry.GetCelestialBody(bodyId);
                if (body == null || !body.SOIRadius.HasValue) continue;

                double soiRadius = body.SOIRadius.Value;
                SimVec3 bodyPos = _positionResolver.Resolve(bodyId, simTime);
                double distance = SimVec3.Distance(worldPosition, bodyPos);

                if (distance < soiRadius && soiRadius < bestSOI)
                {
                    bestId = bodyId;
                    bestSOI = soiRadius;
                }
            }

            return bestId;
        }

        /// <summary>
        /// Resolve dominant SOI body for a ship at current simulation time.
        /// Uses the ship's world position (override if travelling, orbital otherwise).
        /// </summary>
        public EntityId ResolveDominantBodyForShip(CelestialBody ship, double simTime)
        {
            if (ship == null) return EntityId.None;

            SimVec3 shipPos = _positionResolver.Resolve(ship, simTime);
            return ResolveDominantBody(shipPos, simTime);
        }

        /// <summary>
        /// Update SOI tracking for all ships in the registry.
        /// Detects transitions and returns them.
        /// Updates ShipInfo.CurrentSOIBodyId for each ship.
        /// </summary>
        public List<SOITransition> UpdateAllShips(double simTime)
        {
            EnsureCache();

            List<SOITransition> transitions = null;

            foreach (var body in _registry.AllCelestialBodies)
            {
                if (body.BodyType != CelestialBodyType.Ship) continue;
                if (body.ShipInfo == null) continue;

                EntityId previousSOI = body.ShipInfo.CurrentSOIBodyId;
                EntityId newSOI = ResolveDominantBodyForShip(body, simTime);

                if (previousSOI != newSOI)
                {
                    body.ShipInfo.CurrentSOIBodyId = newSOI;

                    if (transitions == null)
                        transitions = new List<SOITransition>();

                    transitions.Add(new SOITransition
                    {
                        ShipId = body.Id,
                        PreviousBodyId = previousSOI,
                        NewBodyId = newSOI,
                        SimTime = simTime
                    });
                }
            }

            return transitions;
        }

        /// <summary>
        /// Get a human-readable status string.
        /// </summary>
        public string GetStatus()
        {
            EnsureCache();
            return $"SOIResolver: {_soiBodies.Count} bodies with SOI";
        }

        private void EnsureCache()
        {
            if (!_dirty) return;

            _soiBodies.Clear();
            foreach (var body in _registry.AllCelestialBodies)
            {
                if (body.SOIRadius.HasValue && body.SOIRadius.Value > 0.0)
                {
                    _soiBodies.Add(body.Id);
                }
            }
            _dirty = false;
        }
    }
}
