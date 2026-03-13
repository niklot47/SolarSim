using System;
using UnityEngine;
using SpaceSim.World.Entities;

namespace SpaceSim.Data.Definitions
{
    /// <summary>
    /// Static data definition for one ship.
    /// Embedded inside StarSystemDefinition as a list entry.
    /// Authored in Unity Inspector.
    /// </summary>
    [Serializable]
    public class ShipDefinition
    {
        [Header("Identity")]
        [Tooltip("Stable string key used for save/load and data references.")]
        public string Key = "";

        [Tooltip("Display name shown in UI.")]
        public string DisplayName = "";

        [Tooltip("Localization key for future localization system.")]
        public string LocalizationKey = "";

        [Header("Ship Properties")]
        public ShipRole Role = ShipRole.Civilian;

        [Tooltip("Ship class/model name (e.g. Corvette, Freighter).")]
        public string ShipClass = "";

        [Header("Placement")]
        [Tooltip("Key of the parent body this ship orbits.")]
        public string ParentBodyKey = "";

        [Header("Physical")]
        [Tooltip("Ship visual radius in world units (Mm). Ships are small.")]
        public double Radius = 0.15;

        [Header("Orbit")]
        [Tooltip("Orbital distance from parent body in world units (Mm).")]
        public double OrbitalRadius = 3.0;

        [Tooltip("Orbital period in simulation seconds.")]
        public double OrbitalPeriod = 15.0;

        [Tooltip("Starting angle in degrees.")]
        public double StartAngleDeg = 0.0;
    }
}
