using System;
using System.Collections.Generic;
using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;

namespace SpaceSim.Simulation.Docking
{
    /// <summary>
    /// Manages the docking lifecycle for ships at stations (orbital and surface).
    /// Handles: approach initiation, approach interpolation, dock completion, undocking.
    /// Pure C# — no UnityEngine dependency. Lives in Simulation layer.
    ///
    /// Orbital station docking:
    /// Ship orbits station → approach to port (station-local interpolation) → Docked
    ///
    /// Surface station docking:
    /// Ship orbits parent body (planet) → approach to station surface position
    /// (parent-body-local interpolation) → Docked
    ///
    /// IMPORTANT: Approach interpolation uses LOCAL coordinates relative to the
    /// appropriate reference body (station for orbital, parent planet for surface).
    /// Each tick the local offset is converted to world by adding reference world position.
    /// This prevents visual glitches when the reference body is moving.
    /// </summary>
    public class DockingSystem
    {
        private readonly WorldRegistry _registry;
        private readonly List<EntityId> _approachingShips = new List<EntityId>();

        /// <summary>Default approach duration in simulation seconds.</summary>
        public double ApproachDuration { get; set; } = 2.0;

        /// <summary>
        /// Fired when a ship completes docking.
        /// Args: shipId, stationId.
        /// </summary>
        public event Action<EntityId, EntityId> OnShipDocked;

        /// <summary>
        /// Fired when a ship undocks.
        /// Args: shipId, stationId.
        /// </summary>
        public event Action<EntityId, EntityId> OnShipUndocked;

        public DockingSystem(WorldRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// Request docking for a ship at an orbital station.
        /// Ship must be in Orbiting state and parented to the station.
        /// Station must be orbital and have a free docking port.
        /// Returns true if docking approach was initiated.
        /// </summary>
        public bool RequestDocking(
            EntityId shipId,
            EntityId stationId,
            double currentSimTime,
            Func<EntityId, double, SimVec3> positionResolver)
        {
            var ship = _registry.GetCelestialBody(shipId);
            var station = _registry.GetCelestialBody(stationId);

            if (ship == null || ship.ShipInfo == null) return false;
            if (station == null || station.StationInfo == null) return false;
            if (!station.StationInfo.HasDocking) return false;
            if (ship.ShipInfo.State != ShipState.Orbiting) return false;

            bool isSurface = station.StationInfo.Kind == StationKind.Surface;

            if (isSurface)
            {
                return RequestSurfaceDocking(ship, station, currentSimTime, positionResolver);
            }
            else
            {
                return RequestOrbitalDocking(ship, station, currentSimTime, positionResolver);
            }
        }

        /// <summary>
        /// Request docking at an orbital station.
        /// Ship must be parented to the station (orbiting it).
        /// Approach interpolation is in station-local space.
        /// </summary>
        private bool RequestOrbitalDocking(
            CelestialBody ship, CelestialBody station,
            double currentSimTime, Func<EntityId, double, SimVec3> positionResolver)
        {
            // Ship must be orbiting the station.
            if (ship.ParentId != station.Id) return false;

            var port = station.StationInfo.Docking.RequestPort(ship.Id);
            if (port == null) return false;

            SimVec3 shipWorldPos = SimVec3.Zero;
            SimVec3 stationWorldPos = SimVec3.Zero;

            if (positionResolver != null)
            {
                shipWorldPos = positionResolver(ship.Id, currentSimTime);
                stationWorldPos = positionResolver(station.Id, currentSimTime);
            }

            // LOCAL start position relative to station.
            SimVec3 startLocal = shipWorldPos - stationWorldPos;

            ship.ShipInfo.State = ShipState.ApproachingStation;
            ship.ShipInfo.DockedAtStationId = station.Id;
            ship.ShipInfo.DockedPortId = port.PortId;
            ship.ShipInfo.DockingStartTime = currentSimTime;
            ship.ShipInfo.DockingDuration = ApproachDuration;
            ship.ShipInfo.DockingStartPosition = startLocal;
            ship.ShipInfo.OverrideWorldPosition = shipWorldPos;

            // Reference body for interpolation = station itself.
            ship.ShipInfo.DockingReferenceBodyId = station.Id;

            ship.Orbit = null;
            ship.AttachmentMode = AttachmentMode.None;

            if (!_approachingShips.Contains(ship.Id))
                _approachingShips.Add(ship.Id);

            return true;
        }

        /// <summary>
        /// Request docking at a surface station.
        /// Ship must be orbiting the station's parent body (the planet).
        /// Approach interpolation is in parent-body-local space.
        /// </summary>
        private bool RequestSurfaceDocking(
            CelestialBody ship, CelestialBody station,
            double currentSimTime, Func<EntityId, double, SimVec3> positionResolver)
        {
            // For surface stations, ship orbits the parent planet, not the station.
            EntityId parentBodyId = station.ParentId;
            if (!parentBodyId.IsValid) return false;
            if (ship.ParentId != parentBodyId) return false;

            var port = station.StationInfo.Docking.RequestPort(ship.Id);
            if (port == null) return false;

            SimVec3 shipWorldPos = SimVec3.Zero;
            SimVec3 parentWorldPos = SimVec3.Zero;
            SimVec3 stationWorldPos = SimVec3.Zero;

            if (positionResolver != null)
            {
                shipWorldPos = positionResolver(ship.Id, currentSimTime);
                parentWorldPos = positionResolver(parentBodyId, currentSimTime);
                stationWorldPos = positionResolver(station.Id, currentSimTime);
            }

            // LOCAL start position relative to parent body (the planet).
            SimVec3 startLocal = shipWorldPos - parentWorldPos;

            // Target local position: station position relative to parent body + port offset.
            // (this is computed each tick in UpdateApproach)

            ship.ShipInfo.State = ShipState.ApproachingStation;
            ship.ShipInfo.DockedAtStationId = station.Id;
            ship.ShipInfo.DockedPortId = port.PortId;
            ship.ShipInfo.DockingStartTime = currentSimTime;
            ship.ShipInfo.DockingDuration = ApproachDuration;
            ship.ShipInfo.DockingStartPosition = startLocal;
            ship.ShipInfo.OverrideWorldPosition = shipWorldPos;

            // Reference body for interpolation = parent planet.
            ship.ShipInfo.DockingReferenceBodyId = parentBodyId;

            // Re-parent ship to station for hierarchy visibility.
            var parentBody = _registry.GetCelestialBody(parentBodyId);
            parentBody?.RemoveChildId(ship.Id);
            ship.ParentId = station.Id;
            station.AddChildId(ship.Id);

            ship.Orbit = null;
            ship.AttachmentMode = AttachmentMode.None;

            if (!_approachingShips.Contains(ship.Id))
                _approachingShips.Add(ship.Id);

            return true;
        }

        /// <summary>
        /// Tick the docking system. Updates approaching ships' positions.
        /// Call each frame/tick after ShipMovementSystem.Update().
        /// </summary>
        public void Update(double currentSimTime, Func<EntityId, double, SimVec3> positionResolver)
        {
            for (int i = _approachingShips.Count - 1; i >= 0; i--)
            {
                var shipId = _approachingShips[i];
                var ship = _registry.GetCelestialBody(shipId);

                if (ship?.ShipInfo == null ||
                    ship.ShipInfo.State != ShipState.ApproachingStation)
                {
                    _approachingShips.RemoveAt(i);
                    continue;
                }

                UpdateApproach(ship, currentSimTime, positionResolver);
            }
        }

        /// <summary>
        /// Undock a ship from its current station.
        /// For orbital stations: ship returns to orbit around the station.
        /// For surface stations: ship returns to orbit around the station's parent body.
        /// Returns true if undocking succeeded.
        /// </summary>
        public bool Undock(EntityId shipId, double currentSimTime)
        {
            var ship = _registry.GetCelestialBody(shipId);
            if (ship?.ShipInfo == null) return false;
            if (ship.ShipInfo.State != ShipState.Docked) return false;

            var stationId = ship.ShipInfo.DockedAtStationId;
            var station = _registry.GetCelestialBody(stationId);

            // Release port on station.
            if (station?.StationInfo?.Docking != null)
            {
                station.StationInfo.Docking.ReleasePort(shipId);
            }

            bool isSurface = station?.StationInfo?.Kind == StationKind.Surface;

            const double stationOrbitRadius = 0.5;
            const double stationOrbitPeriod = 8.0;
            double phase = (currentSimTime * 73.0) % 360.0;

            if (isSurface && station != null && station.ParentId.IsValid)
            {
                // Surface station: return to orbit around parent body (planet).
                var parentBody = _registry.GetCelestialBody(station.ParentId);

                // Re-parent from station to planet.
                station.RemoveChildId(shipId);
                ship.ParentId = station.ParentId;
                parentBody?.AddChildId(shipId);

                // Use a reasonable orbit around the planet.
                double planetOrbitRadius = 3.0;
                double planetOrbitPeriod = 12.0;

                ship.Orbit = new World.ValueTypes.OrbitDefinition
                {
                    SemiMajorAxis = planetOrbitRadius,
                    Eccentricity = 0.0,
                    InclinationDeg = 0.0,
                    LongitudeOfAscendingNodeDeg = 0.0,
                    ArgumentOfPeriapsisDeg = 0.0,
                    MeanAnomalyAtEpochDeg = phase,
                    OrbitalPeriod = planetOrbitPeriod,
                    EpochTime = 0.0,
                    IsPrograde = true
                };
            }
            else
            {
                // Orbital station: return to orbit around the station.
                ship.Orbit = new World.ValueTypes.OrbitDefinition
                {
                    SemiMajorAxis = stationOrbitRadius,
                    Eccentricity = 0.0,
                    InclinationDeg = 0.0,
                    LongitudeOfAscendingNodeDeg = 0.0,
                    ArgumentOfPeriapsisDeg = 0.0,
                    MeanAnomalyAtEpochDeg = phase,
                    OrbitalPeriod = stationOrbitPeriod,
                    EpochTime = 0.0,
                    IsPrograde = true
                };
            }

            ship.AttachmentMode = AttachmentMode.Orbit;
            ship.ShipInfo.State = ShipState.Orbiting;
            ship.ShipInfo.OverrideWorldPosition = null;
            ship.ShipInfo.ClearDockingState();

            OnShipUndocked?.Invoke(shipId, stationId);
            return true;
        }

        /// <summary>
        /// Check if a station has free docking ports.
        /// </summary>
        public bool HasFreePort(EntityId stationId)
        {
            var station = _registry.GetCelestialBody(stationId);
            if (station?.StationInfo?.Docking == null) return false;
            return station.StationInfo.Docking.HasFreePort;
        }

        /// <summary>Number of ships currently in approach phase.</summary>
        public int ApproachingCount => _approachingShips.Count;

        // ---------------------------------------------------------------
        // Internal: approach interpolation (in reference-body-local space)
        // ---------------------------------------------------------------

        private void UpdateApproach(
            CelestialBody ship,
            double currentSimTime,
            Func<EntityId, double, SimVec3> positionResolver)
        {
            var info = ship.ShipInfo;
            double elapsed = currentSimTime - info.DockingStartTime;
            double progress = info.DockingDuration > 0.0
                ? elapsed / info.DockingDuration
                : 1.0;

            if (progress >= 1.0)
            {
                CompleteDocking(ship, currentSimTime);
                return;
            }

            // Get reference body world position.
            // For orbital stations: reference = station.
            // For surface stations: reference = parent planet.
            EntityId refBodyId = info.DockingReferenceBodyId;
            SimVec3 refWorldPos = positionResolver != null
                ? positionResolver(refBodyId, currentSimTime)
                : SimVec3.Zero;

            // Compute target local offset (port position relative to reference body).
            SimVec3 targetLocal = ComputeTargetLocalOffset(info, currentSimTime, positionResolver);

            // Interpolate in LOCAL space.
            SimVec3 currentLocal = Lerp(info.DockingStartPosition, targetLocal, progress);

            // Convert to world.
            info.OverrideWorldPosition = refWorldPos + currentLocal;
        }

        /// <summary>
        /// Compute the target local offset for the docking port, relative to the reference body.
        /// Orbital station: port local offset (relative to station = reference).
        /// Surface station: station position relative to parent planet + port offset.
        /// </summary>
        private SimVec3 ComputeTargetLocalOffset(
            ShipInfo info, double simTime, Func<EntityId, double, SimVec3> positionResolver)
        {
            var station = _registry.GetCelestialBody(info.DockedAtStationId);
            if (station == null) return SimVec3.Zero;

            SimVec3 portLocalOffset = GetPortLocalOffset(info.DockedAtStationId, info.DockedPortId);

            bool isSurface = station.StationInfo?.Kind == StationKind.Surface;

            if (isSurface && positionResolver != null)
            {
                // Surface station: target = (station world pos - reference world pos) + port offset.
                // Reference is the parent planet.
                SimVec3 stationWorldPos = positionResolver(station.Id, simTime);
                SimVec3 refWorldPos = positionResolver(info.DockingReferenceBodyId, simTime);
                SimVec3 stationLocalToRef = stationWorldPos - refWorldPos;
                return stationLocalToRef + portLocalOffset;
            }
            else
            {
                // Orbital station: port is already local to station (= reference body).
                return portLocalOffset;
            }
        }

        private void CompleteDocking(CelestialBody ship, double currentSimTime)
        {
            var info = ship.ShipInfo;

            // Clear override — docked ships get position from WorldPositionResolver.ResolveDocked().
            info.OverrideWorldPosition = null;
            info.State = ShipState.Docked;
            info.DockedAtTime = currentSimTime;

            ship.AttachmentMode = AttachmentMode.LocalSpace;

            _approachingShips.Remove(ship.Id);

            OnShipDocked?.Invoke(ship.Id, info.DockedAtStationId);
        }

        /// <summary>
        /// Get the LOCAL position offset of a docking port relative to station center.
        /// </summary>
        private SimVec3 GetPortLocalOffset(EntityId stationId, int portId)
        {
            var station = _registry.GetCelestialBody(stationId);
            if (station?.StationInfo?.Docking != null)
            {
                var ports = station.StationInfo.Docking.Ports;
                if (portId >= 0 && portId < ports.Count)
                {
                    return ports[portId].LocalPosition;
                }
            }
            return SimVec3.Zero;
        }

        private static SimVec3 Lerp(SimVec3 a, SimVec3 b, double t)
        {
            return new SimVec3(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t,
                a.Z + (b.Z - a.Z) * t);
        }
    }
}
