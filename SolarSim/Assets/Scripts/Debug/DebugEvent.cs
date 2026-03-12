using System;

namespace SpaceSim.Debug
{
    /// <summary>
    /// Severity levels for debug events.
    /// </summary>
    public enum DebugSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Categories for debug events.
    /// </summary>
    public enum DebugCategory
    {
        SIM,
        NPC,
        ECONOMY,
        CONTRACTS,
        UI,
        PATH,
        ORBIT,
        SHIPS,
        SAVELOAD,
        ERROR,
        DEBUG
    }

    /// <summary>
    /// A single structured debug event.
    /// Serializable to JSON without Unity object references.
    /// </summary>
    [Serializable]
    public class DebugEvent
    {
        public string TimestampUtc;
        public string SessionId;
        public string SceneName;
        public long Frame;
        public string Category;
        public string Severity;
        public string Message;
        public string Source;
        public string Data;

        public DebugEvent(
            DebugCategory category,
            DebugSeverity severity,
            string message,
            string source = "",
            string data = "",
            string sceneName = "",
            long frame = 0,
            string sessionId = "")
        {
            TimestampUtc = DateTime.UtcNow.ToString("o");
            SessionId = sessionId;
            SceneName = sceneName;
            Frame = frame;
            Category = category.ToString();
            Severity = severity.ToString();
            Message = message;
            Source = source;
            Data = data;
        }
    }
}
