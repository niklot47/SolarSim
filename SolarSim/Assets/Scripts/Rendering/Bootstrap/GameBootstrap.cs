using UnityEngine;
using SpaceSim.Simulation.Time;
using SpaceSim.Debug;

namespace SpaceSim.Rendering.Bootstrap
{
    /// <summary>
    /// Main entry point MonoBehaviour. Initializes core systems.
    /// Attach to a GameObject in the Bootstrap scene.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        /// <summary>
        /// The simulation clock instance, accessible to other systems.
        /// </summary>
        public static SimulationClock Clock { get; private set; }

        /// <summary>
        /// Whether core systems have been initialized.
        /// </summary>
        public static bool IsInitialized { get; private set; }

        [Header("Debug")]
        [SerializeField] private bool enableDebugSystem = true;

        [Header("Sandbox")]
        [SerializeField] private OrbitalSandboxCoordinator sandboxCoordinator;

        private void Awake()
        {
            if (IsInitialized)
            {
                // Prevent duplicate bootstrap.
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        private void Initialize()
        {
            // 1. Simulation clock.
            Clock = new SimulationClock();

            // 2. Debug system.
            GameDebug.Enabled = enableDebugSystem;
            GameDebug.OnEventLogged = ForwardToUnityLog;

            GameDebug.Log(DebugCategory.DEBUG, "GameBootstrap initialized",
                source: nameof(GameBootstrap),
                sceneName: gameObject.scene.name);

            // 3. Orbital sandbox (if coordinator is assigned).
            if (sandboxCoordinator != null)
            {
                sandboxCoordinator.Setup(Clock);
                GameDebug.Log(DebugCategory.DEBUG, "Orbital sandbox initialized",
                    source: nameof(GameBootstrap));
            }

            IsInitialized = true;

            UnityEngine.Debug.Log($"[GameBootstrap] Initialized. {GameDebug.GetStatus()}");
        }

        private void Update()
        {
            // Tick the simulation clock with Unity delta time.
            Clock?.Tick(UnityEngine.Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (Clock != null)
            {
                GameDebug.Log(DebugCategory.DEBUG, "GameBootstrap destroyed",
                    source: nameof(GameBootstrap));
            }
        }

        /// <summary>
        /// Bridge: forwards debug events to Unity console.
        /// </summary>
        private static void ForwardToUnityLog(DebugEvent evt)
        {
            string msg = $"[{evt.Category}] {evt.Message}";
            switch (evt.Severity)
            {
                case "Error":
                    UnityEngine.Debug.LogError(msg);
                    break;
                case "Warning":
                    UnityEngine.Debug.LogWarning(msg);
                    break;
                default:
                    UnityEngine.Debug.Log(msg);
                    break;
            }
        }

        // --- Editor convenience ---
#if UNITY_EDITOR
        [ContextMenu("Debug: Print Status")]
        private void PrintStatus()
        {
            UnityEngine.Debug.Log(GameDebug.GetStatus());
        }

        [ContextMenu("Debug: Export Bundle")]
        private void EditorExportBundle()
        {
            var bundle = GameDebug.ExportBundle();
            UnityEngine.Debug.Log($"[GameDebug] Bundle exported with {((System.Collections.Generic.List<DebugEvent>)bundle["events"]).Count} events");
        }

        [ContextMenu("Debug: Capture Snapshot")]
        private void EditorCaptureSnapshot()
        {
            var snap = GameDebug.CaptureSnapshot("editor_manual");
            UnityEngine.Debug.Log($"[GameDebug] Snapshot: {snap["eventCount"]} events, {snap["errorCount"]} errors");
        }

        [ContextMenu("Debug: Clear")]
        private void EditorClear()
        {
            GameDebug.Clear();
            UnityEngine.Debug.Log("[GameDebug] Cleared");
        }
#endif
    }
}
