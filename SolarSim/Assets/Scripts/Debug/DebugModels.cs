using System;
using System.Collections.Generic;

namespace SpaceSim.Debug
{
    // ---------------------------------------------------------------
    // Snapshot models
    // ---------------------------------------------------------------

    /// <summary>
    /// A complete debug snapshot of the current game state.
    /// Captured on demand or automatically on errors.
    /// All fields are plain serializable data — no Unity object references.
    /// </summary>
    [Serializable]
    public class DebugSnapshot
    {
        public string TimestampUtc;
        public string SessionId;
        public string Reason;
        public string SceneName;
        public long Frame;
        public double SimulationTime;
        public double TimeScale;
        public bool IsPaused;

        // Subsystem summaries contributed by snapshot providers.
        public List<SubsystemSnapshot> Subsystems = new List<SubsystemSnapshot>();

        // Recent errors at time of snapshot.
        public List<DebugEvent> RecentErrors = new List<DebugEvent>();
    }

    /// <summary>
    /// A snapshot contribution from one subsystem (e.g. Ships, Economy, Docking).
    /// </summary>
    [Serializable]
    public class SubsystemSnapshot
    {
        public string Name;
        public string Status;
        public Dictionary<string, object> Data = new Dictionary<string, object>();
    }

    // ---------------------------------------------------------------
    // Bundle model
    // ---------------------------------------------------------------

    /// <summary>
    /// Complete debug bundle ready for JSON export.
    /// Contains metadata, status, events, errors, snapshots, and violations.
    /// Designed to be copied and sent to AI for analysis.
    /// </summary>
    [Serializable]
    public class DebugBundle
    {
        public BundleMetadata Metadata = new BundleMetadata();
        public string StatusSummary;
        public List<DebugEvent> RecentEvents = new List<DebugEvent>();
        public List<DebugEvent> RecentErrors = new List<DebugEvent>();
        public List<DebugSnapshot> Snapshots = new List<DebugSnapshot>();
        public List<InvariantViolation> Violations = new List<InvariantViolation>();
    }

    /// <summary>
    /// Metadata header for the debug bundle.
    /// Includes filter state so analysts know which log categories/tags were active.
    /// </summary>
    [Serializable]
    public class BundleMetadata
    {
        public string SessionId;
        public string ExportedAtUtc;
        public string SceneName;
        public long Frame;
        public double SimulationTime;
        public int EventBufferCapacity;
        public int ErrorBufferCapacity;
        public int SnapshotBufferCapacity;
        public int TotalEventsLogged;
        public int TotalErrorsLogged;

        /// <summary>
        /// Number of log entries discarded by the active filter.
        /// A non-zero value indicates some logs were intentionally hidden.
        /// </summary>
        public int TotalEventsFiltered;

        /// <summary>
        /// Human-readable summary of the active filter state at export time.
        /// Example: "FILTER: muted_categories=[ECONOMY] muted_tags=[CargoTransfer,Production]"
        /// </summary>
        public string FilterSummary;

        /// <summary>
        /// List of category names that were muted at export time.
        /// Empty when all categories are enabled.
        /// </summary>
        public List<string> MutedCategories = new List<string>();

        /// <summary>
        /// List of source tag names that were muted at export time.
        /// Empty when all tags are enabled.
        /// </summary>
        public List<string> MutedTags = new List<string>();
    }

    // ---------------------------------------------------------------
    // Invariant violation model
    // ---------------------------------------------------------------

    /// <summary>
    /// A single invariant check violation.
    /// </summary>
    [Serializable]
    public class InvariantViolation
    {
        public string CheckName;
        public string Message;
        public string EntityId;
        public string EntityName;
        public string TimestampUtc;
        public Dictionary<string, object> Details = new Dictionary<string, object>();

        public InvariantViolation() { }

        public InvariantViolation(string checkName, string message, string entityId = "", string entityName = "")
        {
            CheckName = checkName;
            Message = message;
            EntityId = entityId;
            EntityName = entityName;
            TimestampUtc = DateTime.UtcNow.ToString("o");
        }
    }
}
