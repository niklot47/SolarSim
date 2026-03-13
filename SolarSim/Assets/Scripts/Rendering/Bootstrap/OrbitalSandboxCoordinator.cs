using UnityEngine;
using UnityEngine.UIElements;
using SpaceSim.Data.Definitions;
using SpaceSim.Simulation.Core;
using SpaceSim.Simulation.Time;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;
using SpaceSim.Rendering.Orbits;
using SpaceSim.Rendering.Cameras;
using SpaceSim.Rendering.Selection;
using SpaceSim.Rendering.Labels;
using SpaceSim.UI.Panels;

namespace SpaceSim.Rendering.Bootstrap
{
    /// <summary>
    /// Coordinator that wires together world data, rendering, selection, and UI
    /// for the orbital sandbox scene.
    /// Loads star system from assigned data asset, or falls back to sample factory.
    /// </summary>
    public class OrbitalSandboxCoordinator : MonoBehaviour
    {
        [Header("Rendering")]
        [SerializeField] private OrbitalMapRenderer mapRenderer;
        [SerializeField] private OrbitalCameraController cameraController;

        [Header("UI")]
        [SerializeField] private UIDocument uiDocument;

        [Header("Data")]
        [Tooltip("Assign a StarSystemDefinition asset. If empty, uses built-in sample system.")]
        [SerializeField] private StarSystemDefinition starSystemDefinition;

        // Services.
        private WorldRegistry _registry;
        private StarSystem _currentSystem;
        private SelectionService _selectionService;

        /// <summary>
        /// Initialize and build the sandbox. Called from GameBootstrap.
        /// </summary>
        public void Setup(SimulationClock clock)
        {
            _registry = new WorldRegistry();
            _selectionService = new SelectionService();

            // Load star system: from data asset if assigned, otherwise fallback.
            _currentSystem = LoadStarSystem();

            if (_currentSystem == null)
            {
                UnityEngine.Debug.LogError("[OrbitalSandboxCoordinator] Failed to load any star system!");
                return;
            }

            // Initialize renderer.
            if (mapRenderer != null)
            {
                mapRenderer.Initialize(_registry, _currentSystem, clock);
                mapRenderer.BuildSceneObjects();
            }

            // Selection bridge.
            var selectionBridge = gameObject.AddComponent<SelectionBridge>();
            selectionBridge.Initialize(_selectionService, mapRenderer, cameraController);

            // Scene click handler.
            var clickHandler = gameObject.AddComponent<BodyClickHandler>();
            clickHandler.Initialize(_selectionService, uiDocument);

            // Body name labels.
            var labelController = gameObject.AddComponent<BodyLabelController>();
            labelController.Initialize(_registry, _currentSystem, mapRenderer, cameraController);

            // UI panels.
            SetupUIPanels(clock);

            UnityEngine.Debug.Log($"[OrbitalSandboxCoordinator] Setup complete. System: {_currentSystem}");
        }

        private StarSystem LoadStarSystem()
        {
            // Try loading from assigned data asset.
            if (starSystemDefinition != null)
            {
                var system = StarSystemLoader.Load(starSystemDefinition, _registry);
                if (system != null)
                {
                    UnityEngine.Debug.Log($"[OrbitalSandboxCoordinator] Loaded from asset: {starSystemDefinition.DisplayName}");
                    return system;
                }
                UnityEngine.Debug.LogWarning("[OrbitalSandboxCoordinator] Asset assigned but loading failed. Falling back to sample.");
            }

            // Fallback to hardcoded sample.
            UnityEngine.Debug.Log("[OrbitalSandboxCoordinator] Using built-in sample star system.");
            return SampleStarSystemFactory.Create(_registry);
        }

        private void SetupUIPanels(SimulationClock clock)
        {
            if (uiDocument == null) return;
            var uiRoot = uiDocument.rootVisualElement;
            if (uiRoot == null) return;

            var listPanel = gameObject.AddComponent<ObjectListPanelController>();
            listPanel.Initialize(_registry, _currentSystem, _selectionService);
            listPanel.SetupUI(uiRoot);

            var detailsPanel = gameObject.AddComponent<ObjectDetailsPanelController>();
            detailsPanel.Initialize(_registry, _selectionService);
            detailsPanel.SetupUI(uiRoot);

            var timePanel = gameObject.AddComponent<TimeControlsPanelController>();
            timePanel.Initialize(clock);
            timePanel.SetupUI(uiRoot);
        }

        public WorldRegistry Registry => _registry;
        public SelectionService Selection => _selectionService;
        public StarSystem CurrentSystem => _currentSystem;
    }
}
