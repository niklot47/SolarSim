using SpaceSim.Shared.Identifiers;

namespace SpaceSim.World.Entities
{
    /// <summary>
    /// Represents a single trade opportunity: move a resource from
    /// a station with surplus to a station with demand.
    /// Pure data object — no Unity dependency.
    /// </summary>
    public struct TradeOpportunity
    {
        /// <summary>Resource type to transport.</summary>
        public ResourceType Resource;

        /// <summary>Station that has surplus of this resource (pick up here).</summary>
        public EntityId SourceStationId;

        /// <summary>Station that needs this resource (deliver here).</summary>
        public EntityId DestinationStationId;

        /// <summary>
        /// Priority score for this opportunity.
        /// Higher = more valuable trade route.
        /// Combines demand, surplus, and distance factors.
        /// </summary>
        public double Score;

        public override string ToString()
        {
            return $"Trade[{Resource} {SourceStationId}->{DestinationStationId} score={Score:F2}]";
        }
    }
}
