using System;
using SpaceSim.Shared.Math;
using SpaceSim.World.ValueTypes;

namespace SpaceSim.Simulation.Orbits
{
    /// <summary>
    /// Provides orbital position calculation using full Keplerian elements.
    ///
    /// Supports:
    /// - Circular orbits (e == 0, i == 0): behaves identically to previous version.
    /// - Elliptical orbits (0 &lt; e &lt; 1): solved via KeplerSolver.
    /// - Inclined orbits (i != 0): full 3D rotation via Ω, ω, i.
    ///
    /// Also supports surface position calculation for surface-attached objects.
    ///
    /// Performance: no allocations, no LINQ, minimal trig calls.
    /// Pure C# — no UnityEngine dependency. Lives in Simulation layer.
    /// </summary>
    public static class OrbitalPositionCalculator
    {
        private const double TwoPi = 2.0 * Math.PI;
        private const double DegToRad = Math.PI / 180.0;

        /// <summary>
        /// Threshold below which eccentricity is treated as zero (circular orbit).
        /// Avoids unnecessary Kepler solving for nearly-circular orbits.
        /// </summary>
        private const double CircularThreshold = 1e-8;

        /// <summary>
        /// Threshold below which inclination is treated as zero (flat orbit in XZ plane).
        /// Avoids unnecessary 3D rotation for nearly-flat orbits.
        /// </summary>
        private const double FlatOrbitThreshold = 1e-8;

        /// <summary>
        /// Calculate the position of a body relative to its parent
        /// at the given simulation time.
        /// Returns position in parent-relative coordinates.
        ///
        /// For circular orbits (e ~= 0, i ~= 0):
        ///   Produces identical results to the original implementation.
        ///
        /// For elliptical orbits:
        ///   Solves Kepler's equation, computes position in orbital plane,
        ///   then rotates by argument of periapsis, inclination,
        ///   and longitude of ascending node.
        /// </summary>
        /// <param name="orbit">Orbital parameters of the body.</param>
        /// <param name="simTime">Current simulation time in seconds.</param>
        /// <returns>Parent-relative position as SimVec3.</returns>
        public static SimVec3 CalculatePosition(OrbitDefinition orbit, double simTime)
        {
            if (orbit == null || orbit.OrbitalPeriod <= 0.0)
                return SimVec3.Zero;

            double a = orbit.SemiMajorAxis;
            if (a <= 0.0) return SimVec3.Zero;

            // Compute mean anomaly at current time (radians).
            double M = orbit.MeanAnomalyAtEpochDeg * DegToRad
                + TwoPi * (simTime - orbit.EpochTime) / orbit.OrbitalPeriod;

            // Normalize to [0, 2*PI).
            M = M % TwoPi;
            if (M < 0.0) M += TwoPi;

            double e = orbit.Eccentricity;

            // Validate eccentricity.
            if (e < 0.0) e = 0.0;
            if (e >= 1.0) e = 0.999;

            bool isCircular = e < CircularThreshold;
            bool isFlat = Math.Abs(orbit.InclinationDeg) < FlatOrbitThreshold
                       && Math.Abs(orbit.LongitudeOfAscendingNodeDeg) < FlatOrbitThreshold
                       && Math.Abs(orbit.ArgumentOfPeriapsisDeg) < FlatOrbitThreshold;

            // FAST PATH: circular flat orbit — identical to original implementation.
            if (isCircular && isFlat)
            {
                double x = a * Math.Cos(M);
                double z = a * Math.Sin(M);
                return new SimVec3(x, 0.0, z);
            }

            // GENERAL PATH: elliptical and/or inclined orbit.

            // Step 1: Solve Kepler's equation M = E - e*sin(E) for eccentric anomaly E.
            double E = isCircular ? M : KeplerSolver.Solve(M, e);

            // Step 2: Compute position in the orbital plane.
            // r = a * (1 - e * cos(E))
            // x_orbit = a * (cos(E) - e)
            // z_orbit = a * sqrt(1 - e^2) * sin(E)
            double cosE = Math.Cos(E);
            double sinE = Math.Sin(E);

            double xOrbit = a * (cosE - e);
            double zOrbit = a * Math.Sqrt(1.0 - e * e) * sinE;

            // Step 3: If flat orbit (no inclination/node/periapsis rotation needed),
            // just return the orbital plane position directly in XZ.
            if (isFlat)
            {
                return new SimVec3(xOrbit, 0.0, zOrbit);
            }

            // Step 4: Rotate from orbital plane to world coordinates.
            // Standard rotation sequence:
            //   1. Rotate by argument of periapsis (ω) around Y axis in orbital plane
            //   2. Rotate by inclination (i) around X axis to tilt the plane
            //   3. Rotate by longitude of ascending node (Ω) around Y axis
            //
            // Combined rotation matrix applied to (xOrbit, 0, zOrbit):
            //
            // Using convention: Y is up, XZ is reference plane.
            // Orbital plane starts in XZ, then tilted by inclination around X,
            // then rotated around Y by Ω.

            double omega = orbit.ArgumentOfPeriapsisDeg * DegToRad;  // ω
            double inc   = orbit.InclinationDeg * DegToRad;          // i
            double node  = orbit.LongitudeOfAscendingNodeDeg * DegToRad; // Ω

            // Apply argument of periapsis rotation in the orbital plane (around Y).
            double cosW = Math.Cos(omega);
            double sinW = Math.Sin(omega);
            double x1 = xOrbit * cosW - zOrbit * sinW;
            double z1 = xOrbit * sinW + zOrbit * cosW;
            double y1 = 0.0;

            // Apply inclination rotation (tilt orbital plane around X axis).
            double cosI = Math.Cos(inc);
            double sinI = Math.Sin(inc);
            double x2 = x1;
            double y2 = -z1 * sinI;
            double z2 = z1 * cosI;

            // Apply longitude of ascending node rotation (around Y axis).
            double cosN = Math.Cos(node);
            double sinN = Math.Sin(node);
            double x3 = x2 * cosN - z2 * sinN;
            double y3 = y2;
            double z3 = x2 * sinN + z2 * cosN;

            return new SimVec3(x3, y3, z3);
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
        /// Useful for orbit line rendering and phase matching.
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
        /// Calculate a point on the orbit path at a given mean anomaly.
        /// Used for rendering orbit lines (elliptical or circular).
        /// Returns parent-relative position.
        /// </summary>
        /// <param name="orbit">Orbital parameters.</param>
        /// <param name="meanAnomalyRad">Mean anomaly in radians [0, 2*PI).</param>
        /// <returns>Parent-relative position as SimVec3.</returns>
        public static SimVec3 CalculateOrbitPoint(OrbitDefinition orbit, double meanAnomalyRad)
        {
            if (orbit == null) return SimVec3.Zero;

            double a = orbit.SemiMajorAxis;
            if (a <= 0.0) return SimVec3.Zero;

            double e = orbit.Eccentricity;
            if (e < 0.0) e = 0.0;
            if (e >= 1.0) e = 0.999;

            bool isCircular = e < CircularThreshold;
            bool isFlat = Math.Abs(orbit.InclinationDeg) < FlatOrbitThreshold
                       && Math.Abs(orbit.LongitudeOfAscendingNodeDeg) < FlatOrbitThreshold
                       && Math.Abs(orbit.ArgumentOfPeriapsisDeg) < FlatOrbitThreshold;

            double M = meanAnomalyRad;

            // Fast path: circular flat orbit.
            if (isCircular && isFlat)
            {
                return new SimVec3(a * Math.Cos(M), 0.0, a * Math.Sin(M));
            }

            double E = isCircular ? M : KeplerSolver.Solve(M, e);

            double cosE = Math.Cos(E);
            double sinE = Math.Sin(E);

            double xOrbit = a * (cosE - e);
            double zOrbit = a * Math.Sqrt(1.0 - e * e) * sinE;

            if (isFlat)
            {
                return new SimVec3(xOrbit, 0.0, zOrbit);
            }

            double omega = orbit.ArgumentOfPeriapsisDeg * DegToRad;
            double inc   = orbit.InclinationDeg * DegToRad;
            double node  = orbit.LongitudeOfAscendingNodeDeg * DegToRad;

            double cosW = Math.Cos(omega);
            double sinW = Math.Sin(omega);
            double x1 = xOrbit * cosW - zOrbit * sinW;
            double z1 = xOrbit * sinW + zOrbit * cosW;

            double cosI = Math.Cos(inc);
            double sinI = Math.Sin(inc);
            double x2 = x1;
            double y2 = -z1 * sinI;
            double z2 = z1 * cosI;

            double cosN = Math.Cos(node);
            double sinN = Math.Sin(node);
            double x3 = x2 * cosN - z2 * sinN;
            double y3 = y2;
            double z3 = x2 * sinN + z2 * cosN;

            return new SimVec3(x3, y3, z3);
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
