using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;

namespace SpaceSim.World.Entities
{
    /// <summary>
    /// One segment of a patched-conics ship route.
    ///
    /// Non-insertion segments: linear interpolation.
    ///   worldPos = refBodyWorldPos + lerp(StartLocal, EndLocal, progress)
    ///
    /// OrbitInsertion segments (Phase 26d): quadratic Bezier curve.
    ///   worldPos = refBodyWorldPos + Bezier(StartLocal, InsertionControlPoint, EndLocal, t)
    ///   Curve starts along approach direction, ends tangent to orbit.
    ///
    /// Pure C# — no UnityEngine dependency.
    /// </summary>
    public class RouteSegment
    {
        public SegmentType Type { get; set; }
        public EntityId ReferenceBodyId { get; set; }
        public double StartTime { get; set; }
        public double Duration { get; set; }
        public SimVec3 StartLocalPosition { get; set; }
        public SimVec3 EndLocalPosition { get; set; }
        public SimVec3? CachedWorldStart { get; set; }
        public SimVec3? CachedWorldEnd { get; set; }

        // OrbitInsertion data.
        public double DestinationOrbitRadius { get; set; }
        public double DestinationOrbitPeriod { get; set; }
        public double ArrivalAngleDeg { get; set; }

        // Phase 26d: Bezier curve for smooth orbit insertion.

        /// <summary>
        /// Quadratic Bezier control point (local coords, relative to ReferenceBody).
        /// Placed so the curve starts along approach direction and ends tangent to orbit.
        /// </summary>
        public SimVec3 InsertionControlPoint { get; set; }

        /// <summary>True when this segment should use Bezier interpolation.</summary>
        public bool UseCurvedInsertion { get; set; }

        /// <summary>Orbit tangent unit vector at EndLocalPosition (prograde direction).</summary>
        public SimVec3 OrbitTangentAtEnd { get; set; }

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
            InsertionControlPoint = SimVec3.Zero;
            UseCurvedInsertion = false;
            OrbitTangentAtEnd = SimVec3.Zero;
        }

        public double GetProgress(double currentTime)
        {
            if (Duration <= 0.0) return 1.0;
            double elapsed = currentTime - StartTime;
            if (elapsed <= 0.0) return 0.0;
            if (elapsed >= Duration) return 1.0;
            return elapsed / Duration;
        }

        public bool IsComplete(double currentTime) => GetProgress(currentTime) >= 1.0;
        public double EndTime => StartTime + Duration;

        public override string ToString()
        {
            string curve = UseCurvedInsertion ? " curved" : "";
            return $"Segment[{Type} ref={ReferenceBodyId} t={StartTime:F1}+{Duration:F1}s{curve}]";
        }
    }
}
