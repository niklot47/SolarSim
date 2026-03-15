using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;

namespace SpaceSim.World.Entities
{
    /// <summary>
    /// Ship-specific data attached to a CelestialBody with BodyType.Ship.
    /// Stored as a plain data object — no Unity dependency.
    /// Contains role, class, movement state, active route, docking state, and cargo.
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
        /// World position override used during travel and docking approach.
        /// When non-null, renderer uses this instead of orbital calculation.
        /// Set by ShipMovementSystem/DockingSystem; cleared when ship returns to orbit or docks.
        /// </summary>
        public SimVec3? OverrideWorldPosition { get; set; }

        /// <summary>
        /// The celestial body whose SOI currently dominates this ship.
        /// Updated by SOIResolver each simulation tick.
        /// EntityId.None if no SOI is resolved.
        /// </summary>
        public EntityId CurrentSOIBodyId { get; set; }

        // --- Cargo ---

        /// <summary>
        /// Ship cargo hold. Non-null when ship has cargo capability.
        /// Initialized by EconomyInitializer after star system build.
        /// </summary>
        public ShipCargo Cargo { get; set; }

        // --- Docking fields ---

        /// <summary>
        /// EntityId of the station this ship is docked at (or approaching).
        /// EntityId.None if not docked/approaching.
        /// </summary>
        public EntityId DockedAtStationId { get; set; }

        /// <summary>
        /// Port id on the station where this ship is docked.
        /// -1 if not docked.
        /// </summary>
        public int DockedPortId { get; set; }

        /// <summary>
        /// Simulation time when docking approach started.
        /// Used for approach interpolation.
        /// </summary>
        public double DockingStartTime { get; set; }

        /// <summary>
        /// Duration of the docking approach in simulation seconds.
        /// </summary>
        public double DockingDuration { get; set; }

        /// <summary>
        /// LOCAL position at the start of the docking approach, relative to the reference body.
        /// For orbital stations: relative to the station.
        /// For surface stations: relative to the parent planet.
        /// </summary>
        public SimVec3 DockingStartPosition { get; set; }

        /// <summary>
        /// The body used as reference frame for docking approach interpolation.
        /// Orbital station: the station itself.
        /// Surface station: the station's parent body (planet).
        /// </summary>
        public EntityId DockingReferenceBodyId { get; set; }

        /// <summary>
        /// Simulation time when docking completed (ship became Docked).
        /// Used by NPC scheduler to know when to undock.
        /// </summary>
        public double DockedAtTime { get; set; }

        /// <summary>Whether the ship is currently docked at a station.</summary>
        public bool IsDocked => State == ShipState.Docked && DockedAtStationId.IsValid;

        public ShipInfo()
        {
            Role = ShipRole.Civilian;
            ShipKey = "";
            ShipClass = "";
            State = ShipState.Orbiting;
            CurrentRoute = null;
            OverrideWorldPosition = null;
            CurrentSOIBodyId = EntityId.None;
            Cargo = null;
            DockedAtStationId = EntityId.None;
            DockedPortId = -1;
            DockingStartTime = 0.0;
            DockingDuration = 0.0;
            DockingStartPosition = SimVec3.Zero;
            DockingReferenceBodyId = EntityId.None;
            DockedAtTime = 0.0;
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
            Cargo = null;
            DockedAtStationId = EntityId.None;
            DockedPortId = -1;
            DockingStartTime = 0.0;
            DockingDuration = 0.0;
            DockingStartPosition = SimVec3.Zero;
            DockingReferenceBodyId = EntityId.None;
            DockedAtTime = 0.0;
        }

        /// <summary>Clear all docking-related fields.</summary>
        public void ClearDockingState()
        {
            DockedAtStationId = EntityId.None;
            DockedPortId = -1;
            DockingStartTime = 0.0;
            DockingDuration = 0.0;
            DockingStartPosition = SimVec3.Zero;
            DockingReferenceBodyId = EntityId.None;
            DockedAtTime = 0.0;
        }

        public override string ToString()
        {
            string routeStr = CurrentRoute != null ? $" route={CurrentRoute}" : "";
            string soiStr = CurrentSOIBodyId.IsValid ? $" soi={CurrentSOIBodyId}" : "";
            string dockStr = IsDocked ? $" docked={DockedAtStationId}:{DockedPortId}" : "";
            string cargoStr = Cargo != null ? $" {Cargo}" : "";
            return $"ShipInfo[{Role} {State} key={ShipKey} class={ShipClass}{routeStr}{soiStr}{dockStr}{cargoStr}]";
        }
    }
}
