using System;
using SpaceSim.Shared.Math;
using SpaceSim.World.ValueTypes;

namespace SpaceSim.Simulation.Orbits
{
    /// <summary>
    /// Provides orbital position calculation.
    /// MVP: computes circular orbit in XZ plane.
    /// API is designed so future extension to elliptical/inclined orbits
    /// can replace the implementation without changing callers.
    /// </summary>
    public static class OrbitalPositionCalculator
    {
        private const double TwoPi = 2.0 * Math.PI;
        private const double DegToRad = Math.PI / 180.0;

        /// <summary>
        /// Calculate the position of a body relative to its parent
        /// at the given simulation time.
        /// Returns position in parent-relative coordinates.
        /// </summary>
        /// <param name="orbit">Orbital parameters of the body.</param>
        /// <param name="simTime">Current simulation time in seconds.</param>
        /// <returns>Parent-relative position as SimVec3.</returns>
        public static SimVec3 CalculatePosition(OrbitDefinition orbit, double simTime)
        {
            if (orbit == null || orbit.OrbitalPeriod <= 0.0)
                return SimVec3.Zero;

            // Mean anomaly at current time.
            double meanAnomaly = orbit.MeanAnomalyAtEpochDeg * DegToRad
                + TwoPi * (simTime - orbit.EpochTime) / orbit.OrbitalPeriod;

            // Normalize to [0, 2pi).
            meanAnomaly = meanAnomaly % TwoPi;
            if (meanAnomaly < 0) meanAnomaly += TwoPi;

            // MVP: treat as circular orbit in XZ plane.
            // Future: solve Kepler's equation for eccentric anomaly,
            //         apply inclination/node/periapsis rotations.
            double r = orbit.SemiMajorAxis;
            double x = r * Math.Cos(meanAnomaly);
            double z = r * Math.Sin(meanAnomaly);

            return new SimVec3(x, 0.0, z);
        }

        /// <summary>
        /// Calculate the absolute world position of a body by walking up
        /// the parent chain. Requires a parent position resolver delegate.
        /// </summary>
        /// <param name="orbit">Orbital parameters.</param>
        /// <param name="simTime">Current simulation time.</param>
        /// <param name="parentWorldPos">Absolute world position of the parent.</param>
        /// <returns>Absolute world position.</returns>
        public static SimVec3 CalculateAbsolutePosition(
            OrbitDefinition orbit, double simTime, SimVec3 parentWorldPos)
        {
            var relativePos = CalculatePosition(orbit, simTime);
            return parentWorldPos + relativePos;
        }

        /// <summary>
        /// Calculate mean anomaly angle in radians at the given time.
        /// Useful for orbit line rendering.
        /// </summary>
        public static double GetMeanAnomalyRad(OrbitDefinition orbit, double simTime)
        {
            if (orbit == null || orbit.OrbitalPeriod <= 0.0) return 0.0;

            double ma = orbit.MeanAnomalyAtEpochDeg * DegToRad
                + TwoPi * (simTime - orbit.EpochTime) / orbit.OrbitalPeriod;
            ma = ma % TwoPi;
            if (ma < 0) ma += TwoPi;
            return ma;
        }
    }
}
