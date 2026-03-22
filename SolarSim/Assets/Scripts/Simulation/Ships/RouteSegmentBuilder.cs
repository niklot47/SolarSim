using System;
using System.Collections.Generic;
using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;

namespace SpaceSim.Simulation.Ships
{
    /// <summary>
    /// Builds a list of RouteSegments for a ship route based on the origin/destination
    /// hierarchy and SOI structure.
    ///
    /// Segment generation rules:
    ///
    /// Case 1: Same parent (e.g. Terra → Luna, both inside Sol's SOI but same local parent)
    ///   LocalOrbitDeparture → LocalTransfer → OrbitInsertion
    ///
    /// Case 2: Different parent, common ancestor is star (interplanetary)
    ///   LocalOrbitDeparture → SOIExit → HeliocentricTransfer → SOIEntry → OrbitInsertion
    ///
    /// Case 3: Parent-to-child or child-to-parent (e.g. Terra → Luna as parent/child)
    ///   LocalOrbitDeparture → LocalTransfer → OrbitInsertion
    ///
    /// IMPORTANT:
    ///   - Keep it SIMPLE — straight-line approximation per segment
    ///   - No Lambert solver, no delta-v, no real physics burns
    ///   - Segments define reference frames explicitly
    ///   - Position interpolation uses local coords relative to reference body
    ///
    /// Phase 26a: builds segments and attaches to ShipRoute.Segments.
    /// Phase 26b: ShipMovementSystem reads and executes segments.
    ///
    /// Pure C# — no UnityEngine dependency. Lives in Simulation layer.
    /// </summary>
    public static class RouteSegmentBuilder
    {
        /// <summary>
        /// Fraction of total travel duration allocated to departure phase.
        /// </summary>
        private const double DepartureFraction = 0.10;

        /// <summary>
        /// Fraction of total travel duration allocated to SOI exit phase.
        /// </summary>
        private const double SOIExitFraction = 0.05;

        /// <summary>
        /// Fraction of total travel duration allocated to SOI entry phase.
        /// </summary>
        private const double SOIEntryFraction = 0.05;

        /// <summary>
        /// Fraction of total travel duration allocated to orbit insertion phase.
        /// </summary>
        private const double InsertionFraction = 0.10;

        /// <summary>
        /// Minimum segment duration in sim-seconds to avoid degenerate segments.
        /// </summary>
        private const double MinSegmentDuration = 0.1;

        /// <summary>
        /// Build route segments for a ship route.
        /// Analyzes the origin/destination hierarchy to determine the segment pattern.
        /// Attaches segments to route.Segments and resets CurrentSegmentIndex to 0.
        ///
        /// Does NOT set route.UseSegmentedRoute = true (that's Phase 26b's job).
        /// </summary>
        /// <param name="route">The ShipRoute to populate with segments.</param>
        /// <param name="originBody">Origin body the ship is departing from.</param>
        /// <param name="arrivalParent">The body the ship will orbit at destination.</param>
        /// <param name="shipWorldPos">Ship's world position at departure time.</param>
        /// <param name="approachWorldPos">Target approach point in world coords.</param>
        /// <param name="departureTime">Simulation time of departure.</param>
        /// <param name="travelDuration">Total travel duration in sim-seconds.</param>
        /// <param name="registry">World registry for hierarchy lookups.</param>
        /// <param name="positionResolver">Position resolver delegate.</param>
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

            // Determine relationship between origin and destination.
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
        }

        // ---------------------------------------------------------------
        // Local transfer: same parent or parent/child relationship
        // Segments: Departure → LocalTransfer → Insertion
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
            // Determine the local frame body.
            EntityId frameBodyId = localFrameBodyId.IsValid ? localFrameBodyId : FindBestLocalFrame(originBody, arrivalParent, registry);
            if (!frameBodyId.IsValid) frameBodyId = originBody.Id;

            double depDuration = Math.Max(travelDuration * DepartureFraction, MinSegmentDuration);
            double insDuration = Math.Max(travelDuration * InsertionFraction, MinSegmentDuration);
            double transferDuration = Math.Max(travelDuration - depDuration - insDuration, MinSegmentDuration);

            double t0 = departureTime;
            double t1 = t0 + depDuration;
            double t2 = t1 + transferDuration;
            double t3 = t2 + insDuration;

            // Resolve positions in local frame.
            SimVec3 frameAtT0 = positionResolver != null ? positionResolver(frameBodyId, t0) : SimVec3.Zero;
            SimVec3 frameAtT1 = positionResolver != null ? positionResolver(frameBodyId, t1) : SimVec3.Zero;
            SimVec3 frameAtT2 = positionResolver != null ? positionResolver(frameBodyId, t2) : SimVec3.Zero;

            SimVec3 shipLocalStart = shipWorldPos - frameAtT0;
            SimVec3 approachLocal = approachWorldPos - frameAtT2;

            // Interpolate an intermediate point for the departure endpoint.
            double depFrac = depDuration / travelDuration;
            SimVec3 depEndLocal = Lerp(shipLocalStart, approachLocal, depFrac);

            // Departure segment.
            var departure = new RouteSegment
            {
                Type = SegmentType.LocalOrbitDeparture,
                ReferenceBodyId = originBody.Id,
                StartTime = t0,
                Duration = depDuration,
                StartLocalPosition = shipWorldPos - (positionResolver != null ? positionResolver(originBody.Id, t0) : SimVec3.Zero),
                EndLocalPosition = (frameAtT0 + depEndLocal) - (positionResolver != null ? positionResolver(originBody.Id, t1) : SimVec3.Zero),
                CachedWorldStart = shipWorldPos,
                CachedWorldEnd = frameAtT1 + depEndLocal
            };
            route.Segments.Add(departure);

            // Local transfer segment.
            var transfer = new RouteSegment
            {
                Type = SegmentType.LocalTransfer,
                ReferenceBodyId = frameBodyId,
                StartTime = t1,
                Duration = transferDuration,
                StartLocalPosition = depEndLocal,
                EndLocalPosition = approachLocal,
                CachedWorldStart = frameAtT1 + depEndLocal,
                CachedWorldEnd = approachWorldPos
            };
            route.Segments.Add(transfer);

            // Orbit insertion segment.
            var insertion = new RouteSegment
            {
                Type = SegmentType.OrbitInsertion,
                ReferenceBodyId = arrivalParent.Id,
                StartTime = t2,
                Duration = insDuration,
                StartLocalPosition = approachWorldPos - (positionResolver != null ? positionResolver(arrivalParent.Id, t2) : SimVec3.Zero),
                EndLocalPosition = ComputeInsertionTarget(route, approachWorldPos, arrivalParent, positionResolver, t2),
                CachedWorldStart = approachWorldPos,
                DestinationOrbitRadius = route.DestinationOrbitRadius,
                DestinationOrbitPeriod = route.DestinationOrbitPeriod,
                ArrivalAngleDeg = route.ArrivalOrbitPhaseDeg
            };
            route.Segments.Add(insertion);
        }

        // ---------------------------------------------------------------
        // Interplanetary transfer: different parents, through star frame
        // Segments: Departure → SOIExit → HeliocentricTransfer → SOIEntry → Insertion
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
            // Use common parent (usually star) as heliocentric frame.
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
            double t5 = t4 + insDuration;

            // Compute interpolation waypoints along the straight-line path.
            double frac1 = (depDuration) / travelDuration;
            double frac2 = (depDuration + exitDuration) / travelDuration;
            double frac3 = (depDuration + exitDuration + cruiseDuration) / travelDuration;
            double frac4 = (depDuration + exitDuration + cruiseDuration + entryDuration) / travelDuration;

            SimVec3 wp1 = Lerp(shipWorldPos, approachWorldPos, frac1);
            SimVec3 wp2 = Lerp(shipWorldPos, approachWorldPos, frac2);
            SimVec3 wp3 = Lerp(shipWorldPos, approachWorldPos, frac3);
            SimVec3 wp4 = Lerp(shipWorldPos, approachWorldPos, frac4);

            // 1. Departure from origin body.
            SimVec3 originAtT0 = positionResolver != null ? positionResolver(originBody.Id, t0) : SimVec3.Zero;
            SimVec3 originAtT1 = positionResolver != null ? positionResolver(originBody.Id, t1) : SimVec3.Zero;

            route.Segments.Add(new RouteSegment
            {
                Type = SegmentType.LocalOrbitDeparture,
                ReferenceBodyId = originBody.Id,
                StartTime = t0,
                Duration = depDuration,
                StartLocalPosition = shipWorldPos - originAtT0,
                EndLocalPosition = wp1 - originAtT1,
                CachedWorldStart = shipWorldPos,
                CachedWorldEnd = wp1
            });

            // 2. SOI Exit — transition from origin SOI to heliocentric frame.
            // Use origin's parent as the frame (one level up).
            EntityId originParentId = originBody.ParentId.IsValid ? originBody.ParentId : helioFrameId;
            SimVec3 originParentAtT1 = positionResolver != null && originParentId.IsValid
                ? positionResolver(originParentId, t1) : SimVec3.Zero;
            SimVec3 originParentAtT2 = positionResolver != null && originParentId.IsValid
                ? positionResolver(originParentId, t2) : SimVec3.Zero;

            route.Segments.Add(new RouteSegment
            {
                Type = SegmentType.SOIExit,
                ReferenceBodyId = originParentId,
                StartTime = t1,
                Duration = exitDuration,
                StartLocalPosition = wp1 - originParentAtT1,
                EndLocalPosition = wp2 - originParentAtT2,
                CachedWorldStart = wp1,
                CachedWorldEnd = wp2
            });

            // 3. Heliocentric transfer (cruise).
            SimVec3 helioAtT2 = positionResolver != null && helioFrameId.IsValid
                ? positionResolver(helioFrameId, t2) : SimVec3.Zero;
            SimVec3 helioAtT3 = positionResolver != null && helioFrameId.IsValid
                ? positionResolver(helioFrameId, t3) : SimVec3.Zero;

            route.Segments.Add(new RouteSegment
            {
                Type = SegmentType.HeliocentricTransfer,
                ReferenceBodyId = helioFrameId,
                StartTime = t2,
                Duration = cruiseDuration,
                StartLocalPosition = wp2 - helioAtT2,
                EndLocalPosition = wp3 - helioAtT3,
                CachedWorldStart = wp2,
                CachedWorldEnd = wp3
            });

            // 4. SOI Entry — transition into destination SOI.
            SimVec3 destAtT3 = positionResolver != null ? positionResolver(arrivalParent.Id, t3) : SimVec3.Zero;
            SimVec3 destAtT4 = positionResolver != null ? positionResolver(arrivalParent.Id, t4) : SimVec3.Zero;

            route.Segments.Add(new RouteSegment
            {
                Type = SegmentType.SOIEntry,
                ReferenceBodyId = arrivalParent.Id,
                StartTime = t3,
                Duration = entryDuration,
                StartLocalPosition = wp3 - destAtT3,
                EndLocalPosition = wp4 - destAtT4,
                CachedWorldStart = wp3,
                CachedWorldEnd = wp4
            });

            // 5. Orbit insertion.
            SimVec3 destAtT4b = positionResolver != null ? positionResolver(arrivalParent.Id, t4) : SimVec3.Zero;

            route.Segments.Add(new RouteSegment
            {
                Type = SegmentType.OrbitInsertion,
                ReferenceBodyId = arrivalParent.Id,
                StartTime = t4,
                Duration = insDuration,
                StartLocalPosition = wp4 - destAtT4b,
                EndLocalPosition = ComputeInsertionTarget(route, approachWorldPos, arrivalParent, positionResolver, t4),
                CachedWorldStart = wp4,
                DestinationOrbitRadius = route.DestinationOrbitRadius,
                DestinationOrbitPeriod = route.DestinationOrbitPeriod,
                ArrivalAngleDeg = route.ArrivalOrbitPhaseDeg
            });
        }

        // ---------------------------------------------------------------
        // Hierarchy analysis helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// Find the common parent body of two bodies by walking up both parent chains.
        /// Returns EntityId.None if no common parent is found (shouldn't happen in a
        /// single star system).
        /// </summary>
        public static EntityId FindCommonParent(
            CelestialBody bodyA,
            CelestialBody bodyB,
            WorldRegistry registry)
        {
            if (bodyA == null || bodyB == null) return EntityId.None;

            // Collect ancestors of A.
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

            // Walk up B's chain until we find a common ancestor.
            current = bodyB;
            depth = 0;
            while (current != null && depth < 20)
            {
                if (ancestorsA.Contains(current.Id))
                    return current.Id;
                if (!current.ParentId.IsValid) break;
                current = registry.GetCelestialBody(current.ParentId);
                depth++;
            }

            return EntityId.None;
        }

        /// <summary>
        /// Determine if this is a local transfer (within the same non-star parent's SOI)
        /// or an interplanetary transfer (requires going through the star frame).
        /// </summary>
        private static bool IsLocalTransfer(
            CelestialBody originBody,
            CelestialBody arrivalParent,
            EntityId commonParentId,
            WorldRegistry registry)
        {
            // Direct parent/child relationship.
            if (originBody.ParentId == arrivalParent.Id) return true;
            if (arrivalParent.ParentId == originBody.Id) return true;

            // Same parent, and parent is not a star.
            if (originBody.ParentId.IsValid && originBody.ParentId == arrivalParent.ParentId)
            {
                var parent = registry.GetCelestialBody(originBody.ParentId);
                if (parent != null && parent.BodyType != CelestialBodyType.Star)
                    return true;
            }

            // Common parent is not a star (e.g. both are moons of the same planet).
            if (commonParentId.IsValid)
            {
                var common = registry.GetCelestialBody(commonParentId);
                if (common != null && common.BodyType != CelestialBodyType.Star
                    && common.Id != originBody.Id && common.Id != arrivalParent.Id)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Find the best local frame body for a local transfer.
        /// </summary>
        private static EntityId FindBestLocalFrame(
            CelestialBody originBody,
            CelestialBody arrivalParent,
            WorldRegistry registry)
        {
            // Parent/child: use the parent.
            if (originBody.ParentId == arrivalParent.Id) return arrivalParent.Id;
            if (arrivalParent.ParentId == originBody.Id) return originBody.Id;

            // Same parent.
            if (originBody.ParentId.IsValid && originBody.ParentId == arrivalParent.ParentId)
                return originBody.ParentId;

            return EntityId.None;
        }

        /// <summary>
        /// Compute the orbit insertion target position in local coords relative to arrivalParent.
        /// </summary>
        private static SimVec3 ComputeInsertionTarget(
            ShipRoute route,
            SimVec3 approachWorldPos,
            CelestialBody arrivalParent,
            Func<EntityId, double, SimVec3> positionResolver,
            double time)
        {
            if (positionResolver == null || arrivalParent == null)
                return SimVec3.Zero;

            SimVec3 parentPos = positionResolver(arrivalParent.Id, time);
            SimVec3 localApproach = approachWorldPos - parentPos;

            // Compute direction from parent to approach point.
            double mag = localApproach.Magnitude;
            if (mag < 1e-10)
                return new SimVec3(route.DestinationOrbitRadius, 0.0, 0.0);

            // Scale to orbit radius.
            double scale = route.DestinationOrbitRadius / mag;
            return new SimVec3(
                localApproach.X * scale,
                localApproach.Y * scale,
                localApproach.Z * scale);
        }

        private static SimVec3 Lerp(SimVec3 a, SimVec3 b, double t)
        {
            return new SimVec3(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t,
                a.Z + (b.Z - a.Z) * t);
        }
    }
}
