using System;
using System.Collections.Generic;
using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;
using SpaceSim.World.ValueTypes;

namespace SpaceSim.Simulation.Ships
{
    /// <summary>
    /// Simulation system responsible for updating ship movement.
    /// Pure C# — no UnityEngine dependency.
    ///
    /// Call Update() each frame with the current simulation time.
    ///
    /// Logic:
    /// - Orbiting ships: no action (handled by orbital system).
    /// - Travelling ships: interpolate position between origin and destination.
    ///   When travel completes, re-parent the ship to the destination body
    ///   and switch back to Orbiting state.
    /// </summary>
    public class ShipMovementSystem
    {
        private readonly WorldRegistry _registry;

        // Cached list of tracked ship ids to avoid allocation per frame.
        private readonly List<EntityId> _trackedShips = new List<EntityId>();

        /// <summary>
        /// Raised when a ship completes travel and arrives at destination.
        /// Args: ship entity id, destination body id.
        /// </summary>
        public event Action<EntityId, EntityId> OnShipArrived;

        public ShipMovementSystem(WorldRegistry registry)
        {
            _registry = registry;
        }

        /// <summary>
        /// Register a ship to be tracked by this system.
        /// Only tracked ships get movement updates.
        /// </summary>
        public void TrackShip(EntityId shipId)
        {
            if (!_trackedShips.Contains(shipId))
                _trackedShips.Add(shipId);
        }

        /// <summary>
        /// Remove a ship from tracking.
        /// </summary>
        public void UntrackShip(EntityId shipId)
        {
            _trackedShips.Remove(shipId);
        }

        /// <summary>
        /// Number of ships being tracked.
        /// </summary>
        public int TrackedCount => _trackedShips.Count;

        /// <summary>
        /// Begin travel for a ship from its current parent to a destination body.
        /// </summary>
        /// <param name="shipId">Ship entity id.</param>
        /// <param name="destinationId">Target body entity id.</param>
        /// <param name="currentSimTime">Current simulation time (departure time).</param>
        /// <param name="travelDuration">Travel duration in simulation seconds.</param>
        /// <returns>True if the route was started successfully.</returns>
        public bool StartRoute(EntityId shipId, EntityId destinationId, double currentSimTime, double travelDuration)
        {
            var ship = _registry.GetCelestialBody(shipId);
            if (ship == null || ship.ShipInfo == null) return false;
            if (!ship.ParentId.IsValid) return false;

            var origin = _registry.GetCelestialBody(ship.ParentId);
            var destination = _registry.GetCelestialBody(destinationId);
            if (origin == null || destination == null) return false;

            // Create route.
            ship.ShipInfo.CurrentRoute = new ShipRoute(
                origin.Id, destination.Id, currentSimTime, travelDuration);
            ship.ShipInfo.State = ShipState.Travelling;

            // Detach from parent's children list (ship is now "in transit").
            origin.RemoveChildId(shipId);
            ship.AttachmentMode = AttachmentMode.None;

            // Hide orbit line by clearing orbit (renderer checks Orbit != null).
            ship.Orbit = null;

            TrackShip(shipId);
            return true;
        }

        /// <summary>
        /// Update all tracked ships. Called once per simulation tick.
        /// </summary>
        /// <param name="currentSimTime">Current simulation time.</param>
        /// <param name="positionResolver">
        /// Delegate that resolves the absolute world position of a body at given time.
        /// Provided by the rendering/coordination layer.
        /// </param>
        public void Update(double currentSimTime, Func<EntityId, double, SimVec3> positionResolver)
        {
            if (positionResolver == null) return;

            // Iterate backwards to allow safe removal during iteration.
            for (int i = _trackedShips.Count - 1; i >= 0; i--)
            {
                var shipId = _trackedShips[i];
                var ship = _registry.GetCelestialBody(shipId);
                if (ship?.ShipInfo == null)
                {
                    _trackedShips.RemoveAt(i);
                    continue;
                }

                UpdateShip(ship, currentSimTime, positionResolver);
            }
        }

        private void UpdateShip(CelestialBody ship, double currentSimTime,
            Func<EntityId, double, SimVec3> positionResolver)
        {
            var info = ship.ShipInfo;
            if (info.State != ShipState.Travelling || info.CurrentRoute == null)
                return;

            var route = info.CurrentRoute;
            double progress = route.GetProgress(currentSimTime);

            if (progress >= 1.0)
            {
                // Travel complete — arrive at destination.
                ArriveAtDestination(ship, route);
                return;
            }

            // Interpolate world position between origin and destination.
            SimVec3 originPos = positionResolver(route.OriginBodyId, currentSimTime);
            SimVec3 destPos = positionResolver(route.DestinationBodyId, currentSimTime);

            double x = originPos.X + (destPos.X - originPos.X) * progress;
            double y = originPos.Y + (destPos.Y - originPos.Y) * progress;
            double z = originPos.Z + (destPos.Z - originPos.Z) * progress;

            info.OverrideWorldPosition = new SimVec3(x, y, z);
        }

        private void ArriveAtDestination(CelestialBody ship, ShipRoute route)
        {
            var destination = _registry.GetCelestialBody(route.DestinationBodyId);
            if (destination == null)
            {
                // Destination gone — just idle.
                ship.ShipInfo.State = ShipState.Idle;
                ship.ShipInfo.CurrentRoute = null;
                ship.ShipInfo.OverrideWorldPosition = null;
                return;
            }

            // Re-parent to destination.
            // Note: coordinator is responsible for cleaning up transit parent (root star).
            ship.ParentId = destination.Id;
            destination.AddChildId(ship.Id);
            ship.AttachmentMode = AttachmentMode.Orbit;

            // Assign a default orbit around destination.
            ship.Orbit = OrbitDefinition.Circular(
                radius: 3.0,
                period: 12.0,
                startAngleDeg: 0.0);

            // Update state.
            ship.ShipInfo.CurrentRoute = null;
            ship.ShipInfo.OverrideWorldPosition = null;
            ship.ShipInfo.State = ShipState.Orbiting;

            // Notify listeners (UI refresh, etc).
            OnShipArrived?.Invoke(ship.Id, destination.Id);
        }
    }
}
