namespace SpaceSim.World.Entities
{
    /// <summary>
    /// Distinguishes orbital stations from surface stations.
    /// </summary>
    public enum StationKind
    {
        /// <summary>Station orbiting a parent body.</summary>
        Orbital,

        /// <summary>Station placed on the surface of a parent body.</summary>
        Surface
    }

    /// <summary>
    /// Station-specific data attached to a CelestialBody with BodyType.Station.
    /// Stored as a plain data object — no Unity dependency.
    /// </summary>
    public class StationInfo
    {
        /// <summary>Whether this station is orbital or surface-based.</summary>
        public StationKind Kind { get; set; }

        /// <summary>
        /// Surface latitude in degrees. Only meaningful for Surface stations.
        /// Range: -90 to 90.
        /// </summary>
        public double SurfaceLatitudeDeg { get; set; }

        /// <summary>
        /// Surface longitude in degrees. Only meaningful for Surface stations.
        /// Range: -180 to 180.
        /// </summary>
        public double SurfaceLongitudeDeg { get; set; }

        /// <summary>
        /// Docking capability data. Non-null if this station supports docking.
        /// Contains the list of docking ports and their occupancy state.
        /// Only orbital stations support docking in the current implementation.
        /// </summary>
        public DockingInfo Docking { get; set; }

        public StationInfo()
        {
            Kind = StationKind.Orbital;
            SurfaceLatitudeDeg = 0.0;
            SurfaceLongitudeDeg = 0.0;
            Docking = null;
        }

        public StationInfo(StationKind kind, double latDeg = 0.0, double lonDeg = 0.0)
        {
            Kind = kind;
            SurfaceLatitudeDeg = latDeg;
            SurfaceLongitudeDeg = lonDeg;
            Docking = null;
        }

        /// <summary>
        /// Initialize docking capability with a given number of ports.
        /// Only makes sense for orbital stations.
        /// </summary>
        /// <param name="portCount">Number of docking ports.</param>
        /// <param name="portDistance">Distance of ports from station center in Mm.</param>
        public void InitializeDocking(int portCount, double portDistance = 0.15)
        {
            if (portCount <= 0)
            {
                Docking = null;
                return;
            }
            Docking = new DockingInfo();
            Docking.GeneratePorts(portCount, portDistance);
        }

        /// <summary>Whether this station has docking capability.</summary>
        public bool HasDocking => Docking != null && Docking.TotalPorts > 0;

        public override string ToString()
        {
            string dockStr = HasDocking ? $" {Docking}" : "";
            if (Kind == StationKind.Surface)
                return $"StationInfo[{Kind} lat={SurfaceLatitudeDeg:F1} lon={SurfaceLongitudeDeg:F1}{dockStr}]";
            return $"StationInfo[{Kind}{dockStr}]";
        }
    }
}
