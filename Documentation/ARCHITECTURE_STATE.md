# ARCHITECTURE_STATE.md

Current snapshot of project implementation status.
Last updated after: Step 24 — Debug Log Filter System (hierarchical Inspector filter, DebugFilterProfile, live-apply).

------------------------------------------------------------------------

## Currently Implemented Systems

### Project Foundation
- **EntityId** — immutable ulong identifier with thread-safe generation
- **SimulationClock** — pure C# time manager (pause/resume/timeScale)
- **WorldEntity** — base class for all domain entities
- **Assembly Definitions** — 7 asmdef files enforcing layer boundaries

### Debug Infrastructure
- **GameDebug** — static API: Log, CaptureSnapshot, ExportBundle, RunInvariantChecks, GetStatus, BuildBundle
- **DebugEvent / RingBuffer** — 1000 events, 200 errors, 20 snapshots
- **DebugSnapshot** — full world state capture via provider pattern
- **IDebugSnapshotProvider** — interface for subsystem contributions
- **BuiltInSnapshotProviders** — 5 providers: World, Ships, Economy, Docking, SOI
- **DebugInvariantChecker** — 7 automated checks
- **DebugExportUtility** — JSON serialization + file export; filter state included in metadata
- **DebugBundle** — complete export package with filter metadata

### Debug Log Filter System (Step 24)
- **DebugFilter** — pure C# runtime filter; checks category + source tag + severity; errors always pass
- **DebugFilterProfile** — ScriptableObject with hierarchical filter groups; create via menu SpaceSim -> Create Debug Filter Profile
- **DebugFilterGroup** — master toggle + list of DebugCategory names + child DebugTagToggle entries
- **DebugFilterProfileEditor** — custom Inspector rendering groups as collapsible tree with indented tag checkboxes
- **DebugFilterProfileCreator** — editor menu to create pre-configured default profile asset

#### Filter architecture
- GameDebug.Log() checks `DebugFilter.IsAllowed(category, severity, source)` before recording
- Errors (severity == Error) **always pass** regardless of filter — invariant violations and crashes are never hidden
- Filter applied at write time: filtered events never enter RingBuffer and never appear in bundle export
- BundleMetadata includes `FilterSummary`, `MutedCategories`, `MutedTags`, `TotalEventsFiltered` so analysts know what was active
- GameBootstrap holds `[SerializeField] DebugFilterProfile` — applied at startup via `ApplyTo(GameDebug.ActiveFilter)`
- Live-apply during Play mode: DebugFilterProfileEditor calls `ApplyTo()` on every Inspector change; GameBootstrap.OnValidate() also re-applies
- `GameDebug.ActiveFilter` exposed as public static for direct runtime access

#### Default filter groups
| Group | Categories | Source Tags |
|---|---|---|
| Navigation | SHIPS, PATH | Navigation, Docking |
| Economy | ECONOMY | CargoTransfer, Production, TradeAI |
| Orbits & SOI | ORBIT | SOI |
| NPC | NPC | — |
| Simulation | SIM | SystemLoader |
| UI | UI | — |
| Debug System | DEBUG | GameDebug, InvariantChecker |

#### Usage workflow
1. Create profile: SpaceSim menu → Create Debug Filter Profile (or Assets → Create → SpaceSim → Debug Filter Profile)
2. Assign profile to GameBootstrap's `debugFilterProfile` field
3. In Inspector, expand/collapse groups, toggle master switches and individual tags
4. Changes apply live during Play mode — no restart needed
5. Export bundle: filter state is recorded in metadata JSON

### Orbital Sandbox
- **CelestialBody** — domain entity with orbital/spin data, parent-child hierarchy, ShipInfo, StationInfo, SOIRadius
- **StarSystem** — container for body ids
- **WorldRegistry** — central entity lookup
- **OrbitalPositionCalculator** — full Keplerian orbit position calculation
- **KeplerSolver** — Newton-Raphson solver for Kepler's equation
- **OrbitSampler** — adaptive subdivision and uniform fallback
- **OrbitalMapRenderer** — scene visuals, orbit lines via OrbitSampler
- **CelestialBodyView** — visual binding

### Fully Symmetric SOI Navigation — Patched-Conics Lite (Steps 19 + 20)

Three-case SOI frame switching. `HandleSOITransition()` receives both `previousSOIBodyId` and `newSOIBodyId`.
Anti-jitter via `_lastFrameSwitchTime` (MinFrameSwitchInterval = 2.0 sim-s).

### Route Safety Check (Step 21)

**RouteSafetyChecker** — pure C# static helper. Validates route against large bodies. Called inside ManeuverPlanner.

### Transfer Planning Lite (Step 22)

**TransferPlannerLite** — retained for reference; superseded by ManeuverPlanner.

### Maneuver Planning Foundation (Step 23, Iteration 3 + Bugfixes)

**ManeuverPlanner** — pure C# static helper in Simulation.Ships.
- ImmediateDirectScore, PlannedDepartureTime, retry spam guard — see Step 23 docs.

------------------------------------------------------------------------

## Debug Filter Coding Standards

### When adding new GameDebug.Log() calls

Every `GameDebug.Log()` call MUST include a meaningful `source:` parameter that serves as the filterable tag. Use existing tags where applicable:

| Source Tag | Used For |
|---|---|
| Navigation | Ship route planning, maneuver selection, frame switching |
| Docking | Dock/undock lifecycle events |
| CargoTransfer | Load/unload cargo operations |
| Production | Station production cycle completions |
| TradeAI | Trade route selection by NPC traders |
| SOI | SOI boundary transitions |
| SystemLoader | Star system loading from assets/JSON |
| GameDebug | Debug system self-diagnostics |
| InvariantChecker | Invariant violation details |

When introducing a new subsystem, add a new source tag and register it in DebugFilterProfile.PopulateDefaults() under the appropriate group.

### When delivering a new feature step

Each step delivery MUST include:
1. **Recommended filter profile** for testing — which groups/tags to enable, which to disable
2. **Expected log output** — what log messages should appear with the recommended filter
3. If new source tags are added: update to DebugFilterProfile.PopulateDefaults()

Example:
```
Рекомендуемый фильтр для тестирования Phase 25:
  ✓ Navigation (все теги)
  ✓ Orbits & SOI (все теги)
  ✗ Economy (отключить целиком)
  ✗ NPC (отключить целиком)
  ✓ Debug System (все теги)

Ожидаемые логи:
  [SHIPS] [Nav] X: selected maneuver score ...
  [ORBIT] [Nav] SOI: X: Terra → Sol ...
```

------------------------------------------------------------------------

## Deferred Systems

| System | Notes |
|---|---|
| Burn Windows / Phase Alignment (Phase 25) | PlannedDepartureTime already on ShipInfo; NPCShipScheduler auto-depart at window |
| Patched Conics Full (Phase 26) | True conic sections per SOI segment |
| Delta-v / Energy Model (Phase 27) | Maneuver cost and route comparison |
| Hohmann Helper (Phase 28) | Optional baseline estimator |
| Advanced Transfers (Phase 29) | Lambert-lite, intercept, non-coplanar |
| Prices / Money | Currency, buy/sell prices |
| Player trading UI | Buy/sell interface |
| Save/Load | WorldRegistry serialization |
| Multi-system support | Multiple star systems |
| Combat, Ship modules, Factions, Contracts | Future systems |

------------------------------------------------------------------------

## Architecture Health Notes

### Clean boundaries maintained
- DebugFilter is pure C# — no UnityEngine dependency
- DebugFilterProfile is a ScriptableObject in Debug assembly (allowed — Debug assembly has noEngineReferences: false)
- DebugFilterProfileEditor lives in Debug assembly under #if UNITY_EDITOR
- GameBootstrap is the sole integration point between DebugFilterProfile and GameDebug
- Filter logic runs at log-write time with zero heap allocations per check (HashSet lookups)
- All existing simulation/world layer code unchanged — filter is transparent to callers

### Known technical debt (inherited + new)
- DebugFilterProfile.TryParseCategory uses switch statement instead of Enum.TryParse (avoids boxing/allocation, intentional)
- DebugFilterProfile.PopulateDefaults() must be manually updated when new source tags are added — not auto-discovered
- No runtime UI for filter control — Inspector only (acceptable for development phase)

------------------------------------------------------------------------

## Next Recommended Development Phase

### Phase 25 — Burn Windows / Phase Alignment
(unchanged from Step 23)
