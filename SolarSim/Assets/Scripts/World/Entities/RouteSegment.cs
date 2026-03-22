using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;

namespace SpaceSim.World.Entities
{
    /// <summary>
    /// One segment of a patched-conics ship route.
    /// Each segment lives in a specific reference frame defined by ReferenceBodyId.
    ///
    /// Position interpretation:
    ///   StartLocalPosition / EndLocalPosition are relative to the reference body.
    ///   World positions are computed at runtime:
    ///     worldPos = referenceBodyWorldPos + lerp(StartLocal, EndLocal, segmentProgress)
    ///
    /// This eliminates the need for runtime frame switching (ReframeRoute).
    /// Each segment knows its frame from creation time.
    ///
    /// Phase 26a — data model only. Segments are built by ManeuverPlanner (Phase 26b)
    /// and consumed by ShipMovementSystem (Phase 26b).
    ///
    /// Pure C# — no UnityEngine dependency. Lives in World layer.
    /// </summary>
    public class RouteSegment
    {
        /// <summary>Type of this segment (departure, transfer, entry, insertion, etc.).</summary>
        public SegmentType Type { get; set; }

        /// <summary>
        /// Body used as reference frame for this segment's local positions.
        /// For HeliocentricTransfer: the star (root body).
        /// For LocalOrbitDeparture: the origin planet/moon.
        /// For SOIEntry/OrbitInsertion: the destination planet/moon.
        /// For LocalTransfer: the shared parent body.
        /// </summary>
        public EntityId ReferenceBodyId { get; set; }

        /// <summary>Simulation time when this segment begins.</summary>
        public double StartTime { get; set; }

        /// <summary>Duration of this segment in simulation seconds.</summary>
        public double Duration { get; set; }

        /// <summary>
        /// Start position relative to ReferenceBody at StartTime.
        /// </summary>
        public SimVec3 StartLocalPosition { get; set; }

        /// <summary>
        /// End position relative to ReferenceBody at (StartTime + Duration).
        /// </summary>
        public SimVec3 EndLocalPosition { get; set; }

        /// <summary>
        /// Cached world-space start position at the time the segment was built.
        /// Used for debug visualization and logging.
        /// May become stale if reference body moves — runtime uses local + resolver.
        /// </summary>
        public SimVec3? CachedWorldStart { get; set; }

        /// <summary>
        /// Cached world-space end position at the time the segment was built.
        /// Used for debug visualization and logging.
        /// </summary>
        public SimVec3? CachedWorldEnd { get; set; }

        /// <summary>
        /// Orbit radius at the end of this segment (for OrbitInsertion segments).
        /// Used to construct the final orbit when the ship completes arrival.
        /// 0.0 for non-insertion segments.
        /// </summary>
        public double DestinationOrbitRadius { get; set; }

        /// <summary>
        /// Orbit period at the end of this segment (for OrbitInsertion segments).
        /// 0.0 for non-insertion segments.
        /// </summary>
        public double DestinationOrbitPeriod { get; set; }

        /// <summary>
        /// Approach angle in degrees for the insertion segment.
        /// Used to set MeanAnomalyAtEpoch when entering orbit.
        /// 0.0 for non-insertion segments.
        /// </summary>
        public double ArrivalAngleDeg { get; set; }

        public RouteSegment()
        {
            Type = SegmentType.HeliocentricTransfer;
            ReferenceBodyId = EntityId.None;
            StartTime = 0.0;
            Duration = 1.0;
            StartLocalPosition = SimVec3.Zero;
            EndLocalPosition = SimVec3.Zero;
            CachedWorldStart = null;
            CachedWorldEnd = null;
            DestinationOrbitRadius = 0.0;
            DestinationOrbitPeriod = 0.0;
            ArrivalAngleDeg = 0.0;
        }

        /// <summary>
        /// Calculate progress within this segment at given simulation time.
        /// Clamped to [0, 1].
        /// </summary>
        public double GetProgress(double currentTime)
        {
            if (Duration <= 0.0) return 1.0;
            double elapsed = currentTime - StartTime;
            if (elapsed <= 0.0) return 0.0;
            if (elapsed >= Duration) return 1.0;
            return elapsed / Duration;
        }

        /// <summary>
        /// Whether this segment is complete at given simulation time.
        /// </summary>
        public bool IsComplete(double currentTime)
        {
            return GetProgress(currentTime) >= 1.0;
        }

        /// <summary>
        /// Simulation time when this segment ends.
        /// </summary>
        public double EndTime => StartTime + Duration;

        public override string ToString()
        {
            return $"Segment[{Type} ref={ReferenceBodyId} t={StartTime:F1}+{Duration:F1}s]";
        }
    }
}
