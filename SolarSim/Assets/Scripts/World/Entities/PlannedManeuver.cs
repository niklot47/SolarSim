using SpaceSim.Shared.Identifiers;

namespace SpaceSim.World.Entities
{
    /// <summary>
    /// A persistent maneuver plan stored on ShipInfo.
    /// Time unit: sim-s = real seconds.
    /// MaxPlanAge = 31,536,000 sim-s = 1 year.
    /// </summary>
    public class PlannedManeuver
    {
        public double   PlannedDepartureTime    { get; set; }
        public EntityId TargetBodyId            { get; set; }
        public double   Score                   { get; set; }
        public double   DirectScore             { get; set; }
        public double   EstimatedTravelDuration { get; set; }
        public double   CreatedAtSimTime        { get; set; }

        /// <summary>1 year in sim-s.</summary>
        public const double MaxPlanAge = 31536000.0;

        public PlannedManeuver() { }

        public PlannedManeuver(
            double departureTime, EntityId targetBodyId,
            double score, double directScore,
            double travelDuration, double createdAt)
        {
            PlannedDepartureTime    = departureTime;
            TargetBodyId            = targetBodyId;
            Score                   = score;
            DirectScore             = directScore;
            EstimatedTravelDuration = travelDuration;
            CreatedAtSimTime        = createdAt;
        }

        public bool IsFresh(double currentSimTime)
            => (currentSimTime - CreatedAtSimTime) < MaxPlanAge;

        public override string ToString()
            => $"PlannedManeuver[target={TargetBodyId} dep={PlannedDepartureTime:F0} " +
               $"score={Score:F2} direct={DirectScore:F2} dur={EstimatedTravelDuration:F0}]";
    }
}
