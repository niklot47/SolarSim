using System.Collections.Generic;
using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;

namespace SpaceSim.World.Entities
{
    /// <summary>
    /// [DEPRECATED — Phase 26c] Legacy route frame enum.
    /// Retained for ShipRoute field compatibility. Not used by segmented execution.
    /// Segmented routes define reference frames per-segment via RouteSegment.ReferenceBodyId.
    /// </summary>
    public enum RouteFrame
    {
        Global,
        LocalParent
    }

    /// <summary>
    /// Route data for a ship travelling between two bodies.
    ///
    /// Phase 26c: Segmented routes are the ONLY active execution path.
    ///
    /// Primary fields (used by current navigation):
    ///   Segments, CurrentSegmentIndex, UseSegmentedRoute — patched-conics model.
    ///   OriginBodyId, DestinationBodyId — route endpoints.
    ///   DepartureTime, TravelDuration — timing.
    ///   StartWorldPosition, ArrivalWorldPosition — cached world coords at build time.
    ///   DestinationOrbitRadius/Period, ArrivalOrbitPhaseDeg — orbit insertion data.
    ///
    /// Deprecated fields (retained for save/load compatibility, not used at runtime):
    ///   Frame, LocalFrameBodyId — replaced by per-segment ReferenceBodyId.
    ///   StartLocalPosition, ArrivalLocalPosition — replaced by per-segment local positions.
    ///   InsertionPhase* fields — replaced by OrbitInsertion segment type.
    ///
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class ShipRoute
    {
        // ===============================================================
        // Patched-Conics Segment Model (PRIMARY — Phase 26)
        // ===============================================================

        /// <summary>
        /// Ordered list of route segments forming the patched-conics trajectory.
        /// Built by RouteSegmentBuilder during StartRoute().
        /// </summary>
        public List<RouteSegment> Segments { get; set; } = new List<RouteSegment>();

        /// <summary>
        /// Index of the segment the ship is currently traversing.
        /// Incremented when the current segment completes.
        /// </summary>
        public int CurrentSegmentIndex { get; set; }

        /// <summary>
        /// When true, ShipMovementSystem uses the Segments list for position interpolation.
        /// Phase 26c: always true for new routes. False only if segments failed to build.
        /// </summary>
        public bool UseSegmentedRoute { get; set; }

        /// <summary>Get the currently active segment, or null.</summary>
        public RouteSegment CurrentSegment
        {
            get
            {
                if (Segments == null || Segments.Count == 0) return null;
                if (CurrentSegmentIndex < 0 || CurrentSegmentIndex >= Segments.Count) return null;
                return Segments[CurrentSegmentIndex];
            }
        }

        /// <summary>Whether all segments have been completed.</summary>
        public bool AllSegmentsComplete
        {
            get
            {
                if (Segments == null || Segments.Count == 0) return true;
                return CurrentSegmentIndex >= Segments.Count;
            }
        }

        /// <summary>Total number of segments in the route.</summary>
        public int SegmentCount => Segments?.Count ?? 0;

        /// <summary>
        /// Advance to the next segment. Returns true if there is a next segment.
        /// </summary>
        public bool AdvanceSegment()
        {
            CurrentSegmentIndex++;
            return CurrentSegmentIndex < (Segments?.Count ?? 0);
        }

        // ===============================================================
        // Route endpoints and timing (used by both segmented and legacy)
        // ===============================================================

        /// <summary>Body the ship departed from.</summary>
        public EntityId OriginBodyId { get; set; }

        /// <summary>Body the ship is travelling to (may be surface station).</summary>
        public EntityId DestinationBodyId { get; set; }

        /// <summary>Simulation time when the ship departed.</summary>
        public double DepartureTime { get; set; }

        /// <summary>Total duration of travel in simulation seconds.</summary>
        public double TravelDuration { get; set; }

        /// <summary>Ship's world position at departure (cached at build time).</summary>
        public SimVec3 StartWorldPosition { get; set; }

        /// <summary>Approach point world position (cached at build time).</summary>
        public SimVec3 ArrivalWorldPosition { get; set; }

        /// <summary>Orbital radius the ship will use at the destination.</summary>
        public double DestinationOrbitRadius { get; set; }

        /// <summary>Orbital period the ship will use at the destination.</summary>
        public double DestinationOrbitPeriod { get; set; }

        /// <summary>Approach angle in degrees (used as fallback for orbit insertion).</summary>
        public double ArrivalOrbitPhaseDeg { get; set; }

        // ===============================================================
        // DEPRECATED legacy fields (Phase 26c)
        // Retained for save/load compatibility. Not used by segmented execution.
        // Will be removed in a future cleanup pass.
        // ===============================================================

        /// <summary>[DEPRECATED] Legacy frame type. Replaced by per-segment ReferenceBodyId.</summary>
        public RouteFrame Frame { get; set; }

        /// <summary>[DEPRECATED] Legacy local frame body. Replaced by per-segment ReferenceBodyId.</summary>
        public EntityId LocalFrameBodyId { get; set; }

        /// <summary>[DEPRECATED] Legacy local start position. Replaced by per-segment StartLocalPosition.</summary>
        public SimVec3 StartLocalPosition { get; set; }

        /// <summary>[DEPRECATED] Legacy local arrival position. Replaced by per-segment EndLocalPosition.</summary>
        public SimVec3 ArrivalLocalPosition { get; set; }

        /// <summary>[DEPRECATED] Legacy insertion phase flag. Replaced by OrbitInsertion segment.</summary>
        public bool InsertionPhaseActive { get; set; }

        /// <summary>[DEPRECATED] Legacy insertion start time.</summary>
        public double InsertionStartTime { get; set; }

        /// <summary>[DEPRECATED] Legacy insertion frame body.</summary>
        public EntityId InsertionFrameBodyId { get; set; }

        /// <summary>[DEPRECATED] Legacy insertion start local position.</summary>
        public SimVec3 InsertionStartLocalPos { get; set; }

        /// <summary>[DEPRECATED] Legacy insertion target local position.</summary>
        public SimVec3 InsertionTargetLocalPos { get; set; }

        /// <summary>[DEPRECATED] Legacy insertion arrival angle.</summary>
        public double InsertionArrivalAngleDeg { get; set; }

        public ShipRoute()
        {
            // Primary fields.
            Segments = new List<RouteSegment>();
            CurrentSegmentIndex = 0;
            UseSegmentedRoute = false;
            OriginBodyId = EntityId.None;
            DestinationBodyId = EntityId.None;
            DepartureTime = 0.0;
            TravelDuration = 1.0;
            StartWorldPosition = SimVec3.Zero;
            ArrivalWorldPosition = SimVec3.Zero;
            DestinationOrbitRadius = 3.0;
            DestinationOrbitPeriod = 12.0;
            ArrivalOrbitPhaseDeg = 0.0;

            // Deprecated legacy fields.
            Frame = RouteFrame.Global;
            LocalFrameBodyId = EntityId.None;
            StartLocalPosition = SimVec3.Zero;
            ArrivalLocalPosition = SimVec3.Zero;
            InsertionPhaseActive = false;
            InsertionStartTime = 0.0;
            InsertionFrameBodyId = EntityId.None;
            InsertionStartLocalPos = SimVec3.Zero;
            InsertionTargetLocalPos = SimVec3.Zero;
            InsertionArrivalAngleDeg = 0.0;
        }

        /// <summary>
        /// [DEPRECATED] Legacy progress for single-route interpolation.
        /// Use GetOverallSegmentProgress() for segmented routes.
        /// </summary>
        public double GetProgress(double currentTime)
        {
            if (TravelDuration <= 0.0) return 1.0;
            double elapsed = currentTime - DepartureTime;
            if (elapsed <= 0.0) return 0.0;
            if (elapsed >= TravelDuration) return 1.0;
            return elapsed / TravelDuration;
        }

        /// <summary>[DEPRECATED] Legacy completion check.</summary>
        public bool IsComplete(double currentTime)
        {
            return GetProgress(currentTime) >= 1.0;
        }

        /// <summary>
        /// Get overall route progress across all segments (0..1).
        /// </summary>
        public double GetOverallSegmentProgress(double currentTime)
        {
            if (!UseSegmentedRoute || Segments == null || Segments.Count == 0)
                return GetProgress(currentTime);

            int count = Segments.Count;
            if (CurrentSegmentIndex >= count) return 1.0;

            double completedFraction = (double)CurrentSegmentIndex / count;
            double currentFraction = Segments[CurrentSegmentIndex].GetProgress(currentTime) / count;
            return completedFraction + currentFraction;
        }

        public override string ToString()
        {
            string segStr = UseSegmentedRoute
                ? $" segments={SegmentCount} current={CurrentSegmentIndex}"
                : "";
            return $"Route[{OriginBodyId}->{DestinationBodyId} " +
                   $"dep={DepartureTime:F1} dur={TravelDuration:F1}{segStr}]";
        }
    }
}
