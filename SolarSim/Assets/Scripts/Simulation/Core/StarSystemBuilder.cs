using System.Collections.Generic;
using SpaceSim.Shared.Identifiers;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;
using SpaceSim.World.ValueTypes;

namespace SpaceSim.Simulation.Core
{
    /// <summary>
    /// Input data for building one celestial body. Pure C# — no Unity dependency.
    /// Mirrors CelestialBodyDefinition fields without requiring UnityEngine.
    /// </summary>
    public class CelestialBodyBuildData
    {
        public string Key;
        public string DisplayName;
        public string LocalizationKey;
        public int BodyType;     // Cast from CelestialBodyType enum.
        public string ParentKey;
        public int AttachmentMode; // Cast from AttachmentMode enum.
        public double Radius;
        public bool IsSelectable;
        public bool HasSurface;

        // Orbit.
        public double SemiMajorAxis;
        public double Eccentricity;
        public double InclinationDeg;
        public double LongitudeOfAscendingNodeDeg;
        public double ArgumentOfPeriapsisDeg;
        public double MeanAnomalyAtEpochDeg;
        public double OrbitalPeriod;
        public double EpochTime;
        public bool IsPrograde;

        // Spin.
        public double AxialTiltDeg;
        public double RotationPeriod;
        public double InitialRotationDeg;
    }

    /// <summary>
    /// Input data for building a star system. Pure C#.
    /// </summary>
    public class StarSystemBuildData
    {
        public string SystemKey;
        public string DisplayName;
        public string LocalizationKey;
        public List<CelestialBodyBuildData> Bodies = new List<CelestialBodyBuildData>();
    }

    /// <summary>
    /// Builds runtime StarSystem + CelestialBody entities from build data.
    /// Pure C# — no UnityEngine dependency.
    /// Resolves parent-child hierarchy by Key/ParentKey matching.
    /// </summary>
    public static class StarSystemBuilder
    {
        /// <summary>
        /// Build a star system and register all bodies in the given registry.
        /// </summary>
        public static StarSystem Build(StarSystemBuildData data, WorldRegistry registry)
        {
            var system = new StarSystem(data.DisplayName);
            system.LocalizationKeyName = data.LocalizationKey ?? "";

            // Phase 1: create all bodies and map key -> EntityId.
            var keyToId = new Dictionary<string, EntityId>();
            var keyToBody = new Dictionary<string, CelestialBody>();

            foreach (var bd in data.Bodies)
            {
                var bodyType = (CelestialBodyType)bd.BodyType;
                var body = new CelestialBody(bd.DisplayName, bodyType)
                {
                    Radius = bd.Radius,
                    IsSelectable = bd.IsSelectable,
                    HasSurface = bd.HasSurface,
                    LocalizationKeyName = bd.LocalizationKey ?? "",
                    AttachmentMode = (AttachmentMode)bd.AttachmentMode,
                    Spin = new SpinDefinition
                    {
                        AxialTiltDeg = bd.AxialTiltDeg,
                        RotationPeriod = bd.RotationPeriod,
                        InitialRotationDeg = bd.InitialRotationDeg
                    }
                };

                // Build orbit if this is not a root body.
                if (!string.IsNullOrEmpty(bd.ParentKey))
                {
                    body.Orbit = new OrbitDefinition
                    {
                        SemiMajorAxis = bd.SemiMajorAxis,
                        Eccentricity = bd.Eccentricity,
                        InclinationDeg = bd.InclinationDeg,
                        LongitudeOfAscendingNodeDeg = bd.LongitudeOfAscendingNodeDeg,
                        ArgumentOfPeriapsisDeg = bd.ArgumentOfPeriapsisDeg,
                        MeanAnomalyAtEpochDeg = bd.MeanAnomalyAtEpochDeg,
                        OrbitalPeriod = bd.OrbitalPeriod,
                        EpochTime = bd.EpochTime,
                        IsPrograde = bd.IsPrograde
                    };
                }

                keyToId[bd.Key] = body.Id;
                keyToBody[bd.Key] = body;
            }

            // Phase 2: resolve parent-child relationships.
            foreach (var bd in data.Bodies)
            {
                if (string.IsNullOrEmpty(bd.ParentKey))
                    continue;

                if (!keyToId.TryGetValue(bd.ParentKey, out var parentId))
                    continue; // Parent not found — skip silently.

                if (!keyToBody.TryGetValue(bd.Key, out var child))
                    continue;

                child.ParentId = parentId;

                if (keyToBody.TryGetValue(bd.ParentKey, out var parent))
                    parent.AddChildId(child.Id);
            }

            // Phase 3: register all in registry and system.
            foreach (var bd in data.Bodies)
            {
                if (!keyToBody.TryGetValue(bd.Key, out var body))
                    continue;

                registry.Add(body);

                bool isRoot = string.IsNullOrEmpty(bd.ParentKey);
                system.AddBody(body.Id, isRoot);
            }

            return system;
        }
    }
}
