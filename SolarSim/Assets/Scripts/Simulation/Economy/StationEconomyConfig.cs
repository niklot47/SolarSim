using System.Collections.Generic;
using SpaceSim.World.Entities;

namespace SpaceSim.Simulation.Economy
{
    /// <summary>
    /// Static configuration for station starting resources.
    /// Maps station localization keys to their initial resource loadouts.
    /// Pure C# — no Unity dependency.
    ///
    /// In the future this could come from ScriptableObjects or JSON,
    /// but for the foundation this is a simple hardcoded config.
    /// </summary>
    public static class StationEconomyConfig
    {
        /// <summary>Default cargo capacity for trader ships.</summary>
        public const double DefaultTraderCargoCapacity = 100.0;

        /// <summary>Default cargo capacity for other NPC ships.</summary>
        public const double DefaultShipCargoCapacity = 50.0;

        /// <summary>Amount of resource a station starts with per type.</summary>
        public const double DefaultStationResourceAmount = 200.0;

        /// <summary>
        /// Get the initial resource loadout for a station by its localization key.
        /// Returns null if no config exists for this station (station gets empty storage).
        /// </summary>
        public static Dictionary<ResourceType, double> GetInitialResources(string stationLocKey)
        {
            switch (stationLocKey)
            {
                case "station.terra1":
                    // Terra-1 (surface base) produces Food.
                    return new Dictionary<ResourceType, double>
                    {
                        { ResourceType.Food, DefaultStationResourceAmount },
                        { ResourceType.Fuel, DefaultStationResourceAmount * 0.5 }
                    };

                case "station.orbita1":
                    // Orbita-1 (orbital station near Terra) — electronics hub.
                    return new Dictionary<ResourceType, double>
                    {
                        { ResourceType.Electronics, DefaultStationResourceAmount },
                        { ResourceType.Fuel, DefaultStationResourceAmount * 0.3 }
                    };

                case "station.ares1":
                    // Ares-1 (surface base) produces Metals.
                    return new Dictionary<ResourceType, double>
                    {
                        { ResourceType.Metals, DefaultStationResourceAmount },
                        { ResourceType.Fuel, DefaultStationResourceAmount * 0.5 }
                    };

                case "station.phobos":
                    // Phobos (orbital station near Ares) — fuel depot.
                    return new Dictionary<ResourceType, double>
                    {
                        { ResourceType.Fuel, DefaultStationResourceAmount },
                        { ResourceType.Metals, DefaultStationResourceAmount * 0.3 }
                    };

                default:
                    return null;
            }
        }

        /// <summary>
        /// Get the cargo capacity for a ship based on its role.
        /// </summary>
        public static double GetCargoCapacity(ShipRole role)
        {
            switch (role)
            {
                case ShipRole.Trader: return DefaultTraderCargoCapacity;
                case ShipRole.Civilian: return DefaultShipCargoCapacity;
                case ShipRole.Patrol: return DefaultShipCargoCapacity * 0.5;
                case ShipRole.Player: return DefaultTraderCargoCapacity;
                default: return DefaultShipCargoCapacity;
            }
        }
    }
}
