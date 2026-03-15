using System;
using System.Collections.Generic;
using SpaceSim.World.Systems;

namespace SpaceSim.Debug
{
    /// <summary>
    /// Central debug service. Collects structured events, captures world state
    /// snapshots via providers, exports JSON bundles to disk, and validates
    /// invariants.
    ///
    /// Static API for easy access from any system.
    ///
    /// Usage:
    ///   GameDebug.Log(DebugCategory.ECONOMY, "Ship loaded cargo");
    ///   GameDebug.CaptureSnapshot("before trade");
    ///   GameDebug.RunInvariantChecks();
    ///   string path = GameDebug.ExportBundle();
    /// </summary>
    public static class GameDebug
    {
        // --- Configuration ---
        private const int EventBufferSize = 1000;
        private const int ErrorBufferSize = 200;
        private const int SnapshotBufferSize = 20;

        // --- State ---
        private static readonly RingBuffer<DebugEvent> _events = new RingBuffer<DebugEvent>(EventBufferSize);
        private static readonly RingBuffer<DebugEvent> _errors = new RingBuffer<DebugEvent>(ErrorBufferSize);
        private static readonly RingBuffer<DebugSnapshot> _snapshots = new RingBuffer<DebugSnapshot>(SnapshotBufferSize);
        private static readonly List<InvariantViolation> _violations = new List<InvariantViolation>();

        private static bool _enabled = true;
        private static string _sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
        private static string _lastExportPath = "";
        private static int _totalEventsLogged = 0;
        private static int _totalErrorsLogged = 0;

        // --- External context (set by Unity-side bridge) ---
        private static string _currentSceneName = "";
        private static long _currentFrame = 0;
        private static double _currentSimTime = 0.0;
        private static double _currentTimeScale = 1.0;
        private static bool _currentIsPaused = false;
        private static string _exportDirectory = "";
        private static WorldRegistry _worldRegistry = null;
        private static DebugInvariantChecker _invariantChecker = null;

        // --- Callbacks ---

        /// <summary>
        /// Optional callback invoked on every Log call. Allows Unity side
        /// to forward to Debug.Log if desired.
        /// </summary>
        public static Action<DebugEvent> OnEventLogged;

        /// <summary>
        /// Optional callback invoked when a snapshot is captured.
        /// </summary>
        public static Action<DebugSnapshot> OnSnapshotCaptured;

        // ---------------------------------------------------------------
        // Public API: Configuration
        // ---------------------------------------------------------------

        /// <summary>Enable or disable the debug system.</summary>
        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        /// <summary>Current session identifier.</summary>
        public static string SessionId => _sessionId;

        /// <summary>
        /// Set the Unity-side context that changes each frame.
        /// Called by GameBootstrap or coordinator each tick.
        /// </summary>
        public static void SetContext(string sceneName, long frame, double simTime, double timeScale, bool isPaused)
        {
            _currentSceneName = sceneName ?? "";
            _currentFrame = frame;
            _currentSimTime = simTime;
            _currentTimeScale = timeScale;
            _currentIsPaused = isPaused;
        }

        /// <summary>
        /// Set the export directory for JSON bundles.
        /// Typically Application.persistentDataPath + "/debug_bundles".
        /// </summary>
        public static void SetExportDirectory(string directory)
        {
            _exportDirectory = directory ?? "";
        }

        /// <summary>
        /// Set the world registry for invariant checking.
        /// </summary>
        public static void SetWorldRegistry(WorldRegistry registry)
        {
            _worldRegistry = registry;
            _invariantChecker = registry != null ? new DebugInvariantChecker(registry) : null;
        }

        // ---------------------------------------------------------------
        // Public API: Snapshot providers
        // ---------------------------------------------------------------

        /// <summary>
        /// Register a subsystem snapshot provider.
        /// The provider will be called during CaptureSnapshot().
        /// </summary>
        public static void RegisterSnapshotProvider(IDebugSnapshotProvider provider)
        {
            SnapshotProviderRegistry.Register(provider);
        }

        /// <summary>
        /// Unregister a snapshot provider by name.
        /// </summary>
        public static void UnregisterSnapshotProvider(string name)
        {
            SnapshotProviderRegistry.Unregister(name);
        }

        // ---------------------------------------------------------------
        // Public API: Logging
        // ---------------------------------------------------------------

        /// <summary>Log a structured debug event.</summary>
        public static void Log(
            DebugCategory category,
            string message,
            DebugSeverity severity = DebugSeverity.Info,
            string source = "",
            string data = "",
            string sceneName = "",
            long frame = 0)
        {
            if (!_enabled) return;

            var evt = new DebugEvent(
                category, severity, message, source, data,
                sceneName.Length > 0 ? sceneName : _currentSceneName,
                frame > 0 ? frame : _currentFrame,
                _sessionId);

            _events.Add(evt);
            _totalEventsLogged++;

            if (severity == DebugSeverity.Error)
            {
                _errors.Add(evt);
                _totalErrorsLogged++;
            }

            OnEventLogged?.Invoke(evt);
        }

        /// <summary>Shortcut for error logging.</summary>
        public static void LogError(DebugCategory category, string message, string source = "", string data = "")
        {
            Log(category, message, DebugSeverity.Error, source, data);
        }

        /// <summary>Shortcut for warning logging.</summary>
        public static void LogWarning(DebugCategory category, string message, string source = "", string data = "")
        {
            Log(category, message, DebugSeverity.Warning, source, data);
        }

        // ---------------------------------------------------------------
        // Public API: Snapshots
        // ---------------------------------------------------------------

        /// <summary>
        /// Capture a full snapshot of the current game state.
        /// Queries all registered snapshot providers.
        /// Stores in the snapshot ring buffer.
        /// Returns the snapshot for immediate use.
        /// </summary>
        public static DebugSnapshot CaptureSnapshot(string reason = "")
        {
            var snapshot = new DebugSnapshot
            {
                TimestampUtc = DateTime.UtcNow.ToString("o"),
                SessionId = _sessionId,
                Reason = reason,
                SceneName = _currentSceneName,
                Frame = _currentFrame,
                SimulationTime = _currentSimTime,
                TimeScale = _currentTimeScale,
                IsPaused = _currentIsPaused,
                Subsystems = SnapshotProviderRegistry.CaptureAll(),
                RecentErrors = _errors.ToList()
            };

            _snapshots.Add(snapshot);
            Log(DebugCategory.DEBUG, $"Snapshot captured: {reason}", source: "GameDebug");
            OnSnapshotCaptured?.Invoke(snapshot);

            return snapshot;
        }

        // ---------------------------------------------------------------
        // Public API: Invariant checks
        // ---------------------------------------------------------------

        /// <summary>
        /// Run all invariant checks against current world state.
        /// Violations are recorded as ERROR events and stored for bundle export.
        /// Returns the list of violations found.
        /// Auto-captures a snapshot if violations are found.
        /// </summary>
        public static List<InvariantViolation> RunInvariantChecks()
        {
            _violations.Clear();

            if (_invariantChecker == null)
            {
                Log(DebugCategory.DEBUG, "Invariant checker not available (no WorldRegistry set)",
                    DebugSeverity.Warning, source: "GameDebug");
                return _violations;
            }

            var found = _invariantChecker.RunAll();
            _violations.AddRange(found);

            Log(DebugCategory.DEBUG,
                $"Invariant check complete: {found.Count} violation(s)",
                found.Count > 0 ? DebugSeverity.Warning : DebugSeverity.Info,
                source: "GameDebug");

            // Auto-snapshot on violations.
            if (found.Count > 0)
            {
                CaptureSnapshot($"invariant_violations_{found.Count}");
            }

            return _violations;
        }

        // ---------------------------------------------------------------
        // Public API: Export
        // ---------------------------------------------------------------

        /// <summary>
        /// Build and export a complete debug bundle to JSON file.
        /// Returns the file path, or empty string if export failed.
        /// </summary>
        public static string ExportBundle()
        {
            var bundle = BuildBundle();
            string json = DebugExportUtility.BundleToJson(bundle);

            if (string.IsNullOrEmpty(_exportDirectory))
            {
                Log(DebugCategory.DEBUG,
                    "Export directory not set — bundle built but not saved to disk",
                    DebugSeverity.Warning, source: "GameDebug");
                _lastExportPath = "(in-memory only)";
                return "";
            }

            string path = DebugExportUtility.WriteToFile(json, _exportDirectory);

            if (!string.IsNullOrEmpty(path))
            {
                _lastExportPath = path;
                Log(DebugCategory.DEBUG, $"Bundle exported to: {path}", source: "GameDebug");
            }
            else
            {
                Log(DebugCategory.DEBUG, "Bundle export failed — could not write file",
                    DebugSeverity.Error, source: "GameDebug");
            }

            return path;
        }

        /// <summary>
        /// Build a debug bundle in memory without writing to disk.
        /// Useful for tests or in-editor inspection.
        /// </summary>
        public static DebugBundle BuildBundle()
        {
            // Capture a fresh snapshot for the bundle.
            CaptureSnapshot("export");

            var bundle = new DebugBundle
            {
                Metadata = new BundleMetadata
                {
                    SessionId = _sessionId,
                    ExportedAtUtc = DateTime.UtcNow.ToString("o"),
                    SceneName = _currentSceneName,
                    Frame = _currentFrame,
                    SimulationTime = _currentSimTime,
                    EventBufferCapacity = EventBufferSize,
                    ErrorBufferCapacity = ErrorBufferSize,
                    SnapshotBufferCapacity = SnapshotBufferSize,
                    TotalEventsLogged = _totalEventsLogged,
                    TotalErrorsLogged = _totalErrorsLogged
                },
                StatusSummary = GetStatus(),
                RecentEvents = _events.ToList(),
                RecentErrors = _errors.ToList(),
                Snapshots = _snapshots.ToList(),
                Violations = new List<InvariantViolation>(_violations)
            };

            return bundle;
        }

        // ---------------------------------------------------------------
        // Public API: Status
        // ---------------------------------------------------------------

        /// <summary>
        /// Returns a human-readable status summary.
        /// </summary>
        public static string GetStatus()
        {
            return $"[GameDebug] session={_sessionId} enabled={_enabled} " +
                   $"events={_events.Count}/{EventBufferSize} (total={_totalEventsLogged}) " +
                   $"errors={_errors.Count}/{ErrorBufferSize} (total={_totalErrorsLogged}) " +
                   $"snapshots={_snapshots.Count}/{SnapshotBufferSize} " +
                   $"providers={SnapshotProviderRegistry.Count} " +
                   $"violations={_violations.Count} " +
                   $"lastExport={(_lastExportPath == "" ? "none" : _lastExportPath)}";
        }

        // ---------------------------------------------------------------
        // Public API: Maintenance
        // ---------------------------------------------------------------

        /// <summary>
        /// Clear all buffers and start a new session.
        /// </summary>
        public static void Clear()
        {
            _events.Clear();
            _errors.Clear();
            _snapshots.Clear();
            _violations.Clear();
            _sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
            _lastExportPath = "";
            _totalEventsLogged = 0;
            _totalErrorsLogged = 0;
        }

        /// <summary>Get a copy of all recent events.</summary>
        public static List<DebugEvent> GetRecentEvents() => _events.ToList();

        /// <summary>Get a copy of all recent errors.</summary>
        public static List<DebugEvent> GetRecentErrors() => _errors.ToList();

        /// <summary>Get a copy of all stored snapshots.</summary>
        public static List<DebugSnapshot> GetSnapshots() => _snapshots.ToList();

        /// <summary>Get the latest snapshot, or null.</summary>
        public static DebugSnapshot GetLatestSnapshot()
        {
            var list = _snapshots.ToList();
            return list.Count > 0 ? list[list.Count - 1] : null;
        }

        /// <summary>Get the last export path.</summary>
        public static string LastExportPath => _lastExportPath;
    }
}
