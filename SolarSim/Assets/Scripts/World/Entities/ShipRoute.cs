using SpaceSim.Shared.Identifiers;

namespace SpaceSim.World.Entities
{
    /// <summary>
    /// Minimal route data for a ship travelling between two bodies.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class ShipRoute
    {
        /// <summary>Body the ship departed from.</summary>
        public EntityId OriginBodyId { get; set; }

        /// <summary>Body the ship is travelling to.</summary>
        public EntityId DestinationBodyId { get; set; }

        /// <summary>Simulation time when the ship departed.</summary>
        public double DepartureTime { get; set; }

        /// <summary>Duration of travel in simulation seconds.</summary>
        public double TravelDuration { get; set; }

        public ShipRoute()
        {
            OriginBodyId = EntityId.None;
            DestinationBodyId = EntityId.None;
            DepartureTime = 0.0;
            TravelDuration = 1.0;
        }

        public ShipRoute(EntityId origin, EntityId destination, double departureTime, double travelDuration)
        {
            OriginBodyId = origin;
            DestinationBodyId = destination;
            DepartureTime = departureTime;
            TravelDuration = travelDuration > 0.0 ? travelDuration : 1.0;
        }

        /// <summary>
        /// Calculate travel progress at given simulation time. Clamped to [0, 1].
        /// </summary>
        public double GetProgress(double currentTime)
        {
            if (TravelDuration <= 0.0) return 1.0;
            double elapsed = currentTime - DepartureTime;
            if (elapsed <= 0.0) return 0.0;
            if (elapsed >= TravelDuration) return 1.0;
            return elapsed / TravelDuration;
        }

        /// <summary>
        /// Whether the route is complete at given simulation time.
        /// </summary>
        public bool IsComplete(double currentTime)
        {
            return GetProgress(currentTime) >= 1.0;
        }

        public override string ToString()
        {
            return $"Route[{OriginBodyId}->{DestinationBodyId} dep={DepartureTime:F1} dur={TravelDuration:F1}]";
        }
    }
}
