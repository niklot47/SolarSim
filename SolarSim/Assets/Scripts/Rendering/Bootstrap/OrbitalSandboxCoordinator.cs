using UnityEngine;
using UnityEngine.UIElements;
using SpaceSim.Data.Definitions;
using SpaceSim.Simulation.Core;
using SpaceSim.Simulation.Ships;
using SpaceSim.Simulation.Time;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;
using SpaceSim.Rendering.Orbits;
using SpaceSim.Rendering.Cameras;
using SpaceSim.Rendering.Selection;
using SpaceSim.Rendering.Labels;
using SpaceSim.UI.Panels;

using EntityId = SpaceSim.Shared.Identifiers.EntityId;

namespace SpaceSim.Rendering.Bootstrap
{
    /// <summary>
    /// Coordinator that wires together world data, rendering, selection, and UI
    /// for the orbital sandbox scene.
    /// Loads star system from assigned data asset, or falls back to sample factory.
    /// Initializes and ticks ShipMovementSystem for travelling ships.
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

        [Header("Ship Movement")]
        [Tooltip("Delay in simulation seconds before the sample trade route starts.")]
        [SerializeField] private double sampleRouteStartDelay = 2.0;

        [Tooltip("Duration in simulation seconds for the sample trade route.")]
        [SerializeField] private double sampleRouteDuration = 60.0;

        // Services.
        private WorldRegistry _registry;
        private StarSystem _currentSystem;
        private SelectionService _selectionService;
        private ShipMovementSystem _shipMovement;
        private SimulationClock _clock;

        // UI panel references for dynamic refresh.
        private ObjectListPanelController _listPanel;

        // Sample route state.
        private bool _sampleRouteStarted;

        /// <summary>
        /// Initialize and build the sandbox. Called from GameBootstrap.
        /// </summary>
        public void Setup(SimulationClock clock)
        {
            _clock = clock;
            _registry = new WorldRegistry();
            _selectionService = new SelectionService();

            // Load star system: from data asset if assigned, otherwise fallback.
            _currentSystem = LoadStarSystem();

            if (_currentSystem == null)
            {
                UnityEngine.Debug.LogError("[OrbitalSandboxCoordinator] Failed to load any star system!");
                return;
            }

            // Initialize ship movement system.
            _shipMovement = new ShipMovementSystem(_registry);
            _shipMovement.OnShipArrived += OnShipArrived;

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

        private void Update()
        {
            if (_clock == null || _shipMovement == null || mapRenderer == null) return;

            double simTime = _clock.CurrentTime;

            // Start sample route after a small delay.
            if (!_sampleRouteStarted && simTime >= sampleRouteStartDelay)
            {
                StartSampleRoute(simTime);
                _sampleRouteStarted = true;
            }

            // Tick ship movement — pass position resolver from renderer.
            _shipMovement.Update(simTime, (bodyId, time) => mapRenderer.ResolveWorldPosition(bodyId, time));
        }

        private void OnDestroy()
        {
            if (_shipMovement != null)
                _shipMovement.OnShipArrived -= OnShipArrived;
        }

        /// <summary>
        /// Called when a ship completes travel and arrives at destination.
        /// Refreshes the object list to reflect new hierarchy.
        /// </summary>
        private void OnShipArrived(EntityId shipId, EntityId destinationId)
        {
            // Ship was temporarily parented to root star during transit.
            // ShipMovementSystem.ArriveAtDestination already re-parents to destination.
            // But we need to remove from root star's children if it was there.
            RemoveFromTransitParent(shipId);

            RefreshObjectList();

            var ship = _registry.GetCelestialBody(shipId);
            var dest = _registry.GetCelestialBody(destinationId);
            UnityEngine.Debug.Log(
                $"[OrbitalSandboxCoordinator] Ship arrived: " +
                $"{ship?.DisplayName ?? shipId.ToString()} at {dest?.DisplayName ?? destinationId.ToString()}");
        }

        /// <summary>
        /// Remove a ship from the root star's children list if it was temporarily parented there during transit.
        /// Called on arrival, after ShipMovementSystem has already re-parented to destination.
        /// </summary>
        private void RemoveFromTransitParent(EntityId shipId)
        {
            // The ship's ParentId is now the destination (set by ShipMovementSystem).
            // But the root star may still have shipId in ChildIds from transit parenting.
            // Clean up all root bodies just in case.
            if (_currentSystem == null) return;
            foreach (var rootId in _currentSystem.RootBodyIds)
            {
                var root = _registry.GetCelestialBody(rootId);
                if (root != null && root.ChildIds.Contains(shipId))
                {
                    // Only remove if ship's actual parent is NOT this root.
                    var ship = _registry.GetCelestialBody(shipId);
                    if (ship != null && ship.ParentId != rootId)
                    {
                        root.RemoveChildId(shipId);
                    }
                }
            }
        }

        /// <summary>
        /// Parent a travelling ship to the system's root body (star)
        /// so it remains visible in the object list during transit.
        /// </summary>
        private void ParentToRootForTransit(EntityId shipId)
        {
            if (_currentSystem == null || _currentSystem.RootBodyIds.Count == 0) return;

            var rootId = _currentSystem.RootBodyIds[0];
            var root = _registry.GetCelestialBody(rootId);
            var ship = _registry.GetCelestialBody(shipId);
            if (root == null || ship == null) return;

            // Set ship's parent to root star for list display purposes.
            // Note: ship.AttachmentMode stays None, ship.Orbit stays null.
            // ShipMovementSystem controls actual position via OverrideWorldPosition.
            ship.ParentId = rootId;
            root.AddChildId(shipId);
        }

        /// <summary>
        /// Refresh the object list UI to reflect current hierarchy.
        /// </summary>
        private void RefreshObjectList()
        {
            if (_listPanel != null)
                _listPanel.Refresh();
        }

        /// <summary>
        /// Start a sample trade route: Trader ship (Cargo-7) flies from Terra to Ares.
        /// </summary>
        private void StartSampleRoute(double currentSimTime)
        {
            // Find the trader ship by ShipKey.
            CelestialBody traderShip = null;
            EntityId destinationId = EntityId.None;

            foreach (var bodyId in _currentSystem.AllBodyIds)
            {
                var body = _registry.GetCelestialBody(bodyId);
                if (body == null) continue;

                if (body.BodyType == CelestialBodyType.Ship
                    && body.ShipInfo != null
                    && body.ShipInfo.ShipKey == "ship_cargo7")
                {
                    traderShip = body;
                }

                // Find Ares by localization key.
                if (body.LocalizationKeyName == "body.ares")
                {
                    destinationId = body.Id;
                }
            }

            if (traderShip != null && destinationId.IsValid)
            {
                bool started = _shipMovement.StartRoute(
                    traderShip.Id, destinationId, currentSimTime, sampleRouteDuration);

                if (started)
                {
                    // Parent the travelling ship to the root star so it stays in the object list.
                    ParentToRootForTransit(traderShip.Id);

                    // Refresh list to reflect departure.
                    RefreshObjectList();

                    UnityEngine.Debug.Log(
                        $"[OrbitalSandboxCoordinator] Sample route started: " +
                        $"{traderShip.DisplayName} -> destination {destinationId} " +
                        $"duration={sampleRouteDuration:F0}s");
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    "[OrbitalSandboxCoordinator] Could not start sample route — ship or destination not found.");
            }
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

            _listPanel = gameObject.AddComponent<ObjectListPanelController>();
            _listPanel.Initialize(_registry, _currentSystem, _selectionService);
            _listPanel.SetupUI(uiRoot);

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
        public ShipMovementSystem ShipMovement => _shipMovement;
    }
}
