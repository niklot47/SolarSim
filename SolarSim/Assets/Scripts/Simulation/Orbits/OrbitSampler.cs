using System;
using System.Collections.Generic;
using SpaceSim.Shared.Math;
using SpaceSim.World.ValueTypes;

namespace SpaceSim.Simulation.Orbits
{
    /// <summary>
    /// Generates orbit sample points for rendering using adaptive subdivision.
    /// Ensures the rendered polyline approximates the true orbital curve
    /// within a configurable distance tolerance.
    ///
    /// Algorithm:
    ///   Start with a small number of evenly spaced seed points.
    ///   For each segment [t0, t1]:
    ///     Compute true midpoint pm = position(mid(t0, t1))
    ///     Compute chord midpoint pc = (p0 + p1) / 2
    ///     If distance(pm, pc) > tolerance → subdivide recursively
    ///     Else → accept segment as-is
    ///
    /// Result: circular orbits produce few points (seed count only);
    /// eccentric orbits produce more points near periapsis where curvature is highest.
    ///
    /// All position evaluation delegates to OrbitalPositionCalculator.CalculateOrbitPoint().
    /// Pure C# — no UnityEngine dependency. Lives in Simulation layer.
    /// </summary>
    public static class OrbitSampler
    {
        /// <summary>Default distance tolerance in world units (Mm).</summary>
        public const double DefaultTolerance = 0.05;

        /// <summary>Default maximum recursion depth per seed segment.</summary>
        public const int DefaultMaxDepth = 7;

        /// <summary>Default number of initial seed segments before subdivision.</summary>
        public const int DefaultSeedSegments = 16;

        /// <summary>Minimum allowed seed segments.</summary>
        public const int MinSeedSegments = 4;

        /// <summary>Maximum allowed seed segments.</summary>
        public const int MaxSeedSegments = 128;

        /// <summary>Hard cap on total points to prevent runaway subdivision.</summary>
        public const int MaxTotalPoints = 2048;

        private const double TwoPi = 2.0 * Math.PI;

        /// <summary>
        /// Generate adaptive orbit sample points.
        /// Returns an ordered list of parent-relative positions forming a closed loop.
        ///
        /// Points are ordered by increasing mean anomaly [0, 2*PI).
        /// The caller should set LineRenderer.loop = true so the last point
        /// connects back to the first.
        ///
        /// For circular orbits (e ~= 0), subdivision adds no extra points
        /// beyond the seed set — the chord error is essentially zero.
        /// For eccentric orbits, more points are added near periapsis
        /// where curvature is highest.
        /// </summary>
        /// <param name="orbit">Orbital parameters to sample.</param>
        /// <param name="tolerance">Max allowed distance between true curve midpoint and chord midpoint (Mm).</param>
        /// <param name="maxDepth">Max recursion depth per seed segment.</param>
        /// <param name="seedSegments">Number of initial evenly-spaced segments before adaptive refinement.</param>
        /// <returns>Ordered list of parent-relative positions.</returns>
        public static List<SimVec3> SampleAdaptive(
            OrbitDefinition orbit,
            double tolerance = DefaultTolerance,
            int maxDepth = DefaultMaxDepth,
            int seedSegments = DefaultSeedSegments)
        {
            var result = new List<SimVec3>(128);

            if (orbit == null || orbit.SemiMajorAxis <= 0.0 || orbit.OrbitalPeriod <= 0.0)
                return result;

            // Clamp parameters.
            if (tolerance < 1e-6) tolerance = 1e-6;
            if (maxDepth < 1) maxDepth = 1;
            if (maxDepth > 12) maxDepth = 12;
            if (seedSegments < MinSeedSegments) seedSegments = MinSeedSegments;
            if (seedSegments > MaxSeedSegments) seedSegments = MaxSeedSegments;

            // Generate seed points at evenly spaced mean anomaly.
            double step = TwoPi / seedSegments;
            var seedAngles = new double[seedSegments];
            var seedPositions = new SimVec3[seedSegments];

            for (int i = 0; i < seedSegments; i++)
            {
                seedAngles[i] = i * step;
                seedPositions[i] = OrbitalPositionCalculator.CalculateOrbitPoint(orbit, seedAngles[i]);
            }

            // Subdivide each seed segment adaptively.
            for (int i = 0; i < seedSegments; i++)
            {
                int next = (i + 1) % seedSegments;
                double t0 = seedAngles[i];
                // For the last segment, wrap around to 2*PI (not 0) to avoid zero-length segment.
                double t1 = (i == seedSegments - 1) ? TwoPi : seedAngles[next];
                SimVec3 p0 = seedPositions[i];
                SimVec3 p1 = seedPositions[next];

                // Add the start point of this segment.
                result.Add(p0);

                // Recursively add interior subdivision points.
                SubdivideSegment(orbit, t0, t1, p0, p1, tolerance, maxDepth, 0, result);

                // Safety cap.
                if (result.Count >= MaxTotalPoints)
                    break;
            }

            return result;
        }

        /// <summary>
        /// Fixed uniform sampling (fallback / debug mode).
        /// Returns points at evenly spaced mean anomaly intervals.
        /// Uses the same OrbitalPositionCalculator.CalculateOrbitPoint() as adaptive mode.
        /// </summary>
        /// <param name="orbit">Orbital parameters.</param>
        /// <param name="segmentCount">Number of sample points.</param>
        /// <returns>Ordered list of parent-relative positions.</returns>
        public static List<SimVec3> SampleUniform(OrbitDefinition orbit, int segmentCount = 128)
        {
            var result = new List<SimVec3>(segmentCount);

            if (orbit == null || orbit.SemiMajorAxis <= 0.0 || orbit.OrbitalPeriod <= 0.0)
                return result;

            if (segmentCount < 8) segmentCount = 8;
            if (segmentCount > 1024) segmentCount = 1024;

            double angleStep = TwoPi / segmentCount;
            for (int i = 0; i < segmentCount; i++)
            {
                result.Add(OrbitalPositionCalculator.CalculateOrbitPoint(orbit, i * angleStep));
            }

            return result;
        }

        /// <summary>
        /// Evaluate a single orbit position at a given mean anomaly.
        /// Thin wrapper around OrbitalPositionCalculator.CalculateOrbitPoint().
        /// </summary>
        public static SimVec3 EvaluateAtMeanAnomaly(OrbitDefinition orbit, double meanAnomalyRad)
        {
            if (orbit == null) return SimVec3.Zero;
            return OrbitalPositionCalculator.CalculateOrbitPoint(orbit, meanAnomalyRad);
        }

        // ---------------------------------------------------------------
        // Recursive adaptive subdivision
        // ---------------------------------------------------------------

        /// <summary>
        /// Recursively subdivide segment [t0, t1] if the chord deviates
        /// from the true curve by more than tolerance.
        ///
        /// Does NOT add p0 (already added by caller).
        /// Does NOT add p1 (will be added as p0 of the next seed segment,
        /// or is the wrap-around to the first point via LineRenderer.loop).
        /// Only adds interior subdivision points in correct order.
        /// </summary>
        private static void SubdivideSegment(
            OrbitDefinition orbit,
            double t0, double t1,
            SimVec3 p0, SimVec3 p1,
            double tolerance,
            int maxDepth,
            int currentDepth,
            List<SimVec3> result)
        {
            // Hard cap on total points.
            if (result.Count >= MaxTotalPoints)
                return;

            // Stop recursion at max depth.
            if (currentDepth >= maxDepth)
                return;

            // Compute true midpoint on the orbit curve.
            double tMid = (t0 + t1) * 0.5;
            SimVec3 pMid = OrbitalPositionCalculator.CalculateOrbitPoint(orbit, tMid);

            // Compute chord midpoint (linear interpolation between p0 and p1).
            SimVec3 pChord = new SimVec3(
                (p0.X + p1.X) * 0.5,
                (p0.Y + p1.Y) * 0.5,
                (p0.Z + p1.Z) * 0.5);

            // Measure deviation: distance between true curve point and chord midpoint.
            double error = SimVec3.Distance(pMid, pChord);

            if (error > tolerance)
            {
                // Left half: [t0, tMid] — may insert more points before pMid.
                SubdivideSegment(orbit, t0, tMid, p0, pMid, tolerance, maxDepth, currentDepth + 1, result);

                // Insert the midpoint itself.
                result.Add(pMid);

                // Right half: [tMid, t1] — may insert more points after pMid.
                SubdivideSegment(orbit, tMid, t1, pMid, p1, tolerance, maxDepth, currentDepth + 1, result);
            }
            // else: chord is accurate enough — no interior points needed for this segment.
        }
    }
}
