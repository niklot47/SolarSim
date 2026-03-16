# ARCHITECTURE_STATE.md

Current snapshot of project implementation status.
Last updated after: Step 15 — Elliptical Orbit Foundation.

------------------------------------------------------------------------

## Currently Implemented Systems

### Project Foundation
- **EntityId** — immutable ulong identifier with thread-safe generation
- **SimulationClock** — pure C# time manager (pause/resume/timeScale)
- **WorldEntity** — base class for all domain entities
- **Assembly Definitions** — 7 asmdef files enforcing layer boundaries

### Debug Infrastructure
- **GameDebug** — static API: Log, CaptureSnapshot, ExportBundle, RunInvariantChecks, GetStatus, BuildBundle
- **DebugEvent / RingBuffer** — bounded structured event storage (1000 events, 200 errors, 20 snapshots)
- **DebugSnapshot** — full world state capture via provider pattern (subsystem summaries, recent errors, sim context)
- **IDebugSnapshotProvider** — interface for subsystems to contribute data to snapshots
- **BuiltInSnapshotProviders** — 5 providers: World (entity counts), Ships (state/role breakdown), Economy (resource totals), Docking (port occupancy), SOI (ship distribution)
- **DebugInvariantChecker** — 7 automated checks: NaN/Infinity positions, duplicate ids, negative cargo, negative storage, orphaned children, docking consistency, self-parenting
- **DebugExportUtility** — JSON serialization + file export to `persistentDataPath/debug_bundles/`
- **DebugBundle** — complete export: metadata, status, events, errors, snapshots, invariant violations

### Orbital Sandbox
- **CelestialBody** — domain entity with orbital/spin data, parent-child hierarchy, ShipInfo, StationInfo, SOIRadius
- **StarSystem** — container for body ids
- **WorldRegistry** — central entity lookup
- **OrbitalPositionCalculator** — full Keplerian orbit position calculation (elliptical + inclined + circular), surface position from lat/lon
- **KeplerSolver** — Newton-Raphson solver for Kepler's equation M = E - e*sin(E), 8 iterations max, O(0) allocations
- **OrbitalMapRenderer** — scene visuals, elliptical orbit lines via CalculateOrbitPoint(), delegates position resolution to WorldPositionResolver
- **CelestialBodyView** — visual binding with role-based ship colors, station kind colors, station scale ×⅓

### Elliptical Orbit Foundation (NEW)
- **KeplerSolver** — pure C# Newton-Raphson solver in Simulation layer
  - Solves M = E - e*sin(E) for eccentric anomaly E
  - Initial guess: E₀ = M + e*sin(M)
  - 8 iterations max, convergence threshold 1e-10 radians
  - Zero allocations, no LINQ
  - Circular orbit shortcut: e < 1e-12 → E = M (no iteration)
- **OrbitalPositionCalculator** — upgraded from circular-only to full Keplerian
  - Fast path: circular flat orbits (e ≈ 0, i ≈ 0) — identical to previous implementation
  - General path: elliptical and/or inclined orbits
  - Step 1: Solve Kepler's equation for eccentric anomaly E
  - Step 2: Compute orbital plane position: x = a(cosE - e), z = a√(1-e²)sinE
  - Step 3: Rotate by ω (argument of periapsis), i (inclination), Ω (longitude of ascending node)
  - New method: `CalculateOrbitPoint(orbit, meanAnomalyRad)` for orbit line rendering
  - All existing callers unchanged (CalculatePosition, CalculateAbsolutePosition, GetMeanAnomalyRad, CalculateSurfacePosition)
- **OrbitalMapRenderer** — orbit lines now use CalculateOrbitPoint() instead of hardcoded circles
  - Correctly renders elliptical and inclined orbits
  - SceneScaleConfig DistanceScale applied to all orbit line points
  - No change to position update logic (still delegates to WorldPositionResolver)
- **Backward compatibility**: all existing circular orbits (e=0, i=0) produce identical results
- **No changes** to: OrbitDefinition, WorldPositionResolver, ShipMovementSystem, DockingSystem, NPCShipScheduler, StarSystemBuilder, any UI or Data layer files

### World Position Resolution
- **WorldPositionResolver** — single source of truth for body world positions (Simulation layer, pure C#)

### Star System Data Loading
- **CelestialBodyDefinition / ShipDefinition / StationDefinition** — serializable Inspector-authored data
- **StarSystemDefinition** — ScriptableObject with body list + ship list + station list
- **StarSystemBuilder** — pure C# builder (5 phases: bodies, parents, register, ships, stations)
- **StarSystemLoader** — Unity-side adapter, converts ScriptableObject definitions to build data
- **SampleSystemAssetCreator** — editor menu to create sample asset
- **SampleStarSystemFactory** — hardcoded fallback

### External Star System Import
- **StarSystemJsonDto** — pure DTO classes
- **StarSystemJsonValidator** — validates JSON structure before conversion
- **JsonStarSystemImporter** — adapter: JSON → StarSystemBuildData → StarSystemBuilder
- **OrbitalSandboxCoordinator** — fallback chain: External JSON → StarSystemDefinition → SampleStarSystemFactory

### Stations Foundation
- Two station kinds: **Orbital** and **Surface**
- **StationInfo** — station metadata including Demand field
- Sample stations: Орбита-1, Терра-1, Фобос, Арес-1

### Docking Foundation
- **DockingPort**, **DockingInfo**, **DockingSystem** — unchanged

### Economy + Cargo Foundation
- **ResourceType**, **StationStorage**, **ShipCargo**, **CargoTransferService** — unchanged
- **StationEconomyConfig**, **EconomyInitializer** — unchanged

### Station Production Foundation
- **StationProductionRecipe**, **StationProductionState**, **StationProductionConfig**, **StationProductionSystem** — unchanged

### Station Demand + Trade Opportunity System
- **StationDemand**, **TradeOpportunity**, **TraderJob** — unchanged
- **StationDemandEvaluator**, **TradeOpportunityResolver** — unchanged
- **NPCShipScheduler** — demand-driven trader routing — unchanged

### Selection and Interaction
- Unchanged

### UI Panels
- Unchanged

### Camera and Labels
- Unchanged

### Ships, Ship Movement, NPC Scheduling, SOI, Bootstrap
- Unchanged (except OrbitalMapRenderer orbit line rendering)

------------------------------------------------------------------------

## Current Temporary Architectural Decisions

### Ships as CelestialBody + ShipInfo
Same as before.

### Stations as CelestialBody + StationInfo
Same as before.

### Transit parenting to root star
Unchanged.

### Inspector serialization: float not double
Unchanged.

### No prices or money
Unchanged.

### Demand thresholds are hardcoded
Unchanged.

### JSON deserialization uses Unity JsonUtility
Unchanged.

### JSON import lives in Data layer
Unchanged.

### JSON schema is not versioned
Unchanged.

### Ship orbits remain circular
ShipMovementSystem assigns circular orbits (e=0) on arrival. Ships do not use elliptical orbits yet. This is intentional — elliptical orbit foundation affects celestial bodies only. Ship orbit assignment can be extended later.

### Orbit line segments fixed at 64
Adequate for visual quality at typical zoom levels. For very high eccentricity orbits near periapsis, 64 segments may show slight angular artifacts. Increase if needed.

------------------------------------------------------------------------

## Deferred Systems

| System | Notes |
|---|---|
| JSON schema versioning | Version field for migration support |
| JSON editor tool | In-editor preview/validation of JSON files |
| Multi-system support | Loading multiple star systems simultaneously |
| Prices / Money | Currency, buy/sell prices, supply/demand |
| Player trading UI | Buy/sell interface for player at docked station |
| Complex factories | Multi-input, multi-output production |
| Economic AI | Multi-hop routes, profit maximization |
| Contracts | Task definition, assignment |
| Factions | Entities, relationships, territory |
| Advanced navigation | Pathfinding, waypoints, SOI-aware routing |
| Orbital transfers | Hohmann, delta-v, patched conics |
| Combat | Weapons, damage |
| Ship modules | Equipment slots |
| Save/Load | WorldRegistry serialization |
| Procedural planets | PPG Lite integration |
| Inclined orbit visualization | SOI wireframe, orbit plane indicators |

------------------------------------------------------------------------

## Architecture Health Notes

### Clean boundaries maintained
- KeplerSolver is pure C# in Simulation layer — zero Unity dependency
- OrbitalPositionCalculator remains pure C# in Simulation layer — no API changes
- OrbitalMapRenderer only calls OrbitalPositionCalculator.CalculateOrbitPoint() for orbit lines
- No changes to World layer, UI layer, Data layer, Debug layer
- ShipMovementSystem, DockingSystem, NPCShipScheduler — NOT modified
- WorldPositionResolver — NOT modified, calls OrbitalPositionCalculator.CalculatePosition() which now handles elliptical orbits transparently
- All existing callers of OrbitalPositionCalculator work without modification

### Known technical debt
- All previous technical debt items remain
- Ship orbits assigned by ShipMovementSystem/DockingSystem are always circular (e=0)
- Orbit line segments (64) may be insufficient for very high eccentricity orbits
- No orbit line rendering for inclined orbits in 2D minimap (if added later)

------------------------------------------------------------------------

## Elliptical Orbit Math Reference

### Kepler's Equation
```
M = E - e * sin(E)
```
- M = mean anomaly (linear time progression)
- E = eccentric anomaly (geometric angle on auxiliary circle)
- e = eccentricity

### Newton-Raphson Solution
```
E_{n+1} = E_n - (E_n - e*sin(E_n) - M) / (1 - e*cos(E_n))
```
Initial guess: E₀ = M + e*sin(M)

### Position in Orbital Plane
```
r = a * (1 - e * cos(E))
x_orbit = a * (cos(E) - e)
z_orbit = a * sqrt(1 - e²) * sin(E)
```

### 3D Rotation Sequence (Y-up convention)
1. Rotate by ω (argument of periapsis) around Y in orbital plane
2. Rotate by i (inclination) around X to tilt the plane
3. Rotate by Ω (longitude of ascending node) around Y

### Backward Compatibility
When e = 0 and i = 0:
- E = M (no Kepler solving)
- x = a * cos(M), z = a * sin(M) — identical to original circular calculation
- No rotation applied

------------------------------------------------------------------------

## Next Recommended Development Phase

**Option A: Player Trading UI** — buy/sell interface when player ship is docked.

**Option B: More Ships / Civilian Traffic** — more trader ships for visible trade network.

**Option C: Test Elliptical Orbits** — add eccentricity/inclination to sample system bodies to visually verify.

**Option D: Prices & Money** — currency, station buy/sell prices based on demand/surplus.

**Option E: Save/Load Foundation** — serialize WorldRegistry state to JSON, reload.

Recommendation: **Option C** (quick visual verification) then **Option A or B**.
