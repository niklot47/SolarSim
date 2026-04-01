using UnityEngine;
using SpaceSim.Simulation.Time;
using SpaceSim.Debug;

namespace SpaceSim.Rendering.Bootstrap
{
    /// <summary>
    /// Main entry point MonoBehaviour. Initializes core systems.
    ///
    /// Time scale presets (sim-s = real seconds, distances compressed):
    ///   x1       — real time (docking, combat)
    ///   x86400   — 1 real second = 1 game day  (default, strategic)
    ///   x864000  — 1 real second = 10 game days (fast-forward)
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        public static SimulationClock Clock       { get; private set; }
        public static bool            IsInitialized { get; private set; }

        [Header("Debug")]
        [SerializeField] private bool               enableDebugSystem  = true;
        [SerializeField] private DebugFilterProfile debugFilterProfile;

        [Header("Simulation")]
        [Tooltip("Initial time scale.\n" +
                 "1 = real time (docking/combat)\n" +
                 "86400 = 1 sec = 1 day (strategic, default)\n" +
                 "864000 = 1 sec = 10 days (fast-forward)")]
        [SerializeField] private double initialTimeScale = 86400.0;

        [Header("Sandbox")]
        [SerializeField] private OrbitalSandboxCoordinator sandboxCoordinator;

        private void Awake()
        {
            if (IsInitialized) { Destroy(gameObject); return; }
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        private void Initialize()
        {
            Clock           = new SimulationClock();
            Clock.TimeScale = initialTimeScale;

            GameDebug.Enabled       = enableDebugSystem;
            GameDebug.OnEventLogged = ForwardToUnityLog;

            if (debugFilterProfile != null)
                debugFilterProfile.ApplyTo(GameDebug.ActiveFilter);

            string exportDir = System.IO.Path.Combine(
                Application.persistentDataPath, "debug_bundles");
            GameDebug.SetExportDirectory(exportDir);

            GameDebug.Log(DebugCategory.DEBUG, "GameBootstrap initialized",
                source: nameof(GameBootstrap), sceneName: gameObject.scene.name);

            if (sandboxCoordinator != null)
            {
                sandboxCoordinator.Setup(Clock);
                GameDebug.Log(DebugCategory.DEBUG, "Orbital sandbox initialized",
                    source: nameof(GameBootstrap));
            }

            IsInitialized = true;

            UnityEngine.Debug.Log(
                $"[GameBootstrap] Initialized. TimeScale={Clock.TimeScale}. {GameDebug.GetStatus()}");
        }

        private void Update()
        {
            Clock?.Tick(UnityEngine.Time.deltaTime);

            if (GameDebug.Enabled && Clock != null)
            {
                GameDebug.SetContext(
                    sceneName:  gameObject.scene.name,
                    frame:      UnityEngine.Time.frameCount,
                    simTime:    Clock.CurrentTime,
                    timeScale:  Clock.TimeScale,
                    isPaused:   Clock.IsPaused);
            }
        }

        private void OnDestroy()
        {
            if (Clock != null)
                GameDebug.Log(DebugCategory.DEBUG, "GameBootstrap destroyed",
                    source: nameof(GameBootstrap));
        }

        private static void ForwardToUnityLog(DebugEvent evt)
        {
            string msg = $"[{evt.Category}] {evt.Message}";
            switch (evt.Severity)
            {
                case "Error":   UnityEngine.Debug.LogError(msg);   break;
                case "Warning": UnityEngine.Debug.LogWarning(msg); break;
                default:        UnityEngine.Debug.Log(msg);        break;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying && debugFilterProfile != null)
                debugFilterProfile.ApplyTo(GameDebug.ActiveFilter);
        }

        [ContextMenu("Debug/Печать статуса")]
        private void PrintStatus() =>
            UnityEngine.Debug.Log(GameDebug.GetStatus());

        [ContextMenu("Debug/Экспорт бандла")]
        private void EditorExportBundle()
        {
            string path = GameDebug.ExportBundle();
            UnityEngine.Debug.Log(string.IsNullOrEmpty(path)
                ? "[GameDebug] Bundle export failed"
                : $"[GameDebug] Bundle exported: {path}");
        }

        [ContextMenu("Debug/Снимок состояния")]
        private void EditorCaptureSnapshot()
        {
            var snap = GameDebug.CaptureSnapshot("editor_manual");
            UnityEngine.Debug.Log(
                $"[GameDebug] Snapshot: {snap.Subsystems.Count} subsystems, " +
                $"simTime={snap.SimulationTime:F0}s, errors={snap.RecentErrors.Count}");
        }

        [ContextMenu("Debug/Проверка инвариантов")]
        private void EditorRunInvariants()
        {
            var v = GameDebug.RunInvariantChecks();
            UnityEngine.Debug.Log(v.Count == 0
                ? "[GameDebug] Invariant check: OK"
                : $"[GameDebug] {v.Count} violation(s)!");
        }

        [ContextMenu("Debug/Очистить")]
        private void EditorClear()
        {
            GameDebug.Clear();
            UnityEngine.Debug.Log("[GameDebug] Cleared");
        }

        [ContextMenu("Debug/Показать фильтр")]
        private void EditorShowFilter() =>
            UnityEngine.Debug.Log($"[GameDebug] {GameDebug.ActiveFilter.GetFilterSummary()}");
#endif
    }
}