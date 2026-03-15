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
    /// Trader ships perform cargo operations when docked.
    /// Runs each tick after ShipMovementSystem.Update() and DockingSystem.Update().
    /// Pure C# — no UnityEngine dependency.
    ///
    /// Docking behavior:
    /// - Orbital station: ship arrives at station orbit → auto-dock after 0.5s delay.
    /// - Surface station: ship arrives at parent body orbit (travel destination = surface station,
    ///   but ShipMovementSystem parents to station's parent body). NPCShipScheduler detects
    ///   the ship was targeting a surface station and requests docking.
    /// - After undock: _pendingDeparture prevents re-docking loop, ship departs immediately.
    ///
    /// Trader behavior (when docked):
    /// 1. Unload all cargo to station.
    /// 2. Load available resources from station.
    /// 3. After DockingWaitTime: undock and travel to another station.
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
                // Ship arrived at the surface station's parent body orbit.
                // Mark it for surface docking on the next scheduler tick.
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
            // Perform cargo operations once when docked (for traders).
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
        /// Traders: unload all cargo, then load available resources.
        /// Other NPC roles: no cargo operations for now.
        /// </summary>
        private void PerformCargoOperations(CelestialBody ship)
        {
            if (_cargoTransfer == null) return;
            if (ship.ShipInfo == null) return;
            if (!ship.ShipInfo.IsDocked) return;

            var stationId = ship.ShipInfo.DockedAtStationId;

            // Only Trader ships do cargo operations for now.
            if (ship.ShipInfo.Role == ShipRole.Trader)
            {
                // Step 1: Unload all cargo to station.
                _cargoTransfer.UnloadAll(ship.Id, stationId);

                // Step 2: Load available resources from station.
                _cargoTransfer.LoadAny(ship.Id, stationId);
            }
        }

        /// <summary>
        /// Ship just undocked — schedule a new route immediately (skip idle delay).
        /// Traders prefer station destinations.
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
        /// Traders prefer stations as destinations for trade loops.
        /// Falls back to any destination if no other station is available.
        /// </summary>
        private EntityId PickTraderDestination(CelestialBody ship)
        {
            // Prefer a different station than where we currently are.
            EntityId excludeId = ship.ParentId;

            // Also exclude the station we're docked at (if any).
            if (ship.ShipInfo.DockedAtStationId.IsValid)
                excludeId = ship.ShipInfo.DockedAtStationId;

            // Try to pick a station first.
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

            // Fallback: any destination.
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
