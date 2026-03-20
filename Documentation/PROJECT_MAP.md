# PROJECT_MAP.md

## Project Overview

Modular sandbox space simulation built in Unity LTS using URP and UI Toolkit.

Long-term goals: orbital simulation sandbox, ships and stations, NPC traffic and tasks, economy and trade, faction and clan politics, maintainable architecture for humans and AI assistants.

------------------------------------------------------------------------

## Architecture Layers

### 1. Simulation

Pure game simulation logic. Time progression, orbital calculations, selection state, star system building, ship movement, NPC scheduling, world position resolution, SOI resolution, docking, economy/cargo transfer, station production, station demand evaluation, trade opportunity resolution.

Rules:
- Must NOT depend on UnityEngine
- Pure C# only
- Deterministic and testable
- Enforced via asmdef with `noEngineReferences: true`

Key files:
- `Scripts/Simulation/Time/SimulationClock.cs` — simulation clock
- `Scripts/Simulation/Core/SelectionService.cs` — tracks selected entity id
- `Scripts/Simulation/Core/SampleStarSystemFactory.cs` — hardcoded fallback sample system
- `Scripts/Simulation/Core/StarSystemBuilder.cs` — builds runtime entities from pure build data
- `Scripts/Simulation/Core/WorldPositionResolver.cs` — single source of truth for body world positions
- `Scripts/Simulation/Orbits/OrbitalPositionCalculator.cs` — **full Keplerian orbit position**: elliptical, inclined, and circular; surface position from lat/lon
- `Scripts/Simulation/Orbits/KeplerSolver.cs` — Newton-Raphson solver for Kepler's equation
- `Scripts/Simulation/Orbits/OrbitSampler.cs` — adaptive orbit geometry sampling; SampleAdaptive(), SampleUniform(), EvaluateAtMeanAnomaly(); delegates to OrbitalPositionCalculator
- `Scripts/Simulation/Ships/ShipMovementSystem.cs` — SOI-aware travel with symmetric frame switching: Global/LocalParent frames, inward reframe (Case 2), outward reframe (Case 3), early insertion on destination SOI entry (Case 1), phased orbit insertion; HandleSOITransition() receives both previousSOIBodyId and newSOIBodyId; per-ship anti-jitter cooldown
- `Scripts/Simulation/Ships/NPCShipScheduler.cs` — demand-driven trader routing via TradeOpportunityResolver
- `Scripts/Simulation/SOI/SOIResolver.cs` — sphere of influence resolution
- `Scripts/Simulation/Docking/DockingSystem.cs` — docking lifecycle
- `Scripts/Simulation/Economy/CargoTransferService.cs` — cargo transfer operations
- `Scripts/Simulation/Economy/StationEconomyConfig.cs` — hardcoded initial resource loadouts
- `Scripts/Simulation/Economy/EconomyInitializer.cs` — initializes storage, production, and cargo
- `Scripts/Simulation/Economy/StationProductionConfig.cs` — hardcoded production recipes
- `Scripts/Simulation/Economy/StationProductionSystem.cs` — ticks production on all stations
- `Scripts/Simulation/Economy/StationDemandEvaluator.cs` — evaluates demand/surplus scores
- `Scripts/Simulation/Economy/TradeOpportunityResolver.cs` — scans station pairs for trade routes

### 2. World

Game domain entities and shared world state models.

Key files:
- `Scripts/World/Entities/WorldEntity.cs` — base class (EntityId + DisplayName)
- `Scripts/World/Entities/CelestialBody.cs` — body type, hierarchy, orbit, ShipInfo, StationInfo
- `Scripts/World/Entities/ShipInfo.cs` — ship data including CurrentTradeJob
- `Scripts/World/Entities/ShipRoute.cs` — travel route data
- `Scripts/World/Entities/ShipCargo.cs` — ship cargo hold
- `Scripts/World/Entities/StationInfo.cs` — station data including Demand
- `Scripts/World/Entities/StationStorage.cs` — station resource storage
- `Scripts/World/Entities/StationProductionRecipe.cs` — production recipe
- `Scripts/World/Entities/StationProductionState.cs` — production progress tracker
- `Scripts/World/Entities/StationDemand.cs` — demand and surplus scores
- `Scripts/World/Entities/TradeOpportunity.cs` — single trade route opportunity
- `Scripts/World/Entities/TraderJob.cs` — trader's current trade assignment
- `Scripts/World/Entities/ResourceType.cs` — enum: Food, Metals, Fuel, Electronics
- `Scripts/World/Entities/DockingPort.cs`, `DockingInfo.cs` — docking port model
- `Scripts/World/Entities/StarSystem.cs` — container with body ids
- `Scripts/World/ValueTypes/OrbitDefinition.cs` — Keplerian orbital elements (a, e, i, Ω, ω, M₀, T)
- `Scripts/World/ValueTypes/SpinDefinition.cs` — axial tilt, rotation period
- `Scripts/World/Systems/WorldRegistry.cs` — central entity registry

### 3. Rendering

Key files:
- `Scripts/Rendering/Bootstrap/GameBootstrap.cs` — Unity entry point
- `Scripts/Rendering/Bootstrap/OrbitalSandboxCoordinator.cs` — wires all services, external JSON import support
- `Scripts/Rendering/Bootstrap/StarSystemLoader.cs` — converts ScriptableObject definitions to build data
- `Scripts/Rendering/Orbits/OrbitalMapRenderer.cs` — scene visuals, orbit lines via **OrbitSampler** (adaptive subdivision or uniform fallback)
- `Scripts/Rendering/Planets/CelestialBodyView.cs` — body visual representation
- `Scripts/Rendering/Cameras/OrbitalCameraController.cs` — camera controls
- `Scripts/Rendering/Selection/SelectionBridge.cs` — selection ring + highlight
- `Scripts/Rendering/Selection/BodyClickHandler.cs` — raycast click selection
- `Scripts/Rendering/Selection/UIInputBlocker.cs` — blocks camera input over UI
- `Scripts/Rendering/Labels/BodyLabelController.cs` — IMGUI labels clipped to viewport; reads panel bounds from UIDocument via worldBound + scaledPixelsPerPoint; collapses clip area when panel is collapsed

### 4. UI

Key files:
- `Scripts/UI/Panels/ObjectListPanelController.cs` — hierarchical body list; filter bar (ships toggle, collapse/expand all); subtree collapse (▼/▶); selection highlight via BindListItem + CSS class
- `Scripts/UI/Panels/ObjectDetailsPanelController.cs` — compact properties panel (right)
- `Scripts/UI/Panels/DetailModalController.cs` — full-screen detail modal with tabs
- `Scripts/UI/Panels/TimeControlsPanelController.cs` — pause/resume and time scale buttons
- `Scripts/UI/Localization/UIStrings.cs` — centralized Russian string table; all user-facing strings routed here
- `Scripts/UI/Core/BodyIconResolver.cs` — resolves PNG icons by body type
- `Assets/UI/UXML/OrbitalSandboxScreen.uxml` — UI layout: left panel (list + filter bar), center viewport, right panel (details), modal overlay
- `Assets/UI/USS/OrbitalSandboxScreen.uss` — full theme: CSS variables, panel styles, filter bar, list item hover/selected states, modal tabs, time controls

### 5. Data

Key files: unchanged.

### Shared

Cross-cutting utilities shared by all layers. Unchanged.

### Debug

Structured debug event and snapshot system. Unchanged.

------------------------------------------------------------------------

## Assembly Definitions

| Assembly | noEngineReferences | Dependencies |
|---|---|---|
| SpaceSim.Shared | true | (none) |
| SpaceSim.World | true | Shared |
| SpaceSim.Simulation | true | Shared, World |
| SpaceSim.Debug | false | Shared, Simulation, World |
| SpaceSim.Data | false | Shared, World, Simulation |
| SpaceSim.UI | false | Shared, World, Simulation |
| SpaceSim.Rendering | false | Shared, World, Simulation, Debug, UI, Data, Unity.InputSystem |

------------------------------------------------------------------------

## Orbital Calculation Pipeline

### Position Calculation (per tick)
```
OrbitDefinition (a, e, i, Ω, ω, M₀, T)
    ↓
OrbitalPositionCalculator.CalculatePosition(orbit, simTime)
    ↓
Mean anomaly M = M₀ + 2π(t - t₀)/T
    ↓
[if e ≈ 0 and flat] → fast path: (a*cos(M), 0, a*sin(M))
    ↓
[if e > 0] KeplerSolver.Solve(M, e) → eccentric anomaly E
    ↓
Orbital plane: x = a(cosE - e), z = a√(1-e²)sinE
    ↓
[if inclined] Rotate by ω, i, Ω → world coordinates (x, y, z)
    ↓
WorldPositionResolver adds parent world position
```

### Orbit Line Rendering (per orbit line creation)
```
OrbitDefinition
    ↓
OrbitSampler.SampleAdaptive(orbit, tolerance, maxDepth, seedSegments)
    - generates seed points at evenly spaced mean anomaly
    - recursively subdivides where chord error > tolerance
    - calls OrbitalPositionCalculator.CalculateOrbitPoint() for every sample
    ↓
List<SimVec3> parent-relative positions (adaptive count)
    ↓
SceneScaleConfig.DistanceScale applied by OrbitalMapRenderer
    ↓
LineRenderer positions (useWorldSpace = false, loop = true)
    ↓
LineRenderer transform.position = parent world position (updated each tick)
```

Both pipelines use the same `OrbitalPositionCalculator.CalculateOrbitPoint()` internally.

------------------------------------------------------------------------

## Star System Loading Pipelines

### Pipeline 1: ScriptableObject (existing)
```
StarSystemDefinition (ScriptableObject)
    → StarSystemLoader.Load()
    → StarSystemBuildData
    → StarSystemBuilder.Build()
    → WorldRegistry + StarSystem
    → EconomyInitializer.Initialize()
```

### Pipeline 2: External JSON
```
JSON file (TextAsset or file path)
    → JsonStarSystemImporter
    → StarSystemJson (DTO)
    → StarSystemJsonValidator.Validate()
    → ConvertToBuildData() → StarSystemBuildData
    → StarSystemBuilder.Build()
    → WorldRegistry + StarSystem
    → EconomyInitializer.Initialize()
```

------------------------------------------------------------------------

## Simulation Tick Order

```
ShipMovementSystem.Update()
DockingSystem.Update()
NPCShipScheduler.Update()
StationProductionSystem.Update()
[Periodic] StationDemandEvaluator.EvaluateAll() + TradeOpportunityResolver.Resolve()
SOIResolver.UpdateAllShips()
    → returns List<SOITransition> (previousBodyId + newBodyId per ship)
    → OrbitalSandboxCoordinator forwards each transition to:
       ShipMovementSystem.HandleSOITransition(shipId, previousSOIBodyId, newSOIBodyId, ...)
           Case 1: newSOI == destination       → StartInsertionPhase() early
           Case 2: newSOI relevant, Global frame → ReframeRoute() inward
           Case 3: previousSOI == LocalFrameBody → ReframeRouteOutward()
```

------------------------------------------------------------------------

## Coding Standards

- Code comments: English only
- Project documentation: English
- Integration instructions: Russian
- UI strings: localization-ready via UIStrings (current: Russian)
- Composition over inheritance, explicit dependencies, single responsibility
- Inspector-serialized fields: use float (not double) for Unity compatibility


------------------------------------------------------------------------

## Strategic Direction — Road to Full Physics

The project's nearest major simulation goal is now a staged transition from the current hybrid navigation model to a more physically grounded orbital flight model.

Current navigation is already strong:
- Keplerian body motion
- SOI-aware inward/outward reframing
- phased insertion and stable arrival

But ships still use route approximation rather than maneuver-derived conic transfer planning.

### Planned sequence

1. **Impact / Collision Check Foundation**
   - Detect route intersections with stars / planets / moons
   - Reject unsafe routes or flag impact states

2. **Transfer Planning Lite**
   - Replace obviously unsafe direct lines with safer planned transfer legs
   - Still deterministic and gameplay-safe

3. **Hohmann Transfer Lite**
   - Approximate coplanar transfer-orbit planning
   - Focus on interplanetary readability and stability

4. **Burn Windows / Phase Alignment**
   - Delay departure until a target-relative launch window exists
   - Make route timing matter

5. **Patched Conics Full**
   - Explicit conic segments per SOI domain
   - SOI crossing becomes conic handoff, not only frame switching

6. **Delta-v / Energy Model**
   - Maneuver budget, propulsion capability, costed insertion/capture/escape

7. **Gravity Assist / Capture / Escape Mechanics**
   - Flyby behavior and energy-driven trajectory change

### Planning rule

Each phase must preserve the project's architecture rules:
- Simulation owns orbital/navigation logic
- World stores route/state data only
- Rendering consumes resolved state without owning physics
- Upgrades should remain deterministic, debug-friendly, and incremental
