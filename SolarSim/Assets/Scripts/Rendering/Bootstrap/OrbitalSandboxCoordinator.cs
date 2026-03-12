using UnityEngine;
using UnityEngine.UIElements;
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
    /// for the orbital sandbox scene. Keeps GameBootstrap thin.
    /// </summary>
    public class OrbitalSandboxCoordinator : MonoBehaviour
    {
        [Header("Rendering")]
        [SerializeField] private OrbitalMapRenderer mapRenderer;
        [SerializeField] private OrbitalCameraController cameraController;

        [Header("UI")]
        [SerializeField] private UIDocument uiDocument;

        // Services.
        private WorldRegistry _registry;
        private StarSystem _currentSystem;
        private SelectionService _selectionService;

        /// <summary>
        /// Initialize and build the sandbox. Called from GameBootstrap.
        /// </summary>
        public void Setup(SimulationClock clock)
        {
            // Create core services.
            _registry = new WorldRegistry();
            _selectionService = new SelectionService();

            // Create sample star system.
            _currentSystem = SampleStarSystemFactory.Create(_registry);

            // Initialize renderer.
            if (mapRenderer != null)
            {
                mapRenderer.Initialize(_registry, _currentSystem, clock);
                mapRenderer.BuildSceneObjects();
            }

            // Selection bridge (highlight ring + camera focus).
            var selectionBridge = gameObject.AddComponent<SelectionBridge>();
            selectionBridge.Initialize(_selectionService, mapRenderer, cameraController);

            // Scene click handler (pass uiDocument for UI-aware picking).
            var clickHandler = gameObject.AddComponent<BodyClickHandler>();
            clickHandler.Initialize(_selectionService, uiDocument);

            // Body name labels.
            var labelController = gameObject.AddComponent<BodyLabelController>();
            labelController.Initialize(_registry, _currentSystem, mapRenderer, cameraController);

            // Setup UI panels from shared UIDocument.
            SetupUIPanels(clock);

            UnityEngine.Debug.Log($"[OrbitalSandboxCoordinator] Setup complete. System: {_currentSystem}");
        }

        private void SetupUIPanels(SimulationClock clock)
        {
            if (uiDocument == null) return;

            var uiRoot = uiDocument.rootVisualElement;
            if (uiRoot == null) return;

            // Object list panel.
            var listPanel = gameObject.AddComponent<ObjectListPanelController>();
            listPanel.Initialize(_registry, _currentSystem, _selectionService);
            listPanel.SetupUI(uiRoot);

            // Object details panel.
            var detailsPanel = gameObject.AddComponent<ObjectDetailsPanelController>();
            detailsPanel.Initialize(_registry, _selectionService);
            detailsPanel.SetupUI(uiRoot);

            // Time controls panel.
            var timePanel = gameObject.AddComponent<TimeControlsPanelController>();
            timePanel.Initialize(clock);
            timePanel.SetupUI(uiRoot);
        }

        /// <summary>Access to world registry for external queries.</summary>
        public WorldRegistry Registry => _registry;

        /// <summary>Access to selection service.</summary>
        public SelectionService Selection => _selectionService;

        /// <summary>Access to current star system.</summary>
        public StarSystem CurrentSystem => _currentSystem;
    }
}
