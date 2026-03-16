# ARCHITECTURE_STATE.md

Current snapshot of project implementation status.
Last updated after: Step 13b — Trade AI Bugfix (same-station cargo ops, random loading).

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
- Context menu actions on GameBootstrap
- GameBootstrap updates `SetContext()` each frame
- Coordinator registers all snapshot providers and sets WorldRegistry for invariant checking

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
- **StarSystemBuilder** — pure C# builder (5 phases)
- **StarSystemLoader** — Unity-side adapter
- **SampleSystemAssetCreator** — editor menu to create sample asset
- **SampleStarSystemFactory** — hardcoded fallback

### Stations Foundation
- Two station kinds: **Orbital** (AttachmentMode.Orbit) and **Surface** (AttachmentMode.Surface)
- **StationInfo** — station metadata: StationKind, SurfaceLatitudeDeg, SurfaceLongitudeDeg, DockingInfo, Storage, Production, **Demand**
- Sample stations: Орбита-1 (Terra, 3 ports), Терра-1 (Terra surface, 2 ports), Фобос (Ares, 2 ports), Арес-1 (Ares surface, 2 ports)

### Docking Foundation
- **DockingPort** — single port model
- **DockingInfo** — port container on StationInfo
- **DockingSystem** — pure C# simulation service in Simulation layer

### Economy + Cargo Foundation
- **ResourceType** — enum: Food, Metals, Fuel, Electronics
- **StationStorage** — dictionary-based resource storage on stations
- **ShipCargo** — dictionary-based cargo hold on ships with capacity enforcement
- **CargoTransferService** — pure C# service: LoadFromStation, UnloadToStation, UnloadAll, LoadAny
- **StationEconomyConfig** — hardcoded initial resource loadouts per station
- **EconomyInitializer** — called after star system build

### Station Production Foundation
- **StationProductionRecipe** — pure data model in World layer
- **StationProductionState** — progress tracker in World layer
- **StationProductionConfig** — hardcoded recipes per station localization key in Simulation layer
- **StationProductionSystem** — pure C# simulation service in Simulation layer
- **Production chain** (sample system):
  - Терра-1: produces 10 Food / 5s (basic, no inputs)
  - Орбита-1: consumes 8 Food → produces 5 Electronics / 8s
  - Арес-1: consumes 4 Electronics → produces 8 Metals / 7s
  - Фобос: consumes 6 Metals → produces 12 Fuel / 6s

### Station Demand + Trade Opportunity System
- **StationDemand** — World layer data model: demand scores and surplus scores per resource type
- **TradeOpportunity** — World layer data struct: resource, source station, destination station, priority score
- **TraderJob** — World layer data model: resource, source/destination station, phase (GoingToSource/LoadingAtSource/GoingToDestination/UnloadingAtDestination)
- **StationDemandEvaluator** — Simulation layer service, evaluates all stations periodically
  - Demand from production inputs: if storage < inputPerCycle × 3, demand increases
  - General demand: any resource below 20 units gets low-priority demand
  - Surplus: resource above 50 units, boosted for production outputs
- **TradeOpportunityResolver** — Simulation layer service, scans all station pairs
  - Score formula: (demand × surplus) / distance
  - Sorted by score descending — best opportunities first
- **ShipInfo.CurrentTradeJob** — nullable TraderJob field tracks active trade assignment
- **StationInfo.Demand** — nullable StationDemand field, updated by evaluator
- **NPCShipScheduler trader behavior**:
  - Traders ask TradeOpportunityResolver for best opportunity
  - Creates TraderJob with correct phase management
  - If docked at source: loads cargo immediately, phase = GoingToDestination
  - If not at source: phase = GoingToSource, coordinator updates to LoadingAtSource on dock
  - Targeted cargo ops: at source loads only target resource, at destination unloads target resource
  - Station validation: cargo ops verify ship is at correct station for current phase
  - Without active job: traders only unload (no random loading)
  - Falls back to random station if no opportunities exist
  - Debug logging: OnTradeRouteSelected logs route selection (category ECONOMY, source TradeAI)
- **OrbitalSandboxCoordinator**:
  - Creates and wires StationDemandEvaluator + TradeOpportunityResolver
  - Initial evaluation on setup
  - Periodic re-evaluation every demandEvalInterval sim-seconds (configurable, default 5s)
  - OnShipDocked updates TraderJob phase (GoingToSource→LoadingAtSource, GoingToDestination→UnloadingAtDestination)

### Verified Trade Behavior (from debug bundles at t=6000+ sim-s)
- 0 errors, 0 invariant violations, 0 same-station load+unload incidents
- Trade routes follow production chain exactly:
  - **Food: Терра-1 → Орбита-1** (8 trips) — feeds electronics production
  - **Metals: Арес-1 → Фобос** (8 trips) — feeds fuel production
  - **Electronics: Орбита-1 → Арес-1** (5 trips) — feeds metals production
- Every cargo transfer is a clean pair: load at source, unload at different destination
- Self-organizing circular trade network confirmed

### Selection and Interaction
- **SelectionService** — pure C# selection state with events
- **SelectionBridge** — selection ring, highlight, camera focus
- **BodyClickHandler** — raycast click selection (UI Toolkit aware)

### UI Panels
- **ObjectListPanelController** — hierarchical body list with PNG icon support
- **ObjectDetailsPanelController** — compact body properties panel
- **DetailModalController** — full-screen modal window with tabbed interface
- **TimeControlsPanelController** — pause + x1/x10/x100
- **UIInputBlocker** — blocks camera zoom/pan/rotate when mouse is over UI panels
- **BodyIconResolver** — maps body types to PNG icon paths
- **UIStrings** — Russian strings including demand/surplus/trade labels

### Camera and Labels
- **OrbitalCameraController** — pan/zoom/rotate/smooth focus
- **BodyLabelController** — IMGUI labels with zoom fade

### Ships Foundation
- CelestialBody with BodyType.Ship + ShipInfo (includes CurrentTradeJob)

### Ship Movement (Anchored Travel Model)
- **ShipMovementSystem** — pure C# simulation, unchanged

### NPC Scheduling
- **NPCShipScheduler** — pure C# scheduler with demand-driven trader routing

### SOI Foundation
- **SOIResolver** — unchanged

### Bootstrap and Coordination
- **GameBootstrap** — unchanged
- **OrbitalSandboxCoordinator** — wires all systems including demand/trade

------------------------------------------------------------------------

## Current Temporary Architectural Decisions

### Ships as CelestialBody + ShipInfo
Same as before. CurrentTradeJob added to ShipInfo.

### Stations as CelestialBody + StationInfo
Same as before. StationDemand added to StationInfo.

### Transit parenting to root star
Unchanged.

### Inspector serialization: float not double
Unchanged. demandEvalInterval added as float.

### No prices or money
Cargo transfer is free and instant. Demand/surplus scoring is not price-based.

### Demand thresholds are hardcoded
StationDemandEvaluator uses hardcoded thresholds. Future: configurable per station.

### Trade scoring is simple
Score = demand × surplus / distance. No multi-hop optimization or competing trader avoidance.

------------------------------------------------------------------------

## Deferred Systems

| System | Notes |
|---|---|
| Prices / Money | Currency, buy/sell prices, supply/demand |
| Dynamic pricing | Price fluctuation based on supply/demand |
| Player trading UI | Buy/sell interface for player at docked station |
| Complex factories | Multi-input, multi-output production |
| Economic AI | Multi-hop routes, profit maximization |
| Trader competition | Multiple traders avoiding same routes |
| Storage capacity limits | Per-resource caps on stations |
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
- All demand/trade classes are pure C# — no UnityEngine references
- StationDemand, TradeOpportunity, TraderJob are pure data in World layer
- StationDemandEvaluator and TradeOpportunityResolver are pure C# in Simulation layer
- NPCShipScheduler remains pure C# with TradeResolver injected
- No existing system APIs changed — only new methods/fields added

### Known technical debt
- SampleStarSystemFactory uses Russian display names (should use localization keys)
- Unity 6 EntityId conflict requires using-alias in Rendering/UI files
- OnGUI labels — acceptable for MVP
- Ship travel is linear interpolation (not physically realistic)
- Docking approach is linear interpolation in local space
- Transit parenting is a UI convenience hack
- ObjectListPanelController.Refresh() rebuilds entire list (fine at current scale)
- SOI values are placeholder approximations
- Surface station position is fixed relative to parent (no spin coupling)
- Docking port positions are auto-generated
- Docked ship visual is just positioned at port
- DebugExportUtility uses manual JSON builder
- Debug JSON locale issue: double formatting uses system locale (comma vs dot)
- Production recipes are hardcoded in StationProductionConfig
- StationStorage has no per-resource capacity limit
- Demand thresholds are hardcoded constants
- Trade scoring is simple (no multi-hop, no competing trader avoidance)
- Single trader — trade network would benefit from multiple traders

------------------------------------------------------------------------

## Next Recommended Development Phase

**Option A: Player Trading UI** — buy/sell interface when player ship is docked. First player interaction with economy.

**Option B: More Ships / Civilian Traffic** — more trader ships to see the trade network busy with visible traffic.

**Option C: Storage Capacity Limits** — per-resource caps create real scarcity and trade pressure.

**Option D: Trader Competition** — traders avoid picking the same routes, distribute across the network.

**Option E: Prices & Money** — add currency, station buy/sell prices based on demand/surplus.

Recommendation: **Option B or A** — more traders make the self-organizing network visibly alive; player trading UI closes the first gameplay loop.
