using SpaceSim.Shared.Identifiers;

namespace SpaceSim.World.Entities
{
    /// <summary>
    /// A persistent maneuver plan stored on ShipInfo.
    /// Created by NPCShipScheduler when ManeuverPlanner returns a delayed window.
    /// The ship waits in WaitingForWindow state until PlannedDepartureTime,
    /// then attempts to execute the plan.
    ///
    /// This makes the "burn window" behavior stateful: the decision is made once
    /// and persisted, rather than recomputed every tick.
    ///
    /// Pure data — no Unity dependency. Lives in World layer.
    /// </summary>
    public class PlannedManeuver
    {
        /// <summary>Simulation time when the ship should depart.</summary>
        public double PlannedDepartureTime { get; set; }

        /// <summary>Target body the ship intends to travel to.</summary>
        public EntityId TargetBodyId { get; set; }

        /// <summary>Geometry score from ManeuverPlanner at planning time.</summary>
        public double Score { get; set; }

        /// <summary>DirectScore from ManeuverPlanner at planning time.</summary>
        public double DirectScore { get; set; }

        /// <summary>Estimated travel duration computed at planning time.</summary>
        public double EstimatedTravelDuration { get; set; }

        /// <summary>Simulation time when this plan was created.</summary>
        public double CreatedAtSimTime { get; set; }

        /// <summary>
        /// Maximum age of the plan in sim-seconds before it is considered stale.
        /// If the plan is older than this when departure time arrives,
        /// it is invalidated and recomputed.
        /// Prevents executing very old plans where the world has changed significantly.
        /// </summary>
        public const double MaxPlanAge = 120.0;

        public PlannedManeuver() { }

        public PlannedManeuver(
            double departureTime,
            EntityId targetBodyId,
            double score,
            double directScore,
            double travelDuration,
            double createdAt)
        {
            PlannedDepartureTime = departureTime;
            TargetBodyId = targetBodyId;
            Score = score;
            DirectScore = directScore;
            EstimatedTravelDuration = travelDuration;
            CreatedAtSimTime = createdAt;
        }

        /// <summary>
        /// Check if the plan is still fresh enough to execute.
        /// Returns false if the plan is older than MaxPlanAge.
        /// </summary>
        public bool IsFresh(double currentSimTime)
        {
            return (currentSimTime - CreatedAtSimTime) < MaxPlanAge;
        }

        public override string ToString()
        {
            return $"PlannedManeuver[target={TargetBodyId} dep={PlannedDepartureTime:F1} " +
                   $"score={Score:F2} direct={DirectScore:F2} dur={EstimatedTravelDuration:F1}]";
        }
    }
}
