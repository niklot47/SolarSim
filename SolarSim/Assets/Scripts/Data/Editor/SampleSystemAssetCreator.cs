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

            // --- Bodies ---

            asset.Bodies.Add(new CelestialBodyDefinition
            {
                Key = "sol", DisplayName = "Sol", LocalizationKey = "body.sol",
                BodyType = CelestialBodyType.Star, ParentKey = "",
                AttachmentMode = AttachmentMode.None,
                Radius = 1.0, IsSelectable = true, HasSurface = false,
                SOIRadius = 1000.0, RotationPeriod = 600.0
            });

            asset.Bodies.Add(new CelestialBodyDefinition
            {
                Key = "terra", DisplayName = "Terra", LocalizationKey = "body.terra",
                BodyType = CelestialBodyType.Planet, ParentKey = "sol",
                AttachmentMode = AttachmentMode.Orbit,
                Radius = 0.3, IsSelectable = true, HasSurface = true,
                SOIRadius = 60.0, SemiMajorAxis = 150.0, OrbitalPeriod = 120.0,
                MeanAnomalyAtEpochDeg = 0.0, RotationPeriod = 30.0
            });

            asset.Bodies.Add(new CelestialBodyDefinition
            {
                Key = "ares", DisplayName = "Ares", LocalizationKey = "body.ares",
                BodyType = CelestialBodyType.Planet, ParentKey = "sol",
                AttachmentMode = AttachmentMode.Orbit,
                Radius = 0.2, IsSelectable = true, HasSurface = true,
                SOIRadius = 40.0, SemiMajorAxis = 275.0, OrbitalPeriod = 240.0,
                MeanAnomalyAtEpochDeg = 90.0, RotationPeriod = 25.0
            });

            asset.Bodies.Add(new CelestialBodyDefinition
            {
                Key = "venus", DisplayName = "Venus", LocalizationKey = "body.venus",
                BodyType = CelestialBodyType.Planet, ParentKey = "sol",
                AttachmentMode = AttachmentMode.Orbit,
                Radius = 0.16, IsSelectable = true, HasSurface = false,
                SOIRadius = 30.0, SemiMajorAxis = 75.0, OrbitalPeriod = 80.0,
                MeanAnomalyAtEpochDeg = 45.0, RotationPeriod = 20.0
            });

            asset.Bodies.Add(new CelestialBodyDefinition
            {
                Key = "luna", DisplayName = "Luna", LocalizationKey = "body.luna",
                BodyType = CelestialBodyType.Moon, ParentKey = "terra",
                AttachmentMode = AttachmentMode.Orbit,
                Radius = 0.08, IsSelectable = true, HasSurface = true,
                SOIRadius = 8.0, SemiMajorAxis = 25.0, OrbitalPeriod = 20.0,
                MeanAnomalyAtEpochDeg = 45.0, RotationPeriod = 20.0
            });

            // --- Ships ---

            asset.Ships.Add(new ShipDefinition
            {
                Key = "ship_aurora",
                DisplayName = "\u041a\u043e\u0440\u0432\u0435\u0442 \u00ab\u0410\u0432\u0440\u043e\u0440\u0430\u00bb",
                LocalizationKey = "ship.aurora",
                Role = ShipRole.Player, ShipClass = "Corvette",
                ParentBodyKey = "terra", Radius = 0.03,
                OrbitalRadius = 3.0, OrbitalPeriod = 12.0, StartAngleDeg = 0.0
            });

            asset.Ships.Add(new ShipDefinition
            {
                Key = "ship_cargo7",
                DisplayName = "\u0422\u0440\u0430\u043d\u0441\u043f\u043e\u0440\u0442 \u00ab\u041a\u0430\u0440\u0433\u043e-7\u00bb",
                LocalizationKey = "ship.cargo7",
                Role = ShipRole.Trader, ShipClass = "Freighter",
                ParentBodyKey = "terra", Radius = 0.024,
                OrbitalRadius = 4.0, OrbitalPeriod = 15.0, StartAngleDeg = 120.0
            });

            asset.Ships.Add(new ShipDefinition
            {
                Key = "ship_strazh3",
                DisplayName = "\u041f\u0430\u0442\u0440\u0443\u043b\u044c \u00ab\u0421\u0442\u0440\u0430\u0436-3\u00bb",
                LocalizationKey = "ship.strazh3",
                Role = ShipRole.Patrol, ShipClass = "Interceptor",
                ParentBodyKey = "ares", Radius = 0.024,
                OrbitalRadius = 3.0, OrbitalPeriod = 10.0, StartAngleDeg = 200.0
            });

            // --- Stations: Terra ---

            asset.Stations.Add(new StationDefinition
            {
                Key = "station_orbita1",
                DisplayName = "\u0421\u0442\u0430\u043d\u0446\u0438\u044f \u00ab\u041e\u0440\u0431\u0438\u0442\u0430-1\u00bb",
                LocalizationKey = "station.orbita1",
                Kind = StationKind.Orbital, ParentBodyKey = "terra",
                Radius = 0.06, OrbitalRadius = 8.0, OrbitalPeriod = 18.0,
                StartAngleDeg = 60.0, RotationPeriod = 40.0,
                DockingPortCount = 3
            });

            asset.Stations.Add(new StationDefinition
            {
                Key = "station_terra1",
                DisplayName = "\u0411\u0430\u0437\u0430 \u00ab\u0422\u0435\u0440\u0440\u0430-1\u00bb",
                LocalizationKey = "station.terra1",
                Kind = StationKind.Surface, ParentBodyKey = "terra",
                Radius = 0.04,
                SurfaceLatitudeDeg = 30.0, SurfaceLongitudeDeg = 45.0,
                DockingPortCount = 2
            });

            // --- Stations: Ares ---

            asset.Stations.Add(new StationDefinition
            {
                Key = "station_phobos",
                DisplayName = "\u0421\u0442\u0430\u043d\u0446\u0438\u044f \u00ab\u0424\u043e\u0431\u043e\u0441\u00bb",
                LocalizationKey = "station.phobos",
                Kind = StationKind.Orbital, ParentBodyKey = "ares",
                Radius = 0.05, OrbitalRadius = 6.0, OrbitalPeriod = 14.0,
                StartAngleDeg = 30.0, RotationPeriod = 30.0,
                DockingPortCount = 2
            });

            asset.Stations.Add(new StationDefinition
            {
                Key = "station_ares1",
                DisplayName = "\u0411\u0430\u0437\u0430 \u00ab\u0410\u0440\u0435\u0441-1\u00bb",
                LocalizationKey = "station.ares1",
                Kind = StationKind.Surface, ParentBodyKey = "ares",
                Radius = 0.035,
                SurfaceLatitudeDeg = -15.0, SurfaceLongitudeDeg = 120.0,
                DockingPortCount = 2
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
