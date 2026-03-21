using System;
using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;
using SpaceSim.World.Systems;

namespace SpaceSim.Simulation.Ships
{
    /// <summary>
    /// Maneuver Planning Foundation (Phase 23, Iteration 3).
    ///
    /// Iteration 1: abstraction layer — immediate + delayed windows, first-safe selection.
    /// Iteration 2: geometry scoring (best-candidate selection), adaptive window interval.
    /// Iteration 3: fixes the "score always ~1.0" observation. Introduces DirectScore.
    ///
    /// ---------------------------------------------------------------------------
    /// Problem fixed in Iteration 3
    /// ---------------------------------------------------------------------------
    ///
    /// Iteration 2 used Score (= best candidate across 10 angle offsets) as the
    /// geometry quality metric and as the basis for the "wait for better window"
    /// decision. Because 10 candidates cover 360° in 30° steps, the best is always
    /// within ±15° of perfect alignment → cos(15°) ≈ 0.97 → Score was always
    /// 0.94–1.00 for every route, every ship, every time.
    ///
    /// Consequence: DelayPreferenceThreshold (0.5) never fired — bestDelScore
    /// could not exceed bestImmScore by 0.5 when both are capped near 1.0.
    ///
    /// Fix: introduce DirectScore = alignment score of candidate 0 (offset 0°,
    /// penalty 0), computed unconditionally regardless of safety. DirectScore
    /// reflects the natural orbital geometry without any angular manipulation,
    /// and varies freely across [-1.0, +1.0]. The delay decision now uses
    /// DirectScore, while Score is retained for logging the actually-used approach.
    ///
    /// ---------------------------------------------------------------------------
    /// Two score values (Iteration 3)
    /// ---------------------------------------------------------------------------
    ///
    ///   Score (best candidate, unchanged from Iteration 2):
    ///     = dot(approachDir_best, destVelDir) - offsetPenalty
    ///     Always ∈ [0.94, 1.0] in practice (10 evenly-spaced candidates guarantee
    ///     a near-optimal angle). Used in logs to describe the chosen approach.
    ///
    ///   DirectScore (candidate 0 only, NEW in Iteration 3):
    ///     directApproachDir = normalize(destPos + approachRadius*dir(baseAngle) - destPos)
    ///     DirectScore       = dot(directApproachDir, destVelDir)
    ///     Range: [-1.0, +1.0]. No offset penalty (offset = 0°).
    ///     -1.0 = ship is directly behind target (chasing — poor geometry).
    ///     +1.0 = ship is directly ahead of target (intercepting — ideal).
    ///     This varies with orbital phase and is the meaningful diagnostic metric.
    ///     Computed unconditionally even when the direct route is safety-blocked.
    ///
    /// ---------------------------------------------------------------------------
    /// Delay decision (fixed in Iteration 3)
    /// ---------------------------------------------------------------------------
    ///
    ///   OLD (broken, Iteration 2):
    ///     bestDelScore > bestImmScore + 0.5   → never true (both ≈ 1.0)
    ///
    ///   NEW (Iteration 3):
    ///     currentGeometryPoor      = bestImmPlan.DirectScore < -0.2
    ///     delayedSignificantlyBetter = bestDelPlan.DirectScore > bestImmPlan.DirectScore + 0.5
    ///     wait = currentGeometryPoor AND delayedSignificantlyBetter
    ///
    ///   The ship waits only when the current natural geometry is poor (ship is
    ///   chasing the target) AND a future window offers substantially better geometry.
    ///
    /// ---------------------------------------------------------------------------
    /// Unchanged from Iterations 1–2
    /// ---------------------------------------------------------------------------
    ///   - Pure C#, no UnityEngine.
    ///   - No LINQ in hot paths.
    ///   - Zero heap allocations per candidate evaluation.
    ///   - RouteSafetyChecker is the safety authority; ManeuverPlanner only scores.
    ///   - Angle-offset table: [0°, ±30°, ±60°, ±90°, ±120°, +150°].
    ///   - BuildGlobalRoute / BuildLocalRoute signatures untouched.
    ///   - ShipMovementSystem is the sole caller.
    /// </summary>
    public static class ManeuverPlanner
    {
        // ---------------------------------------------------------------
        // Strategy type constants (unchanged)
        // ---------------------------------------------------------------

        /// <summary>Immediate departure, direct approach angle (offset 0°).</summary>
        public const int StrategyDirect = 0;

        /// <summary>Immediate departure, rotated approach angle (non-zero offset).</summary>
        public const int StrategyOffset = 1;

        /// <summary>
        /// Ship must wait; a future window was identified.
        /// Success == false. DepartureTime = estimated future departure (informational).
        /// </summary>
        public const int StrategyDelayed = 2;

        // ---------------------------------------------------------------
        // Result type
        // ---------------------------------------------------------------

        /// <summary>
        /// Output of <see cref="Plan"/>. Contains the chosen approach geometry,
        /// the strategy used, and geometry quality metadata added in Iteration 2.
        /// </summary>
        public struct ManeuverPlan
        {
            /// <summary>
            /// True when a safe, immediately-executable plan was found.
            /// False when the ship should wait (delayed window or total failure).
            /// </summary>
            public bool Success;

            /// <summary>
            /// For successful plans: equals currentSimTime (depart now).
            /// For delayed plans: estimated future departure time (informational —
            /// actual route is not built; scheduler retries and re-plans organically).
            /// </summary>
            public double DepartureTime;

            /// <summary>Chosen approach angle in degrees (world XZ plane).</summary>
            public double ApproachAngleDeg;

            /// <summary>Approach radius in Mm (= destOrbitRadius × OrbitApproachMultiplier).</summary>
            public double ApproachRadius;

            /// <summary>World-space approach point for the chosen candidate.</summary>
            public SimVec3 ApproachWorldPos;

            /// <summary>One of the Strategy* constants defined on ManeuverPlanner.</summary>
            public int StrategyType;

            /// <summary>
            /// Index into the angle-offset table for the chosen candidate.
            /// 0 = direct. -1 = no plan found.
            /// </summary>
            public int VariantIndex;

            /// <summary>
            /// Geometry quality score for the chosen candidate.
            /// Range: approximately [-1.25, +1.0]. double.MinValue = no candidate found.
            /// </summary>
            public double Score;

            /// <summary>
            /// Alignment score of the natural approach direction (candidate 0, offset 0°).
            /// Computed unconditionally — even when that direction is safety-blocked.
            /// Range: [-1.0, +1.0].
            ///   +1.0 = ship directly ahead of target's motion (ideal intercept).
            ///   -1.0 = ship directly behind target (chasing — poor geometry).
            /// This is the meaningful orbital configuration metric; drives the
            /// "wait for better window" decision. double.MinValue = not evaluated.
            /// </summary>
            public double DirectScore;

            /// <summary>
            /// True when StrategyType == StrategyDelayed.
            /// Convenience alias; caller can use either.
            /// </summary>
            public bool IsDelayed;

            /// <summary>
            /// True when IsDelayed AND a safe immediate candidate existed but was bypassed
            /// because a future window is significantly better aligned.
            /// Distinguishes "poor alignment" delay from "all immediate blocked" delay.
            /// </summary>
            public bool ImmediateWasAvailable;

            /// <summary>
            /// Name of the first body blocking the direct candidate (index 0) in window 0.
            /// Null when direct was safe or no safety check ran.
            /// </summary>
            public string DirectRouteBlocker;
        }

        // ---------------------------------------------------------------
        // Angle-offset candidates (unchanged from Iteration 1)
        // ---------------------------------------------------------------

        private static readonly double[] AngleOffsetsDeg =
        {
            0.0,
            30.0, -30.0,
            60.0, -60.0,
            90.0, -90.0,
            120.0, -120.0,
            150.0
        };

        /// <summary>Number of angle-offset candidates evaluated per departure window.</summary>
        public static int CandidatesPerWindow => AngleOffsetsDeg.Length;

        // ---------------------------------------------------------------
        // Tuning constants
        // ---------------------------------------------------------------

        /// <summary>Number of future departure windows probed after window 0.</summary>
        private const int DelayWindowCount = 4;

        /// <summary>Window interval as fraction of destination's orbital period.</summary>
        private const double WindowFractionOfPeriod = 0.08;

        /// <summary>Minimum window spacing in sim-s.</summary>
        private const double MinWindowInterval = 5.0;

        /// <summary>Maximum window spacing in sim-s.</summary>
        private const double MaxWindowInterval = 30.0;

        /// <summary>Fallback period when destination has no orbit data (root star).</summary>
        private const double DefaultOrbitalPeriod = 120.0;

        /// <summary>Velocity estimate dt as fraction of orbital period.</summary>
        private const double VelDtFractionOfPeriod = 0.005;

        /// <summary>Minimum velocity estimate dt in sim-s.</summary>
        private const double MinVelDt = 0.1;

        /// <summary>Maximum velocity estimate dt in sim-s.</summary>
        private const double MaxVelDt = 2.0;

        /// <summary>
        /// Weight of the angle-offset penalty term relative to alignment score.
        /// offset 90° → penalty 0.15; offset 150° → penalty 0.25.
        /// </summary>
        private const double OffsetPenaltyWeight = 0.3;

        /// <summary>
        /// DirectScore threshold below which the current natural geometry is considered poor.
        /// Below -0.2 the ship is more than slightly "chasing" the target; above it geometry
        /// is acceptable and the ship departs immediately even if a future window exists.
        /// </summary>
        private const double DirectScorePoorThreshold = -0.2;

        /// <summary>
        /// A delayed window is preferred only when its DirectScore exceeds the immediate
        /// window's DirectScore by at least this margin.
        /// Only evaluated when immediate DirectScore is already below DirectScorePoorThreshold.
        /// 0.5 means the natural geometry must improve by half the [-1, +1] range.
        /// </summary>
        private const double DirectScoreImprovementThreshold = 0.5;

        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;

        // ---------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------

        /// <summary>
        /// Produce a maneuver plan for a ship travelling from <paramref name="shipWorldPos"/>
        /// to the body at <paramref name="arrivalParentId"/>.
        ///
        /// Evaluates all safe candidates across all windows, scores them, and returns
        /// the best according to the decision rules described in the class summary.
        /// </summary>
        public static ManeuverPlan Plan(
            SimVec3 shipWorldPos,
            EntityId arrivalParentId,
            double approachRadius,
            double currentSimTime,
            double travelDuration,
            WorldRegistry registry,
            Func<EntityId, double, SimVec3> positionResolver,
            EntityId excludeOriginId,
            double safetyMargin,
            int safetyCheckSamples)
        {
            // Adaptive timing parameters from destination orbital period.
            double destPeriod    = GetDestOrbitalPeriod(registry, arrivalParentId);
            double windowInterval = ComputeWindowInterval(destPeriod);
            double velDt         = ComputeVelocityDt(destPeriod);

            // Best immediate and best delayed candidates tracked separately.
            bool       foundImmediate = false;
            double     bestImmScore   = double.MinValue;
            ManeuverPlan bestImmPlan  = default;

            bool       foundDelayed   = false;
            double     bestDelScore   = double.MinValue;
            ManeuverPlan bestDelPlan  = default;

            // Direct-route blocker captured from window 0, candidate 0.
            string directBlocker = null;

            // -----------------------------------------------------------
            // Window 0 — immediate departure
            // -----------------------------------------------------------
            {
                double depTime = currentSimTime;
                double arrTime = currentSimTime + travelDuration;
                string blocker;
                ManeuverPlan win = EvaluateWindow(
                    shipWorldPos, arrivalParentId, approachRadius,
                    depTime, arrTime, velDt,
                    registry, positionResolver,
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

            // -----------------------------------------------------------
            // Windows 1…N — delayed departures
            // -----------------------------------------------------------
            for (int step = 1; step <= DelayWindowCount; step++)
            {
                double depTime = currentSimTime + step * windowInterval;
                double arrTime = depTime + travelDuration;
                string blocker;
                ManeuverPlan win = EvaluateWindow(
                    shipWorldPos, arrivalParentId, approachRadius,
                    depTime, arrTime, velDt,
                    registry, positionResolver,
                    excludeOriginId, safetyMargin, safetyCheckSamples,
                    out blocker);
                if (win.Success && win.Score > bestDelScore)
                {
                    foundDelayed = true;
                    bestDelScore = win.Score;
                    bestDelPlan  = win;
                }
            }

            // -----------------------------------------------------------
            // Decision
            // -----------------------------------------------------------

            if (foundImmediate)
            {
                // Delay decision — based on DirectScore, not Score.
                // Score is always ~1.0 (best of 10 candidates), so it cannot drive
                // a 0.5-margin comparison. DirectScore reflects true orbital geometry.
                bool currentGeometryPoor = bestImmPlan.DirectScore < DirectScorePoorThreshold;
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
                        IsDelayed             = true,
                        ImmediateWasAvailable = true,
                        DirectRouteBlocker    = directBlocker
                    };
                }

                // Current geometry is acceptable (or no better delayed window exists).
                // IsDelayed and ImmediateWasAvailable are already false from EvaluateWindow.
                return bestImmPlan;
            }

            if (foundDelayed)
            {
                // No immediate window at all; must wait for the future window.
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
                    IsDelayed             = true,
                    ImmediateWasAvailable = false,
                    DirectRouteBlocker    = directBlocker
                };
            }

            // All windows failed.
            return new ManeuverPlan
            {
                Success               = false,
                DepartureTime         = currentSimTime,
                ApproachRadius        = approachRadius,
                StrategyType          = -1,
                VariantIndex          = -1,
                Score                 = double.MinValue,
                DirectScore           = double.MinValue,
                IsDelayed             = false,
                ImmediateWasAvailable = false,
                DirectRouteBlocker    = directBlocker
            };
        }

        // ---------------------------------------------------------------
        // Internal: single window evaluation
        // Evaluates all candidates; returns the best-scoring safe one.
        // ---------------------------------------------------------------

        /// <summary>
        /// Evaluate every angle-offset candidate for a given departure + arrival time pair.
        /// Scores each safe candidate and returns the highest-scoring one.
        /// Returns a failed plan when no safe candidate exists.
        ///
        /// No heap allocations. All tracking uses stack-local values.
        /// </summary>
        private static ManeuverPlan EvaluateWindow(
            SimVec3 shipWorldPos,
            EntityId arrivalParentId,
            double approachRadius,
            double departureTime,
            double arrivalTime,
            double velDt,
            WorldRegistry registry,
            Func<EntityId, double, SimVec3> positionResolver,
            EntityId excludeOriginId,
            double safetyMargin,
            int safetyCheckSamples,
            out string directBlockerOut)
        {
            // Destination position at estimated arrival — basis for approach point layout.
            SimVec3 destWorldAtArrival = positionResolver(arrivalParentId, arrivalTime);

            // Destination velocity direction at departure time — alignment scoring axis.
            SimVec3 destVelDir = ComputeVelocityDirection(
                arrivalParentId, departureTime, velDt, positionResolver);

            // Base angle: direction from destination toward the ship (approach from near side).
            SimVec3 dir        = shipWorldPos - destWorldAtArrival;
            double baseAngleDeg = Math.Atan2(dir.Z, dir.X) * RadToDeg;

            directBlockerOut = null;

            // Compute the natural approach alignment (candidate 0, offset 0°) unconditionally
            // before the safety loop. This is always available regardless of safety outcome
            // and drives the "poor geometry → wait" decision in Plan().
            double directApproachAngleRad = baseAngleDeg * DegToRad;
            SimVec3 directApproachPos = new SimVec3(
                destWorldAtArrival.X + approachRadius * Math.Cos(directApproachAngleRad),
                destWorldAtArrival.Y,
                destWorldAtArrival.Z + approachRadius * Math.Sin(directApproachAngleRad));
            double directScore = ScoreCandidate(directApproachPos, destWorldAtArrival, destVelDir, 0);

            // Stack-local tracking — no allocations in the loop.
            bool     foundAny    = false;
            double   bestScore   = double.MinValue;
            double   bestAngleDeg = 0.0;
            SimVec3  bestApproach = SimVec3.Zero;
            int      bestVariant  = -1;
            int      bestStrategy = -1;

            for (int i = 0; i < AngleOffsetsDeg.Length; i++)
            {
                double candidateAngleDeg = baseAngleDeg + AngleOffsetsDeg[i];
                double candidateAngleRad = candidateAngleDeg * DegToRad;

                SimVec3 approachPos = new SimVec3(
                    destWorldAtArrival.X + approachRadius * Math.Cos(candidateAngleRad),
                    destWorldAtArrival.Y,
                    destWorldAtArrival.Z + approachRadius * Math.Sin(candidateAngleRad));

                // Safety check — body positions resolved at departureTime.
                string blockerName;
                bool safe = RouteSafetyChecker.IsSafe(
                    shipWorldPos,
                    approachPos,
                    registry,
                    positionResolver,
                    departureTime,
                    excludeOriginId,
                    arrivalParentId,
                    safetyMargin,
                    safetyCheckSamples,
                    out blockerName);

                if (i == 0 && !safe)
                    directBlockerOut = blockerName;

                if (!safe)
                    continue;

                // Score this candidate and keep it if it's the best so far.
                double score = ScoreCandidate(approachPos, destWorldAtArrival, destVelDir, i);

                if (!foundAny || score > bestScore)
                {
                    foundAny     = true;
                    bestScore    = score;
                    bestAngleDeg = candidateAngleDeg;
                    bestApproach = approachPos;
                    bestVariant  = i;
                    bestStrategy = (i == 0) ? StrategyDirect : StrategyOffset;
                }
            }

            if (!foundAny)
            {
                return new ManeuverPlan
                {
                    Success               = false,
                    DepartureTime         = departureTime,
                    ApproachRadius        = approachRadius,
                    StrategyType          = -1,
                    VariantIndex          = -1,
                    Score                 = double.MinValue,
                    DirectScore           = directScore,
                    IsDelayed             = false,
                    ImmediateWasAvailable = false,
                    DirectRouteBlocker    = directBlockerOut
                };
            }

            return new ManeuverPlan
            {
                Success               = true,
                DepartureTime         = departureTime,
                ApproachAngleDeg      = bestAngleDeg,
                ApproachRadius        = approachRadius,
                ApproachWorldPos      = bestApproach,
                StrategyType          = bestStrategy,
                VariantIndex          = bestVariant,
                Score                 = bestScore,
                DirectScore           = directScore,
                IsDelayed             = false,
                ImmediateWasAvailable = false,
                DirectRouteBlocker    = directBlockerOut
            };
        }

        // ---------------------------------------------------------------
        // Geometry scoring
        // ---------------------------------------------------------------

        /// <summary>
        /// Score one approach candidate using alignment with the target body's velocity.
        ///
        /// alignmentScore = dot(approachDir, destVelocityDir)
        ///   approachDir     = unit vector from destination body TO approach point
        ///   destVelocityDir = unit vector of destination body's orbital velocity
        ///
        ///   +1.0 = approach point is directly ahead in orbital motion (favorable).
        ///   -1.0 = approach point is directly behind (chasing — unfavorable).
        ///
        /// offsetPenalty = |angleOffset| / 180° × OffsetPenaltyWeight
        ///   Mild preference for smaller deviations; does not dominate alignment.
        ///
        /// score = alignmentScore - offsetPenalty
        /// </summary>
        private static double ScoreCandidate(
            SimVec3 approachPos,
            SimVec3 destWorldPos,
            SimVec3 destVelocityDir,
            int candidateIndex)
        {
            SimVec3 rawDir = approachPos - destWorldPos;
            double  mag    = rawDir.Magnitude;
            if (mag < 1e-10)
                return -1.0; // degenerate case: approach point on body centre

            // Normalise without allocating a new SimVec3 via Normalized (same result,
            // but explicit here to show the intent clearly).
            SimVec3 approachDir = new SimVec3(rawDir.X / mag, rawDir.Y / mag, rawDir.Z / mag);

            double alignment    = SimVec3.Dot(approachDir, destVelocityDir);
            double offsetAbsDeg = Math.Abs(AngleOffsetsDeg[candidateIndex]);
            double penalty      = offsetAbsDeg / 180.0 * OffsetPenaltyWeight;

            return alignment - penalty;
        }

        // ---------------------------------------------------------------
        // Velocity direction estimate
        // ---------------------------------------------------------------

        /// <summary>
        /// Estimate the orbital velocity direction of a body at <paramref name="simTime"/>
        /// using a two-sample finite difference:
        ///   velocityDir ≈ normalize(pos(t + dt) - pos(t))
        ///
        /// Cost: two position resolver calls per window — negligible.
        /// No heap allocations.
        /// Falls back to (1, 0, 0) for stationary or root bodies (stars).
        /// </summary>
        private static SimVec3 ComputeVelocityDirection(
            EntityId bodyId,
            double simTime,
            double dt,
            Func<EntityId, double, SimVec3> positionResolver)
        {
            SimVec3 p0  = positionResolver(bodyId, simTime);
            SimVec3 p1  = positionResolver(bodyId, simTime + dt);
            SimVec3 vel = p1 - p0;
            double  mag = vel.Magnitude;
            if (mag < 1e-10)
                return new SimVec3(1.0, 0.0, 0.0); // stationary or near-stationary body
            return new SimVec3(vel.X / mag, vel.Y / mag, vel.Z / mag);
        }

        // ---------------------------------------------------------------
        // Adaptive interval helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// Resolve the destination body's orbital period from the registry.
        /// Returns <see cref="DefaultOrbitalPeriod"/> when no orbit data is present
        /// (root stars, or bodies added without an OrbitDefinition).
        /// </summary>
        private static double GetDestOrbitalPeriod(WorldRegistry registry, EntityId arrivalParentId)
        {
            var body = registry.GetCelestialBody(arrivalParentId);
            if (body?.Orbit != null && body.Orbit.OrbitalPeriod > 0.0)
                return body.Orbit.OrbitalPeriod;
            return DefaultOrbitalPeriod;
        }

        /// <summary>
        /// Compute window spacing: 8% of orbital period, clamped to [5, 30] sim-s.
        /// </summary>
        private static double ComputeWindowInterval(double orbitalPeriod)
        {
            double raw = orbitalPeriod * WindowFractionOfPeriod;
            return Math.Max(MinWindowInterval, Math.Min(MaxWindowInterval, raw));
        }

        /// <summary>
        /// Compute velocity-estimate dt: 0.5% of orbital period, clamped to [0.1, 2.0] sim-s.
        /// </summary>
        private static double ComputeVelocityDt(double orbitalPeriod)
        {
            double raw = orbitalPeriod * VelDtFractionOfPeriod;
            return Math.Max(MinVelDt, Math.Min(MaxVelDt, raw));
        }
    }
}
