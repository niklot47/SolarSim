using System;

namespace SpaceSim.World.ValueTypes
{
    /// <summary>
    /// Pure data model for Keplerian orbital elements.
    /// Contains full orbital parameter set for future elliptical/inclined orbits.
    /// Current rendering may use only a subset (semiMajorAxis + period).
    /// </summary>
    [Serializable]
    public class OrbitDefinition
    {
        /// <summary>Semi-major axis in world units (e.g. AU or km-scale).</summary>
        public double SemiMajorAxis;

        /// <summary>Eccentricity. 0 = circle, 0..1 = ellipse.</summary>
        public double Eccentricity;

        /// <summary>Inclination in degrees relative to reference plane.</summary>
        public double InclinationDeg;

        /// <summary>Longitude of ascending node in degrees.</summary>
        public double LongitudeOfAscendingNodeDeg;

        /// <summary>Argument of periapsis in degrees.</summary>
        public double ArgumentOfPeriapsisDeg;

        /// <summary>Mean anomaly at epoch in degrees.</summary>
        public double MeanAnomalyAtEpochDeg;

        /// <summary>Orbital period in simulation seconds.</summary>
        public double OrbitalPeriod;

        /// <summary>Reference epoch time for mean anomaly.</summary>
        public double EpochTime;

        /// <summary>
        /// Whether the orbit is prograde (true) or retrograde (false).
        /// Convenience flag for rendering direction.
        /// </summary>
        public bool IsPrograde;

        public OrbitDefinition()
        {
            SemiMajorAxis = 1.0;
            Eccentricity = 0.0;
            InclinationDeg = 0.0;
            LongitudeOfAscendingNodeDeg = 0.0;
            ArgumentOfPeriapsisDeg = 0.0;
            MeanAnomalyAtEpochDeg = 0.0;
            OrbitalPeriod = 100.0;
            EpochTime = 0.0;
            IsPrograde = true;
        }

        /// <summary>
        /// Create a simple circular orbit in the XZ plane.
        /// </summary>
        public static OrbitDefinition Circular(double radius, double period, double startAngleDeg = 0.0)
        {
            return new OrbitDefinition
            {
                SemiMajorAxis = radius,
                Eccentricity = 0.0,
                InclinationDeg = 0.0,
                LongitudeOfAscendingNodeDeg = 0.0,
                ArgumentOfPeriapsisDeg = 0.0,
                MeanAnomalyAtEpochDeg = startAngleDeg,
                OrbitalPeriod = period,
                EpochTime = 0.0,
                IsPrograde = true
            };
        }

        public override string ToString()
        {
            return $"Orbit[a={SemiMajorAxis:F2} e={Eccentricity:F3} i={InclinationDeg:F1}° P={OrbitalPeriod:F1}s]";
        }
    }
}
