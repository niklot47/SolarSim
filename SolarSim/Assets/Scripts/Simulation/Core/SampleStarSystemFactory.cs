using SpaceSim.World.Entities;
using SpaceSim.World.Systems;
using SpaceSim.World.ValueTypes;

namespace SpaceSim.Simulation.Core
{
    /// <summary>
    /// Creates a simple sample star system for sandbox testing.
    /// Populates WorldRegistry with bodies and parent-child relationships.
    /// Includes sample ships for testing ship foundation.
    /// </summary>
    public static class SampleStarSystemFactory
    {
        /// <summary>
        /// Create a minimal test system: 1 star, 2 planets, 1 moon, 3 ships.
        /// </summary>
        public static StarSystem Create(WorldRegistry registry)
        {
            var system = new StarSystem("\u0422\u0435\u0441\u0442\u043e\u0432\u0430\u044f \u0441\u0438\u0441\u0442\u0435\u043c\u0430");
            system.LocalizationKeyName = "system.test";

            // Star.
            var star = new CelestialBody("\u0421\u043e\u043b\u043d\u0446\u0435", CelestialBodyType.Star)
            {
                Radius = 5.0,
                HasSurface = false,
                IsSelectable = true,
                LocalizationKeyName = "body.sol",
                Spin = SpinDefinition.Simple(600.0)
            };

            // Planet 1.
            var planet1 = new CelestialBody("\u0422\u0435\u0440\u0440\u0430", CelestialBodyType.Planet)
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
            var planet2 = new CelestialBody("\u0410\u0440\u0435\u0441", CelestialBodyType.Planet)
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
            var moon = new CelestialBody("\u041b\u0443\u043d\u0430", CelestialBodyType.Moon)
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

            // --- Sample ships ---

            // Player ship orbiting Terra.
            var playerShip = new CelestialBody("\u041a\u043e\u0440\u0432\u0435\u0442 \u00ab\u0410\u0432\u0440\u043e\u0440\u0430\u00bb", CelestialBodyType.Ship)
            {
                Radius = 0.15,
                HasSurface = false,
                IsSelectable = true,
                LocalizationKeyName = "ship.aurora",
                ParentId = planet1.Id,
                AttachmentMode = AttachmentMode.Orbit,
                Orbit = OrbitDefinition.Circular(radius: 3.5, period: 12.0, startAngleDeg: 0.0),
                Spin = new SpinDefinition(),
                ShipInfo = new ShipInfo(ShipRole.Player, "ship_aurora", "Corvette")
            };

            // NPC trader orbiting Terra.
            var traderShip = new CelestialBody("\u0422\u0440\u0430\u043d\u0441\u043f\u043e\u0440\u0442 \u00ab\u041a\u0430\u0440\u0433\u043e-7\u00bb", CelestialBodyType.Ship)
            {
                Radius = 0.12,
                HasSurface = false,
                IsSelectable = true,
                LocalizationKeyName = "ship.cargo7",
                ParentId = planet1.Id,
                AttachmentMode = AttachmentMode.Orbit,
                Orbit = OrbitDefinition.Circular(radius: 4.0, period: 15.0, startAngleDeg: 120.0),
                Spin = new SpinDefinition(),
                ShipInfo = new ShipInfo(ShipRole.Trader, "ship_cargo7", "Freighter")
            };

            // NPC patrol orbiting Ares.
            var patrolShip = new CelestialBody("\u041f\u0430\u0442\u0440\u0443\u043b\u044c \u00ab\u0421\u0442\u0440\u0430\u0436-3\u00bb", CelestialBodyType.Ship)
            {
                Radius = 0.12,
                HasSurface = false,
                IsSelectable = true,
                LocalizationKeyName = "ship.strazh3",
                ParentId = planet2.Id,
                AttachmentMode = AttachmentMode.Orbit,
                Orbit = OrbitDefinition.Circular(radius: 3.0, period: 10.0, startAngleDeg: 200.0),
                Spin = new SpinDefinition(),
                ShipInfo = new ShipInfo(ShipRole.Patrol, "ship_strazh3", "Interceptor")
            };

            // Wire ship -> parent children.
            planet1.AddChildId(playerShip.Id);
            planet1.AddChildId(traderShip.Id);
            planet2.AddChildId(patrolShip.Id);

            // Register ships.
            registry.Add(playerShip);
            registry.Add(traderShip);
            registry.Add(patrolShip);

            system.AddBody(playerShip.Id);
            system.AddBody(traderShip.Id);
            system.AddBody(patrolShip.Id);

            return system;
        }
    }
}
