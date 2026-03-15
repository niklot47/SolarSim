using UnityEngine;
using SpaceSim.World.Entities;

namespace SpaceSim.UI.Core
{
    /// <summary>
    /// Resolves icon textures for celestial bodies based on their type.
    /// Icons are loaded from Resources at:
    ///   Assets/UI/Icons/32x32/   — small list icons
    ///   Assets/UI/Icons/300x300/ — large detail/modal icons
    ///
    /// Icon filenames match body classifications:
    ///   star, planet, planet_rings, moon, asteroid, dwarf_planet,
    ///   orbital_station, spaceport, ship, comet, asteroid_field, wreckage
    ///
    /// To work with Resources.Load, icons must also be placed in a
    /// Resources folder. Recommended: Assets/Resources/Icons/32x32/ and
    /// Assets/Resources/Icons/300x300/ (or use Addressables in future).
    ///
    /// If icon is not found, returns null (caller should fall back to
    /// colored circle placeholder).
    /// </summary>
    public static class BodyIconResolver
    {
        private const string SmallIconPath = "Icons/32x32/";
        private const string LargeIconPath = "Icons/300x300/";

        /// <summary>
        /// Get the small (32x32) icon for a body. Returns null if not found.
        /// </summary>
        public static Texture2D GetSmallIcon(CelestialBody body)
        {
            string name = GetIconName(body);
            if (string.IsNullOrEmpty(name)) return null;
            return Resources.Load<Texture2D>(SmallIconPath + name);
        }

        /// <summary>
        /// Get the large (300x300) icon for a body. Returns null if not found.
        /// </summary>
        public static Texture2D GetLargeIcon(CelestialBody body)
        {
            string name = GetIconName(body);
            if (string.IsNullOrEmpty(name)) return null;
            return Resources.Load<Texture2D>(LargeIconPath + name);
        }

        /// <summary>
        /// Get the icon filename (without extension) for a body.
        /// </summary>
        public static string GetIconName(CelestialBody body)
        {
            if (body == null) return null;

            // Station sub-types.
            if (body.BodyType == CelestialBodyType.Station && body.StationInfo != null)
            {
                return body.StationInfo.Kind switch
                {
                    StationKind.Orbital => "orbital_station",
                    StationKind.Surface => "spaceport",
                    _ => "orbital_station"
                };
            }

            // Ship.
            if (body.BodyType == CelestialBodyType.Ship)
            {
                return "ship";
            }

            // Standard body types.
            return body.BodyType switch
            {
                CelestialBodyType.Star => "star",
                CelestialBodyType.Planet => "planet",
                CelestialBodyType.Moon => "moon",
                CelestialBodyType.Asteroid => "asteroid",
                CelestialBodyType.SurfaceSite => "spaceport",
                _ => "planet"
            };
        }
    }
}
