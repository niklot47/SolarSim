using System.Collections.Generic;
using SpaceSim.Data.Import;

namespace SpaceSim.Data.Import
{
    /// <summary>
    /// Validation result for a JSON star system.
    /// </summary>
    public class JsonValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();

        public void AddError(string message) => Errors.Add(message);
        public void AddWarning(string message) => Warnings.Add(message);
    }

    /// <summary>
    /// Validates a StarSystemJson DTO before conversion to build data.
    /// Checks:
    /// - No duplicate keys across bodies, stations, and ships
    /// - Parent keys reference existing body keys
    /// - Orbit parameters are valid (positive period, non-negative semi-major axis)
    /// - Required fields are non-empty
    /// Lives in Data layer.
    /// </summary>
    public static class StarSystemJsonValidator
    {
        /// <summary>
        /// Validate the entire JSON structure.
        /// Returns a result with errors and warnings.
        /// </summary>
        public static JsonValidationResult Validate(StarSystemJson json)
        {
            var result = new JsonValidationResult();

            if (json == null)
            {
                result.AddError("JSON data is null");
                return result;
            }

            if (string.IsNullOrEmpty(json.systemName) && string.IsNullOrEmpty(json.systemKey))
            {
                result.AddWarning("System has no name or key");
            }

            // Collect all body keys for parent reference validation.
            var allBodyKeys = new HashSet<string>();
            var allKeys = new HashSet<string>();

            // Validate bodies.
            if (json.bodies != null)
            {
                for (int i = 0; i < json.bodies.Count; i++)
                {
                    var body = json.bodies[i];
                    ValidateBodyKey(body.key, $"bodies[{i}]", allKeys, allBodyKeys, result);
                    ValidateBodyType(body.type, body.key, result);

                    if (body.orbit != null)
                        ValidateOrbit(body.orbit, body.key, result);

                    if (body.radius <= 0.0)
                        result.AddWarning($"Body '{body.key}' has non-positive radius: {body.radius}");
                }

                // Second pass: validate parent references.
                for (int i = 0; i < json.bodies.Count; i++)
                {
                    var body = json.bodies[i];
                    if (!string.IsNullOrEmpty(body.parent))
                    {
                        if (!allBodyKeys.Contains(body.parent))
                        {
                            result.AddError(
                                $"Body '{body.key}' references parent '{body.parent}' which does not exist");
                        }
                        if (body.parent == body.key)
                        {
                            result.AddError($"Body '{body.key}' is its own parent");
                        }
                    }
                }
            }

            // Validate stations.
            if (json.stations != null)
            {
                for (int i = 0; i < json.stations.Count; i++)
                {
                    var station = json.stations[i];
                    ValidateKey(station.key, $"stations[{i}]", allKeys, result);
                    ValidateStationKind(station.kind, station.key, result);

                    if (!string.IsNullOrEmpty(station.parentBody) && !allBodyKeys.Contains(station.parentBody))
                    {
                        result.AddError(
                            $"Station '{station.key}' references parent body '{station.parentBody}' which does not exist");
                    }

                    if (string.IsNullOrEmpty(station.parentBody))
                    {
                        result.AddError($"Station '{station.key}' has no parent body");
                    }

                    if (station.dockingPortCount < 0)
                    {
                        result.AddWarning($"Station '{station.key}' has negative docking port count");
                    }
                }
            }

            // Validate ships.
            if (json.ships != null)
            {
                for (int i = 0; i < json.ships.Count; i++)
                {
                    var ship = json.ships[i];
                    ValidateKey(ship.key, $"ships[{i}]", allKeys, result);
                    ValidateShipRole(ship.role, ship.key, result);

                    if (!string.IsNullOrEmpty(ship.parentBody) && !allBodyKeys.Contains(ship.parentBody))
                    {
                        result.AddError(
                            $"Ship '{ship.key}' references parent body '{ship.parentBody}' which does not exist");
                    }

                    if (string.IsNullOrEmpty(ship.parentBody))
                    {
                        result.AddError($"Ship '{ship.key}' has no parent body");
                    }
                }
            }

            return result;
        }

        private static void ValidateBodyKey(
            string key, string context,
            HashSet<string> allKeys, HashSet<string> bodyKeys,
            JsonValidationResult result)
        {
            if (string.IsNullOrEmpty(key))
            {
                result.AddError($"{context} has empty key");
                return;
            }

            if (!allKeys.Add(key))
            {
                result.AddError($"Duplicate key '{key}' at {context}");
            }
            bodyKeys.Add(key);
        }

        private static void ValidateKey(
            string key, string context,
            HashSet<string> allKeys,
            JsonValidationResult result)
        {
            if (string.IsNullOrEmpty(key))
            {
                result.AddError($"{context} has empty key");
                return;
            }

            if (!allKeys.Add(key))
            {
                result.AddError($"Duplicate key '{key}' at {context}");
            }
        }

        private static void ValidateOrbit(OrbitJson orbit, string bodyKey, JsonValidationResult result)
        {
            if (orbit.semiMajorAxis < 0.0)
            {
                result.AddError($"Body '{bodyKey}' orbit has negative semiMajorAxis: {orbit.semiMajorAxis}");
            }

            if (orbit.period <= 0.0)
            {
                result.AddError($"Body '{bodyKey}' orbit has non-positive period: {orbit.period}");
            }

            if (orbit.eccentricity < 0.0 || orbit.eccentricity >= 1.0)
            {
                result.AddWarning($"Body '{bodyKey}' orbit eccentricity out of [0,1): {orbit.eccentricity}");
            }
        }

        private static readonly HashSet<string> ValidBodyTypes = new HashSet<string>
        {
            "Star", "Planet", "Moon", "Asteroid", "Station", "Ship", "SurfaceSite"
        };

        private static void ValidateBodyType(string type, string bodyKey, JsonValidationResult result)
        {
            if (string.IsNullOrEmpty(type))
            {
                result.AddWarning($"Body '{bodyKey}' has empty type, defaulting to Planet");
                return;
            }

            if (!ValidBodyTypes.Contains(type))
            {
                result.AddError($"Body '{bodyKey}' has unknown type: '{type}'");
            }
        }

        private static readonly HashSet<string> ValidStationKinds = new HashSet<string>
        {
            "Orbital", "Surface"
        };

        private static void ValidateStationKind(string kind, string stationKey, JsonValidationResult result)
        {
            if (string.IsNullOrEmpty(kind))
            {
                result.AddWarning($"Station '{stationKey}' has empty kind, defaulting to Orbital");
                return;
            }

            if (!ValidStationKinds.Contains(kind))
            {
                result.AddError($"Station '{stationKey}' has unknown kind: '{kind}'");
            }
        }

        private static readonly HashSet<string> ValidShipRoles = new HashSet<string>
        {
            "Player", "Trader", "Patrol", "Civilian"
        };

        private static void ValidateShipRole(string role, string shipKey, JsonValidationResult result)
        {
            if (string.IsNullOrEmpty(role))
            {
                result.AddWarning($"Ship '{shipKey}' has empty role, defaulting to Civilian");
                return;
            }

            if (!ValidShipRoles.Contains(role))
            {
                result.AddError($"Ship '{shipKey}' has unknown role: '{role}'");
            }
        }
    }
}
