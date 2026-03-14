using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;

namespace SpaceSim.World.Entities
{
    /// <summary>
    /// Determines how the ship's travel position is interpolated.
    /// </summary>
    public enum RouteFrame
    {
        /// <summary>
        /// Interpolation in absolute world coordinates.
        /// Used for interplanetary travel (e.g. Terra -> Ares).
        /// </summary>
        Global,

        /// <summary>
        /// Interpolation in local coordinates relative to a dominant parent body.
        /// Used for local transfers (e.g. Terra <-> Luna, ship <-> moon).
        /// Position each tick = parentWorldPos + lerp(localStart, localEnd, progress).
        /// </summary>
        LocalParent
    }

    /// <summary>
    /// Route data for a ship travelling between two bodies.
    /// Supports both global and local-frame travel.
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

        // --- Frame selection ---

        /// <summary>How travel position is interpolated.</summary>
        public RouteFrame Frame { get; set; }

        /// <summary>
        /// For LocalParent frame: the dominant body whose world position
        /// is added to the interpolated local offset each tick.
        /// Ignored for Global frame.
        /// </summary>
        public EntityId LocalFrameBodyId { get; set; }

        // --- Global frame data ---

        /// <summary>Ship's world position at departure (Global frame).</summary>
        public SimVec3 StartWorldPosition { get; set; }

        /// <summary>Predicted world position at arrival (Global frame).</summary>
        public SimVec3 ArrivalWorldPosition { get; set; }

        // --- Local frame data ---

        /// <summary>Ship's position relative to LocalFrameBody at departure.</summary>
        public SimVec3 StartLocalPosition { get; set; }

        /// <summary>Target position relative to LocalFrameBody at arrival.</summary>
        public SimVec3 ArrivalLocalPosition { get; set; }

        // --- Destination orbit data ---

        /// <summary>Orbital radius the ship will use at the destination.</summary>
        public double DestinationOrbitRadius { get; set; }

        /// <summary>Orbital period the ship will use at the destination.</summary>
        public double DestinationOrbitPeriod { get; set; }

        /// <summary>
        /// The orbital phase angle (degrees) at which the ship enters
        /// orbit around the destination. Used to set MeanAnomalyAtEpoch
        /// on arrival so the ship doesn't snap.
        /// </summary>
        public double ArrivalOrbitPhaseDeg { get; set; }

        public ShipRoute()
        {
            OriginBodyId = EntityId.None;
            DestinationBodyId = EntityId.None;
            DepartureTime = 0.0;
            TravelDuration = 1.0;
            Frame = RouteFrame.Global;
            LocalFrameBodyId = EntityId.None;
            StartWorldPosition = SimVec3.Zero;
            ArrivalWorldPosition = SimVec3.Zero;
            StartLocalPosition = SimVec3.Zero;
            ArrivalLocalPosition = SimVec3.Zero;
            DestinationOrbitRadius = 3.0;
            DestinationOrbitPeriod = 12.0;
            ArrivalOrbitPhaseDeg = 0.0;
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
            return $"Route[{Frame} {OriginBodyId}->{DestinationBodyId} dep={DepartureTime:F1} dur={TravelDuration:F1}]";
        }
    }
}
