using System;
using System.Collections.Generic;

namespace SpaceSim.Data.Import
{
    /// <summary>
    /// Root DTO for a star system JSON file.
    /// Deserialized from external JSON. Fields map 1:1 to JSON keys.
    /// These classes are pure data containers — no logic, no Unity types.
    ///
    /// JSON is deserialized via UnityEngine.JsonUtility which requires
    /// [Serializable] and public fields (not properties).
    /// </summary>
    [Serializable]
    public class StarSystemJson
    {
        public string systemName = "";
        public string systemKey = "";
        public string localizationKey = "";

        public List<BodyJson> bodies = new List<BodyJson>();
        public List<StationJson> stations = new List<StationJson>();
        public List<ShipJson> ships = new List<ShipJson>();
    }

    /// <summary>
    /// DTO for a celestial body in JSON.
    /// Maps to CelestialBodyBuildData via JsonStarSystemImporter.
    /// </summary>
    [Serializable]
    public class BodyJson
    {
        public string key = "";
        public string name = "";
        public string localizationKey = "";

        /// <summary>Body type string: Star, Planet, Moon, Asteroid, SurfaceSite.</summary>
        public string type = "Planet";

        /// <summary>Key of the parent body. Empty for root bodies (stars).</summary>
        public string parent = "";

        /// <summary>Attachment mode string: None, Orbit, Surface. Default: auto-detected.</summary>
        public string attachmentMode = "";

        public double radius = 1.0;
        public bool isSelectable = true;
        public bool hasSurface = false;
        public double soi = 0.0;

        public OrbitJson orbit;
        public SpinJson spin;
    }

    /// <summary>
    /// DTO for orbital parameters in JSON.
    /// </summary>
    [Serializable]
    public class OrbitJson
    {
        public double semiMajorAxis = 10.0;
        public double period = 100.0;
        public double eccentricity = 0.0;
        public double inclinationDeg = 0.0;
        public double longitudeOfAscendingNodeDeg = 0.0;
        public double argumentOfPeriapsisDeg = 0.0;
        public double meanAnomalyAtEpochDeg = 0.0;
        public double epochTime = 0.0;
        public bool isPrograde = true;
    }

    /// <summary>
    /// DTO for spin/rotation parameters in JSON.
    /// </summary>
    [Serializable]
    public class SpinJson
    {
        public double axialTiltDeg = 0.0;
        public double rotationPeriod = 60.0;
        public double initialRotationDeg = 0.0;
    }

    /// <summary>
    /// DTO for a station in JSON.
    /// Maps to StationBuildData via JsonStarSystemImporter.
    /// </summary>
    [Serializable]
    public class StationJson
    {
        public string key = "";
        public string name = "";
        public string localizationKey = "";

        /// <summary>Station kind string: Orbital or Surface.</summary>
        public string kind = "Orbital";

        /// <summary>Key of the parent body this station belongs to.</summary>
        public string parentBody = "";

        public double radius = 0.06;

        // Orbital station fields.
        public double orbitalRadius = 2.0;
        public double orbitalPeriod = 15.0;
        public double startAngleDeg = 0.0;
        public double rotationPeriod = 0.0;

        // Surface station fields.
        public double surfaceLatitudeDeg = 0.0;
        public double surfaceLongitudeDeg = 0.0;

        // Docking.
        public int dockingPortCount = 0;
    }

    /// <summary>
    /// DTO for a ship in JSON.
    /// Maps to ShipBuildData via JsonStarSystemImporter.
    /// </summary>
    [Serializable]
    public class ShipJson
    {
        public string key = "";
        public string name = "";
        public string localizationKey = "";

        /// <summary>Ship role string: Player, Trader, Patrol, Civilian.</summary>
        public string role = "Civilian";

        public string shipClass = "";

        /// <summary>Key of the parent body this ship orbits.</summary>
        public string parentBody = "";

        public double radius = 0.03;
        public double orbitalRadius = 3.0;
        public double orbitalPeriod = 12.0;
        public double startAngleDeg = 0.0;
    }
}
