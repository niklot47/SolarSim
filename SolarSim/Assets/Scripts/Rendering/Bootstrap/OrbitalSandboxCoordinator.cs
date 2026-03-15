using UnityEngine;
using UnityEngine.UIElements;
using SpaceSim.Data.Definitions;
using SpaceSim.Simulation.Core;
using SpaceSim.Simulation.Docking;
using SpaceSim.Simulation.Ships;
using SpaceSim.Simulation.SOI;
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

        [Header("NPC Scheduling")]
        [Tooltip("NPC ship travel speed in Mm/sim-s.")]
        [Min(0.1f)]
        [SerializeField] private float npcTravelSpeed = 2.0f;

        [Tooltip("Minimum travel duration floor (sim-seconds). SET TO 3 OR LOWER.")]
        [Min(0.5f)]
        [SerializeField] private float npcMinTravelDuration = 3.0f;

        [Tooltip("Idle delay at destination before next departure (sim-seconds).")]
        [Min(0.0f)]
        [SerializeField] private float npcIdleDelay = 3.0f;

        [Header("Docking")]
        [Tooltip("How long NPC ships stay docked at stations (sim-seconds).")]
        [Min(1.0f)]
        [SerializeField] private float npcDockingWaitTime = 5.0f;

        [Tooltip("Duration of the docking approach phase (sim-seconds).")]
        [Min(0.5f)]
        [SerializeField] private float dockingApproachDuration = 2.0f;

        private WorldRegistry _registry;
        private StarSystem _currentSystem;
        private SelectionService _selectionService;
        private ShipMovementSystem _shipMovement;
        private NPCShipScheduler _npcScheduler;
        private WorldPositionResolver _positionResolver;
        private SOIResolver _soiResolver;
        private DockingSystem _dockingSystem;
        private SimulationClock _clock;
        private ObjectListPanelController _listPanel;

        public void Setup(SimulationClock clock)
        {
            _clock = clock;
            _registry = new WorldRegistry();
            _selectionService = new SelectionService();

            _currentSystem = LoadStarSystem();
            if (_currentSystem == null)
            {
                UnityEngine.Debug.LogError("[OrbitalSandboxCoordinator] Failed to load any star system!");
                return;
            }

            // Create simulation-side services.
            _positionResolver = new WorldPositionResolver(_registry);
            _soiResolver = new SOIResolver(_registry, _positionResolver);

            _shipMovement = new ShipMovementSystem(_registry);
            _shipMovement.OnShipArrived += OnShipArrived;

            _dockingSystem = new DockingSystem(_registry);
            _dockingSystem.ApproachDuration = dockingApproachDuration;
            _dockingSystem.OnShipDocked += OnShipDocked;
            _dockingSystem.OnShipUndocked += OnShipUndocked;

            _npcScheduler = new NPCShipScheduler(
                _registry, _shipMovement, () => _clock.CurrentTime,
                npcTravelSpeed, npcMinTravelDuration, npcIdleDelay);
            _npcScheduler.OnRouteScheduled += OnNpcRouteScheduled;
            _npcScheduler.DockingWaitTime = npcDockingWaitTime;
            _npcScheduler.SetDockingSystem(_dockingSystem);

            if (mapRenderer != null)
            {
                mapRenderer.Initialize(_registry, _currentSystem, clock, _positionResolver);
                mapRenderer.BuildSceneObjects();

                _npcScheduler.SetPositionResolver(
                    (bodyId, time) => _positionResolver.Resolve(bodyId, time));
            }

            var selectionBridge = gameObject.AddComponent<SelectionBridge>();
            selectionBridge.Initialize(_selectionService, mapRenderer, cameraController);

            var clickHandler = gameObject.AddComponent<BodyClickHandler>();
            clickHandler.Initialize(_selectionService, uiDocument);

            var labelController = gameObject.AddComponent<BodyLabelController>();
            labelController.Initialize(_registry, _currentSystem, mapRenderer, cameraController);

            SetupUIPanels(clock);

            // Initial SOI pass for all ships.
            _soiResolver.UpdateAllShips(_clock.CurrentTime);

            UnityEngine.Debug.Log(
                $"[OrbitalSandboxCoordinator] Setup complete. System: {_currentSystem}. " +
                $"{_soiResolver.GetStatus()}. " +
                $"NPC: speed={npcTravelSpeed:F1} minDur={npcMinTravelDuration:F1} idle={npcIdleDelay:F1} " +
                $"dockWait={npcDockingWaitTime:F1} approach={dockingApproachDuration:F1}");
        }

        private void Update()
        {
            if (_clock == null || _shipMovement == null || _positionResolver == null) return;

            // Sync Inspector parameters to runtime services.
            if (_npcScheduler != null)
            {
                _npcScheduler.TravelSpeed = npcTravelSpeed;
                _npcScheduler.MinTravelDuration = npcMinTravelDuration;
                _npcScheduler.IdleDelay = npcIdleDelay;
                _npcScheduler.DockingWaitTime = npcDockingWaitTime;
            }
            if (_dockingSystem != null)
            {
                _dockingSystem.ApproachDuration = dockingApproachDuration;
            }

            double simTime = _clock.CurrentTime;

            _shipMovement.Update(simTime, (bodyId, time) => _positionResolver.Resolve(bodyId, time));

            // Update docking approach interpolation.
            _dockingSystem?.Update(simTime, (bodyId, time) => _positionResolver.Resolve(bodyId, time));

            _npcScheduler?.Update();

            // Update SOI tracking for all ships after movement.
            if (_soiResolver != null)
            {
                var transitions = _soiResolver.UpdateAllShips(simTime);
                if (transitions != null)
                {
                    foreach (var t in transitions)
                    {
                        LogSOITransition(t);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (_shipMovement != null) _shipMovement.OnShipArrived -= OnShipArrived;
            if (_npcScheduler != null) _npcScheduler.OnRouteScheduled -= OnNpcRouteScheduled;
            if (_dockingSystem != null)
            {
                _dockingSystem.OnShipDocked -= OnShipDocked;
                _dockingSystem.OnShipUndocked -= OnShipUndocked;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (npcTravelSpeed < 0.1f) npcTravelSpeed = 0.1f;
            if (npcMinTravelDuration < 0.5f) npcMinTravelDuration = 0.5f;
        }
#endif

        private void OnNpcRouteScheduled(EntityId shipId)
        {
            ParentToRootForTransit(shipId);
            RefreshObjectList();
        }

        private void OnShipArrived(EntityId shipId, EntityId destinationId)
        {
            RemoveFromTransitParent(shipId);
            RefreshObjectList();
            var ship = _registry.GetCelestialBody(shipId);
            var dest = _registry.GetCelestialBody(destinationId);
            UnityEngine.Debug.Log(
                $"[OrbitalSandboxCoordinator] Ship arrived: " +
                $"{ship?.DisplayName ?? shipId.ToString()} at {dest?.DisplayName ?? destinationId.ToString()}");
        }

        private void OnShipDocked(EntityId shipId, EntityId stationId)
        {
            RefreshObjectList();
            var ship = _registry.GetCelestialBody(shipId);
            var station = _registry.GetCelestialBody(stationId);
            UnityEngine.Debug.Log(
                $"[Docking] {ship?.DisplayName ?? shipId.ToString()} docked at " +
                $"{station?.DisplayName ?? stationId.ToString()}");
        }

        private void OnShipUndocked(EntityId shipId, EntityId stationId)
        {
            RefreshObjectList();
            var ship = _registry.GetCelestialBody(shipId);
            var station = _registry.GetCelestialBody(stationId);
            UnityEngine.Debug.Log(
                $"[Docking] {ship?.DisplayName ?? shipId.ToString()} undocked from " +
                $"{station?.DisplayName ?? stationId.ToString()}");
        }

        private void LogSOITransition(SOITransition t)
        {
            var ship = _registry.GetCelestialBody(t.ShipId);
            var prevBody = _registry.GetCelestialBody(t.PreviousBodyId);
            var newBody = _registry.GetCelestialBody(t.NewBodyId);

            string shipName = ship?.DisplayName ?? t.ShipId.ToString();
            string prevName = prevBody != null ? prevBody.DisplayName : (t.PreviousBodyId.IsValid ? t.PreviousBodyId.ToString() : "none");
            string newName = newBody != null ? newBody.DisplayName : (t.NewBodyId.IsValid ? t.NewBodyId.ToString() : "none");

            UnityEngine.Debug.Log(
                $"[SOI] {shipName}: {prevName} -> {newName} (t={t.SimTime:F1})");
        }

        private void RemoveFromTransitParent(EntityId shipId)
        {
            if (_currentSystem == null) return;
            foreach (var rootId in _currentSystem.RootBodyIds)
            {
                var root = _registry.GetCelestialBody(rootId);
                if (root != null && root.ChildIds.Contains(shipId))
                {
                    var ship = _registry.GetCelestialBody(shipId);
                    if (ship != null && ship.ParentId != rootId)
                        root.RemoveChildId(shipId);
                }
            }
        }

        private void ParentToRootForTransit(EntityId shipId)
        {
            if (_currentSystem == null || _currentSystem.RootBodyIds.Count == 0) return;
            var rootId = _currentSystem.RootBodyIds[0];
            var root = _registry.GetCelestialBody(rootId);
            var ship = _registry.GetCelestialBody(shipId);
            if (root == null || ship == null) return;
            ship.ParentId = rootId;
            root.AddChildId(shipId);
        }

        private void RefreshObjectList()
        {
            if (_listPanel != null) _listPanel.Refresh();
        }

        private StarSystem LoadStarSystem()
        {
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
        public WorldPositionResolver PositionResolver => _positionResolver;
        public SOIResolver SOI => _soiResolver;
        public DockingSystem Docking => _dockingSystem;
    }
}
