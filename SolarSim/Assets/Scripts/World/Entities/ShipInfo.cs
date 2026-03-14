using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;

namespace SpaceSim.World.Entities
{
    /// <summary>
    /// Ship-specific data attached to a CelestialBody with BodyType.Ship.
    /// Stored as a plain data object — no Unity dependency.
    /// Contains role, class, movement state, and active route.
    /// </summary>
    public class ShipInfo
    {
        /// <summary>Functional role of the ship.</summary>
        public ShipRole Role { get; set; }

        /// <summary>Stable string key for save/load and data references.</summary>
        public string ShipKey { get; set; }

        /// <summary>Ship class/model name (e.g. "Corvette", "Freighter").</summary>
        public string ShipClass { get; set; }

        /// <summary>Current operational state of the ship.</summary>
        public ShipState State { get; set; }

        /// <summary>Active travel route. Null when not travelling.</summary>
        public ShipRoute CurrentRoute { get; set; }

        /// <summary>
        /// World position override used during travel.
        /// When non-null, renderer uses this instead of orbital calculation.
        /// Set by ShipMovementSystem; cleared when ship returns to orbit.
        /// </summary>
        public SimVec3? OverrideWorldPosition { get; set; }

        /// <summary>
        /// The celestial body whose SOI currently dominates this ship.
        /// Updated by ShipSOITracker each simulation tick.
        /// EntityId.None if no SOI is resolved (e.g. deep interplanetary space).
        /// </summary>
        public EntityId CurrentSOIBodyId { get; set; }

        public ShipInfo()
        {
            Role = ShipRole.Civilian;
            ShipKey = "";
            ShipClass = "";
            State = ShipState.Orbiting;
            CurrentRoute = null;
            OverrideWorldPosition = null;
            CurrentSOIBodyId = EntityId.None;
        }

        public ShipInfo(ShipRole role, string shipKey, string shipClass = "")
        {
            Role = role;
            ShipKey = shipKey;
            ShipClass = shipClass;
            State = ShipState.Orbiting;
            CurrentRoute = null;
            OverrideWorldPosition = null;
            CurrentSOIBodyId = EntityId.None;
        }

        public override string ToString()
        {
            string routeStr = CurrentRoute != null ? $" route={CurrentRoute}" : "";
            string soiStr = CurrentSOIBodyId.IsValid ? $" soi={CurrentSOIBodyId}" : "";
            return $"ShipInfo[{Role} {State} key={ShipKey} class={ShipClass}{routeStr}{soiStr}]";
        }
    }
}
