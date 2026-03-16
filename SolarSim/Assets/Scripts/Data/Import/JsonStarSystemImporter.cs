using System;
using System.Collections.Generic;
using UnityEngine;
using SpaceSim.Simulation.Core;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;

namespace SpaceSim.Data.Import
{
    /// <summary>
    /// Loads a star system from an external JSON file or TextAsset.
    /// Converts JSON DTOs into StarSystemBuildData and invokes StarSystemBuilder.
    ///
    /// This is an adapter between external JSON data and the existing build pipeline.
    /// No simulation logic — only deserialization, validation, and conversion.
    /// Lives in Data layer (allowed to reference UnityEngine for TextAsset/JsonUtility).
    ///
    /// Usage:
    ///   var result = JsonStarSystemImporter.LoadFromTextAsset(textAsset, registry);
    ///   if (result.Success) { /* result.System is ready */ }
    ///
    /// The existing ScriptableObject pipeline (StarSystemDefinition → StarSystemLoader)
    /// is not modified. This is an optional additional data source.
    /// </summary>
    public static class JsonStarSystemImporter
    {
        /// <summary>
        /// Result of a JSON import operation.
        /// </summary>
        public class ImportResult
        {
            /// <summary>Whether the import succeeded.</summary>
            public bool Success { get; set; }

            /// <summary>The built star system. Null if import failed.</summary>
            public StarSystem System { get; set; }

            /// <summary>Validation result with errors and warnings.</summary>
            public JsonValidationResult Validation { get; set; }

            /// <summary>Human-readable status message.</summary>
            public string Message { get; set; }

            /// <summary>Number of bodies loaded.</summary>
            public int BodyCount { get; set; }

            /// <summary>Number of stations loaded.</summary>
            public int StationCount { get; set; }

            /// <summary>Number of ships loaded.</summary>
            public int ShipCount { get; set; }
        }

        /// <summary>
        /// Load a star system from a Unity TextAsset containing JSON.
        /// </summary>
        /// <param name="textAsset">TextAsset with JSON content.</param>
        /// <param name="registry">WorldRegistry to populate.</param>
        /// <returns>Import result with system or errors.</returns>
        public static ImportResult LoadFromTextAsset(TextAsset textAsset, WorldRegistry registry)
        {
            if (textAsset == null)
            {
                return new ImportResult
                {
                    Success = false,
                    Message = "TextAsset is null"
                };
            }

            return LoadFromJson(textAsset.text, registry, textAsset.name);
        }

        /// <summary>
        /// Load a star system from a file path.
        /// </summary>
        /// <param name="filePath">Full path to the JSON file.</param>
        /// <param name="registry">WorldRegistry to populate.</param>
        /// <returns>Import result with system or errors.</returns>
        public static ImportResult LoadFromFile(string filePath, WorldRegistry registry)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return new ImportResult
                {
                    Success = false,
                    Message = "File path is empty"
                };
            }

            try
            {
                string json = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                return LoadFromJson(json, registry, System.IO.Path.GetFileNameWithoutExtension(filePath));
            }
            catch (Exception ex)
            {
                return new ImportResult
                {
                    Success = false,
                    Message = $"Failed to read file '{filePath}': {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Load a star system from a raw JSON string.
        /// Core method used by both TextAsset and file path loading.
        /// </summary>
        /// <param name="json">Raw JSON string.</param>
        /// <param name="registry">WorldRegistry to populate.</param>
        /// <param name="sourceName">Name for logging (file name or asset name).</param>
        /// <returns>Import result with system or errors.</returns>
        public static ImportResult LoadFromJson(string json, WorldRegistry registry, string sourceName = "unknown")
        {
            if (string.IsNullOrEmpty(json))
            {
                return new ImportResult
                {
                    Success = false,
                    Message = "JSON string is empty"
                };
            }

            if (registry == null)
            {
                return new ImportResult
                {
                    Success = false,
                    Message = "WorldRegistry is null"
                };
            }

            // Step 1: Deserialize JSON.
            StarSystemJson dto;
            try
            {
                dto = JsonUtility.FromJson<StarSystemJson>(json);
            }
            catch (Exception ex)
            {
                return new ImportResult
                {
                    Success = false,
                    Message = $"JSON parse error: {ex.Message}"
                };
            }

            if (dto == null)
            {
                return new ImportResult
                {
                    Success = false,
                    Message = "JSON deserialized to null"
                };
            }

            // Step 2: Validate.
            var validation = StarSystemJsonValidator.Validate(dto);
            if (!validation.IsValid)
            {
                return new ImportResult
                {
                    Success = false,
                    Validation = validation,
                    Message = $"Validation failed with {validation.Errors.Count} error(s)"
                };
            }

            // Step 3: Convert DTO to StarSystemBuildData.
            StarSystemBuildData buildData;
            try
            {
                buildData = ConvertToBuildData(dto);
            }
            catch (Exception ex)
            {
                return new ImportResult
                {
                    Success = false,
                    Validation = validation,
                    Message = $"Conversion error: {ex.Message}"
                };
            }

            // Step 4: Build via existing StarSystemBuilder.
            StarSystem system;
            try
            {
                system = StarSystemBuilder.Build(buildData, registry);
            }
            catch (Exception ex)
            {
                return new ImportResult
                {
                    Success = false,
                    Validation = validation,
                    Message = $"Build error: {ex.Message}"
                };
            }

            if (system == null)
            {
                return new ImportResult
                {
                    Success = false,
                    Validation = validation,
                    Message = "StarSystemBuilder returned null"
                };
            }

            int bodyCount = dto.bodies != null ? dto.bodies.Count : 0;
            int stationCount = dto.stations != null ? dto.stations.Count : 0;
            int shipCount = dto.ships != null ? dto.ships.Count : 0;

            return new ImportResult
            {
                Success = true,
                System = system,
                Validation = validation,
                BodyCount = bodyCount,
                StationCount = stationCount,
                ShipCount = shipCount,
                Message = $"Loaded external system '{buildData.DisplayName}' from '{sourceName}': " +
                          $"Bodies={bodyCount}, Stations={stationCount}, Ships={shipCount}"
            };
        }

        // ---------------------------------------------------------------
        // Conversion: JSON DTO → StarSystemBuildData
        // ---------------------------------------------------------------

        /// <summary>
        /// Convert a validated StarSystemJson DTO to StarSystemBuildData.
        /// This is the adapter layer that maps JSON field names to the
        /// existing build data format expected by StarSystemBuilder.
        /// </summary>
        private static StarSystemBuildData ConvertToBuildData(StarSystemJson dto)
        {
            var data = new StarSystemBuildData
            {
                SystemKey = !string.IsNullOrEmpty(dto.systemKey) ? dto.systemKey : dto.systemName,
                DisplayName = !string.IsNullOrEmpty(dto.systemName) ? dto.systemName : dto.systemKey,
                LocalizationKey = dto.localizationKey ?? ""
            };

            // Convert bodies.
            if (dto.bodies != null)
            {
                foreach (var bodyJson in dto.bodies)
                {
                    data.Bodies.Add(ConvertBody(bodyJson));
                }
            }

            // Convert stations.
            if (dto.stations != null)
            {
                foreach (var stationJson in dto.stations)
                {
                    data.Stations.Add(ConvertStation(stationJson));
                }
            }

            // Convert ships.
            if (dto.ships != null)
            {
                foreach (var shipJson in dto.ships)
                {
                    data.Ships.Add(ConvertShip(shipJson));
                }
            }

            return data;
        }

        private static CelestialBodyBuildData ConvertBody(BodyJson bodyJson)
        {
            var bodyType = ParseBodyType(bodyJson.type);
            var attachmentMode = ResolveAttachmentMode(bodyJson);

            var bd = new CelestialBodyBuildData
            {
                Key = bodyJson.key,
                DisplayName = bodyJson.name,
                LocalizationKey = bodyJson.localizationKey ?? "",
                BodyType = (int)bodyType,
                ParentKey = bodyJson.parent ?? "",
                AttachmentMode = (int)attachmentMode,
                Radius = bodyJson.radius,
                IsSelectable = bodyJson.isSelectable,
                HasSurface = bodyJson.hasSurface,
                SOIRadius = bodyJson.soi
            };

            // Orbit parameters.
            if (bodyJson.orbit != null)
            {
                bd.SemiMajorAxis = bodyJson.orbit.semiMajorAxis;
                bd.Eccentricity = bodyJson.orbit.eccentricity;
                bd.InclinationDeg = bodyJson.orbit.inclinationDeg;
                bd.LongitudeOfAscendingNodeDeg = bodyJson.orbit.longitudeOfAscendingNodeDeg;
                bd.ArgumentOfPeriapsisDeg = bodyJson.orbit.argumentOfPeriapsisDeg;
                bd.MeanAnomalyAtEpochDeg = bodyJson.orbit.meanAnomalyAtEpochDeg;
                bd.OrbitalPeriod = bodyJson.orbit.period;
                bd.EpochTime = bodyJson.orbit.epochTime;
                bd.IsPrograde = bodyJson.orbit.isPrograde;
            }

            // Spin parameters.
            if (bodyJson.spin != null)
            {
                bd.AxialTiltDeg = bodyJson.spin.axialTiltDeg;
                bd.RotationPeriod = bodyJson.spin.rotationPeriod;
                bd.InitialRotationDeg = bodyJson.spin.initialRotationDeg;
            }
            else
            {
                // Default spin.
                bd.RotationPeriod = 60.0;
            }

            return bd;
        }

        private static StationBuildData ConvertStation(StationJson stationJson)
        {
            return new StationBuildData
            {
                Key = stationJson.key,
                DisplayName = stationJson.name,
                LocalizationKey = stationJson.localizationKey ?? "",
                Kind = (int)ParseStationKind(stationJson.kind),
                ParentBodyKey = stationJson.parentBody ?? "",
                Radius = stationJson.radius,
                OrbitalRadius = stationJson.orbitalRadius,
                OrbitalPeriod = stationJson.orbitalPeriod,
                StartAngleDeg = stationJson.startAngleDeg,
                RotationPeriod = stationJson.rotationPeriod,
                SurfaceLatitudeDeg = stationJson.surfaceLatitudeDeg,
                SurfaceLongitudeDeg = stationJson.surfaceLongitudeDeg,
                DockingPortCount = stationJson.dockingPortCount
            };
        }

        private static ShipBuildData ConvertShip(ShipJson shipJson)
        {
            return new ShipBuildData
            {
                Key = shipJson.key,
                DisplayName = shipJson.name,
                LocalizationKey = shipJson.localizationKey ?? "",
                Role = (int)ParseShipRole(shipJson.role),
                ShipClass = shipJson.shipClass ?? "",
                ParentBodyKey = shipJson.parentBody ?? "",
                Radius = shipJson.radius,
                OrbitalRadius = shipJson.orbitalRadius,
                OrbitalPeriod = shipJson.orbitalPeriod,
                StartAngleDeg = shipJson.startAngleDeg
            };
        }

        // ---------------------------------------------------------------
        // Enum parsing helpers
        // ---------------------------------------------------------------

        private static CelestialBodyType ParseBodyType(string type)
        {
            if (string.IsNullOrEmpty(type)) return CelestialBodyType.Planet;

            switch (type)
            {
                case "Star": return CelestialBodyType.Star;
                case "Planet": return CelestialBodyType.Planet;
                case "Moon": return CelestialBodyType.Moon;
                case "Asteroid": return CelestialBodyType.Asteroid;
                case "Station": return CelestialBodyType.Station;
                case "Ship": return CelestialBodyType.Ship;
                case "SurfaceSite": return CelestialBodyType.SurfaceSite;
                default: return CelestialBodyType.Planet;
            }
        }

        private static StationKind ParseStationKind(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return StationKind.Orbital;

            switch (kind)
            {
                case "Orbital": return StationKind.Orbital;
                case "Surface": return StationKind.Surface;
                default: return StationKind.Orbital;
            }
        }

        private static ShipRole ParseShipRole(string role)
        {
            if (string.IsNullOrEmpty(role)) return ShipRole.Civilian;

            switch (role)
            {
                case "Player": return ShipRole.Player;
                case "Trader": return ShipRole.Trader;
                case "Patrol": return ShipRole.Patrol;
                case "Civilian": return ShipRole.Civilian;
                default: return ShipRole.Civilian;
            }
        }

        /// <summary>
        /// Resolve attachment mode. If explicitly specified in JSON, use that.
        /// Otherwise: root bodies (no parent) = None, bodies with parent = Orbit.
        /// </summary>
        private static AttachmentMode ResolveAttachmentMode(BodyJson bodyJson)
        {
            // Explicit override.
            if (!string.IsNullOrEmpty(bodyJson.attachmentMode))
            {
                switch (bodyJson.attachmentMode)
                {
                    case "None": return AttachmentMode.None;
                    case "Orbit": return AttachmentMode.Orbit;
                    case "Surface": return AttachmentMode.Surface;
                    case "LocalSpace": return AttachmentMode.LocalSpace;
                }
            }

            // Auto-detect: no parent = None, has parent = Orbit.
            if (string.IsNullOrEmpty(bodyJson.parent))
                return AttachmentMode.None;

            return AttachmentMode.Orbit;
        }
    }
}
