using SpaceSim.Shared.Identifiers;

namespace SpaceSim.World.Entities
{
    /// <summary>
    /// Phase of a trader's current job.
    /// </summary>
    public enum TraderJobPhase
    {
        /// <summary>Travelling to source station to pick up cargo.</summary>
        GoingToSource,

        /// <summary>Loading cargo at source station.</summary>
        LoadingAtSource,

        /// <summary>Travelling to destination station to deliver cargo.</summary>
        GoingToDestination,

        /// <summary>Unloading cargo at destination station.</summary>
        UnloadingAtDestination
    }

    /// <summary>
    /// Tracks the current trade assignment for a trader ship.
    /// Stored on ShipInfo so the scheduler knows what the trader is doing.
    /// Pure data object — no Unity dependency.
    /// </summary>
    public class TraderJob
    {
        /// <summary>Resource being transported.</summary>
        public ResourceType Resource { get; set; }

        /// <summary>Station to pick up cargo from.</summary>
        public EntityId SourceStationId { get; set; }

        /// <summary>Station to deliver cargo to.</summary>
        public EntityId DestinationStationId { get; set; }

        /// <summary>Current phase of the trade job.</summary>
        public TraderJobPhase Phase { get; set; }

        public TraderJob() { }

        public TraderJob(ResourceType resource, EntityId source, EntityId destination)
        {
            Resource = resource;
            SourceStationId = source;
            DestinationStationId = destination;
            Phase = TraderJobPhase.GoingToSource;
        }

        public override string ToString()
        {
            return $"TraderJob[{Resource} {SourceStationId}->{DestinationStationId} phase={Phase}]";
        }
    }
}
