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
