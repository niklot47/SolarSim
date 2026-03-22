using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceSim.Debug
{
    /// <summary>
    /// A single source tag toggle within a filter group.
    /// Represents a specific log source (e.g. "Navigation", "CargoTransfer").
    /// </summary>
    [Serializable]
    public class DebugTagToggle
    {
        [Tooltip("Source tag string used in GameDebug.Log() calls.")]
        public string Tag = "";

        [Tooltip("When unchecked, logs with this source tag are silently discarded.")]
        public bool Enabled = true;
    }

    /// <summary>
    /// A hierarchical group of log filters.
    /// Maps to one or more DebugCategory values.
    /// Contains a master toggle (disables the entire group)
    /// and child tag toggles (disable specific sources within the group).
    ///
    /// In Inspector, rendered as:
    ///   [✓] Economy
    ///       [✓] CargoTransfer
    ///       [✓] Production
    ///       [✓] TradeAI
    /// </summary>
    [Serializable]
    public class DebugFilterGroup
    {
        [Tooltip("Display name shown in Inspector.")]
        public string DisplayName = "";

        [Tooltip("Master toggle: when unchecked, ALL logs in this group are disabled.")]
        public bool Enabled = true;

        [Tooltip("DebugCategory names covered by this group. Case-sensitive, must match enum.")]
        public List<string> Categories = new List<string>();

        [Tooltip("Individual source tag toggles within this group.")]
        public List<DebugTagToggle> Tags = new List<DebugTagToggle>();

        [HideInInspector]
        public bool FoldoutOpen = true;
    }

    /// <summary>
    /// ScriptableObject holding debug log filter configuration.
    /// Create via: Create -> SpaceSim -> Debug Filter Profile.
    ///
    /// Contains hierarchical filter groups that map DebugCategory + source tags
    /// to enable/disable toggles. Applied at runtime via GameDebug.SetFilter().
    ///
    /// Default profile ships with pre-configured groups matching the project's
    /// log architecture. New groups/tags can be added as the project grows.
    ///
    /// Lives in Data layer (needs UnityEngine for ScriptableObject).
    /// </summary>
    [CreateAssetMenu(fileName = "DebugFilterProfile", menuName = "SpaceSim/Debug Filter Profile")]
    public class DebugFilterProfile : ScriptableObject
    {
        [Header("Global Settings")]
        [Tooltip("Master switch. When off, only Error-severity logs pass.")]
        public bool EnableLogging = true;

        [Tooltip("Minimum severity level. Logs below this are discarded (except Errors).")]
        public DebugSeverity MinimumSeverity = DebugSeverity.Info;

        [Header("Filter Groups")]
        [Tooltip("Hierarchical filter groups. Each group controls one or more categories and their source tags.")]
        public List<DebugFilterGroup> Groups = new List<DebugFilterGroup>();

        /// <summary>
        /// Apply this profile's settings to a runtime DebugFilter instance.
        /// Called by GameBootstrap on startup and whenever Inspector values change.
        /// </summary>
        public void ApplyTo(DebugFilter filter)
        {
            if (filter == null) return;

            filter.Enabled = EnableLogging;
            filter.MinimumSeverity = MinimumSeverity;

            // Reset filter state before applying.
            filter.EnableAllCategories();
            filter.UnmuteAllTags();

            foreach (var group in Groups)
            {
                if (!group.Enabled)
                {
                    // Master toggle off: disable all categories in this group.
                    foreach (string catName in group.Categories)
                    {
                        if (TryParseCategory(catName, out DebugCategory cat))
                            filter.SetCategoryEnabled(cat, false);
                    }

                    // Also mute all tags in a disabled group.
                    foreach (var tagToggle in group.Tags)
                    {
                        if (!string.IsNullOrEmpty(tagToggle.Tag))
                            filter.MuteTag(tagToggle.Tag);
                    }
                }
                else
                {
                    // Master toggle on: enable categories.
                    foreach (string catName in group.Categories)
                    {
                        if (TryParseCategory(catName, out DebugCategory cat))
                            filter.SetCategoryEnabled(cat, true);
                    }

                    // Apply individual tag toggles.
                    foreach (var tagToggle in group.Tags)
                    {
                        if (string.IsNullOrEmpty(tagToggle.Tag)) continue;

                        if (!tagToggle.Enabled)
                            filter.MuteTag(tagToggle.Tag);
                        else
                            filter.UnmuteTag(tagToggle.Tag);
                    }
                }
            }
        }

        /// <summary>
        /// Create default groups matching the current project's log architecture.
        /// Called by the asset creator editor script.
        /// </summary>
        public void PopulateDefaults()
        {
            Groups.Clear();

            // Navigation & Movement.
            Groups.Add(new DebugFilterGroup
            {
                DisplayName = "Navigation",
                Enabled = true,
                Categories = new List<string> { "SHIPS", "PATH" },
                Tags = new List<DebugTagToggle>
                {
                    new DebugTagToggle { Tag = "Navigation", Enabled = true },
                    new DebugTagToggle { Tag = "Docking", Enabled = true }
                }
            });

            // Economy.
            Groups.Add(new DebugFilterGroup
            {
                DisplayName = "Economy",
                Enabled = true,
                Categories = new List<string> { "ECONOMY" },
                Tags = new List<DebugTagToggle>
                {
                    new DebugTagToggle { Tag = "CargoTransfer", Enabled = true },
                    new DebugTagToggle { Tag = "Production", Enabled = true },
                    new DebugTagToggle { Tag = "TradeAI", Enabled = true }
                }
            });

            // Orbits & SOI.
            Groups.Add(new DebugFilterGroup
            {
                DisplayName = "Orbits & SOI",
                Enabled = true,
                Categories = new List<string> { "ORBIT" },
                Tags = new List<DebugTagToggle>
                {
                    new DebugTagToggle { Tag = "SOI", Enabled = true }
                }
            });

            // NPC Scheduling.
            Groups.Add(new DebugFilterGroup
            {
                DisplayName = "NPC",
                Enabled = true,
                Categories = new List<string> { "NPC" },
                Tags = new List<DebugTagToggle>()
            });

            // Simulation Core.
            Groups.Add(new DebugFilterGroup
            {
                DisplayName = "Simulation",
                Enabled = true,
                Categories = new List<string> { "SIM" },
                Tags = new List<DebugTagToggle>
                {
                    new DebugTagToggle { Tag = "SystemLoader", Enabled = true }
                }
            });

            // UI.
            Groups.Add(new DebugFilterGroup
            {
                DisplayName = "UI",
                Enabled = true,
                Categories = new List<string> { "UI" },
                Tags = new List<DebugTagToggle>()
            });

            // Debug system self-logs.
            Groups.Add(new DebugFilterGroup
            {
                DisplayName = "Debug System",
                Enabled = true,
                Categories = new List<string> { "DEBUG" },
                Tags = new List<DebugTagToggle>
                {
                    new DebugTagToggle { Tag = "GameDebug", Enabled = true },
                    new DebugTagToggle { Tag = "InvariantChecker", Enabled = true }
                }
            });
        }

        private static bool TryParseCategory(string name, out DebugCategory category)
        {
            category = default;
            if (string.IsNullOrEmpty(name)) return false;

            switch (name)
            {
                case "SIM": category = DebugCategory.SIM; return true;
                case "NPC": category = DebugCategory.NPC; return true;
                case "ECONOMY": category = DebugCategory.ECONOMY; return true;
                case "CONTRACTS": category = DebugCategory.CONTRACTS; return true;
                case "UI": category = DebugCategory.UI; return true;
                case "PATH": category = DebugCategory.PATH; return true;
                case "ORBIT": category = DebugCategory.ORBIT; return true;
                case "SHIPS": category = DebugCategory.SHIPS; return true;
                case "SAVELOAD": category = DebugCategory.SAVELOAD; return true;
                case "ERROR": category = DebugCategory.ERROR; return true;
                case "DEBUG": category = DebugCategory.DEBUG; return true;
                default: return false;
            }
        }
    }
}
