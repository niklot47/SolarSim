# ARCHITECTURE_STATE.md

Current snapshot of project implementation status.
Last updated after: Step 18 — SOI-Aware Navigation & Phased Orbit Insertion.

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
- **OrbitSampler** — Simulation-side orbit sampling with adaptive subdivision and uniform fallback
- **OrbitalMapRenderer** — scene visuals, orbit lines via OrbitSampler (adaptive or uniform mode), delegates position resolution to WorldPositionResolver
- **CelestialBodyView** — visual binding with role-based ship colors, station kind colors, station scale ×⅓

### SOI-Aware Navigation & Phased Orbit Insertion (NEW — Step 18)
- **ShipMovementSystem** — rewritten arrival logic with two-phase travel:
  - **Phase 1 (Travelling)**: ship interpolates from origin to approach point (OrbitApproachMultiplier × orbit radius from destination). Frame selection is SOI-aware via parent hierarchy analysis.
  - **Phase 2 (InsertingIntoOrbit)**: ship converges from approach point to orbit radius in LOCAL frame of arrival parent body. Uses smooth-step easing. Duration: `OrbitInsertionDuration` sim-seconds (default 1.5).
  - `OrbitApproachMultiplier` (default 2.5) — controls how far from target the travel ends
  - `OrbitInsertionDuration` (default 1.5) — duration of insertion approach in sim-seconds
  - `OnNavEvent` callback — navigation debug events without coupling to Debug assembly
  - `DetermineArrivalParent()` — helper shared by insertion and completion phases
- **ShipState** — new value: `InsertingIntoOrbit` (between Travelling and Orbiting)
- **ShipRoute** — new fields for insertion phase: `InsertionPhaseActive`, `InsertionStartTime`, `InsertionFrameBodyId`, `InsertionStartLocalPos`, `InsertionTargetLocalPos`, `InsertionArrivalAngleDeg`
- **OrbitalSandboxCoordinator** — exposes `orbitInsertionDuration` and `orbitApproachMultiplier` in Inspector; wires `OnNavEvent` → `GameDebug`; SOI transitions logged as `[Nav] SOI:` events

### Frame Selection (DetermineRouteFrame — improved)
Determines reference frame for ship travel based on parent hierarchy:
- **LocalParent**: destination is parent or child of origin's body, or they share a non-star parent → travel in frame of common parent (e.g. Terra for Terra↔Luna transfers)
- **Global**: common parent is a star, or no common parent → interplanetary travel in star frame (e.g. Terra→Ares)
- Station cases: handled explicitly (checks station's parent body)
- No regression from existing logic; extended with cleaner comments

### Insertion Phase Mechanics
```
Phase 1 completes (ship at approach point)
    ↓
StartInsertionPhase():
    - Reads actual ship world position at approach point
    - Resolves arrival parent body position NOW
    - Computes local position of ship relative to parent
    - Computes orbit insertion target: (r*cos(θ), 0, r*sin(θ)) in parent-local space
    - Sets InsertionFrameBodyId = arrival parent
    - Sets ShipState = InsertingIntoOrbit
    ↓
UpdateInsertionPhase() each tick:
    - t = (currentTime - InsertionStartTime) / OrbitInsertionDuration
    - smoothT = t² × (3 - 2t)  [smooth-step]
    - localPos = lerp(startLocal, targetLocal, smoothT)
    - worldPos = parentBodyWorldPos(now) + localPos   ← tracks moving body!
    ↓
CompleteArrival() when t >= 1.0:
    - Sets orbit using InsertionArrivalAngleDeg (real angle, not predicted)
    - Fires OnShipArrived
```

### Why local-frame insertion matters
- Arrival parent body (planet, moon) is moving each tick
- Insertion target is computed LOCAL to that body
- Ship position = parentPos(now) + localOffset → stable, no drift
- Works correctly whether destination moves fast or slow

### Navigation Debug Logging
Events logged via OnNavEvent → GameDebug (DebugCategory.SHIPS / ORBIT):
- `[Nav] <ship>: inserting into orbit around <body> (r=X Mm, angle=Y°)` — insertion start
- `[Nav] <ship>: arrived, orbiting <body> at r=X Mm` — arrival complete
- `[Nav] SOI: <ship>: <prev> → <new> (t=X)` — SOI boundary crossing

### Adaptive Orbit Line Rendering
- **OrbitSampler** (Simulation layer) — orbit geometry sampling with two modes
  - `SampleAdaptive(orbit, tolerance, maxDepth, seedSegments)` — curvature-based subdivision
  - `SampleUniform(orbit, segmentCount)` — fixed-count fallback for debugging
  - `EvaluateAtMeanAnomaly(orbit, M)` — single point evaluation
- **OrbitalMapRenderer** — orbit line points computed via OrbitSampler
  - Inspector toggle: `adaptiveOrbitLines` (default: true)
  - Adaptive params: `orbitLineTolerance` (0.05), `orbitLineMaxDepth` (7), `orbitLineSeedSegments` (16)
  - Uniform fallback: `orbitLineFixedSegments` (128)

### Elliptical Orbit Foundation
- **KeplerSolver** — pure C# Newton-Raphson solver in Simulation layer
- **OrbitalPositionCalculator** — full Keplerian: elliptical, inclined, and circular
- **Backward compatibility**: all existing circular orbits (e=0, i=0) produce identical results

### World Position Resolution
- **WorldPositionResolver** — single source of truth for body world positions (Simulation layer, pure C#)

### Star System Data Loading
- **CelestialBodyDefinition / ShipDefinition / StationDefinition** — serializable Inspector-authored data
- **StarSystemDefinition** — ScriptableObject with body list + ship list + station list
- **StarSystemBuilder** — pure C# builder (5 phases: bodies, parents, register, ships, stations)
- **StarSystemLoader** — Unity-side adapter
- **SampleSystemAssetCreator** — editor menu to create sample asset
- **SampleStarSystemFactory** — hardcoded fallback

### External Star System Import
- **StarSystemJsonDto**, **StarSystemJsonValidator**, **JsonStarSystemImporter**
- **OrbitalSandboxCoordinator** — fallback chain: External JSON → StarSystemDefinition → SampleStarSystemFactory

### Stations Foundation
- Two station kinds: **Orbital** and **Surface**
- **StationInfo** — station metadata including Demand field

### Docking Foundation
- **DockingPort**, **DockingInfo**, **DockingSystem** — unchanged

### Economy + Cargo Foundation
- **ResourceType**, **StationStorage**, **ShipCargo**, **CargoTransferService** — unchanged

### Station Production Foundation
- **StationProductionRecipe**, **StationProductionState**, **StationProductionConfig**, **StationProductionSystem** — unchanged

### Station Demand + Trade Opportunity System
- **StationDemand**, **TradeOpportunity**, **TraderJob** — unchanged
- **StationDemandEvaluator**, **TradeOpportunityResolver** — unchanged
- **NPCShipScheduler** — unchanged; `InsertingIntoOrbit` treated same as Travelling (skipped)

### Selection, UI, Camera, SOI, Bootstrap
- Unchanged

### Object List Panel (UI improvements)
- **ObjectListPanelController** — hierarchical body list with filter bar, subtree collapse, selection highlight

### IMGUI Label Clipping (Rendering improvement)
- **BodyLabelController** — IMGUI body name labels clipped to viewport between panels

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

### Ship orbits remain circular
ShipMovementSystem assigns circular orbits (e=0) on arrival. Orbit radius and period are set from route data.

### Orbit insertion is simplified
Orbit insertion is a smooth linear interpolation to a point on the circular orbit — not a real Hohmann transfer or burn. The approach direction is the near-side angle (ship approaches from the side it came from), not from a specific burn point.

### Orbit insertion duration is fixed
Same duration for all routes regardless of distance. A future improvement could scale duration with route length or SOI size.

------------------------------------------------------------------------

## Deferred Systems

| System | Notes |
|---|---|
| JSON schema versioning | Version field for migration support |
| JSON editor tool | In-editor preview/validation |
| Multi-system support | Multiple star systems |
| Prices / Money | Currency, buy/sell prices |
| Player trading UI | Buy/sell interface |
| Complex factories | Multi-input, multi-output |
| Economic AI | Multi-hop routes, profit |
| Contracts | Task definition, assignment |
| Factions | Entities, relationships, territory |
| Advanced navigation | Pathfinding, SOI-aware routing |
| Orbital transfers | Hohmann, delta-v, patched conics |
| Combat | Weapons, damage |
| Ship modules | Equipment slots |
| Save/Load | WorldRegistry serialization |
| Procedural planets | PPG Lite integration |
| Inclined orbit visualization | SOI wireframe, orbit plane indicators |
| True anomaly spacing | Non-uniform seed distribution for high-e orbits |
| Patched conics | True SOI sphere crossing with trajectory continuity |
| Insertion burn scaling | Duration proportional to SOI radius or route distance |
| SOI exit detection | Mid-travel frame switch when SOI boundary is crossed |

------------------------------------------------------------------------

## Architecture Health Notes

### Clean boundaries maintained
- ShipMovementSystem remains pure C# in Simulation layer — no UnityEngine reference
- OnNavEvent callback avoids coupling to Debug assembly (caller wires to GameDebug)
- ShipRoute remains a pure data class — no logic
- InsertingIntoOrbit state is handled exclusively in ShipMovementSystem.Update
- NPCShipScheduler, DockingSystem, Economy, SOIResolver — NOT modified
- UI layer (UIStrings) updated with one new string entry only

### Known technical debt
- All previous technical debt items remain
- Ship orbits always circular (e=0) on arrival
- Approach direction is near-side angle (ship comes from same side it approached from), not a proper prograde/retrograde angle
- Insertion duration fixed regardless of route length
- No mid-travel SOI frame switch (SOI changes are detected and logged but don't alter in-flight trajectory)

------------------------------------------------------------------------

## Ship Travel Flow (Step 18)

```
StartRoute():
    DetermineRouteFrame() → LocalParent or Global
    BuildLocalRoute() or BuildGlobalRoute()
        → ArrivalWorldPosition = approach point (2.5× orbit radius)
    ship.State = Travelling
    ↓
Update() each tick (Travelling):
    lerp(startPos, approachPos, progress)
    when progress >= 1.0 → StartInsertionPhase()
    ↓
StartInsertionPhase():
    compute actual approach angle from real ship position
    compute orbit insertion point in parent-local space
    ship.State = InsertingIntoOrbit
    ↓
Update() each tick (InsertingIntoOrbit):
    t = elapsed / OrbitInsertionDuration
    smoothT = t²(3-2t)
    worldPos = parentPos(now) + lerp(startLocal, targetLocal, smoothT)
    when t >= 1.0 → CompleteArrival()
    ↓
CompleteArrival():
    ship.ParentId = arrivalParent
    ship.Orbit = circular orbit at destination radius
    ship.State = Orbiting
    OnShipArrived fired
```

------------------------------------------------------------------------

## Next Recommended Development Phase

**Option A: Player Trading UI** — buy/sell interface when player ship is docked.

**Option B: More Ships / Civilian Traffic** — more trader ships for visible trade network.

**Option C: Test Elliptical Orbits** — add eccentricity/inclination to sample system bodies.

**Option D: Prices & Money** — currency, station buy/sell prices.

**Option E: Save/Load Foundation** — serialize WorldRegistry state.

Recommendation: **Option A** (Player Trading UI) or **Option C** (elliptical orbits for visual testing).
