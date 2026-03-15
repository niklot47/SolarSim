using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SpaceSim.Debug
{
    /// <summary>
    /// Exports debug bundles to JSON files on disk.
    /// Uses manual JSON building to avoid dependency on JsonUtility
    /// (which cannot handle Dictionary and object fields properly)
    /// and to keep the Debug assembly independent from UnityEngine.
    ///
    /// All number formatting uses CultureInfo.InvariantCulture to ensure
    /// dot decimal separator regardless of system locale.
    ///
    /// Output: pretty-printed JSON in persistentDataPath/debug_bundles/.
    /// </summary>
    public static class DebugExportUtility
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// Serialize a DebugBundle to a pretty-printed JSON string.
        /// </summary>
        public static string BundleToJson(DebugBundle bundle)
        {
            if (bundle == null) return "{}";

            var sb = new StringBuilder(8192);
            sb.AppendLine("{");

            // Metadata.
            sb.AppendLine("  \"metadata\": {");
            WriteString(sb, "sessionId", bundle.Metadata.SessionId, 4);
            WriteString(sb, "exportedAtUtc", bundle.Metadata.ExportedAtUtc, 4);
            WriteString(sb, "sceneName", bundle.Metadata.SceneName, 4);
            WriteLong(sb, "frame", bundle.Metadata.Frame, 4);
            WriteDouble(sb, "simulationTime", bundle.Metadata.SimulationTime, 4);
            WriteInt(sb, "eventBufferCapacity", bundle.Metadata.EventBufferCapacity, 4);
            WriteInt(sb, "errorBufferCapacity", bundle.Metadata.ErrorBufferCapacity, 4);
            WriteInt(sb, "snapshotBufferCapacity", bundle.Metadata.SnapshotBufferCapacity, 4);
            WriteInt(sb, "totalEventsLogged", bundle.Metadata.TotalEventsLogged, 4);
            WriteIntLast(sb, "totalErrorsLogged", bundle.Metadata.TotalErrorsLogged, 4);
            sb.AppendLine("  },");

            // Status.
            WriteString(sb, "statusSummary", bundle.StatusSummary, 2);

            // Events.
            sb.AppendLine("  \"recentEvents\": [");
            WriteEventList(sb, bundle.RecentEvents, 4);
            sb.AppendLine("  ],");

            // Errors.
            sb.AppendLine("  \"recentErrors\": [");
            WriteEventList(sb, bundle.RecentErrors, 4);
            sb.AppendLine("  ],");

            // Snapshots.
            sb.AppendLine("  \"snapshots\": [");
            WriteSnapshotList(sb, bundle.Snapshots, 4);
            sb.AppendLine("  ],");

            // Violations.
            sb.AppendLine("  \"violations\": [");
            WriteViolationList(sb, bundle.Violations, 4);
            sb.AppendLine("  ]");

            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>
        /// Write JSON to a file at the given directory.
        /// Creates directory if it does not exist.
        /// Returns the full file path, or empty string on failure.
        /// </summary>
        public static string WriteToFile(string json, string directory)
        {
            try
            {
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string filename = $"debug_bundle_{timestamp}.json";
                string fullPath = Path.Combine(directory, filename);

                File.WriteAllText(fullPath, json, Encoding.UTF8);
                return fullPath;
            }
            catch (Exception)
            {
                return "";
            }
        }

        // ---------------------------------------------------------------
        // JSON writing helpers (all use InvariantCulture)
        // ---------------------------------------------------------------

        private static void WriteEventList(StringBuilder sb, List<DebugEvent> events, int indent)
        {
            if (events == null || events.Count == 0) return;
            string pad = new string(' ', indent);
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                sb.Append(pad);
                sb.Append("{");
                sb.Append(string.Format(Inv, "\"ts\":\"{0}\",", Escape(e.TimestampUtc)));
                sb.Append(string.Format(Inv, "\"cat\":\"{0}\",", Escape(e.Category)));
                sb.Append(string.Format(Inv, "\"sev\":\"{0}\",", Escape(e.Severity)));
                sb.Append(string.Format(Inv, "\"msg\":\"{0}\"", Escape(e.Message)));
                if (!string.IsNullOrEmpty(e.Source))
                    sb.Append(string.Format(Inv, ",\"src\":\"{0}\"", Escape(e.Source)));
                if (!string.IsNullOrEmpty(e.Data))
                    sb.Append(string.Format(Inv, ",\"data\":\"{0}\"", Escape(e.Data)));
                sb.Append("}");
                if (i < events.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
        }

        private static void WriteSnapshotList(StringBuilder sb, List<DebugSnapshot> snapshots, int indent)
        {
            if (snapshots == null || snapshots.Count == 0) return;
            string pad = new string(' ', indent);
            string pad2 = new string(' ', indent + 2);

            for (int i = 0; i < snapshots.Count; i++)
            {
                var s = snapshots[i];
                sb.AppendLine($"{pad}{{");
                sb.AppendLine(string.Format(Inv, "{0}\"timestampUtc\": \"{1}\",", pad2, Escape(s.TimestampUtc)));
                sb.AppendLine(string.Format(Inv, "{0}\"reason\": \"{1}\",", pad2, Escape(s.Reason)));
                sb.AppendLine(string.Format(Inv, "{0}\"sceneName\": \"{1}\",", pad2, Escape(s.SceneName)));
                sb.AppendLine(string.Format(Inv, "{0}\"frame\": {1},", pad2, s.Frame));
                sb.AppendLine(string.Format(Inv, "{0}\"simulationTime\": {1:F2},", pad2, s.SimulationTime));
                sb.AppendLine(string.Format(Inv, "{0}\"timeScale\": {1:F1},", pad2, s.TimeScale));
                sb.AppendLine(string.Format(Inv, "{0}\"isPaused\": {1},", pad2, s.IsPaused ? "true" : "false"));

                // Subsystems.
                sb.AppendLine($"{pad2}\"subsystems\": [");
                for (int j = 0; j < s.Subsystems.Count; j++)
                {
                    var sub = s.Subsystems[j];
                    string pad3 = new string(' ', indent + 4);
                    sb.Append(string.Format(Inv, "{0}{{\"name\":\"{1}\",\"status\":\"{2}\"",
                        pad3, Escape(sub.Name), Escape(sub.Status)));
                    if (sub.Data != null && sub.Data.Count > 0)
                    {
                        sb.Append(",\"data\":{");
                        bool first = true;
                        foreach (var kvp in sub.Data)
                        {
                            if (!first) sb.Append(",");
                            sb.Append(string.Format(Inv, "\"{0}\":", Escape(kvp.Key)));
                            WriteValue(sb, kvp.Value);
                            first = false;
                        }
                        sb.Append("}");
                    }
                    sb.Append("}");
                    if (j < s.Subsystems.Count - 1) sb.Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine($"{pad2}],");

                // Recent errors count.
                sb.AppendLine(string.Format(Inv, "{0}\"recentErrorCount\": {1}", pad2, s.RecentErrors.Count));

                sb.Append(pad);
                sb.Append("}");
                if (i < snapshots.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
        }

        private static void WriteViolationList(StringBuilder sb, List<InvariantViolation> violations, int indent)
        {
            if (violations == null || violations.Count == 0) return;
            string pad = new string(' ', indent);

            for (int i = 0; i < violations.Count; i++)
            {
                var v = violations[i];
                sb.Append(pad);
                sb.Append("{");
                sb.Append(string.Format(Inv, "\"check\":\"{0}\",", Escape(v.CheckName)));
                sb.Append(string.Format(Inv, "\"msg\":\"{0}\",", Escape(v.Message)));
                sb.Append(string.Format(Inv, "\"entity\":\"{0}\",", Escape(v.EntityId)));
                sb.Append(string.Format(Inv, "\"name\":\"{0}\"", Escape(v.EntityName)));
                sb.Append("}");
                if (i < violations.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
        }

        private static void WriteString(StringBuilder sb, string name, string value, int indent)
        {
            sb.AppendLine(string.Format(Inv, "{0}\"{1}\": \"{2}\",",
                new string(' ', indent), name, Escape(value ?? "")));
        }

        private static void WriteLong(StringBuilder sb, string name, long value, int indent)
        {
            sb.AppendLine(string.Format(Inv, "{0}\"{1}\": {2},",
                new string(' ', indent), name, value));
        }

        private static void WriteInt(StringBuilder sb, string name, int value, int indent)
        {
            sb.AppendLine(string.Format(Inv, "{0}\"{1}\": {2},",
                new string(' ', indent), name, value));
        }

        private static void WriteIntLast(StringBuilder sb, string name, int value, int indent)
        {
            sb.AppendLine(string.Format(Inv, "{0}\"{1}\": {2}",
                new string(' ', indent), name, value));
        }

        private static void WriteDouble(StringBuilder sb, string name, double value, int indent)
        {
            sb.AppendLine(string.Format(Inv, "{0}\"{1}\": {2:F2},",
                new string(' ', indent), name, value));
        }

        private static void WriteValue(StringBuilder sb, object value)
        {
            if (value == null) { sb.Append("null"); return; }
            if (value is string s) { sb.Append(string.Format(Inv, "\"{0}\"", Escape(s))); return; }
            if (value is bool b) { sb.Append(b ? "true" : "false"); return; }
            if (value is int vi) { sb.Append(vi.ToString(Inv)); return; }
            if (value is long vl) { sb.Append(vl.ToString(Inv)); return; }
            if (value is ulong vu) { sb.Append(vu.ToString(Inv)); return; }
            if (value is float vf) { sb.Append(vf.ToString("F2", Inv)); return; }
            if (value is double vd) { sb.Append(vd.ToString("F2", Inv)); return; }
            sb.Append(string.Format(Inv, "\"{0}\"", Escape(value.ToString())));
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }
    }
}
