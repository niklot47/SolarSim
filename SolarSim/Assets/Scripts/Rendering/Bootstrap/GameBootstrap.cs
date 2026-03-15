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

            // Set export directory to persistentDataPath/debug_bundles.
            string exportDir = System.IO.Path.Combine(
                Application.persistentDataPath, "debug_bundles");
            GameDebug.SetExportDirectory(exportDir);

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
            UnityEngine.Debug.Log($"[GameBootstrap] Debug export dir: {exportDir}");
        }

        private void Update()
        {
            // Tick the simulation clock with Unity delta time.
            Clock?.Tick(UnityEngine.Time.deltaTime);

            // Update debug context each frame so snapshots have fresh data.
            if (GameDebug.Enabled && Clock != null)
            {
                GameDebug.SetContext(
                    sceneName: gameObject.scene.name,
                    frame: UnityEngine.Time.frameCount,
                    simTime: Clock.CurrentTime,
                    timeScale: Clock.TimeScale,
                    isPaused: Clock.IsPaused);
            }
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
        [ContextMenu("Debug/Печать статуса")]
        private void PrintStatus()
        {
            UnityEngine.Debug.Log(GameDebug.GetStatus());
        }

        [ContextMenu("Debug/Экспорт бандла")]
        private void EditorExportBundle()
        {
            string path = GameDebug.ExportBundle();
            if (!string.IsNullOrEmpty(path))
                UnityEngine.Debug.Log($"[GameDebug] Bundle exported: {path}");
            else
                UnityEngine.Debug.LogWarning("[GameDebug] Bundle export returned no path");
        }

        [ContextMenu("Debug/Снимок состояния")]
        private void EditorCaptureSnapshot()
        {
            var snap = GameDebug.CaptureSnapshot("editor_manual");
            UnityEngine.Debug.Log(
                $"[GameDebug] Snapshot: {snap.Subsystems.Count} subsystems, " +
                $"simTime={snap.SimulationTime:F2}, errors={snap.RecentErrors.Count}");
        }

        [ContextMenu("Debug/Проверка инвариантов")]
        private void EditorRunInvariants()
        {
            var violations = GameDebug.RunInvariantChecks();
            if (violations.Count == 0)
                UnityEngine.Debug.Log("[GameDebug] Invariant check: OK (0 violations)");
            else
                UnityEngine.Debug.LogWarning($"[GameDebug] Invariant check: {violations.Count} violation(s)!");
        }

        [ContextMenu("Debug/Очистить")]
        private void EditorClear()
        {
            GameDebug.Clear();
            UnityEngine.Debug.Log("[GameDebug] Cleared");
        }

        [ContextMenu("Debug/Экспорт + Инварианты")]
        private void EditorFullDebugDump()
        {
            GameDebug.RunInvariantChecks();
            string path = GameDebug.ExportBundle();
            UnityEngine.Debug.Log($"[GameDebug] Full dump: {path}");
        }
#endif
    }
}
