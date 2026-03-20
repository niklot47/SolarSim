using System;
using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;
using SpaceSim.World.Systems;

namespace SpaceSim.Simulation.Ships
{
    /// <summary>
    /// Lightweight transfer planner that tries several simple approach geometry variants
    /// for the same destination body before giving up on a route.
    ///
    /// This is NOT a full pathfinder and NOT a Hohmann transfer planner.
    /// It only varies the approach point geometry (angle and, optionally, radius)
    /// for the final straight-line segment from the ship to the destination's approach
    /// distance. Destination body is always unchanged.
    ///
    /// Candidate generation strategy:
    ///   Compute the "direct" approach angle — the direction from the destination to the
    ///   ship's current position. Then try rotating that angle by a set of fixed offsets
    ///   (±30°, ±60°, ±90°, ±120°, ±150°). This creates approach points distributed
    ///   around the destination, systematically covering the most likely safe corridors.
    ///
    /// Candidates are tried in order (direct first, then alternatives). The first safe
    /// candidate — as determined by RouteSafetyChecker — is returned. If all candidates
    /// fail the safety check, the plan reports failure and the caller rejects the route.
    ///
    /// All geometry is computed in world space. The chosen approach world position and
    /// angle are returned to the caller (ShipMovementSystem), which converts to local
    /// frame coordinates if needed.
    ///
    /// Performance: O(candidates × registry_size). No allocations in hot path. No LINQ.
    /// Only called when a new route is being planned — never every frame.
    ///
    /// Pure C# — no UnityEngine dependency. Lives in Simulation layer.
    /// </summary>
    public static class TransferPlannerLite
    {
        // ---------------------------------------------------------------
        // Candidate configuration
        // ---------------------------------------------------------------

        /// <summary>
        /// Approach angle offsets tried in order (degrees).
        /// Index 0 is the direct approach (zero offset).
        /// The remaining entries are alternatives, tried left-right symmetrically
        /// in increasing angular steps so the nearest alternatives are preferred.
        /// </summary>
        private static readonly double[] AngleOffsetsDeg =
        {
            0.0,          // direct (same side as ship's current position)
            30.0, -30.0,  // close alternatives — least deviation from direct
            60.0, -60.0,
            90.0, -90.0,  // perpendicular approaches
            120.0, -120.0,
            150.0         // nearly opposite — last resort before the very opposite side
        };

        /// <summary>Total number of angle-offset candidates tried.</summary>
        public static int CandidateCount => AngleOffsetsDeg.Length;

        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;

        // ---------------------------------------------------------------
        // Result type
        // ---------------------------------------------------------------

        /// <summary>
        /// Result returned by <see cref="FindSafeApproach"/>.
        /// Contains the chosen approach geometry if planning succeeded.
        /// </summary>
        public struct ApproachPlan
        {
            /// <summary>True if a safe approach was found among the candidates.</summary>
            public bool Success;

            /// <summary>
            /// World-space position of the chosen approach point (end of Phase 1 travel).
            /// Valid only when Success is true.
            /// </summary>
            public SimVec3 ApproachWorldPos;

            /// <summary>
            /// Approach angle in degrees used to generate the approach point.
            /// Measured in the world XZ plane relative to the destination body.
            /// Used to set ArrivalOrbitPhaseDeg on the ShipRoute.
            /// Valid only when Success is true.
            /// </summary>
            public double ApproachAngleDeg;

            /// <summary>
            /// Approach radius used (world units / Mm).
            /// Equal to the base approach radius for all current candidates.
            /// Valid only when Success is true.
            /// </summary>
            public double ApproachRadius;

            /// <summary>
            /// Index of the chosen candidate in AngleOffsetsDeg.
            /// 0 = direct approach was safe.
            /// >0 = an alternative was selected.
            /// -1 = no safe candidate found (Success == false).
            /// </summary>
            public int VariantIndex;

            /// <summary>
            /// Name of the first blocking body encountered on the direct route.
            /// Non-null when the direct route (index 0) failed.
            /// Useful for logging.
            /// </summary>
            public string DirectRouteBlocker;
        }

        // ---------------------------------------------------------------
        // Public API: FindSafeApproach
        // ---------------------------------------------------------------

        /// <summary>
        /// Try to find a safe approach geometry for a route from
        /// <paramref name="shipStartWorldPos"/> to the body at <paramref name="arrivalParentId"/>.
        ///
        /// Tries up to <see cref="CandidateCount"/> candidates in order.
        /// Returns immediately when the first safe candidate is found.
        ///
        /// Body world positions for the safety check are resolved at <paramref name="currentSimTime"/>
        /// (consistent with Phase 21 RouteSafetyChecker behavior).
        ///
        /// The destination body position used to compute the approach point is resolved at
        /// <paramref name="estimatedArrivalTime"/> so the approach point tracks where the
        /// body will be when the ship arrives.
        /// </summary>
        /// <param name="shipStartWorldPos">Ship world position at departure.</param>
        /// <param name="arrivalParentId">Id of the body the ship is heading to.</param>
        /// <param name="approachRadius">Distance from the body center for the approach point (Mm).
        /// Typically destOrbitRadius × OrbitApproachMultiplier.</param>
        /// <param name="currentSimTime">Current simulation time (used for body position resolution
        /// in the safety check).</param>
        /// <param name="estimatedArrivalTime">Departure time + travel duration (used to predict
        /// destination body position at arrival).</param>
        /// <param name="registry">World registry to enumerate bodies from.</param>
        /// <param name="positionResolver">Delegate to resolve body world positions.</param>
        /// <param name="excludeOriginId">Origin body — excluded from safety checks.</param>
        /// <param name="safetyMargin">Extra clearance added to each body radius (Mm).</param>
        /// <param name="safetyCheckSamples">Sample count passed to RouteSafetyChecker.</param>
        /// <returns>An <see cref="ApproachPlan"/> with Success=true if any safe candidate
        /// was found, otherwise Success=false.</returns>
        public static ApproachPlan FindSafeApproach(
            SimVec3 shipStartWorldPos,
            EntityId arrivalParentId,
            double approachRadius,
            double currentSimTime,
            double estimatedArrivalTime,
            WorldRegistry registry,
            Func<EntityId, double, SimVec3> positionResolver,
            EntityId excludeOriginId,
            double safetyMargin,
            int safetyCheckSamples)
        {
            // Resolve destination world position at the estimated arrival time.
            // This is where the approach point will physically be when the ship arrives.
            SimVec3 destWorldAtArrival = positionResolver(arrivalParentId, estimatedArrivalTime);

            // Compute the base "direct" approach angle:
            // direction from destination body toward the ship's current position.
            SimVec3 dir = shipStartWorldPos - destWorldAtArrival;
            double baseAngleRad = Math.Atan2(dir.Z, dir.X);
            double baseAngleDeg = baseAngleRad * RadToDeg;

            string directBlocker = null;

            for (int i = 0; i < AngleOffsetsDeg.Length; i++)
            {
                double candidateAngleDeg = baseAngleDeg + AngleOffsetsDeg[i];
                double candidateAngleRad = candidateAngleDeg * DegToRad;

                // Compute approach point: offset from destination by approachRadius in
                // the chosen direction. Y=0 keeps travel in the reference plane.
                SimVec3 approachWorldPos = new SimVec3(
                    destWorldAtArrival.X + approachRadius * Math.Cos(candidateAngleRad),
                    destWorldAtArrival.Y,
                    destWorldAtArrival.Z + approachRadius * Math.Sin(candidateAngleRad));

                // Check whether the route from ship start → approach point is safe.
                // Body positions are resolved at currentSimTime (departure time approximation).
                string blockerName;
                bool safe = RouteSafetyChecker.IsSafe(
                    shipStartWorldPos,
                    approachWorldPos,
                    registry,
                    positionResolver,
                    currentSimTime,
                    excludeOriginId,
                    arrivalParentId,   // destination body always excluded
                    safetyMargin,
                    safetyCheckSamples,
                    out blockerName);

                // Record the direct-route blocker for the caller's log message.
                if (i == 0 && !safe)
                    directBlocker = blockerName;

                if (safe)
                {
                    return new ApproachPlan
                    {
                        Success = true,
                        ApproachWorldPos = approachWorldPos,
                        ApproachAngleDeg = candidateAngleDeg,
                        ApproachRadius = approachRadius,
                        VariantIndex = i,
                        DirectRouteBlocker = directBlocker
                    };
                }
            }

            // All candidates failed.
            return new ApproachPlan
            {
                Success = false,
                VariantIndex = -1,
                DirectRouteBlocker = directBlocker
            };
        }

        // ---------------------------------------------------------------
        // Public API: DirectApproach (safety-disabled fallback)
        // ---------------------------------------------------------------

        /// <summary>
        /// Compute the direct approach geometry without any safety checking.
        /// Used when ImpactSafetyEnabled is false (legacy / debug mode).
        /// Equivalent to what Phase 21 and earlier computed unconditionally.
        /// </summary>
        public static ApproachPlan DirectApproach(
            SimVec3 shipStartWorldPos,
            EntityId arrivalParentId,
            double approachRadius,
            double estimatedArrivalTime,
            Func<EntityId, double, SimVec3> positionResolver)
        {
            SimVec3 destWorldAtArrival = positionResolver(arrivalParentId, estimatedArrivalTime);
            SimVec3 dir = shipStartWorldPos - destWorldAtArrival;
            double angleRad = Math.Atan2(dir.Z, dir.X);
            double angleDeg = angleRad * RadToDeg;

            SimVec3 approachWorldPos = new SimVec3(
                destWorldAtArrival.X + approachRadius * Math.Cos(angleRad),
                destWorldAtArrival.Y,
                destWorldAtArrival.Z + approachRadius * Math.Sin(angleRad));

            return new ApproachPlan
            {
                Success = true,
                ApproachWorldPos = approachWorldPos,
                ApproachAngleDeg = angleDeg,
                ApproachRadius = approachRadius,
                VariantIndex = 0,
                DirectRouteBlocker = null
            };
        }
    }
}
