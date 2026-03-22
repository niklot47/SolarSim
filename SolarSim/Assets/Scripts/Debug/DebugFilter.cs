using System.Collections.Generic;

namespace SpaceSim.Debug
{
    /// <summary>
    /// Runtime filter state for the debug log system.
    /// Checks whether a log entry should be recorded based on:
    ///   1. Category enabled/disabled
    ///   2. Source tag (string) muted/unmuted
    ///   3. Minimum severity level
    ///
    /// Errors (severity == Error) ALWAYS pass regardless of filter settings
    /// to ensure bugs are never silently lost.
    ///
    /// Pure C# — no UnityEngine dependency beyond enum parsing.
    /// Thread-safe via lock on mutable collections.
    /// </summary>
    public class DebugFilter
    {
        private readonly object _lock = new object();

        // Category filter: true = allowed, false = muted.
        private readonly Dictionary<DebugCategory, bool> _categoryEnabled =
            new Dictionary<DebugCategory, bool>();

        // Source tag filter: tags in this set are muted.
        private readonly HashSet<string> _mutedTags = new HashSet<string>();

        // Minimum severity: logs below this level are discarded.
        private DebugSeverity _minimumSeverity = DebugSeverity.Info;

        // Master switch: when false, only errors pass.
        private bool _enabled = true;

        /// <summary>Whether the filter is active. When false, only errors pass.</summary>
        public bool Enabled
        {
            get { lock (_lock) return _enabled; }
            set { lock (_lock) _enabled = value; }
        }

        /// <summary>Minimum severity threshold.</summary>
        public DebugSeverity MinimumSeverity
        {
            get { lock (_lock) return _minimumSeverity; }
            set { lock (_lock) _minimumSeverity = value; }
        }

        /// <summary>
        /// Check whether a log entry passes the filter.
        /// Returns true if the entry should be recorded.
        ///
        /// RULE: Error severity ALWAYS passes regardless of category/tag/severity settings.
        /// This ensures invariant violations and crashes are never hidden.
        /// </summary>
        public bool IsAllowed(DebugCategory category, DebugSeverity severity, string sourceTag)
        {
            // Errors always pass — non-negotiable safety rule.
            if (severity == DebugSeverity.Error)
                return true;

            lock (_lock)
            {
                if (!_enabled)
                    return false;

                // Severity floor check.
                if (severity < _minimumSeverity)
                    return false;

                // Category check.
                if (_categoryEnabled.TryGetValue(category, out bool catEnabled))
                {
                    if (!catEnabled)
                        return false;
                }

                // Source tag check.
                if (!string.IsNullOrEmpty(sourceTag) && _mutedTags.Contains(sourceTag))
                    return false;
            }

            return true;
        }

        // ---------------------------------------------------------------
        // Configuration API
        // ---------------------------------------------------------------

        /// <summary>Set whether a category is enabled.</summary>
        public void SetCategoryEnabled(DebugCategory category, bool enabled)
        {
            lock (_lock) _categoryEnabled[category] = enabled;
        }

        /// <summary>Check if a category is currently enabled.</summary>
        public bool IsCategoryEnabled(DebugCategory category)
        {
            lock (_lock)
            {
                if (_categoryEnabled.TryGetValue(category, out bool enabled))
                    return enabled;
                return true; // Default: enabled.
            }
        }

        /// <summary>Enable all categories.</summary>
        public void EnableAllCategories()
        {
            lock (_lock)
            {
                _categoryEnabled.Clear();
            }
        }

        /// <summary>Mute a specific source tag.</summary>
        public void MuteTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return;
            lock (_lock) _mutedTags.Add(tag);
        }

        /// <summary>Unmute a specific source tag.</summary>
        public void UnmuteTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return;
            lock (_lock) _mutedTags.Remove(tag);
        }

        /// <summary>Check if a tag is muted.</summary>
        public bool IsTagMuted(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return false;
            lock (_lock) return _mutedTags.Contains(tag);
        }

        /// <summary>Unmute all tags.</summary>
        public void UnmuteAllTags()
        {
            lock (_lock) _mutedTags.Clear();
        }

        /// <summary>Reset filter to default (everything enabled).</summary>
        public void Reset()
        {
            lock (_lock)
            {
                _categoryEnabled.Clear();
                _mutedTags.Clear();
                _minimumSeverity = DebugSeverity.Info;
                _enabled = true;
            }
        }

        // ---------------------------------------------------------------
        // Status / export helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// Get a human-readable summary of current filter state.
        /// Used in debug bundle metadata so analysts know what was filtered.
        /// </summary>
        public string GetFilterSummary()
        {
            lock (_lock)
            {
                if (!_enabled)
                    return "FILTER: disabled (errors only)";

                var parts = new List<string>();

                // Muted categories.
                var mutedCats = new List<string>();
                foreach (var kvp in _categoryEnabled)
                {
                    if (!kvp.Value)
                        mutedCats.Add(kvp.Key.ToString());
                }
                if (mutedCats.Count > 0)
                    parts.Add("muted_categories=[" + string.Join(",", mutedCats) + "]");

                // Muted tags.
                if (_mutedTags.Count > 0)
                {
                    var tagList = new List<string>(_mutedTags);
                    parts.Add("muted_tags=[" + string.Join(",", tagList) + "]");
                }

                // Severity floor.
                if (_minimumSeverity != DebugSeverity.Info)
                    parts.Add("min_severity=" + _minimumSeverity);

                return parts.Count > 0
                    ? "FILTER: " + string.Join(" ", parts)
                    : "FILTER: all enabled";
            }
        }

        /// <summary>
        /// Get list of currently muted category names (for bundle metadata).
        /// </summary>
        public List<string> GetMutedCategoryNames()
        {
            var result = new List<string>();
            lock (_lock)
            {
                foreach (var kvp in _categoryEnabled)
                {
                    if (!kvp.Value)
                        result.Add(kvp.Key.ToString());
                }
            }
            return result;
        }

        /// <summary>
        /// Get list of currently muted tag names (for bundle metadata).
        /// </summary>
        public List<string> GetMutedTagNames()
        {
            lock (_lock) return new List<string>(_mutedTags);
        }
    }
}
