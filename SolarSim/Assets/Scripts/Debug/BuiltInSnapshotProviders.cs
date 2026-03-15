using System.Collections.Generic;
using SpaceSim.Shared.Identifiers;
using SpaceSim.Simulation.Core;
using SpaceSim.Simulation.Docking;
using SpaceSim.Simulation.Ships;
using SpaceSim.Simulation.SOI;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;

namespace SpaceSim.Debug
{
    /// <summary>
    /// Snapshot provider for the ship subsystem.
    /// Reports ship counts by state and role.
    /// </summary>
    public class ShipSnapshotProvider : IDebugSnapshotProvider
    {
        private readonly WorldRegistry _registry;
        public string ProviderName => "Ships";

        public ShipSnapshotProvider(WorldRegistry registry) { _registry = registry; }

        public SubsystemSnapshot CaptureSnapshot()
        {
            var snap = new SubsystemSnapshot { Name = ProviderName };
            if (_registry == null) { snap.Status = "unavailable"; return snap; }

            int total = 0, orbiting = 0, travelling = 0, docked = 0, approaching = 0, idle = 0;
            int traders = 0, patrol = 0, civilian = 0, player = 0;

            foreach (var body in _registry.AllCelestialBodies)
            {
                if (body.BodyType != CelestialBodyType.Ship || body.ShipInfo == null) continue;
                total++;
                switch (body.ShipInfo.State)
                {
                    case ShipState.Orbiting: orbiting++; break;
                    case ShipState.Travelling: travelling++; break;
                    case ShipState.Docked: docked++; break;
                    case ShipState.ApproachingStation: approaching++; break;
                    case ShipState.Idle: idle++; break;
                }
                switch (body.ShipInfo.Role)
                {
                    case ShipRole.Trader: traders++; break;
                    case ShipRole.Patrol: patrol++; break;
                    case ShipRole.Civilian: civilian++; break;
                    case ShipRole.Player: player++; break;
                }
            }

            snap.Status = $"{total} ships";
            snap.Data["total"] = total;
            snap.Data["orbiting"] = orbiting;
            snap.Data["travelling"] = travelling;
            snap.Data["docked"] = docked;
            snap.Data["approaching"] = approaching;
            snap.Data["idle"] = idle;
            snap.Data["traders"] = traders;
            snap.Data["patrol"] = patrol;
            snap.Data["civilian"] = civilian;
            snap.Data["player"] = player;
            return snap;
        }
    }

    /// <summary>
    /// Snapshot provider for the economy subsystem.
    /// Reports station storage and ship cargo totals.
    /// </summary>
    public class EconomySnapshotProvider : IDebugSnapshotProvider
    {
        private readonly WorldRegistry _registry;
        public string ProviderName => "Economy";

        public EconomySnapshotProvider(WorldRegistry registry) { _registry = registry; }

        public SubsystemSnapshot CaptureSnapshot()
        {
            var snap = new SubsystemSnapshot { Name = ProviderName };
            if (_registry == null) { snap.Status = "unavailable"; return snap; }

            int stationsWithStorage = 0;
            double totalStationResources = 0;
            int shipsWithCargo = 0;
            double totalShipCargo = 0;
            var resourceTotals = new Dictionary<string, double>();

            foreach (var body in _registry.AllCelestialBodies)
            {
                if (body.StationInfo?.Storage != null)
                {
                    stationsWithStorage++;
                    foreach (var kvp in body.StationInfo.Storage.GetAll())
                    {
                        totalStationResources += kvp.Value;
                        string key = $"station_{kvp.Key}";
                        if (!resourceTotals.ContainsKey(key)) resourceTotals[key] = 0;
                        resourceTotals[key] += kvp.Value;
                    }
                }

                if (body.ShipInfo?.Cargo != null && !body.ShipInfo.Cargo.IsEmpty)
                {
                    shipsWithCargo++;
                    totalShipCargo += body.ShipInfo.Cargo.TotalUsed;
                    foreach (var kvp in body.ShipInfo.Cargo.GetAll())
                    {
                        string key = $"ship_{kvp.Key}";
                        if (!resourceTotals.ContainsKey(key)) resourceTotals[key] = 0;
                        resourceTotals[key] += kvp.Value;
                    }
                }
            }

            snap.Status = $"{stationsWithStorage} stations, {shipsWithCargo} ships with cargo";
            snap.Data["stationsWithStorage"] = stationsWithStorage;
            snap.Data["totalStationResources"] = totalStationResources;
            snap.Data["shipsWithCargo"] = shipsWithCargo;
            snap.Data["totalShipCargo"] = totalShipCargo;

            foreach (var kvp in resourceTotals)
                snap.Data[kvp.Key] = kvp.Value;

            return snap;
        }
    }

    /// <summary>
    /// Snapshot provider for the docking subsystem.
    /// Reports docking port counts and occupancy.
    /// </summary>
    public class DockingSnapshotProvider : IDebugSnapshotProvider
    {
        private readonly WorldRegistry _registry;
        private readonly DockingSystem _dockingSystem;
        public string ProviderName => "Docking";

        public DockingSnapshotProvider(WorldRegistry registry, DockingSystem dockingSystem)
        {
            _registry = registry;
            _dockingSystem = dockingSystem;
        }

        public SubsystemSnapshot CaptureSnapshot()
        {
            var snap = new SubsystemSnapshot { Name = ProviderName };
            if (_registry == null) { snap.Status = "unavailable"; return snap; }

            int totalPorts = 0, occupiedPorts = 0, stationsWithDocking = 0;

            foreach (var body in _registry.AllCelestialBodies)
            {
                if (body.StationInfo?.Docking == null) continue;
                stationsWithDocking++;
                totalPorts += body.StationInfo.Docking.TotalPorts;
                occupiedPorts += body.StationInfo.Docking.OccupiedCount;
            }

            int approachingCount = _dockingSystem?.ApproachingCount ?? 0;

            snap.Status = $"{occupiedPorts}/{totalPorts} ports occupied";
            snap.Data["stationsWithDocking"] = stationsWithDocking;
            snap.Data["totalPorts"] = totalPorts;
            snap.Data["occupiedPorts"] = occupiedPorts;
            snap.Data["freePorts"] = totalPorts - occupiedPorts;
            snap.Data["approachingShips"] = approachingCount;
            return snap;
        }
    }

    /// <summary>
    /// Snapshot provider for the SOI subsystem.
    /// Reports SOI body count and current ship SOI assignments.
    /// </summary>
    public class SOISnapshotProvider : IDebugSnapshotProvider
    {
        private readonly WorldRegistry _registry;
        private readonly SOIResolver _soiResolver;
        public string ProviderName => "SOI";

        public SOISnapshotProvider(WorldRegistry registry, SOIResolver soiResolver)
        {
            _registry = registry;
            _soiResolver = soiResolver;
        }

        public SubsystemSnapshot CaptureSnapshot()
        {
            var snap = new SubsystemSnapshot { Name = ProviderName };
            if (_registry == null || _soiResolver == null) { snap.Status = "unavailable"; return snap; }

            snap.Status = _soiResolver.GetStatus();

            // Count ships per SOI body.
            var soiCounts = new Dictionary<string, int>();
            foreach (var body in _registry.AllCelestialBodies)
            {
                if (body.ShipInfo == null) continue;
                string soiName = "none";
                if (body.ShipInfo.CurrentSOIBodyId.IsValid)
                {
                    var soiBody = _registry.GetCelestialBody(body.ShipInfo.CurrentSOIBodyId);
                    soiName = soiBody?.DisplayName ?? body.ShipInfo.CurrentSOIBodyId.ToString();
                }
                if (!soiCounts.ContainsKey(soiName)) soiCounts[soiName] = 0;
                soiCounts[soiName]++;
            }

            foreach (var kvp in soiCounts)
                snap.Data[$"ships_in_{kvp.Key}"] = kvp.Value;

            return snap;
        }
    }

    /// <summary>
    /// Snapshot provider for the world registry itself.
    /// Reports entity counts by type.
    /// </summary>
    public class WorldSnapshotProvider : IDebugSnapshotProvider
    {
        private readonly WorldRegistry _registry;
        public string ProviderName => "World";

        public WorldSnapshotProvider(WorldRegistry registry) { _registry = registry; }

        public SubsystemSnapshot CaptureSnapshot()
        {
            var snap = new SubsystemSnapshot { Name = ProviderName };
            if (_registry == null) { snap.Status = "unavailable"; return snap; }

            int stars = 0, planets = 0, moons = 0, stations = 0, ships = 0, other = 0;

            foreach (var body in _registry.AllCelestialBodies)
            {
                switch (body.BodyType)
                {
                    case CelestialBodyType.Star: stars++; break;
                    case CelestialBodyType.Planet: planets++; break;
                    case CelestialBodyType.Moon: moons++; break;
                    case CelestialBodyType.Station: stations++; break;
                    case CelestialBodyType.Ship: ships++; break;
                    default: other++; break;
                }
            }

            int total = _registry.Count;
            snap.Status = $"{total} entities";
            snap.Data["total"] = total;
            snap.Data["stars"] = stars;
            snap.Data["planets"] = planets;
            snap.Data["moons"] = moons;
            snap.Data["stations"] = stations;
            snap.Data["ships"] = ships;
            snap.Data["other"] = other;
            return snap;
        }
    }
}
