# ARCHITECTURE_STATE.md

Current snapshot of project implementation status.
Last updated after: Step 17 — UI Improvements (object list filter bar, subtree collapse, selection highlight, label viewport clipping).

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
- **OrbitSampler** — **NEW** Simulation-side orbit sampling with adaptive subdivision and uniform fallback
- **OrbitalMapRenderer** — scene visuals, orbit lines via OrbitSampler (adaptive or uniform mode), delegates position resolution to WorldPositionResolver
- **CelestialBodyView** — visual binding with role-based ship colors, station kind colors, station scale ×⅓

### Adaptive Orbit Line Rendering (NEW)
- **OrbitSampler** (Simulation layer) — orbit geometry sampling with two modes
  - `SampleAdaptive(orbit, tolerance, maxDepth, seedSegments)` — curvature-based subdivision
    - Starts with evenly-spaced seed points (default 16)
    - Recursively subdivides segments where chord-to-curve deviation exceeds tolerance
    - Circular orbits: ~16 points (no subdivision needed, chord error ≈ 0)
    - Eccentric orbits: more points near periapsis where curvature is highest
    - Parameters: tolerance (default 0.05 Mm), maxDepth (default 7), seedSegments (default 16)
    - Hard cap: 2048 total points maximum
  - `SampleUniform(orbit, segmentCount)` — fixed-count fallback for debugging
  - `EvaluateAtMeanAnomaly(orbit, M)` — single point evaluation
  - All methods delegate to `OrbitalPositionCalculator.CalculateOrbitPoint()` — zero math duplication
- **OrbitalMapRenderer** — orbit line points computed via OrbitSampler
  - Inspector toggle: `adaptiveOrbitLines` (default: true)
  - Adaptive params: `orbitLineTolerance` (0.05), `orbitLineMaxDepth` (7), `orbitLineSeedSegments` (16)
  - Uniform fallback: `orbitLineFixedSegments` (128)
  - Orbit lines generated once on creation or orbit parameter change — NOT every frame
  - No separate circle-only or approximate ellipse logic in Rendering layer

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
- **NPCShipScheduler** — demand-driven trader routing — unchanged

### Selection, UI, Camera, Ships, Movement, SOI, Bootstrap
- Unchanged

### Object List Panel (UI improvements)
- **ObjectListPanelController** — hierarchical body list with:
  - **Filter bar** (inside collapsible panel body, above list): Ships toggle, Collapse All (▶▶), Expand All (▼▼)
  - **Subtree collapse** — ▼/▶ button on items with children; `_collapsedIds` HashSet drives `AddBodyAndChildren` recursion
  - **Ship filter** — `_showShips` flag; hides `CelestialBodyType.Ship` entries; `_bodyIdsWithChildren` cache respects filter
  - **Selection highlight** — driven entirely from `BindListItem` using `_currentSelectionId`; adds/removes CSS class `list-item-selected` on the inner row element; clears Unity's inline background color on the wrapper to bypass built-in opaque selection highlight
  - Collapse button click handled via `RegisterCallback<PointerDownEvent>(TrickleDown)` to intercept before ListView + `RegisterCallback<ClickEvent>` for toggle; `userData` stores `EntityId` to avoid closure/rebind issues

### IMGUI Label Clipping (Rendering improvement)
- **BodyLabelController** — IMGUI body name labels now clipped to viewport between panels:
  - Accepts `UIDocument` in `Initialize()` — queries `left-panel`, `right-panel`, `left-panel-body`, `right-panel-body`
  - `GUI.BeginClip` restricts rendering to `Rect(leftEdge, 0, rightEdge-leftEdge, Screen.height)`
  - Panel edges computed via `worldBound.xMax/xMin * scaledPixelsPerPoint` (converts UI Toolkit points → IMGUI physical pixels)
  - `IsPanelCollapsed(panelBody)` checks `resolvedStyle.display == None` — when panel is collapsed to header-only, the edge is treated as 0 / Screen.width so labels extend to full screen width
- **OrbitalSandboxCoordinator** — passes `uiDocument` to `labelController.Initialize()`

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
ShipMovementSystem assigns circular orbits (e=0) on arrival.

### Orbit line points are generated once per orbit creation
Not regenerated every frame. If orbit parameters change dynamically at runtime (not currently the case for celestial bodies), orbit lines would need explicit regeneration.

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

------------------------------------------------------------------------

## Architecture Health Notes

### Clean boundaries maintained
- OrbitSampler is pure C# in Simulation layer — delegates all math to OrbitalPositionCalculator
- OrbitalMapRenderer calls OrbitSampler — contains zero orbital math
- KeplerSolver, OrbitalPositionCalculator remain pure C# — no changes
- UI layer changes (ObjectListPanelController, UIStrings) contained within UI assembly
- BodyLabelController change is Rendering layer only — reads UIDocument bounds, no simulation dependency
- ShipMovementSystem, DockingSystem, NPCShipScheduler, WorldPositionResolver — NOT modified

### Known technical debt
- All previous technical debt items remain
- Ship orbits always circular (e=0)
- Orbit line regeneration for ships (EnsureOrbitLine) runs when ship state changes — could cache more aggressively

------------------------------------------------------------------------

## Adaptive Orbit Line Algorithm Reference

### Subdivision logic
```
For each seed segment [t0, t1]:
    p0 = CalculateOrbitPoint(t0)
    p1 = CalculateOrbitPoint(t1)
    tMid = (t0 + t1) / 2
    pMid = CalculateOrbitPoint(tMid)       // true curve point
    pChord = (p0 + p1) / 2                 // linear midpoint
    error = distance(pMid, pChord)
    if error > tolerance:
        subdivide [t0, tMid] and [tMid, t1] recursively
    else:
        accept segment as-is
```

### Behavior by orbit type
- **Circular (e=0)**: chord error ≈ 0 for 16 seed segments → ~16 points total
- **Low eccentricity (e<0.1)**: minimal subdivision → ~20–30 points
- **Medium eccentricity (e~0.5)**: subdivision near periapsis → ~60–100 points
- **High eccentricity (e>0.8)**: heavy subdivision near periapsis → ~150–300 points

### Safety limits
- Max total points: 2048
- Max recursion depth: 7 (configurable)
- Tolerance floor: 1e-6 Mm

------------------------------------------------------------------------

## Orbit Line Rendering Pipeline

```
OrbitDefinition (a, e, i, Ω, ω)
    ↓
OrbitalMapRenderer calls OrbitSampler.SampleAdaptive(orbit, tolerance, maxDepth, seeds)
    ↓
OrbitSampler: generate seed points → recursive subdivision using CalculateOrbitPoint()
    ↓
List<SimVec3> parent-relative positions (variable count, denser at high curvature)
    ↓
OrbitalMapRenderer applies SceneScaleConfig.DistanceScale
    ↓
LineRenderer positions (useWorldSpace = false, loop = true)
    ↓
LineRenderer transform.position = parent world position (updated each tick)
```

Same `OrbitalPositionCalculator.CalculateOrbitPoint()` is used by:
- OrbitSampler (orbit line geometry)
- OrbitalPositionCalculator.CalculatePosition() (runtime body positions)

Bodies move exactly on the rendered orbit line.

------------------------------------------------------------------------

## Next Recommended Development Phase

**Option A: Player Trading UI** — buy/sell interface when player ship is docked.

**Option B: More Ships / Civilian Traffic** — more trader ships for visible trade network.

**Option C: Test Elliptical Orbits** — add eccentricity/inclination to sample system bodies.

**Option D: Prices & Money** — currency, station buy/sell prices.

**Option E: Save/Load Foundation** — serialize WorldRegistry state.

Recommendation: **Option C** then **Option A or B**.
