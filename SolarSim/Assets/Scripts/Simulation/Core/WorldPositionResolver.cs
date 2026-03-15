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
    /// Handles orbital bodies, surface stations, travelling ships, docked ships,
    /// and recursive parent chains.
    /// Pure C# — no UnityEngine dependency. Lives in Simulation layer.
    ///
    /// Resolution order for ships:
    /// 1. Docked → station world pos + port local offset (dynamic each tick)
    /// 2. Override position → used for Travelling and ApproachingStation
    /// 3. Orbital → standard parent chain + orbit calculation
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
        /// - Docked ships (station position + port local offset) — CHECKED FIRST
        /// - Travelling / approaching ships with OverrideWorldPosition
        /// - Surface stations (lat/lon on parent surface)
        /// - Orbital bodies (recursive parent chain)
        /// - Root bodies (position = Zero)
        /// </summary>
        public SimVec3 Resolve(CelestialBody body, double simTime)
        {
            if (body == null) return SimVec3.Zero;

            if (body.BodyType == CelestialBodyType.Ship && body.ShipInfo != null)
            {
                // PRIORITY 1: Docked ship — always compute from station + port offset.
                // This MUST be checked before OverrideWorldPosition because docked ships
                // have OverrideWorldPosition cleared, but even if it somehow gets set,
                // the docked position should take priority.
                if (body.ShipInfo.State == ShipState.Docked
                    && body.ShipInfo.DockedAtStationId.IsValid)
                {
                    return ResolveDocked(body, simTime);
                }

                // PRIORITY 2: Override position — used during Travelling and ApproachingStation.
                // DockingSystem sets OverrideWorldPosition each tick during approach.
                // ShipMovementSystem sets it during travel.
                if (body.ShipInfo.OverrideWorldPosition.HasValue)
                {
                    return body.ShipInfo.OverrideWorldPosition.Value;
                }
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
        /// Resolve position of a docked ship: station world position + port local offset.
        /// Computed dynamically each tick so the ship follows the moving station.
        /// </summary>
        private SimVec3 ResolveDocked(CelestialBody ship, double simTime)
        {
            var stationId = ship.ShipInfo.DockedAtStationId;
            var station = _registry.GetCelestialBody(stationId);
            if (station == null) return SimVec3.Zero;

            SimVec3 stationPos = Resolve(station, simTime);

            // Add port local offset if available.
            if (station.StationInfo?.Docking != null)
            {
                int portId = ship.ShipInfo.DockedPortId;
                var ports = station.StationInfo.Docking.Ports;
                if (portId >= 0 && portId < ports.Count)
                {
                    return stationPos + ports[portId].LocalPosition;
                }
            }

            return stationPos;
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
