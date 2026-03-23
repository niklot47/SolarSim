using System;
using System.Collections.Generic;
using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;

namespace SpaceSim.Simulation.Ships
{
    /// <summary>
    /// Builds route segments for patched-conics ship routes.
    ///
    /// Phase 26d fix: OrbitInsertion endpoint is placed at the tangent-aligned point
    /// on the orbit circle (where orbit tangent is parallel to approach direction),
    /// not at the closest radial point. This eliminates the loop/reversal artifact.
    ///
    /// Pure C# — no UnityEngine dependency.
    /// </summary>
    public static class RouteSegmentBuilder
    {
        private const double DepartureFraction = 0.10;
        private const double SOIExitFraction = 0.05;
        private const double SOIEntryFraction = 0.05;
        private const double InsertionFraction = 0.10;
        private const double MinSegmentDuration = 0.1;

        public static void BuildSegments(
            ShipRoute route,
            CelestialBody originBody,
            CelestialBody arrivalParent,
            SimVec3 shipWorldPos,
            SimVec3 approachWorldPos,
            double departureTime,
            double travelDuration,
            WorldRegistry registry,
            Func<EntityId, double, SimVec3> positionResolver)
        {
            if (route == null || originBody == null || arrivalParent == null)
                return;

            route.Segments.Clear();
            route.CurrentSegmentIndex = 0;

            EntityId commonParentId = FindCommonParent(originBody, arrivalParent, registry);
            bool isLocal = IsLocalTransfer(originBody, arrivalParent, commonParentId, registry);

            if (isLocal)
            {
                BuildLocalSegments(
                    route, originBody, arrivalParent, commonParentId,
                    shipWorldPos, approachWorldPos,
                    departureTime, travelDuration,
                    registry, positionResolver);
            }
            else
            {
                BuildInterplanetarySegments(
                    route, originBody, arrivalParent, commonParentId,
                    shipWorldPos, approachWorldPos,
                    departureTime, travelDuration,
                    registry, positionResolver);
            }

            // Configure smooth Bezier curve on the final insertion segment.
            ConfigureInsertionCurve(route);
        }

        // ---------------------------------------------------------------
        // Local: Departure → LocalTransfer → Insertion
        // ---------------------------------------------------------------

        private static void BuildLocalSegments(
            ShipRoute route,
            CelestialBody originBody,
            CelestialBody arrivalParent,
            EntityId localFrameBodyId,
            SimVec3 shipWorldPos,
            SimVec3 approachWorldPos,
            double departureTime,
            double travelDuration,
            WorldRegistry registry,
            Func<EntityId, double, SimVec3> positionResolver)
        {
            EntityId frameBodyId = localFrameBodyId.IsValid
                ? localFrameBodyId
                : FindBestLocalFrame(originBody, arrivalParent, registry);
            if (!frameBodyId.IsValid) frameBodyId = originBody.Id;

            double depDuration = Math.Max(travelDuration * DepartureFraction, MinSegmentDuration);
            double insDuration = Math.Max(travelDuration * InsertionFraction, MinSegmentDuration);
            double transferDuration = Math.Max(travelDuration - depDuration - insDuration, MinSegmentDuration);

            double t0 = departureTime;
            double t1 = t0 + depDuration;
            double t2 = t1 + transferDuration;

            SimVec3 frameAtT0 = positionResolver != null ? positionResolver(frameBodyId, t0) : SimVec3.Zero;
            SimVec3 frameAtT1 = positionResolver != null ? positionResolver(frameBodyId, t1) : SimVec3.Zero;
            SimVec3 frameAtT2 = positionResolver != null ? positionResolver(frameBodyId, t2) : SimVec3.Zero;

            SimVec3 shipLocalStart = shipWorldPos - frameAtT0;
            SimVec3 approachLocal = approachWorldPos - frameAtT2;

            double depFrac = depDuration / travelDuration;
            SimVec3 depEndLocal = Lerp(shipLocalStart, approachLocal, depFrac);

            route.Segments.Add(new RouteSegment
            {
                Type = SegmentType.LocalOrbitDeparture,
                ReferenceBodyId = originBody.Id,
                StartTime = t0,
                Duration = depDuration,
                StartLocalPosition = shipWorldPos - (positionResolver != null ? positionResolver(originBody.Id, t0) : SimVec3.Zero),
                EndLocalPosition = (frameAtT0 + depEndLocal) - (positionResolver != null ? positionResolver(originBody.Id, t1) : SimVec3.Zero),
                CachedWorldStart = shipWorldPos,
                CachedWorldEnd = frameAtT1 + depEndLocal
            });

            route.Segments.Add(new RouteSegment
            {
                Type = SegmentType.LocalTransfer,
                ReferenceBodyId = frameBodyId,
                StartTime = t1,
                Duration = transferDuration,
                StartLocalPosition = depEndLocal,
                EndLocalPosition = approachLocal,
                CachedWorldStart = frameAtT1 + depEndLocal,
                CachedWorldEnd = approachWorldPos
            });

            // Insertion: endpoint is computed by ConfigureInsertionCurve later,
            // but we need an initial EndLocalPosition. Use approach direction scaled to orbit radius.
            SimVec3 insertionStart = approachWorldPos - (positionResolver != null ? positionResolver(arrivalParent.Id, t2) : SimVec3.Zero);

            route.Segments.Add(new RouteSegment
            {
                Type = SegmentType.OrbitInsertion,
                ReferenceBodyId = arrivalParent.Id,
                StartTime = t2,
                Duration = insDuration,
                StartLocalPosition = insertionStart,
                EndLocalPosition = SimVec3.Zero, // Overwritten by ConfigureInsertionCurve.
                CachedWorldStart = approachWorldPos,
                DestinationOrbitRadius = route.DestinationOrbitRadius,
                DestinationOrbitPeriod = route.DestinationOrbitPeriod,
                ArrivalAngleDeg = route.ArrivalOrbitPhaseDeg
            });
        }

        // ---------------------------------------------------------------
        // Interplanetary: Departure → SOIExit → Helio → SOIEntry → Insertion
        // ---------------------------------------------------------------

        private static void BuildInterplanetarySegments(
            ShipRoute route,
            CelestialBody originBody,
            CelestialBody arrivalParent,
            EntityId commonParentId,
            SimVec3 shipWorldPos,
            SimVec3 approachWorldPos,
            double departureTime,
            double travelDuration,
            WorldRegistry registry,
            Func<EntityId, double, SimVec3> positionResolver)
        {
            EntityId helioFrameId = commonParentId.IsValid ? commonParentId : EntityId.None;

            double depDuration = Math.Max(travelDuration * DepartureFraction, MinSegmentDuration);
            double exitDuration = Math.Max(travelDuration * SOIExitFraction, MinSegmentDuration);
            double entryDuration = Math.Max(travelDuration * SOIEntryFraction, MinSegmentDuration);
            double insDuration = Math.Max(travelDuration * InsertionFraction, MinSegmentDuration);
            double cruiseDuration = Math.Max(
                travelDuration - depDuration - exitDuration - entryDuration - insDuration,
                MinSegmentDuration);

            double t0 = departureTime;
            double t1 = t0 + depDuration;
            double t2 = t1 + exitDuration;
            double t3 = t2 + cruiseDuration;
            double t4 = t3 + entryDuration;

            double frac1 = depDuration / travelDuration;
            double frac2 = (depDuration + exitDuration) / travelDuration;
            double frac3 = (depDuration + exitDuration + cruiseDuration) / travelDuration;
            double frac4 = (depDuration + exitDuration + cruiseDuration + entryDuration) / travelDuration;

            SimVec3 wp1 = Lerp(shipWorldPos, approachWorldPos, frac1);
            SimVec3 wp2 = Lerp(shipWorldPos, approachWorldPos, frac2);
            SimVec3 wp3 = Lerp(shipWorldPos, approachWorldPos, frac3);
            SimVec3 wp4 = Lerp(shipWorldPos, approachWorldPos, frac4);

            SimVec3 originAtT0 = positionResolver != null ? positionResolver(originBody.Id, t0) : SimVec3.Zero;
            SimVec3 originAtT1 = positionResolver != null ? positionResolver(originBody.Id, t1) : SimVec3.Zero;

            route.Segments.Add(new RouteSegment
            {
                Type = SegmentType.LocalOrbitDeparture,
                ReferenceBodyId = originBody.Id,
                StartTime = t0, Duration = depDuration,
                StartLocalPosition = shipWorldPos - originAtT0,
                EndLocalPosition = wp1 - originAtT1,
                CachedWorldStart = shipWorldPos, CachedWorldEnd = wp1
            });

            EntityId originParentId = originBody.ParentId.IsValid ? originBody.ParentId : helioFrameId;
            SimVec3 opAtT1 = positionResolver != null && originParentId.IsValid ? positionResolver(originParentId, t1) : SimVec3.Zero;
            SimVec3 opAtT2 = positionResolver != null && originParentId.IsValid ? positionResolver(originParentId, t2) : SimVec3.Zero;

            route.Segments.Add(new RouteSegment
            {
                Type = SegmentType.SOIExit,
                ReferenceBodyId = originParentId,
                StartTime = t1, Duration = exitDuration,
                StartLocalPosition = wp1 - opAtT1,
                EndLocalPosition = wp2 - opAtT2,
                CachedWorldStart = wp1, CachedWorldEnd = wp2
            });

            SimVec3 hAtT2 = positionResolver != null && helioFrameId.IsValid ? positionResolver(helioFrameId, t2) : SimVec3.Zero;
            SimVec3 hAtT3 = positionResolver != null && helioFrameId.IsValid ? positionResolver(helioFrameId, t3) : SimVec3.Zero;

            route.Segments.Add(new RouteSegment
            {
                Type = SegmentType.HeliocentricTransfer,
                ReferenceBodyId = helioFrameId,
                StartTime = t2, Duration = cruiseDuration,
                StartLocalPosition = wp2 - hAtT2,
                EndLocalPosition = wp3 - hAtT3,
                CachedWorldStart = wp2, CachedWorldEnd = wp3
            });

            SimVec3 dAtT3 = positionResolver != null ? positionResolver(arrivalParent.Id, t3) : SimVec3.Zero;
            SimVec3 dAtT4 = positionResolver != null ? positionResolver(arrivalParent.Id, t4) : SimVec3.Zero;

            route.Segments.Add(new RouteSegment
            {
                Type = SegmentType.SOIEntry,
                ReferenceBodyId = arrivalParent.Id,
                StartTime = t3, Duration = entryDuration,
                StartLocalPosition = wp3 - dAtT3,
                EndLocalPosition = wp4 - dAtT4,
                CachedWorldStart = wp3, CachedWorldEnd = wp4
            });

            SimVec3 dAtT4b = positionResolver != null ? positionResolver(arrivalParent.Id, t4) : SimVec3.Zero;
            SimVec3 insertionStart = wp4 - dAtT4b;

            route.Segments.Add(new RouteSegment
            {
                Type = SegmentType.OrbitInsertion,
                ReferenceBodyId = arrivalParent.Id,
                StartTime = t4, Duration = insDuration,
                StartLocalPosition = insertionStart,
                EndLocalPosition = SimVec3.Zero, // Overwritten by ConfigureInsertionCurve.
                CachedWorldStart = wp4,
                DestinationOrbitRadius = route.DestinationOrbitRadius,
                DestinationOrbitPeriod = route.DestinationOrbitPeriod,
                ArrivalAngleDeg = route.ArrivalOrbitPhaseDeg
            });
        }

        // ---------------------------------------------------------------
        // Phase 26d fix: Tangent-aligned insertion point + Bezier curve
        // ---------------------------------------------------------------

        /// <summary>
        /// Compute the orbit entry point and Bezier control point for smooth insertion.
        ///
        /// Key insight: the entry point on the orbit should be where the orbit's tangent
        /// is parallel to the approach direction — NOT the closest radial point.
        ///
        /// Algorithm:
        ///   1. Compute approach direction in local coords (from P0 toward body center)
        ///   2. Find the point on the orbit circle where the tangent matches approach dir.
        ///      For a circular orbit in XZ: if approach dir is (dx, 0, dz), the tangent
        ///      at angle θ is (-sin θ, 0, cos θ). We want tangent ∥ approach dir.
        ///      → θ = atan2(-dx, dz) (prograde) or θ + π (retrograde)
        ///   3. Pick the θ that is on the "correct side" (ship doesn't cross through body)
        ///   4. P1 = R * (cos θ, 0, sin θ)
        ///   5. Control point C placed so curve starts along approach and ends at P1 tangentially
        /// </summary>
        private static void ConfigureInsertionCurve(ShipRoute route)
        {
            if (route.Segments == null || route.Segments.Count == 0)
                return;

            RouteSegment insertion = null;
            for (int i = route.Segments.Count - 1; i >= 0; i--)
            {
                if (route.Segments[i].Type == SegmentType.OrbitInsertion)
                {
                    insertion = route.Segments[i];
                    break;
                }
            }

            if (insertion == null)
                return;

            SimVec3 p0 = insertion.StartLocalPosition;
            double orbitR = insertion.DestinationOrbitRadius;
            if (orbitR < 1e-10)
            {
                insertion.UseCurvedInsertion = false;
                return;
            }

            // Approach direction: from P0 toward the body center (origin in local coords).
            // Normalized in XZ plane (Y ignored for 2D orbit geometry).
            SimVec3 toCenter = new SimVec3(-p0.X, 0.0, -p0.Z);
            double toCenterMag = Math.Sqrt(toCenter.X * toCenter.X + toCenter.Z * toCenter.Z);
            if (toCenterMag < 1e-10)
            {
                insertion.UseCurvedInsertion = false;
                return;
            }

            double dx = toCenter.X / toCenterMag;
            double dz = toCenter.Z / toCenterMag;

            // Find orbit angle θ where tangent(-sin θ, 0, cos θ) is parallel to approach.
            // tangent ∥ (dx, dz) means: -sin θ / dx = cos θ / dz
            // → θ = atan2(-dx, dz)
            double thetaA = Math.Atan2(-dx, dz);
            double thetaB = thetaA + Math.PI;

            // Two candidate points on the orbit.
            SimVec3 candA = new SimVec3(orbitR * Math.Cos(thetaA), 0.0, orbitR * Math.Sin(thetaA));
            SimVec3 candB = new SimVec3(orbitR * Math.Cos(thetaB), 0.0, orbitR * Math.Sin(thetaB));

            // Pick the candidate that is closer to P0 (less travel distance).
            double distA = (candA - p0).SqrMagnitude;
            double distB = (candB - p0).SqrMagnitude;

            SimVec3 p1;
            SimVec3 orbitTangent;

            if (distA <= distB)
            {
                p1 = candA;
                orbitTangent = new SimVec3(-Math.Sin(thetaA), 0.0, Math.Cos(thetaA));
            }
            else
            {
                p1 = candB;
                orbitTangent = new SimVec3(-Math.Sin(thetaB), 0.0, Math.Cos(thetaB));
            }

            // Ensure tangent points in the same general direction as approach
            // (not backwards). If dot < 0, flip tangent (retrograde insertion).
            SimVec3 approachDir = new SimVec3(dx, 0.0, dz);
            if (SimVec3.Dot(orbitTangent, approachDir) < 0.0)
            {
                orbitTangent = new SimVec3(-orbitTangent.X, -orbitTangent.Y, -orbitTangent.Z);
            }

            // Update the insertion endpoint.
            insertion.EndLocalPosition = p1;
            insertion.OrbitTangentAtEnd = orbitTangent;

            // Update ArrivalAngleDeg to match the new P1 position.
            double arrAngleRad = Math.Atan2(p1.Z, p1.X);
            insertion.ArrivalAngleDeg = NormalizeDeg(arrAngleRad * 180.0 / Math.PI);

            // Bezier control point: C = P1 - k * orbitTangent
            // k = distance(P0, P1) * 0.5 gives a gentle curve proportional to approach distance.
            double approachDist = (p1 - p0).Magnitude;
            double k = approachDist * 0.5;
            if (k < orbitR * 0.3) k = orbitR * 0.3; // Minimum curve size.

            SimVec3 controlPoint = new SimVec3(
                p1.X - k * orbitTangent.X,
                p1.Y - k * orbitTangent.Y,
                p1.Z - k * orbitTangent.Z);

            insertion.InsertionControlPoint = controlPoint;
            insertion.UseCurvedInsertion = true;
        }

        // ---------------------------------------------------------------
        // Hierarchy helpers
        // ---------------------------------------------------------------

        public static EntityId FindCommonParent(
            CelestialBody bodyA, CelestialBody bodyB, WorldRegistry registry)
        {
            if (bodyA == null || bodyB == null) return EntityId.None;
            var ancestorsA = new HashSet<EntityId>();
            var current = bodyA;
            int depth = 0;
            while (current != null && depth < 20)
            {
                ancestorsA.Add(current.Id);
                if (!current.ParentId.IsValid) break;
                current = registry.GetCelestialBody(current.ParentId);
                depth++;
            }
            current = bodyB;
            depth = 0;
            while (current != null && depth < 20)
            {
                if (ancestorsA.Contains(current.Id)) return current.Id;
                if (!current.ParentId.IsValid) break;
                current = registry.GetCelestialBody(current.ParentId);
                depth++;
            }
            return EntityId.None;
        }

        private static bool IsLocalTransfer(
            CelestialBody originBody, CelestialBody arrivalParent,
            EntityId commonParentId, WorldRegistry registry)
        {
            if (originBody.ParentId == arrivalParent.Id) return true;
            if (arrivalParent.ParentId == originBody.Id) return true;
            if (originBody.ParentId.IsValid && originBody.ParentId == arrivalParent.ParentId)
            {
                var parent = registry.GetCelestialBody(originBody.ParentId);
                if (parent != null && parent.BodyType != CelestialBodyType.Star) return true;
            }
            if (commonParentId.IsValid)
            {
                var common = registry.GetCelestialBody(commonParentId);
                if (common != null && common.BodyType != CelestialBodyType.Star
                    && common.Id != originBody.Id && common.Id != arrivalParent.Id)
                    return true;
            }
            return false;
        }

        private static EntityId FindBestLocalFrame(
            CelestialBody originBody, CelestialBody arrivalParent, WorldRegistry registry)
        {
            if (originBody.ParentId == arrivalParent.Id) return arrivalParent.Id;
            if (arrivalParent.ParentId == originBody.Id) return originBody.Id;
            if (originBody.ParentId.IsValid && originBody.ParentId == arrivalParent.ParentId)
                return originBody.ParentId;
            return EntityId.None;
        }

        private static SimVec3 Lerp(SimVec3 a, SimVec3 b, double t)
        {
            return new SimVec3(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t,
                a.Z + (b.Z - a.Z) * t);
        }

        private static double NormalizeDeg(double deg)
        {
            deg %= 360.0;
            return deg < 0 ? deg + 360.0 : deg;
        }
    }
}
