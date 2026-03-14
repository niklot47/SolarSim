using System;
using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;
using SpaceSim.Simulation.Orbits;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;

namespace SpaceSim.Simulation.Core
{
    /// <summary>
    /// Single source of truth for resolving absolute world positions of all celestial bodies.
    /// Handles orbital bodies, surface stations, travelling ships, and recursive parent chains.
    /// Pure C# — no UnityEngine dependency. Lives in Simulation layer.
    /// </summary>
    public class WorldPositionResolver
    {
        private readonly WorldRegistry _registry;

        /// <summary>Small height offset for surface stations to avoid z-fighting.</summary>
        private const double SurfaceStationOffset = 0.05;

        /// <summary>Safety limit for recursive parent chain depth.</summary>
        private const int MaxParentDepth = 20;

        public WorldPositionResolver(WorldRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// Resolve absolute world position for any body by EntityId at given simulation time.
        /// </summary>
        public SimVec3 Resolve(EntityId bodyId, double simTime)
        {
            var body = _registry.GetCelestialBody(bodyId);
            if (body == null) return SimVec3.Zero;
            return Resolve(body, simTime);
        }

        /// <summary>
        /// Resolve absolute world position for a CelestialBody at given simulation time.
        /// Handles:
        /// - Travelling ships with OverrideWorldPosition
        /// - Surface stations (lat/lon on parent surface)
        /// - Orbital bodies (recursive parent chain)
        /// - Root bodies (position = Zero)
        /// </summary>
        public SimVec3 Resolve(CelestialBody body, double simTime)
        {
            if (body == null) return SimVec3.Zero;

            // Travelling ship with override position — use it directly.
            if (body.BodyType == CelestialBodyType.Ship
                && body.ShipInfo != null
                && body.ShipInfo.OverrideWorldPosition.HasValue)
            {
                return body.ShipInfo.OverrideWorldPosition.Value;
            }

            // Surface station: parent position + surface offset from lat/lon.
            if (body.AttachmentMode == AttachmentMode.Surface
                && body.StationInfo != null
                && body.ParentId.IsValid)
            {
                var parent = _registry.GetCelestialBody(body.ParentId);
                if (parent != null)
                {
                    SimVec3 parentPos = Resolve(parent, simTime);
                    SimVec3 surfaceOffset = OrbitalPositionCalculator.CalculateSurfacePosition(
                        parent.Radius,
                        body.StationInfo.SurfaceLatitudeDeg,
                        body.StationInfo.SurfaceLongitudeDeg,
                        SurfaceStationOffset);
                    return parentPos + surfaceOffset;
                }
            }

            // Standard orbital body.
            if (body.Orbit != null && body.ParentId.IsValid)
            {
                SimVec3 parentPos = ResolveParentPosition(body.ParentId, simTime, 0);
                return OrbitalPositionCalculator.CalculateAbsolutePosition(
                    body.Orbit, simTime, parentPos);
            }

            // Root body (star) or body with no orbit — position at origin.
            return SimVec3.Zero;
        }

        /// <summary>
        /// Resolve parent position with depth guard against infinite recursion.
        /// </summary>
        private SimVec3 ResolveParentPosition(EntityId parentId, double simTime, int depth)
        {
            if (depth >= MaxParentDepth) return SimVec3.Zero;

            var parent = _registry.GetCelestialBody(parentId);
            if (parent == null) return SimVec3.Zero;

            return Resolve(parent, simTime);
        }
    }
}
