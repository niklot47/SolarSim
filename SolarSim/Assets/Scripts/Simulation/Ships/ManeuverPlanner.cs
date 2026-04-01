using System;
using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;
using SpaceSim.World.Systems;

namespace SpaceSim.Simulation.Ships
{
    /// <summary>
    /// Maneuver Planning Foundation (Phase 23, Iteration 3).
    ///
    /// Time unit: sim-s = real seconds.
    /// Distances: compressed Mm (Terra orbit = 150 Mm).
    /// Default TimeScale = 86400 (1 real second = 1 game day).
    ///
    /// Key periods at compressed scale:
    ///   Luna  ~= 2,360,621 sim-s  (27.3 days)
    ///   Terra ~= 31,557,600 sim-s (365.3 days)
    ///   Ares  ~= 78,336,853 sim-s (906.7 days)
    /// </summary>
    public static class ManeuverPlanner
    {
        public const int StrategyDirect  = 0;
        public const int StrategyOffset  = 1;
        public const int StrategyDelayed = 2;

        public struct ManeuverPlan
        {
            public bool      Success;
            public double    DepartureTime;
            public double    ApproachAngleDeg;
            public double    ApproachRadius;
            public SimVec3   ApproachWorldPos;
            public int       StrategyType;
            public int       VariantIndex;
            public double    Score;
            public double    DirectScore;
            public double    ImmediateDirectScore;
            public bool      IsDelayed;
            public bool      ImmediateWasAvailable;
            public string    DirectRouteBlocker;
        }

        private static readonly double[] AngleOffsetsDeg =
        {
            0.0,
            30.0, -30.0,
            60.0, -60.0,
            90.0, -90.0,
            120.0, -120.0,
            150.0
        };

        public static int CandidatesPerWindow => AngleOffsetsDeg.Length;

        // ---------------------------------------------------------------
        // Timing constants — real seconds, compressed distances
        // ---------------------------------------------------------------

        private const int    DelayWindowCount         = 4;
        private const double WindowFractionOfPeriod   = 0.08;

        /// <summary>Minimum window interval: ~13 hours.</summary>
        private const double MinWindowInterval  = 47212.0;

        /// <summary>Maximum window interval: ~37 days.</summary>
        private const double MaxWindowInterval  = 3155760.0;

        /// <summary>Fallback orbital period: 1 Earth year in sim-s.</summary>
        private const double DefaultOrbitalPeriod = 31557600.0;

        private const double VelDtFractionOfPeriod = 0.005;

        /// <summary>Minimum velocity dt: 3 hours.</summary>
        private const double MinVelDt = 11803.0;

        /// <summary>Maximum velocity dt: ~1.8 days.</summary>
        private const double MaxVelDt = 157788.0;

        private const double OffsetPenaltyWeight             = 0.3;
        private const double DirectScorePoorThreshold        = -0.2;
        private const double DirectScoreImprovementThreshold = 0.5;

        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;

        // ---------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------

        public static ManeuverPlan Plan(
            SimVec3  shipWorldPos,
            EntityId arrivalParentId,
            double   approachRadius,
            double   currentSimTime,
            double   travelDuration,
            WorldRegistry registry,
            Func<EntityId, double, SimVec3> positionResolver,
            EntityId excludeOriginId,
            double   safetyMargin,
            int      safetyCheckSamples)
        {
            double destPeriod    = GetDestOrbitalPeriod(registry, arrivalParentId);
            double windowInterval = ComputeWindowInterval(destPeriod);
            double velDt         = ComputeVelocityDt(destPeriod);

            bool         foundImmediate = false;
            double       bestImmScore   = double.MinValue;
            ManeuverPlan bestImmPlan    = default;

            bool         foundDelayed   = false;
            double       bestDelScore   = double.MinValue;
            ManeuverPlan bestDelPlan    = default;

            string directBlocker = null;

            // Window 0 — immediate departure.
            {
                double depTime = currentSimTime;
                double arrTime = currentSimTime + travelDuration;
                string blocker;
                ManeuverPlan win = EvaluateWindow(
                    shipWorldPos, arrivalParentId, approachRadius,
                    depTime, arrTime, velDt, registry, positionResolver,
                    excludeOriginId, safetyMargin, safetyCheckSamples,
                    out blocker);
                directBlocker = blocker;
                if (win.Success && win.Score > bestImmScore)
                {
                    foundImmediate = true;
                    bestImmScore   = win.Score;
                    bestImmPlan    = win;
                }
            }

            // Windows 1..N — delayed departures.
            for (int step = 1; step <= DelayWindowCount; step++)
            {
                double depTime = currentSimTime + step * windowInterval;
                double arrTime = depTime + travelDuration;
                string blocker;
                ManeuverPlan win = EvaluateWindow(
                    shipWorldPos, arrivalParentId, approachRadius,
                    depTime, arrTime, velDt, registry, positionResolver,
                    excludeOriginId, safetyMargin, safetyCheckSamples,
                    out blocker);
                if (win.Success && win.Score > bestDelScore)
                {
                    foundDelayed = true;
                    bestDelScore = win.Score;
                    bestDelPlan  = win;
                }
            }

            if (foundImmediate)
            {
                bool currentGeometryPoor       = bestImmPlan.DirectScore < DirectScorePoorThreshold;
                bool delayedSignificantlyBetter = foundDelayed
                    && bestDelPlan.DirectScore > bestImmPlan.DirectScore + DirectScoreImprovementThreshold;

                if (currentGeometryPoor && delayedSignificantlyBetter)
                {
                    return new ManeuverPlan
                    {
                        Success               = false,
                        DepartureTime         = bestDelPlan.DepartureTime,
                        ApproachAngleDeg      = bestDelPlan.ApproachAngleDeg,
                        ApproachRadius        = approachRadius,
                        ApproachWorldPos      = bestDelPlan.ApproachWorldPos,
                        StrategyType          = StrategyDelayed,
                        VariantIndex          = bestDelPlan.VariantIndex,
                        Score                 = bestDelPlan.Score,
                        DirectScore           = bestDelPlan.DirectScore,
                        ImmediateDirectScore  = bestImmPlan.DirectScore,
                        IsDelayed             = true,
                        ImmediateWasAvailable = true,
                        DirectRouteBlocker    = directBlocker
                    };
                }

                return bestImmPlan;
            }

            if (foundDelayed)
            {
                return new ManeuverPlan
                {
                    Success               = false,
                    DepartureTime         = bestDelPlan.DepartureTime,
                    ApproachAngleDeg      = bestDelPlan.ApproachAngleDeg,
                    ApproachRadius        = approachRadius,
                    ApproachWorldPos      = bestDelPlan.ApproachWorldPos,
                    StrategyType          = StrategyDelayed,
                    VariantIndex          = bestDelPlan.VariantIndex,
                    Score                 = bestDelPlan.Score,
                    DirectScore           = bestDelPlan.DirectScore,
                    ImmediateDirectScore  = double.MinValue,
                    IsDelayed             = true,
                    ImmediateWasAvailable = false,
                    DirectRouteBlocker    = directBlocker
                };
            }

            return new ManeuverPlan
            {
                Success               = false,
                DepartureTime         = currentSimTime,
                ApproachRadius        = approachRadius,
                StrategyType          = -1,
                VariantIndex          = -1,
                Score                 = double.MinValue,
                DirectScore           = double.MinValue,
                ImmediateDirectScore  = double.MinValue,
                IsDelayed             = false,
                ImmediateWasAvailable = false,
                DirectRouteBlocker    = directBlocker
            };
        }

        // ---------------------------------------------------------------
        // Internal: single window evaluation
        // ---------------------------------------------------------------

        private static ManeuverPlan EvaluateWindow(
            SimVec3  shipWorldPos,
            EntityId arrivalParentId,
            double   approachRadius,
            double   departureTime,
            double   arrivalTime,
            double   velDt,
            WorldRegistry registry,
            Func<EntityId, double, SimVec3> positionResolver,
            EntityId excludeOriginId,
            double   safetyMargin,
            int      safetyCheckSamples,
            out string directBlockerOut)
        {
            SimVec3 destWorldAtArrival = positionResolver(arrivalParentId, arrivalTime);
            SimVec3 destVelDir = ComputeVelocityDirection(
                arrivalParentId, departureTime, velDt, positionResolver);

            SimVec3 dir        = shipWorldPos - destWorldAtArrival;
            double  baseAngleDeg = Math.Atan2(dir.Z, dir.X) * RadToDeg;

            directBlockerOut = null;

            // Compute direct score unconditionally.
            double directAngleRad = baseAngleDeg * DegToRad;
            SimVec3 directApproach = new SimVec3(
                destWorldAtArrival.X + approachRadius * Math.Cos(directAngleRad),
                destWorldAtArrival.Y,
                destWorldAtArrival.Z + approachRadius * Math.Sin(directAngleRad));
            double directScore = ScoreCandidate(directApproach, destWorldAtArrival, destVelDir, 0);

            bool    foundAny     = false;
            double  bestScore    = double.MinValue;
            double  bestAngleDeg = 0.0;
            SimVec3 bestApproach = SimVec3.Zero;
            int     bestVariant  = -1;
            int     bestStrategy = -1;

            for (int i = 0; i < AngleOffsetsDeg.Length; i++)
            {
                double candAngleDeg = baseAngleDeg + AngleOffsetsDeg[i];
                double candAngleRad = candAngleDeg * DegToRad;

                SimVec3 approachPos = new SimVec3(
                    destWorldAtArrival.X + approachRadius * Math.Cos(candAngleRad),
                    destWorldAtArrival.Y,
                    destWorldAtArrival.Z + approachRadius * Math.Sin(candAngleRad));

                string blockerName;
                bool safe = RouteSafetyChecker.IsSafe(
                    shipWorldPos, approachPos, registry, positionResolver,
                    departureTime, excludeOriginId, arrivalParentId,
                    safetyMargin, safetyCheckSamples, out blockerName);

                if (i == 0 && !safe) directBlockerOut = blockerName;
                if (!safe) continue;

                double score = ScoreCandidate(approachPos, destWorldAtArrival, destVelDir, i);
                if (!foundAny || score > bestScore)
                {
                    foundAny     = true;
                    bestScore    = score;
                    bestAngleDeg = candAngleDeg;
                    bestApproach = approachPos;
                    bestVariant  = i;
                    bestStrategy = (i == 0) ? StrategyDirect : StrategyOffset;
                }
            }

            if (!foundAny)
            {
                return new ManeuverPlan
                {
                    Success       = false, DepartureTime = departureTime,
                    ApproachRadius = approachRadius, StrategyType = -1, VariantIndex = -1,
                    Score = double.MinValue, DirectScore = directScore,
                    ImmediateDirectScore = double.MinValue
                };
            }

            return new ManeuverPlan
            {
                Success           = true,
                DepartureTime     = departureTime,
                ApproachAngleDeg  = bestAngleDeg,
                ApproachRadius    = approachRadius,
                ApproachWorldPos  = bestApproach,
                StrategyType      = bestStrategy,
                VariantIndex      = bestVariant,
                Score             = bestScore,
                DirectScore       = directScore,
                ImmediateDirectScore = double.MinValue,
                DirectRouteBlocker   = directBlockerOut
            };
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static double ScoreCandidate(
            SimVec3 approachPos, SimVec3 destWorldPos, SimVec3 destVelDir, int candidateIndex)
        {
            SimVec3 raw = approachPos - destWorldPos;
            double  mag = raw.Magnitude;
            if (mag < 1e-10) return -1.0;
            SimVec3 dir     = new SimVec3(raw.X / mag, raw.Y / mag, raw.Z / mag);
            double  align   = SimVec3.Dot(dir, destVelDir);
            double  penalty = Math.Abs(AngleOffsetsDeg[candidateIndex]) / 180.0 * OffsetPenaltyWeight;
            return align - penalty;
        }

        private static SimVec3 ComputeVelocityDirection(
            EntityId bodyId, double simTime, double dt,
            Func<EntityId, double, SimVec3> positionResolver)
        {
            SimVec3 p0  = positionResolver(bodyId, simTime);
            SimVec3 p1  = positionResolver(bodyId, simTime + dt);
            SimVec3 vel = p1 - p0;
            double  mag = vel.Magnitude;
            if (mag < 1e-10) return new SimVec3(1.0, 0.0, 0.0);
            return new SimVec3(vel.X / mag, vel.Y / mag, vel.Z / mag);
        }

        private static double GetDestOrbitalPeriod(WorldRegistry registry, EntityId arrivalParentId)
        {
            var body = registry.GetCelestialBody(arrivalParentId);
            if (body?.Orbit != null && body.Orbit.OrbitalPeriod > 0.0)
                return body.Orbit.OrbitalPeriod;
            return DefaultOrbitalPeriod;
        }

        private static double ComputeWindowInterval(double orbitalPeriod)
            => Math.Max(MinWindowInterval, Math.Min(MaxWindowInterval, orbitalPeriod * WindowFractionOfPeriod));

        private static double ComputeVelocityDt(double orbitalPeriod)
            => Math.Max(MinVelDt, Math.Min(MaxVelDt, orbitalPeriod * VelDtFractionOfPeriod));

        public static double GetWindowInterval(WorldRegistry registry, EntityId arrivalParentId)
            => ComputeWindowInterval(GetDestOrbitalPeriod(registry, arrivalParentId));
    }
}
