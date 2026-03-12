using SpaceSim.World.Entities;
using SpaceSim.World.Systems;
using SpaceSim.World.ValueTypes;

namespace SpaceSim.Simulation.Core
{
    /// <summary>
    /// Creates a simple sample star system for sandbox testing.
    /// Populates WorldRegistry with bodies and parent-child relationships.
    /// </summary>
    public static class SampleStarSystemFactory
    {
        /// <summary>
        /// Create a minimal test system: 1 star, 2 planets, 1 moon.
        /// </summary>
        public static StarSystem Create(WorldRegistry registry)
        {
            var system = new StarSystem("Тестовая система");
            system.LocalizationKeyName = "system.test";

            // Star.
            var star = new CelestialBody("Солнце", CelestialBodyType.Star)
            {
                Radius = 5.0,
                HasSurface = false,
                IsSelectable = true,
                LocalizationKeyName = "body.sol",
                Spin = SpinDefinition.Simple(600.0)
            };

            // Planet 1.
            var planet1 = new CelestialBody("Терра", CelestialBodyType.Planet)
            {
                Radius = 1.5,
                HasSurface = true,
                IsSelectable = true,
                LocalizationKeyName = "body.terra",
                ParentId = star.Id,
                AttachmentMode = AttachmentMode.Orbit,
                Orbit = OrbitDefinition.Circular(radius: 30.0, period: 120.0, startAngleDeg: 0.0),
                Spin = SpinDefinition.Simple(30.0)
            };

            // Planet 2.
            var planet2 = new CelestialBody("Арес", CelestialBodyType.Planet)
            {
                Radius = 1.0,
                HasSurface = true,
                IsSelectable = true,
                LocalizationKeyName = "body.ares",
                ParentId = star.Id,
                AttachmentMode = AttachmentMode.Orbit,
                Orbit = OrbitDefinition.Circular(radius: 55.0, period: 240.0, startAngleDeg: 90.0),
                Spin = SpinDefinition.Simple(25.0)
            };

            // Moon of planet 1.
            var moon = new CelestialBody("Луна", CelestialBodyType.Moon)
            {
                Radius = 0.4,
                HasSurface = true,
                IsSelectable = true,
                LocalizationKeyName = "body.luna",
                ParentId = planet1.Id,
                AttachmentMode = AttachmentMode.Orbit,
                Orbit = OrbitDefinition.Circular(radius: 5.0, period: 20.0, startAngleDeg: 45.0),
                Spin = SpinDefinition.Simple(20.0)
            };

            // Wire parent-child relationships.
            star.AddChildId(planet1.Id);
            star.AddChildId(planet2.Id);
            planet1.AddChildId(moon.Id);

            // Register all in registry.
            registry.Add(star);
            registry.Add(planet1);
            registry.Add(planet2);
            registry.Add(moon);

            // Register in system.
            system.AddBody(star.Id, isRoot: true);
            system.AddBody(planet1.Id);
            system.AddBody(planet2.Id);
            system.AddBody(moon.Id);

            return system;
        }
    }
}
