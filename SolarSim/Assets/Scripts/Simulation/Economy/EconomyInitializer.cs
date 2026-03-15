using SpaceSim.World.Entities;
using SpaceSim.World.Systems;

namespace SpaceSim.Simulation.Economy
{
    /// <summary>
    /// Initializes economy data (station storage, ship cargo) after star system is built.
    /// Call once after StarSystemBuilder.Build() or SampleStarSystemFactory.Create().
    /// Pure C# — no Unity dependency.
    /// </summary>
    public static class EconomyInitializer
    {
        /// <summary>
        /// Initialize storage on all stations and cargo on all ships in the registry.
        /// Safe to call multiple times — skips entities that already have storage/cargo.
        /// </summary>
        public static void Initialize(WorldRegistry registry)
        {
            if (registry == null) return;

            foreach (var body in registry.AllCelestialBodies)
            {
                if (body.BodyType == CelestialBodyType.Station && body.StationInfo != null)
                {
                    InitializeStationStorage(body);
                }
                else if (body.BodyType == CelestialBodyType.Ship && body.ShipInfo != null)
                {
                    InitializeShipCargo(body);
                }
            }
        }

        private static void InitializeStationStorage(CelestialBody station)
        {
            if (station.StationInfo.Storage != null) return; // Already initialized.

            station.StationInfo.Storage = new StationStorage();

            var initialResources = StationEconomyConfig.GetInitialResources(
                station.LocalizationKeyName ?? "");

            if (initialResources != null)
            {
                foreach (var kvp in initialResources)
                {
                    station.StationInfo.Storage.SetAmount(kvp.Key, kvp.Value);
                }
            }
        }

        private static void InitializeShipCargo(CelestialBody ship)
        {
            if (ship.ShipInfo.Cargo != null) return; // Already initialized.

            double capacity = StationEconomyConfig.GetCargoCapacity(ship.ShipInfo.Role);
            ship.ShipInfo.Cargo = new ShipCargo(capacity);
        }
    }
}
