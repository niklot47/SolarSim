using System;
using System.Collections.Generic;
using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;

namespace SpaceSim.World.Entities
{
    /// <summary>
    /// Docking capability data for a station.
    /// Contains a list of docking ports and methods to manage them.
    /// Pure data object — no Unity dependency.
    /// Attached to StationInfo when station has docking capability.
    /// </summary>
    public class DockingInfo
    {
        /// <summary>List of docking ports on this station.</summary>
        public List<DockingPort> Ports { get; } = new List<DockingPort>();

        /// <summary>Total number of ports.</summary>
        public int TotalPorts => Ports.Count;

        /// <summary>Number of currently occupied ports.</summary>
        public int OccupiedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Ports.Count; i++)
                {
                    if (!Ports[i].IsFree) count++;
                }
                return count;
            }
        }

        /// <summary>Number of free ports.</summary>
        public int FreeCount => TotalPorts - OccupiedCount;

        /// <summary>Whether any port is available.</summary>
        public bool HasFreePort => FreeCount > 0;

        /// <summary>
        /// Generate ports with evenly spaced positions around the station.
        /// Positions are small offsets in the XZ plane at station radius + offset.
        /// </summary>
        /// <param name="portCount">Number of ports to generate.</param>
        /// <param name="portDistance">Distance from station center in Mm.</param>
        public void GeneratePorts(int portCount, double portDistance = 0.15)
        {
            Ports.Clear();
            if (portCount <= 0) return;

            double angleStep = 2.0 * Math.PI / portCount;
            for (int i = 0; i < portCount; i++)
            {
                double angle = i * angleStep;
                double x = portDistance * Math.Cos(angle);
                double z = portDistance * Math.Sin(angle);
                Ports.Add(new DockingPort(i, new SimVec3(x, 0.0, z)));
            }
        }

        /// <summary>
        /// Find a free port and reserve it for the given ship.
        /// Returns the port, or null if no free ports available.
        /// </summary>
        public DockingPort RequestPort(EntityId shipId)
        {
            for (int i = 0; i < Ports.Count; i++)
            {
                if (Ports[i].IsFree)
                {
                    Ports[i].Occupy(shipId);
                    return Ports[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Release the port occupied by the given ship.
        /// Returns true if a port was found and released.
        /// </summary>
        public bool ReleasePort(EntityId shipId)
        {
            for (int i = 0; i < Ports.Count; i++)
            {
                if (Ports[i].OccupiedShipId == shipId)
                {
                    Ports[i].Release();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Get the port occupied by a specific ship, or null.
        /// </summary>
        public DockingPort GetPortForShip(EntityId shipId)
        {
            for (int i = 0; i < Ports.Count; i++)
            {
                if (Ports[i].OccupiedShipId == shipId)
                    return Ports[i];
            }
            return null;
        }

        public override string ToString()
        {
            return $"DockingInfo[ports={TotalPorts} occupied={OccupiedCount}]";
        }
    }
}
