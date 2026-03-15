using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;

namespace SpaceSim.World.Entities
{
    /// <summary>
    /// Represents a single docking port on a station.
    /// Pure data object — no Unity dependency.
    /// Ports have a local offset position relative to the station center.
    /// </summary>
    public class DockingPort
    {
        /// <summary>Unique port identifier within the station (0-based index).</summary>
        public int PortId { get; set; }

        /// <summary>
        /// Local position offset relative to the station center, in world units (Mm).
        /// Used to position docked ships around the station.
        /// </summary>
        public SimVec3 LocalPosition { get; set; }

        /// <summary>
        /// EntityId of the ship currently occupying this port.
        /// EntityId.None if the port is free.
        /// </summary>
        public EntityId OccupiedShipId { get; set; }

        /// <summary>Whether this port is currently free.</summary>
        public bool IsFree => !OccupiedShipId.IsValid;

        public DockingPort()
        {
            PortId = 0;
            LocalPosition = SimVec3.Zero;
            OccupiedShipId = EntityId.None;
        }

        public DockingPort(int portId, SimVec3 localPosition)
        {
            PortId = portId;
            LocalPosition = localPosition;
            OccupiedShipId = EntityId.None;
        }

        /// <summary>Reserve this port for a ship.</summary>
        public void Occupy(EntityId shipId)
        {
            OccupiedShipId = shipId;
        }

        /// <summary>Release this port.</summary>
        public void Release()
        {
            OccupiedShipId = EntityId.None;
        }

        public override string ToString()
        {
            string status = IsFree ? "free" : $"ship={OccupiedShipId}";
            return $"Port[{PortId} {status} pos={LocalPosition}]";
        }
    }
}
