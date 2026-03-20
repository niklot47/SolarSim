using System;
using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;

namespace SpaceSim.Simulation.Ships
{
    /// <summary>
    /// Validates a planned ship route for collisions with large celestial bodies.
    ///
    /// Approximation strategy: sample the straight-line route at N evenly spaced points
    /// and also compute the closest-point-on-segment distance for each blocker body.
    /// Both checks together are robust for bodies whose radius is large relative to
    /// the route segment spacing.
    ///
    /// Blocking body types: Star, Planet, Moon, Asteroid.
    /// Not checked: Station, Ship, SurfaceSite.
    ///
    /// Two bodies are always excluded from checks:
    ///   - The ship's origin body (it is departing from there safely).
    ///   - The route's arrival parent body (it is the intended destination).
    ///
    /// Body world positions are resolved at departure time (simTime), which is a
    /// practical approximation for short and medium travel durations.
    ///
    /// Pure C# — no UnityEngine dependency. Lives in Simulation layer.
    /// </summary>
    public static class RouteSafetyChecker
    {
        // ---------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------

        /// <summary>
        /// Check whether the straight-line route from <paramref name="routeStart"/>
        /// to <paramref name="routeEnd"/> passes safely clear of all blocking
        /// celestial bodies in the registry.
        ///
        /// Returns true if the route is safe, false if it intersects a blocking body.
        /// When false, <paramref name="intersectedBodyName"/> is set to the name
        /// of the first detected blocker for logging.
        /// </summary>
        /// <param name="routeStart">Route start in world coordinates.</param>
        /// <param name="routeEnd">Route end (approach point) in world coordinates.</param>
        /// <param name="registry">World registry to enumerate bodies from.</param>
        /// <param name="positionResolver">Delegate to resolve world positions.</param>
        /// <param name="simTime">Simulation time used for position resolution.</param>
        /// <param name="excludeOriginBodyId">Origin body — excluded from checks.</param>
        /// <param name="excludeDestBodyId">Destination arrival body — excluded from checks.</param>
        /// <param name="safetyMargin">Extra clearance added to each body's radius (world units / Mm).</param>
        /// <param name="sampleCount">Number of evenly spaced sample points along the route.</param>
        /// <param name="intersectedBodyName">Set to the name of the first blocking body if unsafe.</param>
        /// <returns>True if the route is safe, false if a collision is detected.</returns>
        public static bool IsSafe(
            SimVec3 routeStart,
            SimVec3 routeEnd,
            WorldRegistry registry,
            Func<EntityId, double, SimVec3> positionResolver,
            double simTime,
            EntityId excludeOriginBodyId,
            EntityId excludeDestBodyId,
            double safetyMargin,
            int sampleCount,
            out string intersectedBodyName)
        {
            intersectedBodyName = null;

            if (registry == null || positionResolver == null)
                return true;

            if (sampleCount < 2)
                sampleCount = 2;

            if (safetyMargin < 0.0)
                safetyMargin = 0.0;

            double stepInv = 1.0 / (sampleCount - 1);

            foreach (var body in registry.AllCelestialBodies)
            {
                // Only large bodies can block routes in this phase.
                if (!IsBlockingType(body.BodyType))
                    continue;

                // Skip origin and destination — ship is legitimately at or going to these.
                if (body.Id == excludeOriginBodyId || body.Id == excludeDestBodyId)
                    continue;

                double threshold = body.Radius + safetyMargin;
                double thresholdSq = threshold * threshold;

                SimVec3 bodyPos = positionResolver(body.Id, simTime);

                // Check 1: closest point on the route segment to the body center.
                // This is the most important check — catches cases where sample points
                // straddle a body (the closest approach falls between two samples).
                double closestDistSq = PointToSegmentDistanceSq(bodyPos, routeStart, routeEnd);
                if (closestDistSq < thresholdSq)
                {
                    intersectedBodyName = body.DisplayName;
                    return false;
                }

                // Check 2: sampled points along the route as an additional guard.
                // Redundant given Check 1 mathematically, but kept as a safety net
                // for floating-point edge cases and very thin segments.
                for (int i = 0; i < sampleCount; i++)
                {
                    double t = i * stepInv;
                    double px = routeStart.X + (routeEnd.X - routeStart.X) * t;
                    double py = routeStart.Y + (routeEnd.Y - routeStart.Y) * t;
                    double pz = routeStart.Z + (routeEnd.Z - routeStart.Z) * t;

                    double dx = px - bodyPos.X;
                    double dy = py - bodyPos.Y;
                    double dz = pz - bodyPos.Z;

                    if (dx * dx + dy * dy + dz * dz < thresholdSq)
                    {
                        intersectedBodyName = body.DisplayName;
                        return false;
                    }
                }
            }

            return true;
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// Returns true if this body type should block ship routes.
        /// Only large natural bodies are checked — not ships, stations, or surface sites.
        /// </summary>
        private static bool IsBlockingType(CelestialBodyType type)
        {
            return type == CelestialBodyType.Star
                || type == CelestialBodyType.Planet
                || type == CelestialBodyType.Moon
                || type == CelestialBodyType.Asteroid;
        }

        /// <summary>
        /// Returns the squared distance from <paramref name="point"/> to the
        /// closest point on segment [<paramref name="segA"/>, <paramref name="segB"/>].
        /// Zero-length segments return the squared distance to segA.
        /// No allocations.
        /// </summary>
        private static double PointToSegmentDistanceSq(SimVec3 point, SimVec3 segA, SimVec3 segB)
        {
            double abx = segB.X - segA.X;
            double aby = segB.Y - segA.Y;
            double abz = segB.Z - segA.Z;
            double abLenSq = abx * abx + aby * aby + abz * abz;

            // Degenerate segment — return distance to segA.
            if (abLenSq < 1e-15)
            {
                double dx = point.X - segA.X;
                double dy = point.Y - segA.Y;
                double dz = point.Z - segA.Z;
                return dx * dx + dy * dy + dz * dz;
            }

            double apx = point.X - segA.X;
            double apy = point.Y - segA.Y;
            double apz = point.Z - segA.Z;

            // t is the projection of (point - segA) onto the segment direction, clamped to [0, 1].
            double t = (apx * abx + apy * aby + apz * abz) / abLenSq;
            if (t < 0.0) t = 0.0;
            if (t > 1.0) t = 1.0;

            // Closest point on segment.
            double cx = segA.X + abx * t;
            double cy = segA.Y + aby * t;
            double cz = segA.Z + abz * t;

            double ex = point.X - cx;
            double ey = point.Y - cy;
            double ez = point.Z - cz;
            return ex * ex + ey * ey + ez * ez;
        }
    }
}
