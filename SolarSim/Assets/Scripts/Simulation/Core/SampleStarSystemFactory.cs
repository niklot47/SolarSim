using SpaceSim.World.Entities;
using SpaceSim.World.Systems;
using SpaceSim.World.ValueTypes;

namespace SpaceSim.Simulation.Core
{
    /// <summary>
    /// Creates a simple sample star system for sandbox testing.
    /// Populates WorldRegistry with bodies and parent-child relationships.
    /// Includes sample ships and stations for testing.
    /// </summary>
    public static class SampleStarSystemFactory
    {
        /// <summary>
        /// Create a minimal test system: 1 star, 3 planets, 1 moon, 3 ships, 4 stations.
        /// Body radii are scaled down and orbit radii scaled up for visual clarity.
        /// SOI values are approximate placeholders for testing SOI resolver.
        /// Orbital and surface stations have docking ports.
        /// </summary>
        public static StarSystem Create(WorldRegistry registry)
        {
            var system = new StarSystem("\u0422\u0435\u0441\u0442\u043e\u0432\u0430\u044f \u0441\u0438\u0441\u0442\u0435\u043c\u0430");
            system.LocalizationKeyName = "system.test";

            // Star.
            var star = new CelestialBody("\u0421\u043e\u043b\u043d\u0446\u0435", CelestialBodyType.Star)
            {
                Radius = 1.0,
                HasSurface = false,
                IsSelectable = true,
                LocalizationKeyName = "body.sol",
                Spin = SpinDefinition.Simple(600.0),
                SOIRadius = 1000.0
            };

            // Terra.
            var planet1 = new CelestialBody("\u0422\u0435\u0440\u0440\u0430", CelestialBodyType.Planet)
            {
                Radius = 0.3,
                HasSurface = true,
                IsSelectable = true,
                LocalizationKeyName = "body.terra",
                ParentId = star.Id,
                AttachmentMode = AttachmentMode.Orbit,
                Orbit = OrbitDefinition.Circular(radius: 150.0, period: 120.0, startAngleDeg: 0.0),
                Spin = SpinDefinition.Simple(30.0),
                SOIRadius = 60.0
            };

            // Ares.
            var planet2 = new CelestialBody("\u0410\u0440\u0435\u0441", CelestialBodyType.Planet)
            {
                Radius = 0.2,
                HasSurface = true,
                IsSelectable = true,
                LocalizationKeyName = "body.ares",
                ParentId = star.Id,
                AttachmentMode = AttachmentMode.Orbit,
                Orbit = OrbitDefinition.Circular(radius: 275.0, period: 240.0, startAngleDeg: 90.0),
                Spin = SpinDefinition.Simple(25.0),
                SOIRadius = 40.0
            };

            // Venus.
            var venus = new CelestialBody("\u0412\u0435\u043d\u0435\u0440\u0430", CelestialBodyType.Planet)
            {
                Radius = 0.16,
                HasSurface = false,
                IsSelectable = true,
                LocalizationKeyName = "body.venus",
                ParentId = star.Id,
                AttachmentMode = AttachmentMode.Orbit,
                Orbit = OrbitDefinition.Circular(radius: 75.0, period: 80.0, startAngleDeg: 45.0),
                Spin = SpinDefinition.Simple(20.0),
                SOIRadius = 30.0
            };

            // Luna.
            var moon = new CelestialBody("\u041b\u0443\u043d\u0430", CelestialBodyType.Moon)
            {
                Radius = 0.08,
                HasSurface = true,
                IsSelectable = true,
                LocalizationKeyName = "body.luna",
                ParentId = planet1.Id,
                AttachmentMode = AttachmentMode.Orbit,
                Orbit = OrbitDefinition.Circular(radius: 25.0, period: 20.0, startAngleDeg: 45.0),
                Spin = SpinDefinition.Simple(20.0),
                SOIRadius = 8.0
            };

            // Wire parent-child.
            star.AddChildId(planet1.Id);
            star.AddChildId(planet2.Id);
            star.AddChildId(venus.Id);
            planet1.AddChildId(moon.Id);

            registry.Add(star);
            registry.Add(planet1);
            registry.Add(planet2);
            registry.Add(venus);
            registry.Add(moon);

            system.AddBody(star.Id, isRoot: true);
            system.AddBody(planet1.Id);
            system.AddBody(planet2.Id);
            system.AddBody(venus.Id);
            system.AddBody(moon.Id);

            // --- Ships ---

            var playerShip = new CelestialBody("\u041a\u043e\u0440\u0432\u0435\u0442 \u00ab\u0410\u0432\u0440\u043e\u0440\u0430\u00bb", CelestialBodyType.Ship)
            {
                Radius = 0.03, HasSurface = false, IsSelectable = true,
                LocalizationKeyName = "ship.aurora",
                ParentId = planet1.Id, AttachmentMode = AttachmentMode.Orbit,
                Orbit = OrbitDefinition.Circular(radius: 3.0, period: 12.0, startAngleDeg: 0.0),
                Spin = new SpinDefinition(),
                ShipInfo = new ShipInfo(ShipRole.Player, "ship_aurora", "Corvette")
            };

            var traderShip = new CelestialBody("\u0422\u0440\u0430\u043d\u0441\u043f\u043e\u0440\u0442 \u00ab\u041a\u0430\u0440\u0433\u043e-7\u00bb", CelestialBodyType.Ship)
            {
                Radius = 0.024, HasSurface = false, IsSelectable = true,
                LocalizationKeyName = "ship.cargo7",
                ParentId = planet1.Id, AttachmentMode = AttachmentMode.Orbit,
                Orbit = OrbitDefinition.Circular(radius: 4.0, period: 15.0, startAngleDeg: 120.0),
                Spin = new SpinDefinition(),
                ShipInfo = new ShipInfo(ShipRole.Trader, "ship_cargo7", "Freighter")
            };

            var patrolShip = new CelestialBody("\u041f\u0430\u0442\u0440\u0443\u043b\u044c \u00ab\u0421\u0442\u0440\u0430\u0436-3\u00bb", CelestialBodyType.Ship)
            {
                Radius = 0.024, HasSurface = false, IsSelectable = true,
                LocalizationKeyName = "ship.strazh3",
                ParentId = planet2.Id, AttachmentMode = AttachmentMode.Orbit,
                Orbit = OrbitDefinition.Circular(radius: 3.0, period: 10.0, startAngleDeg: 200.0),
                Spin = new SpinDefinition(),
                ShipInfo = new ShipInfo(ShipRole.Patrol, "ship_strazh3", "Interceptor")
            };

            planet1.AddChildId(playerShip.Id);
            planet1.AddChildId(traderShip.Id);
            planet2.AddChildId(patrolShip.Id);

            registry.Add(playerShip);
            registry.Add(traderShip);
            registry.Add(patrolShip);

            system.AddBody(playerShip.Id);
            system.AddBody(traderShip.Id);
            system.AddBody(patrolShip.Id);

            // --- Stations: Terra ---

            var orbitalStationInfo1 = new StationInfo(StationKind.Orbital);
            orbitalStationInfo1.InitializeDocking(3);

            var orbitalStation1 = new CelestialBody("\u0421\u0442\u0430\u043d\u0446\u0438\u044f \u00ab\u041e\u0440\u0431\u0438\u0442\u0430-1\u00bb", CelestialBodyType.Station)
            {
                Radius = 0.06, HasSurface = false, IsSelectable = true,
                LocalizationKeyName = "station.orbita1",
                ParentId = planet1.Id, AttachmentMode = AttachmentMode.Orbit,
                Orbit = OrbitDefinition.Circular(radius: 8.0, period: 18.0, startAngleDeg: 60.0),
                Spin = SpinDefinition.Simple(40.0),
                StationInfo = orbitalStationInfo1
            };

            var surfaceStationInfo1 = new StationInfo(StationKind.Surface, latDeg: 30.0, lonDeg: 45.0);
            surfaceStationInfo1.InitializeDocking(2);

            var surfaceStation1 = new CelestialBody("\u0411\u0430\u0437\u0430 \u00ab\u0422\u0435\u0440\u0440\u0430-1\u00bb", CelestialBodyType.Station)
            {
                Radius = 0.04, HasSurface = false, IsSelectable = true,
                LocalizationKeyName = "station.terra1",
                ParentId = planet1.Id, AttachmentMode = AttachmentMode.Surface,
                Orbit = null, Spin = new SpinDefinition(),
                StationInfo = surfaceStationInfo1
            };

            planet1.AddChildId(orbitalStation1.Id);
            planet1.AddChildId(surfaceStation1.Id);

            registry.Add(orbitalStation1);
            registry.Add(surfaceStation1);

            system.AddBody(orbitalStation1.Id);
            system.AddBody(surfaceStation1.Id);

            // --- Stations: Ares ---

            var orbitalStationInfoAres = new StationInfo(StationKind.Orbital);
            orbitalStationInfoAres.InitializeDocking(2);

            var orbitalStationAres = new CelestialBody("\u0421\u0442\u0430\u043d\u0446\u0438\u044f \u00ab\u0424\u043e\u0431\u043e\u0441\u00bb", CelestialBodyType.Station)
            {
                Radius = 0.05, HasSurface = false, IsSelectable = true,
                LocalizationKeyName = "station.phobos",
                ParentId = planet2.Id, AttachmentMode = AttachmentMode.Orbit,
                Orbit = OrbitDefinition.Circular(radius: 6.0, period: 14.0, startAngleDeg: 30.0),
                Spin = SpinDefinition.Simple(30.0),
                StationInfo = orbitalStationInfoAres
            };

            var surfaceStationInfoAres = new StationInfo(StationKind.Surface, latDeg: -15.0, lonDeg: 120.0);
            surfaceStationInfoAres.InitializeDocking(2);

            var surfaceStationAres = new CelestialBody("\u0411\u0430\u0437\u0430 \u00ab\u0410\u0440\u0435\u0441-1\u00bb", CelestialBodyType.Station)
            {
                Radius = 0.035, HasSurface = false, IsSelectable = true,
                LocalizationKeyName = "station.ares1",
                ParentId = planet2.Id, AttachmentMode = AttachmentMode.Surface,
                Orbit = null, Spin = new SpinDefinition(),
                StationInfo = surfaceStationInfoAres
            };

            planet2.AddChildId(orbitalStationAres.Id);
            planet2.AddChildId(surfaceStationAres.Id);

            registry.Add(orbitalStationAres);
            registry.Add(surfaceStationAres);

            system.AddBody(orbitalStationAres.Id);
            system.AddBody(surfaceStationAres.Id);

            return system;
        }
    }
}
