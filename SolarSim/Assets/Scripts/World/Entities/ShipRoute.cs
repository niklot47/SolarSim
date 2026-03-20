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
        /// Used for local transfers (e.g. Terra &lt;-> Luna, ship &lt;-> moon).
        /// Position each tick = parentWorldPos + lerp(localStart, localEnd, progress).
        /// </summary>
        LocalParent
    }

    /// <summary>
    /// Route data for a ship travelling between two bodies.
    /// Supports both global and local-frame travel.
    ///
    /// Travel is two-phase:
    ///   Phase 1 (Travelling): ship moves from origin to an approach point
    ///     located OrbitApproachMultiplier × orbit radius away from the destination body.
    ///   Phase 2 (InsertingIntoOrbit): ship moves from approach point to final orbit radius
    ///     in the local frame of the arrival parent body (smooth, stable, body-tracking).
    ///
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class ShipRoute
    {
        /// <summary>Body the ship departed from.</summary>
        public EntityId OriginBodyId { get; set; }

        /// <summary>Body the ship is travelling to (may be surface station).</summary>
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

        // --- Global frame data (Phase 1) ---

        /// <summary>Ship's world position at departure.</summary>
        public SimVec3 StartWorldPosition { get; set; }

        /// <summary>
        /// Approach point at end of Phase 1 (world coords).
        /// Located OrbitApproachMultiplier × orbit radius from the destination body.
        /// </summary>
        public SimVec3 ArrivalWorldPosition { get; set; }

        // --- Local frame data (Phase 1) ---

        /// <summary>Ship's position relative to LocalFrameBody at departure.</summary>
        public SimVec3 StartLocalPosition { get; set; }

        /// <summary>
        /// Approach point in LocalFrameBody-relative coordinates at end of Phase 1.
        /// </summary>
        public SimVec3 ArrivalLocalPosition { get; set; }

        // --- Destination orbit data ---

        /// <summary>Orbital radius the ship will use at the destination.</summary>
        public double DestinationOrbitRadius { get; set; }

        /// <summary>Orbital period the ship will use at the destination.</summary>
        public double DestinationOrbitPeriod { get; set; }

        /// <summary>
        /// Predicted approach angle (degrees) used as fallback if insertion phase
        /// is skipped. Normally overridden by InsertionArrivalAngleDeg.
        /// </summary>
        public double ArrivalOrbitPhaseDeg { get; set; }

        // -------------------------------------------------------------------
        // Phase 2: Orbit insertion approach
        // Set during StartInsertionPhase when Phase 1 completes.
        // -------------------------------------------------------------------

        /// <summary>Whether the insertion phase is currently active.</summary>
        public bool InsertionPhaseActive { get; set; }

        /// <summary>Simulation time when insertion phase started.</summary>
        public double InsertionStartTime { get; set; }

        /// <summary>
        /// Body used as reference frame during insertion.
        /// Ship position = InsertionFrameBody world position + local interpolated offset.
        /// This keeps the ship stable even as the target body moves.
        /// </summary>
        public EntityId InsertionFrameBodyId { get; set; }

        /// <summary>
        /// Ship's local position (relative to InsertionFrameBody) at start of insertion.
        /// Equals approach point minus frame body position at insertion start time.
        /// </summary>
        public SimVec3 InsertionStartLocalPos { get; set; }

        /// <summary>
        /// Target local position (relative to InsertionFrameBody) at end of insertion.
        /// This is the orbit insertion point: (orbitRadius * cos(angle), 0, orbitRadius * sin(angle)).
        /// </summary>
        public SimVec3 InsertionTargetLocalPos { get; set; }

        /// <summary>
        /// Actual approach angle in degrees, measured at insertion phase start.
        /// More accurate than ArrivalOrbitPhaseDeg since it uses the real ship position.
        /// Used to set MeanAnomalyAtEpoch when inserting into orbit.
        /// </summary>
        public double InsertionArrivalAngleDeg { get; set; }

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
            InsertionPhaseActive = false;
            InsertionStartTime = 0.0;
            InsertionFrameBodyId = EntityId.None;
            InsertionStartLocalPos = SimVec3.Zero;
            InsertionTargetLocalPos = SimVec3.Zero;
            InsertionArrivalAngleDeg = 0.0;
        }

        /// <summary>
        /// Calculate travel progress (Phase 1) at given simulation time. Clamped to [0, 1].
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
        /// Whether Phase 1 travel is complete at given simulation time.
        /// </summary>
        public bool IsComplete(double currentTime)
        {
            return GetProgress(currentTime) >= 1.0;
        }

        public override string ToString()
        {
            string insertStr = InsertionPhaseActive ? " [inserting]" : "";
            return $"Route[{Frame} {OriginBodyId}->{DestinationBodyId} dep={DepartureTime:F1} dur={TravelDuration:F1}{insertStr}]";
        }
    }
}
