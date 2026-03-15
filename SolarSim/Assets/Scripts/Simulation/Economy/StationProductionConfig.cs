using System.Collections.Generic;
using SpaceSim.World.Entities;

namespace SpaceSim.Simulation.Economy
{
    /// <summary>
    /// Static configuration for station production recipes.
    /// Maps station localization keys to their production definitions.
    /// Pure C# — no Unity dependency.
    ///
    /// Production chain (sample system):
    ///   Terra-1 → produces Food (basic, no inputs)
    ///   Orbita-1 → consumes Food, produces Electronics
    ///   Ares-1 → consumes Electronics, produces Metals
    ///   Phobos → consumes Metals, produces Fuel
    /// </summary>
    public static class StationProductionConfig
    {
        /// <summary>
        /// Get the production recipe for a station by its localization key.
        /// Returns null if this station has no production capability.
        /// </summary>
        public static StationProductionRecipe GetRecipe(string stationLocKey)
        {
            switch (stationLocKey)
            {
                case "station.terra1":
                    // Basic producer: Food from nothing (agricultural base).
                    return new StationProductionRecipe(
                        output: ResourceType.Food,
                        outputAmount: 10.0,
                        cycleTime: 5.0);

                case "station.orbita1":
                    // Converts Food into Electronics.
                    return new StationProductionRecipe(
                        output: ResourceType.Electronics,
                        outputAmount: 5.0,
                        cycleTime: 8.0,
                        inputs: new Dictionary<ResourceType, double>
                        {
                            { ResourceType.Food, 8.0 }
                        });

                case "station.ares1":
                    // Converts Electronics into Metals.
                    return new StationProductionRecipe(
                        output: ResourceType.Metals,
                        outputAmount: 8.0,
                        cycleTime: 7.0,
                        inputs: new Dictionary<ResourceType, double>
                        {
                            { ResourceType.Electronics, 4.0 }
                        });

                case "station.phobos":
                    // Converts Metals into Fuel.
                    return new StationProductionRecipe(
                        output: ResourceType.Fuel,
                        outputAmount: 12.0,
                        cycleTime: 6.0,
                        inputs: new Dictionary<ResourceType, double>
                        {
                            { ResourceType.Metals, 6.0 }
                        });

                default:
                    return null;
            }
        }
    }
}
