using System.Collections.Generic;
using UnityEngine;

namespace SpaceSim.Data.Definitions
{
    /// <summary>
    /// ScriptableObject defining a complete star system.
    /// Create via: Create -> SpaceSim -> Star System Definition.
    /// Bodies are embedded as a flat list; parent-child resolved by Key/ParentKey.
    /// </summary>
    [CreateAssetMenu(fileName = "NewStarSystem", menuName = "SpaceSim/Star System Definition")]
    public class StarSystemDefinition : ScriptableObject
    {
        [Header("System Identity")]
        [Tooltip("Stable string key for this system.")]
        public string SystemKey = "system_default";

        [Tooltip("Display name shown in UI.")]
        public string DisplayName = "Star System";

        [Tooltip("Localization key for future localization.")]
        public string LocalizationKey = "";

        [Header("Bodies")]
        [Tooltip("All celestial bodies in this system. Order does not matter; hierarchy is resolved by Key/ParentKey.")]
        public List<CelestialBodyDefinition> Bodies = new List<CelestialBodyDefinition>();
    }
}
