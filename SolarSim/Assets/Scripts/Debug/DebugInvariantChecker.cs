using System;
using System.Collections.Generic;
using SpaceSim.Shared.Identifiers;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;

namespace SpaceSim.Debug
{
    /// <summary>
    /// Runs invariant validation checks on the current world state.
    /// Detects obviously broken values that indicate bugs:
    /// NaN/Infinity coordinates, duplicate ids, negative cargo, etc.
    ///
    /// All checks are defensive — they catch exceptions internally
    /// and never crash the game.
    /// </summary>
    public class DebugInvariantChecker
    {
        private readonly WorldRegistry _registry;

        public DebugInvariantChecker(WorldRegistry registry)
        {
            _registry = registry;
        }

        /// <summary>
        /// Run all invariant checks and return violations found.
        /// Each violation is also logged as an ERROR debug event.
        /// </summary>
        public List<InvariantViolation> RunAll()
        {
            var violations = new List<InvariantViolation>();

            try { CheckNaNPositions(violations); } catch (Exception ex) { AddCheckerError(violations, "NaN Positions", ex); }
            try { CheckDuplicateIds(violations); } catch (Exception ex) { AddCheckerError(violations, "Duplicate IDs", ex); }
            try { CheckNegativeCargo(violations); } catch (Exception ex) { AddCheckerError(violations, "Negative Cargo", ex); }
            try { CheckNegativeStorage(violations); } catch (Exception ex) { AddCheckerError(violations, "Negative Storage", ex); }
            try { CheckOrphanedChildren(violations); } catch (Exception ex) { AddCheckerError(violations, "Orphaned Children", ex); }
            try { CheckDockingConsistency(violations); } catch (Exception ex) { AddCheckerError(violations, "Docking Consistency", ex); }
            try { CheckSelfParenting(violations); } catch (Exception ex) { AddCheckerError(violations, "Self-Parenting", ex); }

            // Log all violations as error events.
            foreach (var v in violations)
            {
                GameDebug.LogError(
                    DebugCategory.ERROR,
                    $"[Invariant:{v.CheckName}] {v.Message} entity={v.EntityName}({v.EntityId})",
                    source: "InvariantChecker");
            }

            return violations;
        }

        /// <summary>
        /// Check that no tracked entity has NaN or Infinity in orbit parameters.
        /// </summary>
        private void CheckNaNPositions(List<InvariantViolation> violations)
        {
            if (_registry == null) return;

            foreach (var body in _registry.AllCelestialBodies)
            {
                // Check orbit parameters.
                if (body.Orbit != null)
                {
                    if (IsInvalidDouble(body.Orbit.SemiMajorAxis) ||
                        IsInvalidDouble(body.Orbit.OrbitalPeriod) ||
                        IsInvalidDouble(body.Orbit.MeanAnomalyAtEpochDeg))
                    {
                        violations.Add(new InvariantViolation(
                            "NaN/Inf Orbit",
                            $"Orbit has NaN/Infinity values (a={body.Orbit.SemiMajorAxis}, P={body.Orbit.OrbitalPeriod})",
                            body.Id.ToString(), body.DisplayName));
                    }
                }

                // Check override world position for ships.
                if (body.ShipInfo?.OverrideWorldPosition.HasValue == true)
                {
                    var pos = body.ShipInfo.OverrideWorldPosition.Value;
                    if (IsInvalidDouble(pos.X) || IsInvalidDouble(pos.Y) || IsInvalidDouble(pos.Z))
                    {
                        violations.Add(new InvariantViolation(
                            "NaN/Inf Position",
                            $"Ship has NaN/Infinity override position ({pos})",
                            body.Id.ToString(), body.DisplayName));
                    }
                }

                // Check radius.
                if (IsInvalidDouble(body.Radius) || body.Radius < 0.0)
                {
                    violations.Add(new InvariantViolation(
                        "Invalid Radius",
                        $"Body has invalid radius: {body.Radius}",
                        body.Id.ToString(), body.DisplayName));
                }
            }
        }

        /// <summary>
        /// Check for duplicate EntityIds in the registry.
        /// (WorldRegistry uses Dictionary so true duplicates are impossible,
        /// but we check for duplicate display names among same-type entities
        /// which might indicate a builder bug.)
        /// </summary>
        private void CheckDuplicateIds(List<InvariantViolation> violations)
        {
            if (_registry == null) return;

            var seen = new HashSet<ulong>();
            foreach (var body in _registry.AllCelestialBodies)
            {
                if (!seen.Add(body.Id.Value))
                {
                    violations.Add(new InvariantViolation(
                        "Duplicate EntityId",
                        $"Duplicate id found in registry",
                        body.Id.ToString(), body.DisplayName));
                }
            }
        }

        /// <summary>
        /// Check that no ship has negative cargo amounts.
        /// </summary>
        private void CheckNegativeCargo(List<InvariantViolation> violations)
        {
            if (_registry == null) return;

            foreach (var body in _registry.AllCelestialBodies)
            {
                if (body.ShipInfo?.Cargo == null) continue;

                foreach (var kvp in body.ShipInfo.Cargo.GetAll())
                {
                    if (kvp.Value < -0.001)
                    {
                        violations.Add(new InvariantViolation(
                            "Negative Cargo",
                            $"Ship cargo has negative {kvp.Key}: {kvp.Value:F2}",
                            body.Id.ToString(), body.DisplayName));
                    }
                }

                if (body.ShipInfo.Cargo.TotalUsed > body.ShipInfo.Cargo.Capacity + 0.01)
                {
                    violations.Add(new InvariantViolation(
                        "Cargo Over Capacity",
                        $"Ship cargo exceeds capacity: {body.ShipInfo.Cargo.TotalUsed:F1}/{body.ShipInfo.Cargo.Capacity:F1}",
                        body.Id.ToString(), body.DisplayName));
                }
            }
        }

        /// <summary>
        /// Check that no station has negative storage amounts.
        /// </summary>
        private void CheckNegativeStorage(List<InvariantViolation> violations)
        {
            if (_registry == null) return;

            foreach (var body in _registry.AllCelestialBodies)
            {
                if (body.StationInfo?.Storage == null) continue;

                foreach (var kvp in body.StationInfo.Storage.GetAll())
                {
                    if (kvp.Value < -0.001)
                    {
                        violations.Add(new InvariantViolation(
                            "Negative Storage",
                            $"Station storage has negative {kvp.Key}: {kvp.Value:F2}",
                            body.Id.ToString(), body.DisplayName));
                    }
                }
            }
        }

        /// <summary>
        /// Check that child ids actually exist in the registry.
        /// </summary>
        private void CheckOrphanedChildren(List<InvariantViolation> violations)
        {
            if (_registry == null) return;

            foreach (var body in _registry.AllCelestialBodies)
            {
                foreach (var childId in body.ChildIds)
                {
                    if (!_registry.Contains(childId))
                    {
                        violations.Add(new InvariantViolation(
                            "Orphaned Child",
                            $"Child {childId} not found in registry",
                            body.Id.ToString(), body.DisplayName));
                    }
                }
            }
        }

        /// <summary>
        /// Check docking state consistency:
        /// - Docked ship must reference a valid station.
        /// - Docked ship's port must show the ship as occupant.
        /// </summary>
        private void CheckDockingConsistency(List<InvariantViolation> violations)
        {
            if (_registry == null) return;

            foreach (var body in _registry.AllCelestialBodies)
            {
                if (body.ShipInfo == null) continue;
                if (body.ShipInfo.State != ShipState.Docked) continue;

                var stationId = body.ShipInfo.DockedAtStationId;
                if (!stationId.IsValid)
                {
                    violations.Add(new InvariantViolation(
                        "Docked No Station",
                        "Ship is Docked but DockedAtStationId is invalid",
                        body.Id.ToString(), body.DisplayName));
                    continue;
                }

                var station = _registry.GetCelestialBody(stationId);
                if (station == null)
                {
                    violations.Add(new InvariantViolation(
                        "Docked Missing Station",
                        $"Ship docked at station {stationId} but station not found",
                        body.Id.ToString(), body.DisplayName));
                    continue;
                }

                // Check port occupancy.
                if (station.StationInfo?.Docking != null)
                {
                    int portId = body.ShipInfo.DockedPortId;
                    var ports = station.StationInfo.Docking.Ports;
                    if (portId >= 0 && portId < ports.Count)
                    {
                        if (ports[portId].OccupiedShipId != body.Id)
                        {
                            violations.Add(new InvariantViolation(
                                "Port Mismatch",
                                $"Ship says docked at port {portId} but port shows occupant={ports[portId].OccupiedShipId}",
                                body.Id.ToString(), body.DisplayName));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Check that no body is its own parent.
        /// </summary>
        private void CheckSelfParenting(List<InvariantViolation> violations)
        {
            if (_registry == null) return;

            foreach (var body in _registry.AllCelestialBodies)
            {
                if (body.ParentId.IsValid && body.ParentId == body.Id)
                {
                    violations.Add(new InvariantViolation(
                        "Self-Parent",
                        "Body is its own parent",
                        body.Id.ToString(), body.DisplayName));
                }
            }
        }

        private static void AddCheckerError(List<InvariantViolation> violations, string checkName, Exception ex)
        {
            violations.Add(new InvariantViolation(
                checkName,
                $"Checker itself threw: {ex.GetType().Name}: {ex.Message}"));
        }

        private static bool IsInvalidDouble(double v)
        {
            return double.IsNaN(v) || double.IsInfinity(v);
        }
    }
}
