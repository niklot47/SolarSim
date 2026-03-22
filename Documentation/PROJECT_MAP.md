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
- `Scripts/Simulation/Orbits/OrbitalPositionCalculator.cs` — full Keplerian orbit position
- `Scripts/Simulation/Orbits/KeplerSolver.cs` — Newton-Raphson solver for Kepler's equation
- `Scripts/Simulation/Orbits/OrbitSampler.cs` — adaptive orbit geometry sampling
- `Scripts/Simulation/Ships/ShipMovementSystem.cs` — SOI-aware travel; ManeuverPlanner integration; PlannedDepartureTime
- `Scripts/Simulation/Ships/RouteSafetyChecker.cs` — route collision validation
- `Scripts/Simulation/Ships/TransferPlannerLite.cs` — retained reference; superseded by ManeuverPlanner
- `Scripts/Simulation/Ships/ManeuverPlanner.cs` — multi-window maneuver planning with geometry scoring
- `Scripts/Simulation/Ships/NPCShipScheduler.cs` — demand-driven trader routing; PlannedDepartureTime guard
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
- `Scripts/World/Entities/ShipInfo.cs` — includes `PlannedDepartureTime` field
- (all other World files unchanged)

### 3. Rendering

Key files:
- `Scripts/Rendering/Bootstrap/GameBootstrap.cs` — Unity entry point; **holds `[SerializeField] DebugFilterProfile` reference; applies filter at startup and on OnValidate**
- `Scripts/Rendering/Bootstrap/OrbitalSandboxCoordinator.cs` — wires all services
- `Scripts/Rendering/Bootstrap/StarSystemLoader.cs` — converts ScriptableObject definitions to build data
- (all other Rendering files unchanged)

### 4. UI

Key files: unchanged.

### 5. Data / Shared

Unchanged.

### Debug

Structured debug event and snapshot system for AI-assisted debugging. Exports JSON bundles to disk for offline analysis. **Includes configurable log filter system with hierarchical Inspector UI.**

Key files:
- `Scripts/Debug/GameDebug.cs` — static API; **Log() checks DebugFilter before recording; ActiveFilter property; TotalEventsFiltered counter**
- `Scripts/Debug/DebugFilter.cs` — **(Step 24)** pure C# runtime filter; category + source tag + severity checks; errors always pass; thread-safe
- `Scripts/Debug/DebugFilterProfile.cs` — **(Step 24)** ScriptableObject with hierarchical DebugFilterGroup list; PopulateDefaults() creates standard groups; ApplyTo(DebugFilter) method
- `Scripts/Debug/DebugFilterProfileEditor.cs` — **(Step 24)** custom Inspector; tree view with foldout groups, master toggles, indented child tag toggles; live-apply during Play mode
- `Scripts/Debug/DebugFilterProfileCreator.cs` — **(Step 24)** editor menu to create default profile asset
- `Scripts/Debug/DebugEvent.cs` — structured event model with category, severity, timestamp
- `Scripts/Debug/DebugModels.cs` — DebugSnapshot, SubsystemSnapshot, DebugBundle, BundleMetadata **(extended with FilterSummary, MutedCategories, MutedTags, TotalEventsFiltered)**, InvariantViolation
- `Scripts/Debug/RingBuffer.cs` — bounded collection
- `Scripts/Debug/DebugSnapshotProviders.cs` — IDebugSnapshotProvider interface + SnapshotProviderRegistry
- `Scripts/Debug/DebugInvariantChecker.cs` — 7 invariant checks
- `Scripts/Debug/DebugExportUtility.cs` — JSON serialization + file export; **serializes filter metadata fields**
- `Scripts/Debug/BuiltInSnapshotProviders.cs` — 5 providers

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

## Debug Filter System (Step 24)

### Architecture

```
DebugFilterProfile (ScriptableObject, authored in Inspector)
    contains: List<DebugFilterGroup>
    each group: DisplayName, Enabled (master), List<string> Categories, List<DebugTagToggle> Tags
    |
    v
GameBootstrap.Initialize()
    debugFilterProfile.ApplyTo(GameDebug.ActiveFilter)
    |
    v
DebugFilter (pure C# runtime instance, static on GameDebug)
    IsAllowed(category, severity, sourceTag) → bool
    Errors ALWAYS pass
    |
    v
GameDebug.Log()
    if (!_filter.IsAllowed(...)) { _totalEventsFiltered++; return; }
    → event enters RingBuffer → forwarded to Unity console via OnEventLogged
    |
    v
DebugExportUtility.BundleToJson()
    BundleMetadata includes: FilterSummary, MutedCategories, MutedTags, TotalEventsFiltered
```

### Inspector tree rendering

```
[DebugFilterProfile Inspector]

Global Settings:
  [✓] Enable Logging
  Minimum Severity: [Info ▾]

[Enable All] [Disable All] [Reset Defaults]

Filter Groups:
  ┌─────────────────────────────────────────┐
  │ ▼ [✓] Navigation              [SHIPS,PATH] │
  │      [✓] Navigation                        │
  │      [✓] Docking                           │
  ├─────────────────────────────────────────┤
  │ ▼ [✓] Economy                  [ECONOMY]   │
  │      [✓] CargoTransfer                     │
  │      [✓] Production                        │
  │      [✓] TradeAI                           │
  ├─────────────────────────────────────────┤
  │ ▶ [✗] Orbits & SOI             [ORBIT]     │  ← collapsed, disabled
  └─────────────────────────────────────────┘
```

### Source tag registry

| Source Tag | Category | Description |
|---|---|---|
| Navigation | SHIPS | Route planning, maneuver scores, frame switching, orbit insertion |
| Docking | SHIPS | Dock/undock lifecycle |
| CargoTransfer | ECONOMY | Ship load/unload at stations |
| Production | ECONOMY | Station production cycle completions |
| TradeAI | ECONOMY | Trader route selection decisions |
| SOI | ORBIT | SOI boundary transitions |
| SystemLoader | SIM | Star system loading from assets/JSON |
| GameDebug | DEBUG | Debug system self-diagnostics |
| InvariantChecker | DEBUG | Invariant violation details |

------------------------------------------------------------------------

## Simulation Tick Order

```
ShipMovementSystem.Update()
DockingSystem.Update()
NPCShipScheduler.Update()
StationProductionSystem.Update()
[Periodic] StationDemandEvaluator + TradeOpportunityResolver
SOIResolver.UpdateAllShips()
```

------------------------------------------------------------------------

## Coding Standards

- Code comments: English only
- Project documentation: English
- Integration instructions: Russian
- UI strings: localization-ready via UIStrings (current: Russian)
- Composition over inheritance, explicit dependencies, single responsibility
- Inspector-serialized fields: use float (not double) for Unity compatibility

### Debug logging standards (Step 24)

- Every `GameDebug.Log()` call MUST include `source:` parameter with a registered tag name
- New source tags must be added to `DebugFilterProfile.PopulateDefaults()` under the appropriate group
- Error-severity logs must never depend on filter state — they always pass
- Each feature delivery must include recommended filter settings for testing
- Source tags use PascalCase, one or two words maximum (e.g. "CargoTransfer", not "cargo_transfer_service")

------------------------------------------------------------------------

## Strategic Direction — Road to Full Physics

Completed:
1. Impact / Collision Check Foundation ✓ (Phase 21)
2. Transfer Planning Lite ✓ (Phase 22)
3. Maneuver Planning Foundation ✓ (Phase 23, Iterations 1–3 + Bugfixes)
4. Debug Log Filter System ✓ (Step 24)

### Phase 25 — Burn Windows / Phase Alignment
### Phase 26 — Patched Conics Full
### Phase 27 — Delta-v / Energy Model
### Phase 28 — Hohmann Helper (Optional)
### Phase 29 — Advanced Transfers (Lambert-lite / Intercept)
