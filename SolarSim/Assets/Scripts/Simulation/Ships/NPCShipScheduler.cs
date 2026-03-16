using System;
using System.Collections.Generic;
using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;
using SpaceSim.Simulation.Docking;
using SpaceSim.Simulation.Economy;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;

namespace SpaceSim.Simulation.Ships
{
    /// <summary>
    /// Automatically assigns new routes to NPC ships that have finished travelling.
    /// Also handles automatic docking at stations and undocking after wait time.
    /// Trader ships use TradeOpportunityResolver for smart route selection
    /// and perform targeted cargo operations based on their TraderJob.
    /// Runs each tick after ShipMovementSystem.Update() and DockingSystem.Update().
    /// Pure C# — no UnityEngine dependency.
    ///
    /// Trader behavior (demand-driven):
    /// 1. Ask TradeOpportunityResolver for best opportunity.
    /// 2. Travel to source station, load specific resource.
    /// 3. Travel to destination station, unload specific resource.
    /// 4. Repeat. Falls back to random station if no opportunities exist.
    ///
    /// Trade job phase flow:
    ///   GoingToSource → (dock at source) → LoadingAtSource → GoingToDestination →
    ///   (dock at destination) → UnloadingAtDestination → job cleared → new job
    ///
    /// When trader is already docked at source when job is assigned:
    ///   Cargo loaded immediately, phase set to GoingToDestination, ship flies to destination.
    /// </summary>
    public class NPCShipScheduler
    {
        private readonly WorldRegistry _registry;
        private readonly ShipMovementSystem _movementSystem;
        private readonly Func<double> _getSimTime;

        /// <summary>Travel speed in world units per sim-second (Mm/sim-s).</summary>
        public double TravelSpeed { get; set; }

        /// <summary>Minimum travel duration floor (sim-seconds).</summary>
        public double MinTravelDuration { get; set; }

        /// <summary>Delay after arrival before scheduling next route (sim-seconds).</summary>
        public double IdleDelay { get; set; }

        /// <summary>How long NPC ships stay docked at stations (sim-seconds).</summary>
        public double DockingWaitTime { get; set; } = 5.0;

        private readonly Dictionary<EntityId, double> _arrivalTimes = new Dictionary<EntityId, double>();
        private readonly Dictionary<EntityId, EntityId> _lastPatrolOrigin = new Dictionary<EntityId, EntityId>();

        /// <summary>
        /// Ships that just undocked and need to leave immediately.
        /// Prevents the re-docking loop: undock → Orbiting station → re-dock.
        /// </summary>
        private readonly HashSet<EntityId> _pendingDeparture = new HashSet<EntityId>();

        /// <summary>
        /// Tracks which surface station a ship should dock at after arriving at a planet.
        /// Key: shipId, Value: surface station EntityId.
        /// Set when ship's travel destination is a surface station.
        /// </summary>
        private readonly Dictionary<EntityId, EntityId> _pendingSurfaceDock = new Dictionary<EntityId, EntityId>();

        /// <summary>
        /// Tracks ships that have already performed cargo operations at their current docking.
        /// Prevents repeated load/unload every tick while docked.
        /// </summary>
        private readonly HashSet<EntityId> _cargoHandled = new HashSet<EntityId>();

        private readonly Random _rng;
        private readonly List<EntityId> _destinationCandidates = new List<EntityId>();
        private readonly List<EntityId> _stationCandidates = new List<EntityId>();
        private bool _candidatesDirty = true;

        private Func<EntityId, double, SimVec3> _positionResolver;
        private DockingSystem _dockingSystem;
        private CargoTransferService _cargoTransfer;
        private TradeOpportunityResolver _tradeResolver;

        /// <summary>Optional callback for debug logging of trade route selection.</summary>
        public Action<EntityId, string> OnTradeRouteSelected;

        public event Action<EntityId> OnRouteScheduled;

        public NPCShipScheduler(
            WorldRegistry registry,
            ShipMovementSystem movementSystem,
            Func<double> getSimTime,
            double travelSpeed = 2.0,
            double minTravelDuration = 3.0,
            double idleDelay = 3.0,
            int seed = 42)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _movementSystem = movementSystem ?? throw new ArgumentNullException(nameof(movementSystem));
            _getSimTime = getSimTime ?? throw new ArgumentNullException(nameof(getSimTime));
            TravelSpeed = travelSpeed > 0.0 ? travelSpeed : 2.0;
            MinTravelDuration = minTravelDuration > 0.0 ? minTravelDuration : 3.0;
            IdleDelay = idleDelay;
            _rng = new Random(seed);

            // Listen for ship arrivals to detect surface station destinations.
            _movementSystem.OnShipArrived += OnShipArrivedAtDestination;
        }

        public void SetPositionResolver(Func<EntityId, double, SimVec3> resolver)
        {
            _positionResolver = resolver;
        }

        public void SetDockingSystem(DockingSystem dockingSystem)
        {
            _dockingSystem = dockingSystem;
        }

        /// <summary>
        /// Set the cargo transfer service for NPC trader behavior.
        /// </summary>
        public void SetCargoTransfer(CargoTransferService cargoTransfer)
        {
            _cargoTransfer = cargoTransfer;
        }

        /// <summary>
        /// Set the trade opportunity resolver for demand-driven routing.
        /// </summary>
        public void SetTradeResolver(TradeOpportunityResolver tradeResolver)
        {
            _tradeResolver = tradeResolver;
        }

        public void InvalidateDestinationCache()
        {
            _candidatesDirty = true;
        }

        /// <summary>
        /// Called when a ship arrives at its travel destination.
        /// If the destination is a surface station, record it for pending surface dock.
        /// </summary>
        private void OnShipArrivedAtDestination(EntityId shipId, EntityId destinationId)
        {
            var ship = _registry.GetCelestialBody(shipId);
            if (ship?.ShipInfo == null) return;
            if (ship.ShipInfo.Role == ShipRole.Player) return;

            var destination = _registry.GetCelestialBody(destinationId);
            if (destination == null) return;

            // If destination is a surface station with docking, mark for surface dock.
            if (destination.BodyType == CelestialBodyType.Station &&
                destination.StationInfo != null &&
                destination.StationInfo.Kind == StationKind.Surface &&
                destination.StationInfo.HasDocking)
            {
                _pendingSurfaceDock[shipId] = destinationId;
            }
        }

        public void Update()
        {
            if (_candidatesDirty)
                RebuildDestinationCandidates();

            if (_destinationCandidates.Count < 2)
                return;

            double simTime = _getSimTime();

            foreach (var body in _registry.AllCelestialBodies)
            {
                if (body.BodyType != CelestialBodyType.Ship)
                    continue;
                if (body.ShipInfo == null)
                    continue;
                if (body.ShipInfo.Role == ShipRole.Player)
                    continue;

                // Handle docked ships — check if wait time elapsed, then undock.
                if (body.ShipInfo.State == ShipState.Docked && _dockingSystem != null)
                {
                    HandleDockedShip(body, simTime);
                    continue;
                }

                // Skip non-Orbiting ships.
                if (body.ShipInfo.State != ShipState.Orbiting)
                    continue;

                // PRIORITY 1: Ship just undocked — must leave immediately, do NOT re-dock.
                if (_pendingDeparture.Contains(body.Id))
                {
                    ScheduleDepartureFromStation(body, simTime);
                    continue;
                }

                // PRIORITY 2: Ship has a pending surface dock (arrived at planet, needs to dock at surface station).
                if (_dockingSystem != null && _pendingSurfaceDock.TryGetValue(body.Id, out EntityId surfaceStationId))
                {
                    if (!_arrivalTimes.ContainsKey(body.Id))
                        _arrivalTimes[body.Id] = simTime;

                    double arrivedAt = _arrivalTimes[body.Id];
                    if (simTime - arrivedAt >= 0.5)
                    {
                        bool docked = _dockingSystem.RequestDocking(
                            body.Id, surfaceStationId, simTime, _positionResolver);
                        if (docked)
                        {
                            _arrivalTimes.Remove(body.Id);
                            _pendingSurfaceDock.Remove(body.Id);
                        }
                        else
                        {
                            // Port unavailable — give up on surface dock, schedule normal route.
                            _pendingSurfaceDock.Remove(body.Id);
                            ScheduleNewRouteIfReady(body, simTime);
                        }
                    }
                    continue;
                }

                // PRIORITY 3: Ship orbiting a dockable orbital station — auto-dock.
                if (_dockingSystem != null)
                {
                    var parent = _registry.GetCelestialBody(body.ParentId);
                    if (parent != null && parent.BodyType == CelestialBodyType.Station &&
                        parent.StationInfo != null && parent.StationInfo.HasDocking &&
                        parent.StationInfo.Kind == StationKind.Orbital)
                    {
                        HandleOrbitalStationOrbit(body, parent, simTime);
                        continue;
                    }
                }

                // Normal Orbiting behavior at non-station bodies.
                ScheduleNewRouteIfReady(body, simTime);
            }
        }

        /// <summary>
        /// Handle ship orbiting an orbital station — request docking after brief delay.
        /// </summary>
        private void HandleOrbitalStationOrbit(CelestialBody ship, CelestialBody station, double simTime)
        {
            if (!_arrivalTimes.ContainsKey(ship.Id))
                _arrivalTimes[ship.Id] = simTime;

            double arrivedAt = _arrivalTimes[ship.Id];
            if (simTime - arrivedAt < 0.5)
                return;

            bool docked = _dockingSystem.RequestDocking(
                ship.Id, station.Id, simTime, _positionResolver);

            if (docked)
            {
                _arrivalTimes.Remove(ship.Id);
            }
            else
            {
                // No free port — leave the station.
                ScheduleNewRouteIfReady(ship, simTime);
            }
        }

        private void HandleDockedShip(CelestialBody ship, double simTime)
        {
            // Perform cargo operations once when docked.
            if (!_cargoHandled.Contains(ship.Id))
            {
                PerformCargoOperations(ship);
                _cargoHandled.Add(ship.Id);
            }

            double dockedAt = ship.ShipInfo.DockedAtTime;
            if (simTime - dockedAt < DockingWaitTime) return;

            bool undocked = _dockingSystem.Undock(ship.Id, simTime);
            if (undocked)
            {
                // Mark for immediate departure — prevents re-docking loop.
                _pendingDeparture.Add(ship.Id);
                _arrivalTimes[ship.Id] = simTime;
                _cargoHandled.Remove(ship.Id);
            }
        }

        /// <summary>
        /// Perform cargo load/unload operations for NPC ships when docked.
        /// Traders with a TraderJob: perform targeted operations based on job phase.
        /// Traders without a job: only unload cargo (do NOT load random resources).
        /// Other NPC roles: no cargo operations for now.
        /// </summary>
        private void PerformCargoOperations(CelestialBody ship)
        {
            if (_cargoTransfer == null) return;
            if (ship.ShipInfo == null) return;
            if (!ship.ShipInfo.IsDocked) return;

            var stationId = ship.ShipInfo.DockedAtStationId;

            // Only Trader ships do cargo operations.
            if (ship.ShipInfo.Role != ShipRole.Trader) return;

            var job = ship.ShipInfo.CurrentTradeJob;

            if (job != null)
            {
                PerformTradeJobCargoOps(ship, stationId, job);
            }
            else
            {
                // No active job — only unload cargo to free up hold.
                // Do NOT load random resources — wait for a proper trade job.
                _cargoTransfer.UnloadAll(ship.Id, stationId);
            }
        }

        /// <summary>
        /// Perform targeted cargo operations based on the trader's current job phase.
        /// Only LoadingAtSource and UnloadingAtDestination are valid phases for docked cargo ops.
        /// Validates that the ship is at the correct station for the current phase.
        /// Other phases while docked indicate a logic error — just unload and clear job.
        /// </summary>
        private void PerformTradeJobCargoOps(CelestialBody ship, EntityId stationId, TraderJob job)
        {
            if (job.Phase == TraderJobPhase.LoadingAtSource)
            {
                // Verify we are actually at the source station.
                if (stationId != job.SourceStationId)
                {
                    // Wrong station for loading — clear the job, unload, let scheduler pick new job.
                    _cargoTransfer.UnloadAll(ship.Id, stationId);
                    ship.ShipInfo.CurrentTradeJob = null;
                    return;
                }

                // At source station: unload any irrelevant cargo first, then load target resource.
                _cargoTransfer.UnloadAll(ship.Id, stationId);
                _cargoTransfer.LoadFromStation(ship.Id, stationId, job.Resource, ship.ShipInfo.Cargo.FreeSpace);

                // Advance to delivery phase.
                job.Phase = TraderJobPhase.GoingToDestination;
            }
            else if (job.Phase == TraderJobPhase.UnloadingAtDestination)
            {
                // Verify we are actually at the destination station.
                if (stationId != job.DestinationStationId)
                {
                    // Wrong station for unloading — clear the job, unload.
                    _cargoTransfer.UnloadAll(ship.Id, stationId);
                    ship.ShipInfo.CurrentTradeJob = null;
                    return;
                }

                // At destination station: unload the target resource, then unload everything else.
                _cargoTransfer.UnloadToStation(ship.Id, stationId, job.Resource, ship.ShipInfo.Cargo.GetAmount(job.Resource));
                _cargoTransfer.UnloadAll(ship.Id, stationId);

                // Job complete — clear it.
                ship.ShipInfo.CurrentTradeJob = null;
            }
            else
            {
                // GoingToSource or GoingToDestination while docked — unexpected.
                // Just unload and clear the job so we can get a fresh assignment.
                _cargoTransfer.UnloadAll(ship.Id, stationId);
                ship.ShipInfo.CurrentTradeJob = null;
            }
        }

        /// <summary>
        /// Ship just undocked — schedule a new route immediately (skip idle delay).
        /// Traders use trade job to determine next destination.
        /// </summary>
        private void ScheduleDepartureFromStation(CelestialBody ship, double simTime)
        {
            EntityId destination = PickDestination(ship);
            if (!destination.IsValid)
                return;

            EntityId currentParent = ship.ParentId;
            double duration = ComputeTravelDuration(ship, destination, simTime);

            bool started = _movementSystem.StartRoute(
                ship.Id, destination, simTime, duration, _positionResolver);

            if (started)
            {
                _lastPatrolOrigin[ship.Id] = currentParent;
                _arrivalTimes.Remove(ship.Id);
                _pendingDeparture.Remove(ship.Id);
                _pendingSurfaceDock.Remove(ship.Id);
                OnRouteScheduled?.Invoke(ship.Id);
            }
        }

        private void ScheduleNewRouteIfReady(CelestialBody ship, double simTime)
        {
            if (!_arrivalTimes.ContainsKey(ship.Id))
                _arrivalTimes[ship.Id] = simTime;

            double arrivedAt = _arrivalTimes[ship.Id];
            if (simTime - arrivedAt < IdleDelay)
                return;

            EntityId destination = PickDestination(ship);
            if (!destination.IsValid)
                return;

            EntityId currentParent = ship.ParentId;
            double duration = ComputeTravelDuration(ship, destination, simTime);

            bool started = _movementSystem.StartRoute(
                ship.Id, destination, simTime, duration, _positionResolver);

            if (started)
            {
                _lastPatrolOrigin[ship.Id] = currentParent;
                _arrivalTimes.Remove(ship.Id);
                _pendingDeparture.Remove(ship.Id);
                _pendingSurfaceDock.Remove(ship.Id);
                OnRouteScheduled?.Invoke(ship.Id);
            }
        }

        private double ComputeTravelDuration(CelestialBody ship, EntityId destinationId, double simTime)
        {
            double speed = TravelSpeed > 0.0 ? TravelSpeed : 2.0;

            if (_positionResolver == null)
                return MinTravelDuration;

            var destination = _registry.GetCelestialBody(destinationId);
            if (destination == null)
                return MinTravelDuration;

            var origin = _registry.GetCelestialBody(ship.ParentId);
            if (origin == null)
                return MinTravelDuration;

            SimVec3 shipPos = ComputeShipWorldPos(ship, simTime);

            EntityId localFrameBodyId = EntityId.None;
            bool isLocal = false;

            if (origin.ParentId == destinationId)
            {
                localFrameBodyId = destinationId;
                isLocal = true;
            }
            else if (destination.ParentId == origin.Id)
            {
                localFrameBodyId = origin.Id;
                isLocal = true;
            }
            else if (origin.ParentId.IsValid && origin.ParentId == destination.ParentId)
            {
                localFrameBodyId = origin.ParentId;
                isLocal = true;
            }

            double distance;

            if (isLocal && localFrameBodyId.IsValid)
            {
                SimVec3 framePos = _positionResolver(localFrameBodyId, simTime);
                SimVec3 shipLocal = shipPos - framePos;
                SimVec3 destPos = _positionResolver(destinationId, simTime);
                SimVec3 destLocal = destPos - framePos;
                distance = SimVec3.Distance(shipLocal, destLocal);
            }
            else
            {
                SimVec3 destPos = _positionResolver(destinationId, simTime);
                distance = SimVec3.Distance(shipPos, destPos);
            }

            double duration = distance / speed;
            if (duration < MinTravelDuration)
                duration = MinTravelDuration;

            return duration;
        }

        private SimVec3 ComputeShipWorldPos(CelestialBody ship, double simTime)
        {
            if (_positionResolver != null && ship.Orbit != null && ship.ParentId.IsValid)
            {
                SimVec3 parentPos = _positionResolver(ship.ParentId, simTime);
                SimVec3 localPos = Simulation.Orbits.OrbitalPositionCalculator
                    .CalculatePosition(ship.Orbit, simTime);
                return parentPos + localPos;
            }
            if (_positionResolver != null && ship.ParentId.IsValid)
            {
                return _positionResolver(ship.ParentId, simTime);
            }
            return SimVec3.Zero;
        }

        private EntityId PickDestination(CelestialBody ship)
        {
            switch (ship.ShipInfo.Role)
            {
                case ShipRole.Trader:
                    return PickTraderDestination(ship);
                case ShipRole.Patrol:
                    return PickPatrolDestination(ship);
                case ShipRole.Civilian:
                    return PickRandomDestination(ship.ParentId);
                default:
                    return PickRandomDestination(ship.ParentId);
            }
        }

        /// <summary>
        /// Traders use demand-driven routing via TradeOpportunityResolver.
        /// If a trade job is active, follow it. Otherwise, find a new opportunity.
        /// Falls back to random station if no opportunities exist.
        ///
        /// When the trader is already docked at the source station of a new job:
        ///  - Load cargo immediately via CargoTransferService.
        ///  - Set phase to GoingToDestination so the ship flies directly to destination.
        ///  - This prevents the phase mismatch bug where LoadingAtSource phase
        ///    reaches a different station.
        /// </summary>
        private EntityId PickTraderDestination(CelestialBody ship)
        {
            var job = ship.ShipInfo.CurrentTradeJob;

            // If we have an active job, follow its phases.
            if (job != null)
            {
                if (job.Phase == TraderJobPhase.GoingToSource)
                {
                    return job.SourceStationId;
                }
                else if (job.Phase == TraderJobPhase.GoingToDestination)
                {
                    return job.DestinationStationId;
                }
                // Other phases should not reach PickDestination — clear invalid job.
                ship.ShipInfo.CurrentTradeJob = null;
            }

            // No active job — try to find a new trade opportunity.
            if (_tradeResolver != null && _tradeResolver.Opportunities.Count > 0)
            {
                var opportunity = _tradeResolver.FindBestForTrader(ship.Id, ship.ParentId);
                if (opportunity.HasValue)
                {
                    var opp = opportunity.Value;
                    var newJob = new TraderJob(opp.Resource, opp.SourceStationId, opp.DestinationStationId);

                    // Check if trader is currently docked at the source station.
                    // If so, load cargo immediately and skip directly to delivery.
                    bool isDockedAtSource = ship.ShipInfo.IsDocked
                        && ship.ShipInfo.DockedAtStationId == opp.SourceStationId;

                    if (isDockedAtSource && _cargoTransfer != null)
                    {
                        // Load cargo now while still docked at source.
                        _cargoTransfer.UnloadAll(ship.Id, opp.SourceStationId);
                        double freeSpace = ship.ShipInfo.Cargo != null ? ship.ShipInfo.Cargo.FreeSpace : 100.0;
                        _cargoTransfer.LoadFromStation(ship.Id, opp.SourceStationId, opp.Resource, freeSpace);

                        newJob.Phase = TraderJobPhase.GoingToDestination;
                        ship.ShipInfo.CurrentTradeJob = newJob;

                        LogTradeRoute(ship, opp);
                        return newJob.DestinationStationId;
                    }

                    // Not at source — normal flow: travel to source first.
                    newJob.Phase = TraderJobPhase.GoingToSource;
                    ship.ShipInfo.CurrentTradeJob = newJob;

                    LogTradeRoute(ship, opp);
                    return newJob.SourceStationId;
                }
            }

            // Fallback: random station selection (legacy behavior).
            return PickRandomStationDestination(ship);
        }

        /// <summary>
        /// Log trade route selection via callback.
        /// </summary>
        private void LogTradeRoute(CelestialBody ship, TradeOpportunity opp)
        {
            var source = _registry.GetCelestialBody(opp.SourceStationId);
            var dest = _registry.GetCelestialBody(opp.DestinationStationId);
            string sourceName = source?.DisplayName ?? opp.SourceStationId.ToString();
            string destName = dest?.DisplayName ?? opp.DestinationStationId.ToString();
            OnTradeRouteSelected?.Invoke(ship.Id,
                $"{ship.DisplayName} selected trade: {opp.Resource} {sourceName} -> {destName} (score={opp.Score:F1})");
        }

        /// <summary>
        /// Fallback random station destination for traders when no trade opportunities exist.
        /// </summary>
        private EntityId PickRandomStationDestination(CelestialBody ship)
        {
            EntityId excludeId = ship.ParentId;
            if (ship.ShipInfo.DockedAtStationId.IsValid)
                excludeId = ship.ShipInfo.DockedAtStationId;

            if (_stationCandidates.Count > 1)
            {
                int count = 0;
                for (int i = 0; i < _stationCandidates.Count; i++)
                {
                    if (_stationCandidates[i] != excludeId && _stationCandidates[i] != ship.ParentId)
                        count++;
                }
                if (count > 0)
                {
                    int pick = _rng.Next(count);
                    int idx = 0;
                    for (int i = 0; i < _stationCandidates.Count; i++)
                    {
                        if (_stationCandidates[i] == excludeId || _stationCandidates[i] == ship.ParentId)
                            continue;
                        if (idx == pick)
                            return _stationCandidates[i];
                        idx++;
                    }
                }
            }

            return PickRandomDestination(ship.ParentId);
        }

        private EntityId PickRandomDestination(EntityId excludeBodyId)
        {
            int count = 0;
            for (int i = 0; i < _destinationCandidates.Count; i++)
            {
                if (_destinationCandidates[i] != excludeBodyId)
                    count++;
            }
            if (count == 0) return EntityId.None;

            int pick = _rng.Next(count);
            int idx = 0;
            for (int i = 0; i < _destinationCandidates.Count; i++)
            {
                if (_destinationCandidates[i] == excludeBodyId)
                    continue;
                if (idx == pick)
                    return _destinationCandidates[i];
                idx++;
            }
            return EntityId.None;
        }

        private EntityId PickPatrolDestination(CelestialBody ship)
        {
            if (_lastPatrolOrigin.TryGetValue(ship.Id, out EntityId previousOrigin))
            {
                if (_destinationCandidates.Contains(previousOrigin) && previousOrigin != ship.ParentId)
                    return previousOrigin;
            }
            return PickRandomDestination(ship.ParentId);
        }

        private void RebuildDestinationCandidates()
        {
            _destinationCandidates.Clear();
            _stationCandidates.Clear();

            foreach (var body in _registry.AllCelestialBodies)
            {
                if (body.BodyType == CelestialBodyType.Planet ||
                    body.BodyType == CelestialBodyType.Moon ||
                    body.BodyType == CelestialBodyType.Station)
                {
                    _destinationCandidates.Add(body.Id);
                }

                // Separate station list for trader preference.
                if (body.BodyType == CelestialBodyType.Station &&
                    body.StationInfo != null &&
                    body.StationInfo.HasDocking)
                {
                    _stationCandidates.Add(body.Id);
                }
            }
            _candidatesDirty = false;
        }

        public string GetStatus()
        {
            return $"NPCShipScheduler: {_destinationCandidates.Count} destinations, " +
                   $"{_stationCandidates.Count} stations, " +
                   $"{_arrivalTimes.Count} ships waiting, " +
                   $"{_pendingDeparture.Count} pending departure, " +
                   $"{_pendingSurfaceDock.Count} pending surface dock, speed={TravelSpeed:F1} Mm/s";
        }
    }
}
