namespace SpaceSim.Shared.Units
{
    /// <summary>
    /// Defines the world unit policy for the entire project.
    ///
    /// Orbital distances: megameter (Mm) = 1000 km.
    /// Body radii: megameter (Mm).
    /// Time: simulation seconds (sim-s).
    /// Speed: Mm/sim-s.
    /// </summary>
    public static class WorldUnits
    {
        public const string DistanceUnitName = "Mm";
        public const string TimeUnitName = "sim-s";
        public const double KmPerUnit = 1000.0;
        public const double MetersPerUnit = 1_000_000.0;
        public const double EarthOrbitMm = 149_598.0;
        public const double EarthRadiusMm = 6.371;
        public const double SunRadiusMm = 696.0;
    }
}
