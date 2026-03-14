using System;
using SpaceSim.Shared.Math;
using SpaceSim.World.ValueTypes;

namespace SpaceSim.Simulation.Orbits
{
    /// <summary>
    /// Provides orbital position calculation.
    /// MVP: computes circular orbit in XZ plane.
    /// Also supports surface position calculation for surface-attached objects.
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

        /// <summary>
        /// Calculate the position of a surface-attached object relative to its parent body.
        /// Uses latitude/longitude on a sphere of given radius.
        /// Position is fixed in the parent's local space (no rotation simulation for MVP).
        /// Y axis is up; XZ plane is the equator.
        /// </summary>
        /// <param name="parentRadius">Parent body radius in world units.</param>
        /// <param name="latitudeDeg">Latitude in degrees (-90 to 90).</param>
        /// <param name="longitudeDeg">Longitude in degrees (-180 to 180).</param>
        /// <param name="surfaceOffset">Small height offset above surface to avoid z-fighting.</param>
        /// <returns>Parent-relative position as SimVec3.</returns>
        public static SimVec3 CalculateSurfacePosition(
            double parentRadius, double latitudeDeg, double longitudeDeg, double surfaceOffset = 0.0)
        {
            double r = parentRadius + surfaceOffset;
            double latRad = latitudeDeg * DegToRad;
            double lonRad = longitudeDeg * DegToRad;

            // Spherical to cartesian: Y is up (pole axis), XZ is equator.
            double cosLat = Math.Cos(latRad);
            double x = r * cosLat * Math.Cos(lonRad);
            double y = r * Math.Sin(latRad);
            double z = r * cosLat * Math.Sin(lonRad);

            return new SimVec3(x, y, z);
        }
    }
}
