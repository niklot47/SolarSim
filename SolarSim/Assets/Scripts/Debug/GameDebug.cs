using System;
using System.Collections.Generic;

namespace SpaceSim.Debug
{
    /// <summary>
    /// Central debug service. Collects structured events, provides
    /// snapshot/export placeholders, and status reporting.
    /// Static API for easy access from any system.
    /// </summary>
    public static class GameDebug
    {
        // --- Configuration ---
        private const int EventBufferSize = 1000;
        private const int ErrorBufferSize = 200;

        // --- State ---
        private static readonly RingBuffer<DebugEvent> _events = new RingBuffer<DebugEvent>(EventBufferSize);
        private static readonly RingBuffer<DebugEvent> _errors = new RingBuffer<DebugEvent>(ErrorBufferSize);

        private static bool _enabled = true;
        private static string _sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
        private static string _lastExportPath = "";

        // --- Callbacks (optional, for Unity-side bridging) ---

        /// <summary>
        /// Optional callback invoked on every Log call. Allows Unity side
        /// to forward to Debug.Log if desired.
        /// </summary>
        public static Action<DebugEvent> OnEventLogged;

        // --- Public API ---

        /// <summary>
        /// Enable or disable the debug system.
        /// </summary>
        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        /// <summary>
        /// Current session identifier.
        /// </summary>
        public static string SessionId => _sessionId;

        /// <summary>
        /// Log a structured debug event.
        /// </summary>
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
                sceneName, frame, _sessionId);

            _events.Add(evt);

            if (severity == DebugSeverity.Error)
                _errors.Add(evt);

            OnEventLogged?.Invoke(evt);
        }

        /// <summary>
        /// Shortcut for error logging.
        /// </summary>
        public static void LogError(DebugCategory category, string message, string source = "", string data = "")
        {
            Log(category, message, DebugSeverity.Error, source, data);
        }

        /// <summary>
        /// Shortcut for warning logging.
        /// </summary>
        public static void LogWarning(DebugCategory category, string message, string source = "", string data = "")
        {
            Log(category, message, DebugSeverity.Warning, source, data);
        }

        /// <summary>
        /// Placeholder for snapshot capture. Will be extended in debug infrastructure step.
        /// Returns a simple snapshot dictionary.
        /// </summary>
        public static Dictionary<string, object> CaptureSnapshot(string reason = "")
        {
            var snapshot = new Dictionary<string, object>
            {
                ["timestampUtc"] = DateTime.UtcNow.ToString("o"),
                ["sessionId"] = _sessionId,
                ["reason"] = reason,
                ["eventCount"] = _events.Count,
                ["errorCount"] = _errors.Count
            };

            Log(DebugCategory.DEBUG, $"Snapshot captured: {reason}", source: "GameDebug");
            return snapshot;
        }

        /// <summary>
        /// Placeholder for bundle export. Will write JSON in full debug infrastructure step.
        /// Currently returns a bundle as a dictionary for testing.
        /// </summary>
        public static Dictionary<string, object> ExportBundle()
        {
            var bundle = new Dictionary<string, object>
            {
                ["metadata"] = new Dictionary<string, object>
                {
                    ["sessionId"] = _sessionId,
                    ["exportedAtUtc"] = DateTime.UtcNow.ToString("o"),
                    ["eventBufferCapacity"] = EventBufferSize,
                    ["errorBufferCapacity"] = ErrorBufferSize
                },
                ["status"] = GetStatus(),
                ["events"] = _events.ToList(),
                ["errors"] = _errors.ToList(),
                ["snapshot"] = CaptureSnapshot("export")
            };

            Log(DebugCategory.DEBUG, "Bundle exported", source: "GameDebug");
            return bundle;
        }

        /// <summary>
        /// Returns a human-readable status summary.
        /// </summary>
        public static string GetStatus()
        {
            return $"[GameDebug] session={_sessionId} enabled={_enabled} " +
                   $"events={_events.Count}/{EventBufferSize} " +
                   $"errors={_errors.Count}/{ErrorBufferSize} " +
                   $"lastExport={(_lastExportPath == "" ? "none" : _lastExportPath)}";
        }

        /// <summary>
        /// Clear all buffers and start a new session.
        /// </summary>
        public static void Clear()
        {
            _events.Clear();
            _errors.Clear();
            _sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
            _lastExportPath = "";
        }

        /// <summary>
        /// Get a copy of all recent events.
        /// </summary>
        public static List<DebugEvent> GetRecentEvents() => _events.ToList();

        /// <summary>
        /// Get a copy of all recent errors.
        /// </summary>
        public static List<DebugEvent> GetRecentErrors() => _errors.ToList();
    }
}
