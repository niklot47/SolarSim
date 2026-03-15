using System;
using UnityEngine;
using SpaceSim.World.Entities;

namespace SpaceSim.Data.Definitions
{
    /// <summary>
    /// Static data definition for one station (orbital or surface).
    /// Embedded inside StarSystemDefinition as a list entry.
    /// Authored in Unity Inspector.
    /// </summary>
    [Serializable]
    public class StationDefinition
    {
        [Header("Identity")]
        [Tooltip("Stable string key used for data references.")]
        public string Key = "";

        [Tooltip("Display name shown in UI.")]
        public string DisplayName = "";

        [Tooltip("Localization key for future localization system.")]
        public string LocalizationKey = "";

        [Header("Station Properties")]
        public StationKind Kind = StationKind.Orbital;

        [Header("Placement")]
        [Tooltip("Key of the parent body this station belongs to.")]
        public string ParentBodyKey = "";

        [Header("Physical")]
        [Tooltip("Station visual radius in world units (Mm). Stations are small.")]
        public double Radius = 0.08;

        [Header("Orbit (Orbital stations only)")]
        [Tooltip("Orbital distance from parent body in world units (Mm).")]
        public double OrbitalRadius = 2.0;

        [Tooltip("Orbital period in simulation seconds.")]
        public double OrbitalPeriod = 15.0;

        [Tooltip("Starting angle in degrees.")]
        public double StartAngleDeg = 0.0;

        [Header("Spin (Orbital stations only)")]
        [Tooltip("Rotation period in simulation seconds. 0 = no spin.")]
        public double RotationPeriod = 0.0;

        [Header("Surface Placement (Surface stations only)")]
        [Tooltip("Latitude in degrees (-90 to 90).")]
        [Range(-90f, 90f)]
        public double SurfaceLatitudeDeg = 0.0;

        [Tooltip("Longitude in degrees (-180 to 180).")]
        [Range(-180f, 180f)]
        public double SurfaceLongitudeDeg = 0.0;

        [Header("Docking (Orbital stations only)")]
        [Tooltip("Number of docking ports. 0 = no docking capability.")]
        [Min(0)]
        public int DockingPortCount = 3;
    }
}
