#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using SpaceSim.Data.Definitions;
using SpaceSim.World.Entities;

namespace SpaceSim.Data.Editor
{
    /// <summary>
    /// Editor utility to create the sample star system as a data asset.
    /// Menu: SpaceSim -> Create Sample Star System Asset.
    /// </summary>
    public static class SampleSystemAssetCreator
    {
        [MenuItem("SpaceSim/Create Sample Star System Asset")]
        public static void CreateSampleAsset()
        {
            var asset = ScriptableObject.CreateInstance<StarSystemDefinition>();
            asset.SystemKey = "test_system";
            asset.DisplayName = "Test System";
            asset.LocalizationKey = "system.test";

            // Star.
            asset.Bodies.Add(new CelestialBodyDefinition
            {
                Key = "sol",
                DisplayName = "Sol",
                LocalizationKey = "body.sol",
                BodyType = CelestialBodyType.Star,
                ParentKey = "",
                AttachmentMode = AttachmentMode.None,
                Radius = 5.0,
                IsSelectable = true,
                HasSurface = false,
                RotationPeriod = 600.0
            });

            // Planet 1.
            asset.Bodies.Add(new CelestialBodyDefinition
            {
                Key = "terra",
                DisplayName = "Terra",
                LocalizationKey = "body.terra",
                BodyType = CelestialBodyType.Planet,
                ParentKey = "sol",
                AttachmentMode = AttachmentMode.Orbit,
                Radius = 1.5,
                IsSelectable = true,
                HasSurface = true,
                SemiMajorAxis = 30.0,
                OrbitalPeriod = 120.0,
                MeanAnomalyAtEpochDeg = 0.0,
                RotationPeriod = 30.0
            });

            // Planet 2.
            asset.Bodies.Add(new CelestialBodyDefinition
            {
                Key = "ares",
                DisplayName = "Ares",
                LocalizationKey = "body.ares",
                BodyType = CelestialBodyType.Planet,
                ParentKey = "sol",
                AttachmentMode = AttachmentMode.Orbit,
                Radius = 1.0,
                IsSelectable = true,
                HasSurface = true,
                SemiMajorAxis = 55.0,
                OrbitalPeriod = 240.0,
                MeanAnomalyAtEpochDeg = 90.0,
                RotationPeriod = 25.0
            });

            // Moon.
            asset.Bodies.Add(new CelestialBodyDefinition
            {
                Key = "luna",
                DisplayName = "Luna",
                LocalizationKey = "body.luna",
                BodyType = CelestialBodyType.Moon,
                ParentKey = "terra",
                AttachmentMode = AttachmentMode.Orbit,
                Radius = 0.4,
                IsSelectable = true,
                HasSurface = true,
                SemiMajorAxis = 5.0,
                OrbitalPeriod = 20.0,
                MeanAnomalyAtEpochDeg = 45.0,
                RotationPeriod = 20.0
            });

            // --- Ships ---

            // Player ship orbiting Terra.
            asset.Ships.Add(new ShipDefinition
            {
                Key = "ship_aurora",
                DisplayName = "\u041a\u043e\u0440\u0432\u0435\u0442 \u00ab\u0410\u0432\u0440\u043e\u0440\u0430\u00bb",
                LocalizationKey = "ship.aurora",
                Role = ShipRole.Player,
                ShipClass = "Corvette",
                ParentBodyKey = "terra",
                Radius = 0.15,
                OrbitalRadius = 3.5,
                OrbitalPeriod = 12.0,
                StartAngleDeg = 0.0
            });

            // NPC trader orbiting Terra.
            asset.Ships.Add(new ShipDefinition
            {
                Key = "ship_cargo7",
                DisplayName = "\u0422\u0440\u0430\u043d\u0441\u043f\u043e\u0440\u0442 \u00ab\u041a\u0430\u0440\u0433\u043e-7\u00bb",
                LocalizationKey = "ship.cargo7",
                Role = ShipRole.Trader,
                ShipClass = "Freighter",
                ParentBodyKey = "terra",
                Radius = 0.12,
                OrbitalRadius = 4.0,
                OrbitalPeriod = 15.0,
                StartAngleDeg = 120.0
            });

            // NPC patrol orbiting Ares.
            asset.Ships.Add(new ShipDefinition
            {
                Key = "ship_strazh3",
                DisplayName = "\u041f\u0430\u0442\u0440\u0443\u043b\u044c \u00ab\u0421\u0442\u0440\u0430\u0436-3\u00bb",
                LocalizationKey = "ship.strazh3",
                Role = ShipRole.Patrol,
                ShipClass = "Interceptor",
                ParentBodyKey = "ares",
                Radius = 0.12,
                OrbitalRadius = 3.0,
                OrbitalPeriod = 10.0,
                StartAngleDeg = 200.0
            });

            // Save asset.
            string path = "Assets/Data/Config/SampleStarSystem.asset";
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;

            Debug.Log($"[SampleSystemAssetCreator] Created sample star system at {path}");
        }
    }
}
#endif
