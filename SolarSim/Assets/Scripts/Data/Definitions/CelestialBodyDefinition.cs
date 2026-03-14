using System;
using UnityEngine;
using SpaceSim.World.Entities;

namespace SpaceSim.Data.Definitions
{
    /// <summary>
    /// Static data definition for one celestial body.
    /// Embedded inside StarSystemDefinition as a list entry.
    /// Authored in Unity Inspector. Not a standalone ScriptableObject.
    /// </summary>
    [Serializable]
    public class CelestialBodyDefinition
    {
        [Header("Identity")]
        [Tooltip("Stable string key used for parent references and save/load.")]
        public string Key = "";

        [Tooltip("Display name shown in UI. Will be replaced by localization key later.")]
        public string DisplayName = "";

        [Tooltip("Localization key for future localization system.")]
        public string LocalizationKey = "";

        [Header("Classification")]
        public CelestialBodyType BodyType = CelestialBodyType.Planet;

        [Tooltip("Key of the parent body. Empty for root bodies (stars).")]
        public string ParentKey = "";

        public AttachmentMode AttachmentMode = AttachmentMode.None;

        [Header("Physical")]
        [Tooltip("Body radius in world units (Mm).")]
        public double Radius = 1.0;

        public bool IsSelectable = true;
        public bool HasSurface = false;

        [Header("Sphere of Influence")]
        [Tooltip("SOI radius in world units (Mm). 0 or negative = no SOI. " +
                 "Used by SOIResolver to determine dominant body for ships.")]
        public double SOIRadius = 0.0;

        [Header("Orbit (ignored for root bodies)")]
        [Tooltip("Semi-major axis in world units (Mm).")]
        public double SemiMajorAxis = 10.0;

        public double Eccentricity = 0.0;
        public double InclinationDeg = 0.0;
        public double LongitudeOfAscendingNodeDeg = 0.0;
        public double ArgumentOfPeriapsisDeg = 0.0;
        public double MeanAnomalyAtEpochDeg = 0.0;

        [Tooltip("Orbital period in simulation seconds.")]
        public double OrbitalPeriod = 100.0;

        public double EpochTime = 0.0;
        public bool IsPrograde = true;

        [Header("Spin")]
        public double AxialTiltDeg = 0.0;

        [Tooltip("Rotation period in simulation seconds.")]
        public double RotationPeriod = 60.0;

        public double InitialRotationDeg = 0.0;
    }
}
