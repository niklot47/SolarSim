using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SpaceSim.Data.Definitions;
using SpaceSim.Data.Import;
using SpaceSim.Debug;
using SpaceSim.Simulation.Core;
using SpaceSim.Simulation.Docking;
using SpaceSim.Simulation.Economy;
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

        [Tooltip("Assign a JSON TextAsset for external system import. Takes priority over StarSystemDefinition if set.")]
        [SerializeField] private TextAsset externalSystemFile;

        [Header("NPC Scheduling")]
        [Min(0.1f)]
        [SerializeField] private float npcTravelSpeed = 2.0f;
        [Min(0.5f)]
        [SerializeField] private float npcMinTravelDuration = 3.0f;
        [Min(0.0f)]
        [SerializeField] private float npcIdleDelay = 3.0f;

        [Header("Docking")]
        [Min(1.0f)]
        [SerializeField] private float npcDockingWaitTime = 5.0f;
        [Min(0.5f)]
        [SerializeField] private float dockingApproachDuration = 2.0f;

        [Header("Economy")]
        [Tooltip("How often demand/trade opportunities are recalculated (sim-seconds).")]
        [Min(1.0f)]
        [SerializeField] private float demandEvalInterval = 5.0f;

        private WorldRegistry _registry;
        private StarSystem _currentSystem;
        private SelectionService _selectionService;
        private ShipMovementSystem _shipMovement;
        private NPCShipScheduler _npcScheduler;
        private WorldPositionResolver _positionResolver;
        private SOIResolver _soiResolver;
        private DockingSystem _dockingSystem;
        private CargoTransferService _cargoTransfer;
        private StationProductionSystem _productionSystem;
        private StationDemandEvaluator _demandEvaluator;
        private TradeOpportunityResolver _tradeResolver;
        private SimulationClock _clock;
        private ObjectListPanelController _listPanel;

        private double _lastDemandEvalTime;

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

            EconomyInitializer.Initialize(_registry);

            _positionResolver = new WorldPositionResolver(_registry);
            _soiResolver = new SOIResolver(_registry, _positionResolver);

            _shipMovement = new ShipMovementSystem(_registry);
            _shipMovement.OnShipArrived += OnShipArrived;

            _dockingSystem = new DockingSystem(_registry);
            _dockingSystem.ApproachDuration = dockingApproachDuration;
            _dockingSystem.OnShipDocked += OnShipDocked;
            _dockingSystem.OnShipUndocked += OnShipUndocked;

            _cargoTransfer = new CargoTransferService(_registry);
            _cargoTransfer.OnCargoTransferred += OnCargoTransferred;

            _productionSystem = new StationProductionSystem(_registry);
            _productionSystem.OnProductionCycleCompleted += OnProductionCycleCompleted;

            // Demand and trade systems.
            _demandEvaluator = new StationDemandEvaluator(_registry);
            _tradeResolver = new TradeOpportunityResolver(_registry);

            // Initial demand evaluation so traders have data on first tick.
            _demandEvaluator.EvaluateAll();
            _tradeResolver.Resolve(
                (bodyId, time) => _positionResolver.Resolve(bodyId, time),
                _clock.CurrentTime);
            _lastDemandEvalTime = _clock.CurrentTime;

            _npcScheduler = new NPCShipScheduler(
                _registry, _shipMovement, () => _clock.CurrentTime,
                npcTravelSpeed, npcMinTravelDuration, npcIdleDelay);
            _npcScheduler.OnRouteScheduled += OnNpcRouteScheduled;
            _npcScheduler.DockingWaitTime = npcDockingWaitTime;
            _npcScheduler.SetDockingSystem(_dockingSystem);
            _npcScheduler.SetCargoTransfer(_cargoTransfer);
            _npcScheduler.SetTradeResolver(_tradeResolver);
            _npcScheduler.OnTradeRouteSelected += OnTradeRouteSelected;

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

            SetupUIPanels(clock, labelController);

            RegisterDebugProviders();

            _soiResolver.UpdateAllShips(_clock.CurrentTime);

            UnityEngine.Debug.Log(
                $"[OrbitalSandboxCoordinator] Setup complete. System: {_currentSystem}. " +
                $"{_soiResolver.GetStatus()}. " +
                $"NPC: speed={npcTravelSpeed:F1} minDur={npcMinTravelDuration:F1} idle={npcIdleDelay:F1} " +
                $"dockWait={npcDockingWaitTime:F1} approach={dockingApproachDuration:F1} " +
                $"economy=enabled production=enabled demand=enabled trade=enabled " +
                $"demandInterval={demandEvalInterval:F1}s " +
                $"{_tradeResolver.GetStatus()}");
        }

        private void RegisterDebugProviders()
        {
            GameDebug.SetWorldRegistry(_registry);
            GameDebug.RegisterSnapshotProvider(new WorldSnapshotProvider(_registry));
            GameDebug.RegisterSnapshotProvider(new ShipSnapshotProvider(_registry));
            GameDebug.RegisterSnapshotProvider(new EconomySnapshotProvider(_registry));
            GameDebug.RegisterSnapshotProvider(new DockingSnapshotProvider(_registry, _dockingSystem));
            GameDebug.RegisterSnapshotProvider(new SOISnapshotProvider(_registry, _soiResolver));

            GameDebug.Log(DebugCategory.DEBUG,
                $"Debug providers registered: {GameDebug.GetStatus()}",
                source: nameof(OrbitalSandboxCoordinator));
        }

        private void Update()
        {
            if (_clock == null || _shipMovement == null || _positionResolver == null) return;

            if (_npcScheduler != null)
            {
                _npcScheduler.TravelSpeed = npcTravelSpeed;
                _npcScheduler.MinTravelDuration = npcMinTravelDuration;
                _npcScheduler.IdleDelay = npcIdleDelay;
                _npcScheduler.DockingWaitTime = npcDockingWaitTime;
            }
            if (_dockingSystem != null)
                _dockingSystem.ApproachDuration = dockingApproachDuration;

            double simTime = _clock.CurrentTime;

            _shipMovement.Update(simTime, (bodyId, time) => _positionResolver.Resolve(bodyId, time));
            _dockingSystem?.Update(simTime, (bodyId, time) => _positionResolver.Resolve(bodyId, time));
            _npcScheduler?.Update();
            _productionSystem?.Update(simTime);

            // Periodically re-evaluate demand and trade opportunities.
            if (_demandEvaluator != null && simTime - _lastDemandEvalTime >= demandEvalInterval)
            {
                _demandEvaluator.EvaluateAll();
                _tradeResolver.Resolve(
                    (bodyId, time) => _positionResolver.Resolve(bodyId, time),
                    simTime);
                _lastDemandEvalTime = simTime;
            }

            if (_soiResolver != null)
            {
                var transitions = _soiResolver.UpdateAllShips(simTime);
                if (transitions != null)
                {
                    foreach (var t in transitions)
                        LogSOITransition(t);
                }
            }
        }

        private void OnDestroy()
        {
            if (_shipMovement != null) _shipMovement.OnShipArrived -= OnShipArrived;
            if (_npcScheduler != null)
            {
                _npcScheduler.OnRouteScheduled -= OnNpcRouteScheduled;
                _npcScheduler.OnTradeRouteSelected -= OnTradeRouteSelected;
            }
            if (_dockingSystem != null)
            {
                _dockingSystem.OnShipDocked -= OnShipDocked;
                _dockingSystem.OnShipUndocked -= OnShipUndocked;
            }
            if (_cargoTransfer != null)
                _cargoTransfer.OnCargoTransferred -= OnCargoTransferred;
            if (_productionSystem != null)
                _productionSystem.OnProductionCycleCompleted -= OnProductionCycleCompleted;
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
            GameDebug.Log(DebugCategory.SHIPS,
                $"Ship arrived: {ship?.DisplayName ?? shipId.ToString()} at {dest?.DisplayName ?? destinationId.ToString()}",
                source: nameof(OrbitalSandboxCoordinator));
        }

        private void OnShipDocked(EntityId shipId, EntityId stationId)
        {
            RefreshObjectList();
            var ship = _registry.GetCelestialBody(shipId);
            var station = _registry.GetCelestialBody(stationId);
            GameDebug.Log(DebugCategory.SHIPS,
                $"{ship?.DisplayName ?? shipId.ToString()} docked at {station?.DisplayName ?? stationId.ToString()}",
                source: "Docking");

            // Update trade job phase when docked.
            if (ship?.ShipInfo?.CurrentTradeJob != null)
            {
                var job = ship.ShipInfo.CurrentTradeJob;
                if (job.Phase == TraderJobPhase.GoingToSource && stationId == job.SourceStationId)
                {
                    job.Phase = TraderJobPhase.LoadingAtSource;
                }
                else if (job.Phase == TraderJobPhase.GoingToDestination && stationId == job.DestinationStationId)
                {
                    job.Phase = TraderJobPhase.UnloadingAtDestination;
                }
            }
        }

        private void OnShipUndocked(EntityId shipId, EntityId stationId)
        {
            RefreshObjectList();
            var ship = _registry.GetCelestialBody(shipId);
            var station = _registry.GetCelestialBody(stationId);
            GameDebug.Log(DebugCategory.SHIPS,
                $"{ship?.DisplayName ?? shipId.ToString()} undocked from {station?.DisplayName ?? stationId.ToString()}",
                source: "Docking");
        }

        private void OnCargoTransferred(EntityId shipId, EntityId stationId, ResourceType resource, double amount, string direction)
        {
            var ship = _registry.GetCelestialBody(shipId);
            var station = _registry.GetCelestialBody(stationId);
            string verb = direction == "load" ? "loaded" : "unloaded";
            GameDebug.Log(DebugCategory.ECONOMY,
                $"Ship {ship?.DisplayName ?? shipId.ToString()} {verb} {amount:F0} {resource} at {station?.DisplayName ?? stationId.ToString()}",
                source: "CargoTransfer");
        }

        private void OnProductionCycleCompleted(
            EntityId stationId, ResourceType outputResource, double outputAmount,
            Dictionary<ResourceType, double> inputsConsumed)
        {
            var station = _registry.GetCelestialBody(stationId);
            string stationName = station?.DisplayName ?? stationId.ToString();
            string inputStr = "";
            if (inputsConsumed != null && inputsConsumed.Count > 0)
            {
                var parts = new List<string>();
                foreach (var input in inputsConsumed)
                    parts.Add($"{input.Value:F0} {input.Key}");
                inputStr = $" consumed {string.Join(", ", parts)} and";
            }
            GameDebug.Log(DebugCategory.ECONOMY,
                $"{stationName}{inputStr} produced {outputAmount:F0} {outputResource}",
                source: "Production");
        }

        private void OnTradeRouteSelected(EntityId shipId, string message)
        {
            GameDebug.Log(DebugCategory.ECONOMY, message, source: "TradeAI");
        }

        private void LogSOITransition(SOITransition t)
        {
            var ship = _registry.GetCelestialBody(t.ShipId);
            var prevBody = _registry.GetCelestialBody(t.PreviousBodyId);
            var newBody = _registry.GetCelestialBody(t.NewBodyId);
            string shipName = ship?.DisplayName ?? t.ShipId.ToString();
            string prevName = prevBody != null ? prevBody.DisplayName : (t.PreviousBodyId.IsValid ? t.PreviousBodyId.ToString() : "none");
            string newName = newBody != null ? newBody.DisplayName : (t.NewBodyId.IsValid ? t.NewBodyId.ToString() : "none");
            GameDebug.Log(DebugCategory.ORBIT, $"SOI transition: {shipName}: {prevName} -> {newName} (t={t.SimTime:F1})", source: "SOI");
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

        /// <summary>
        /// Load star system with fallback chain:
        /// 1. External JSON file (if externalSystemFile is assigned)
        /// 2. StarSystemDefinition ScriptableObject (if assigned)
        /// 3. Built-in sample system (SampleStarSystemFactory)
        /// </summary>
        private StarSystem LoadStarSystem()
        {
            // Priority 1: External JSON file.
            if (externalSystemFile != null)
            {
                var result = JsonStarSystemImporter.LoadFromTextAsset(externalSystemFile, _registry);

                if (result.Success)
                {
                    UnityEngine.Debug.Log($"[SystemLoader] {result.Message}");
                    GameDebug.Log(DebugCategory.SIM, result.Message, source: "SystemLoader");

                    // Log validation warnings if any.
                    if (result.Validation != null)
                    {
                        foreach (var warning in result.Validation.Warnings)
                        {
                            UnityEngine.Debug.LogWarning($"[SystemLoader] Warning: {warning}");
                            GameDebug.LogWarning(DebugCategory.SIM, $"JSON warning: {warning}", source: "SystemLoader");
                        }
                    }

                    return result.System;
                }

                // JSON import failed — log errors and fall through.
                UnityEngine.Debug.LogWarning($"[SystemLoader] External JSON import failed: {result.Message}");
                GameDebug.LogWarning(DebugCategory.SIM,
                    $"External JSON import failed: {result.Message}", source: "SystemLoader");

                if (result.Validation != null)
                {
                    foreach (var error in result.Validation.Errors)
                    {
                        UnityEngine.Debug.LogError($"[SystemLoader] Validation error: {error}");
                        GameDebug.LogError(DebugCategory.SIM, $"JSON validation: {error}", source: "SystemLoader");
                    }
                }

                UnityEngine.Debug.LogWarning("[SystemLoader] Falling back to ScriptableObject or sample.");
            }

            // Priority 2: ScriptableObject asset.
            if (starSystemDefinition != null)
            {
                var system = StarSystemLoader.Load(starSystemDefinition, _registry);
                if (system != null)
                {
                    string msg = $"Loaded from asset: {starSystemDefinition.DisplayName}";
                    UnityEngine.Debug.Log($"[SystemLoader] {msg}");
                    GameDebug.Log(DebugCategory.SIM, msg, source: "SystemLoader");
                    return system;
                }
                UnityEngine.Debug.LogWarning("[SystemLoader] Asset load failed. Falling back to sample.");
            }

            // Priority 3: Built-in sample.
            UnityEngine.Debug.Log("[SystemLoader] Using built-in sample star system.");
            GameDebug.Log(DebugCategory.SIM, "Using built-in sample star system.", source: "SystemLoader");
            return SampleStarSystemFactory.Create(_registry);
        }

        private void SetupUIPanels(SimulationClock clock, BodyLabelController labelController)
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

            // Create and wire the modal detail controller.
            var modalController = gameObject.AddComponent<DetailModalController>();
            modalController.Initialize(_registry);
            modalController.SetupUI(uiRoot);
            modalController.OnLabelsVisibilityChanged = (visible) => labelController.Visible = visible;
            detailsPanel.SetModalController(modalController);

            // Create input blocker to prevent camera zoom over UI panels and modal.
            var inputBlocker = gameObject.AddComponent<UIInputBlocker>();
            inputBlocker.Initialize(cameraController, uiDocument, modalController);

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
        public CargoTransferService CargoTransfer => _cargoTransfer;
        public StationProductionSystem Production => _productionSystem;
        public StationDemandEvaluator DemandEvaluator => _demandEvaluator;
        public TradeOpportunityResolver TradeResolver => _tradeResolver;
    }
}
