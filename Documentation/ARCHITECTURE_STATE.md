# ARCHITECTURE_STATE.md

Current snapshot of project implementation status.
Last updated after: Step 14 — External Star System Import Foundation (JSON loader).

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
- **OrbitalPositionCalculator** — circular orbit positions in XZ plane (MVP) + surface position from lat/lon
- **OrbitalMapRenderer** — scene visuals, orbit lines, delegates position resolution to WorldPositionResolver
- **CelestialBodyView** — visual binding with role-based ship colors, station kind colors, station scale ×⅓

### World Position Resolution
- **WorldPositionResolver** — single source of truth for body world positions (Simulation layer, pure C#)

### Star System Data Loading
- **CelestialBodyDefinition / ShipDefinition / StationDefinition** — serializable Inspector-authored data
- **StarSystemDefinition** — ScriptableObject with body list + ship list + station list
- **StarSystemBuilder** — pure C# builder (5 phases: bodies, parents, register, ships, stations)
- **StarSystemLoader** — Unity-side adapter, converts ScriptableObject definitions to build data
- **SampleSystemAssetCreator** — editor menu to create sample asset
- **SampleStarSystemFactory** — hardcoded fallback

### External Star System Import (NEW)
- **StarSystemJsonDto** — pure DTO classes: StarSystemJson, BodyJson, OrbitJson, SpinJson, StationJson, ShipJson
- **StarSystemJsonValidator** — validates JSON structure before conversion:
  - Duplicate key detection across bodies, stations, ships
  - Parent key existence validation
  - Orbit parameter validity (positive period, non-negative semi-major axis)
  - Body type, station kind, ship role enum validation
  - Self-parenting detection
  - Returns JsonValidationResult with errors and warnings
- **JsonStarSystemImporter** — adapter: JSON → StarSystemBuildData → StarSystemBuilder
  - LoadFromTextAsset(TextAsset, WorldRegistry) — for Inspector-assigned files
  - LoadFromFile(string path, WorldRegistry) — for runtime file loading
  - LoadFromJson(string json, WorldRegistry) — core method
  - Returns ImportResult with Success, System, Validation, counts, message
  - Full error handling at every step (parse, validate, convert, build)
- **SampleExternalSystem.json** — example JSON file matching the sample Sol system
- **OrbitalSandboxCoordinator** — updated with `externalSystemFile` Inspector field
  - Fallback chain: External JSON → StarSystemDefinition → SampleStarSystemFactory
  - Debug logging via GameDebug for all import paths

### Stations Foundation
- Two station kinds: **Orbital** and **Surface**
- **StationInfo** — station metadata including Demand field
- Sample stations: Орбита-1, Терра-1, Фобос, Арес-1

### Docking Foundation
- **DockingPort**, **DockingInfo**, **DockingSystem** — unchanged from previous step

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
- Unchanged from previous step

### UI Panels
- Unchanged from previous step

### Camera and Labels
- Unchanged from previous step

### Ships, Ship Movement, NPC Scheduling, SOI, Bootstrap
- Unchanged from previous step (except coordinator LoadStarSystem method)

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
JsonUtility requires [Serializable] and public fields. Nested objects (orbit, spin) must not be null in JSON. Future: consider Newtonsoft JSON for more flexible parsing (nullable fields, comments, etc.).

### JSON import lives in Data layer
JsonStarSystemImporter uses UnityEngine.TextAsset and JsonUtility, so it belongs in SpaceSim.Data (which allows Unity references). The DTO classes and validator also live in Data layer for colocation.

### JSON schema is not versioned
No schema version field yet. Future: add "schemaVersion" field for migration support.

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
| Elliptical/inclined orbits | Kepler equation solver, 3D orbit planes |

------------------------------------------------------------------------

## Architecture Health Notes

### Clean boundaries maintained
- JSON DTO classes are pure [Serializable] C# — no logic, only data
- StarSystemJsonValidator is pure C# — no Unity dependency (lives in Data for colocation)
- JsonStarSystemImporter is in Data layer — uses UnityEngine.JsonUtility and TextAsset (appropriate)
- No changes to Simulation or World layers
- StarSystemBuilder, WorldRegistry, ShipMovementSystem, DockingSystem — NOT modified
- Import pipeline feeds into existing StarSystemBuildData → StarSystemBuilder.Build() pipeline
- Both ScriptableObject and JSON pipelines produce identical runtime entities

### Known technical debt
- All previous technical debt items remain
- JsonUtility does not support Dictionary — orbit/spin are separate nested objects
- JsonUtility does not support null reference types — missing orbit/spin fields default to type defaults
- JSON validation warnings are logged but do not prevent loading
- No JSON schema version field for future migration

------------------------------------------------------------------------

## Star System Loading Pipelines

### Pipeline 1: ScriptableObject (existing, unchanged)
```
StarSystemDefinition (ScriptableObject)
    → StarSystemLoader.Load()
    → StarSystemBuildData
    → StarSystemBuilder.Build()
    → WorldRegistry + StarSystem
```

### Pipeline 2: External JSON (NEW)
```
JSON file (TextAsset or file path)
    → JsonStarSystemImporter.LoadFromTextAsset() / LoadFromFile() / LoadFromJson()
    → StarSystemJson (DTO)
    → StarSystemJsonValidator.Validate()
    → ConvertToBuildData() → StarSystemBuildData
    → StarSystemBuilder.Build()
    → WorldRegistry + StarSystem
```

### Coordinator fallback chain
```
externalSystemFile (TextAsset) assigned?
    → YES: JsonStarSystemImporter.LoadFromTextAsset()
        → Success: use result
        → Fail: log errors, fall through ↓
starSystemDefinition (ScriptableObject) assigned?
    → YES: StarSystemLoader.Load()
        → Success: use result
        → Fail: fall through ↓
SampleStarSystemFactory.Create() (built-in fallback)
```

------------------------------------------------------------------------

## Next Recommended Development Phase

**Option A: Player Trading UI** — buy/sell interface when player ship is docked.

**Option B: More Ships / Civilian Traffic** — more trader ships for visible trade network.

**Option C: JSON Editor Tool** — in-editor preview and validation of JSON star system files.

**Option D: Prices & Money** — currency, station buy/sell prices based on demand/surplus.

**Option E: Save/Load Foundation** — serialize WorldRegistry state to JSON, reload.

Recommendation: **Option A or B** — player trading UI closes the first gameplay loop; more traders make the network visibly alive.
