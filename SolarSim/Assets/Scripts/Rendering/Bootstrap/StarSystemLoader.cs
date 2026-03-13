using SpaceSim.Data.Definitions;
using SpaceSim.Simulation.Core;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;

namespace SpaceSim.Rendering.Bootstrap
{
    /// <summary>
    /// Converts a StarSystemDefinition ScriptableObject into pure build data
    /// and invokes StarSystemBuilder to create runtime entities.
    /// Thin integration layer between Unity assets and the pure-C# builder.
    /// </summary>
    public static class StarSystemLoader
    {
        /// <summary>
        /// Load a star system from a definition asset into the given registry.
        /// Returns the built StarSystem, or null if definition is null/empty.
        /// </summary>
        public static StarSystem Load(StarSystemDefinition definition, WorldRegistry registry)
        {
            if (definition == null || definition.Bodies == null || definition.Bodies.Count == 0)
                return null;

            var buildData = ConvertToBuildData(definition);
            return StarSystemBuilder.Build(buildData, registry);
        }

        private static StarSystemBuildData ConvertToBuildData(StarSystemDefinition def)
        {
            var data = new StarSystemBuildData
            {
                SystemKey = def.SystemKey,
                DisplayName = def.DisplayName,
                LocalizationKey = def.LocalizationKey
            };

            foreach (var bodyDef in def.Bodies)
            {
                data.Bodies.Add(new CelestialBodyBuildData
                {
                    Key = bodyDef.Key,
                    DisplayName = bodyDef.DisplayName,
                    LocalizationKey = bodyDef.LocalizationKey,
                    BodyType = (int)bodyDef.BodyType,
                    ParentKey = bodyDef.ParentKey,
                    AttachmentMode = (int)bodyDef.AttachmentMode,
                    Radius = bodyDef.Radius,
                    IsSelectable = bodyDef.IsSelectable,
                    HasSurface = bodyDef.HasSurface,

                    SemiMajorAxis = bodyDef.SemiMajorAxis,
                    Eccentricity = bodyDef.Eccentricity,
                    InclinationDeg = bodyDef.InclinationDeg,
                    LongitudeOfAscendingNodeDeg = bodyDef.LongitudeOfAscendingNodeDeg,
                    ArgumentOfPeriapsisDeg = bodyDef.ArgumentOfPeriapsisDeg,
                    MeanAnomalyAtEpochDeg = bodyDef.MeanAnomalyAtEpochDeg,
                    OrbitalPeriod = bodyDef.OrbitalPeriod,
                    EpochTime = bodyDef.EpochTime,
                    IsPrograde = bodyDef.IsPrograde,

                    AxialTiltDeg = bodyDef.AxialTiltDeg,
                    RotationPeriod = bodyDef.RotationPeriod,
                    InitialRotationDeg = bodyDef.InitialRotationDeg
                });
            }

            return data;
        }
    }
}
