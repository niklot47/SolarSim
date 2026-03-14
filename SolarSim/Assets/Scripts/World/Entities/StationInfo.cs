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

        public StationInfo()
        {
            Kind = StationKind.Orbital;
            SurfaceLatitudeDeg = 0.0;
            SurfaceLongitudeDeg = 0.0;
        }

        public StationInfo(StationKind kind, double latDeg = 0.0, double lonDeg = 0.0)
        {
            Kind = kind;
            SurfaceLatitudeDeg = latDeg;
            SurfaceLongitudeDeg = lonDeg;
        }

        public override string ToString()
        {
            if (Kind == StationKind.Surface)
                return $"StationInfo[{Kind} lat={SurfaceLatitudeDeg:F1} lon={SurfaceLongitudeDeg:F1}]";
            return $"StationInfo[{Kind}]";
        }
    }
}
