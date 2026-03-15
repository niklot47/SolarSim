# ARCHITECTURE_STATE.md

Current snapshot of project implementation status.
Last updated after: Step 12 — UI Update 1 (Green Terminal Theme, Collapsible Panels, Detail Modal, Icons, Input Blocking).

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
- Context menu actions on GameBootstrap: Экспорт бандла, Снимок состояния, Проверка инвариантов, Экспорт + Инварианты, Очистить
- GameBootstrap updates `SetContext()` each frame (scene, frame, simTime, timeScale, isPaused)
- Coordinator registers all snapshot providers and sets WorldRegistry for invariant checking

### Orbital Sandbox
- **CelestialBody** — domain entity with orbital/spin data, parent-child hierarchy, ShipInfo, StationInfo, SOIRadius
- **StarSystem** — container for body ids
- **WorldRegistry** — central entity lookup
- **OrbitalPositionCalculator** — circular orbit positions in XZ plane (MVP) + surface position from lat/lon
- **OrbitalMapRenderer** — scene visuals (spheres for bodies, cubes for stations), orbit lines, delegates position resolution to WorldPositionResolver
- **CelestialBodyView** — visual binding with role-based ship colors, station kind colors, station scale ×⅓

### World Position Resolution
- **WorldPositionResolver** — single source of truth for body world positions (Simulation layer, pure C#)
  - Resolution priority for ships: (1) Docked → station + port offset, (2) Override → travel/approach, (3) Orbital → parent chain
  - Orbital bodies: recursive parent chain + OrbitalPositionCalculator
  - Surface stations: parent position + lat/lon surface offset
  - Travelling/approaching ships: OverrideWorldPosition passthrough
  - Docked ships: station world position + port local offset (dynamic each tick)
  - Root bodies: position at origin
- OrbitalMapRenderer is a thin consumer — no simulation logic in Rendering

### Star System Data Loading
- **CelestialBodyDefinition / ShipDefinition / StationDefinition** — serializable Inspector-authored data
- **StarSystemDefinition** — ScriptableObject with body list + ship list + station list
- **StarSystemBuilder** — pure C# builder (5 phases: create, parents, register, ships, stations + docking ports for any station type)
- **StarSystemLoader** — Unity-side adapter, converts all definition types to build data (includes DockingPortCount)
- **SampleSystemAssetCreator** — editor menu to create sample asset (Sol system with 4 stations incl. Ares)
- **SampleStarSystemFactory** — hardcoded fallback (Sol, Terra, Ares, Venus, Luna + 3 ships + 4 stations)

### Stations Foundation
- Two station kinds: **Orbital** (AttachmentMode.Orbit) and **Surface** (AttachmentMode.Surface)
- **StationInfo** — station metadata: StationKind, SurfaceLatitudeDeg, SurfaceLongitudeDeg, DockingInfo, Storage, Production
- **StationDefinition** — Inspector-authored station data (includes DockingPortCount)
- Orbital stations: orbit + optional spin, orbit lines, cube visual (cyan), docking ports
- Surface stations: fixed position on parent surface via lat/lon, no orbit line, cube visual (orange), docking ports
- Station cubes rendered at ⅓ scale for visual clarity
- Sample stations: Орбита-1 (Terra, 3 ports), Терра-1 (Terra surface, 2 ports), Фобос (Ares, 2 ports), Арес-1 (Ares surface, 2 ports)

### Docking Foundation
- **DockingPort** — single port model: PortId, LocalPosition (SimVec3), OccupiedShipId, Occupy/Release
- **DockingInfo** — port container on StationInfo: GeneratePorts, RequestPort, ReleasePort, GetPortForShip
- **DockingSystem** — pure C# simulation service in Simulation layer
  - Orbital docking: `RequestOrbitalDocking()` — ship on station orbit → approach in station-local space → dock
  - Surface docking: `RequestSurfaceDocking()` — ship on planet orbit → approach in planet-local space → dock at surface station
  - `Update(simTime, posResolver)` — interpolates approaching ships using DockingReferenceBodyId local frame
  - `Undock(shipId, simTime)` — orbital: orbit around station; surface: orbit around parent planet
  - `OnShipDocked` / `OnShipUndocked` events
- **ShipInfo docking fields**: DockedAtStationId, DockedPortId, DockingStartTime/Duration/StartPosition, DockingReferenceBodyId, DockedAtTime
- **ShipMovementSystem surface station handling**: when destination is surface station, arrival body = station's parent planet, orbit = default (r=3.0), OnShipArrived fires with original station id
- **NPCShipScheduler docking behavior**:
  - `_pendingSurfaceDock`: tracks ships arriving at planet whose destination was a surface station
  - `_pendingDeparture`: prevents undocked ships from immediately re-docking (anti-loop)
  - Orbital stations: auto-dock after 0.5s delay when ship orbits station
  - Surface stations: auto-dock after 0.5s delay via pending surface dock tracking
  - After DockingWaitTime: undock → immediate departure
- Default timing: 3 ports per orbital station, 2 ports per surface station, 0.15 Mm port offset, 2 sim-s approach, 5 sim-s docked wait
- Docked ships visible and selectable, position follows station via WorldPositionResolver

### Economy + Cargo Foundation
- **ResourceType** — enum: Food, Metals, Fuel, Electronics
- **StationStorage** — dictionary-based resource storage on stations with Add/Remove/GetAmount
- **ShipCargo** — dictionary-based cargo hold on ships with capacity enforcement (Add/Remove/FreeSpace/TotalUsed)
- **CargoTransferService** — pure C# service: LoadFromStation, UnloadToStation, UnloadAll, LoadAny; validates Docked state; fires OnCargoTransferred event
- **StationEconomyConfig** — hardcoded initial resource loadouts per station (by localization key) and cargo capacity per ship role
- **EconomyInitializer** — called after star system build; creates StationStorage + StationProduction + ShipCargo on all entities
- **NPCShipScheduler trader behavior**:
  - When docked: unload all cargo → load available resources from station
  - Cargo operations happen once per docking (`_cargoHandled` set prevents repeated ops)
  - Traders prefer station destinations (`_stationCandidates` list)
  - After undock: `_pendingDeparture` → immediate departure to another station
- **UI display**: ObjectDetailsPanelController shows ship cargo and station storage as multiline text rows with short resource names
- **Debug logging**: all cargo transfers logged via GameDebug with category ECONOMY
- **Initial station resources**: Терра-1 → Food+Fuel, Орбита-1 → Electronics+Fuel, Арес-1 → Metals+Fuel, Фобос → Fuel+Metals
- **Verified by debug bundle**: total resources conserved across transfers (1120 total at start), no invariant violations

### Station Production Foundation
- **StationProductionRecipe** — pure data model in World layer: OutputResource, OutputAmountPerCycle, InputsPerCycle (dict), CycleTime
- **StationProductionState** — progress tracker in World layer: Progress (accumulated sim-seconds), LastUpdateTime, IsStalled, StallReason, TotalCyclesCompleted, ProgressFraction
- **StationProductionConfig** — hardcoded recipes per station localization key in Simulation layer
- **StationProductionSystem** — pure C# simulation service in Simulation layer
  - Accumulates progress with simulation delta time each tick
  - Checks input resource availability before cycle completion
  - Consumes inputs atomically per completed cycle
  - Adds output resource to StationStorage
  - Pauses (stalls) if required inputs missing in storage
  - Pauses if output storage would exceed capacity
  - Resumes automatically when conditions are met
  - Fires `OnProductionCycleCompleted` event (coordinator logs via GameDebug, category ECONOMY)
- **Production chain** (sample system):
  - Терра-1: produces 10 Food / 5s (basic, no inputs)
  - Орбита-1: consumes 8 Food → produces 5 Electronics / 8s
  - Арес-1: consumes 4 Electronics → produces 8 Metals / 7s
  - Фобос: consumes 6 Metals → produces 12 Fuel / 6s
- **StationInfo.Production** — nullable field, initialized by EconomyInitializer from StationProductionConfig
- **UI display**: ObjectDetailsPanelController shows production info with output rate, input rate, stall status
- **UIStrings**: short resource names (Еда, Мет., Топл., Элек.) and production labels (Произв., Стоп)
- **OrbitalSandboxScreen.uxml**: details-production-row for station production display
- **Integration**: StationProductionSystem ticked after NPCShipScheduler, before SOIResolver
- **Verified by debug bundle**: production cycles logged, resources produced/consumed correctly, 0 errors, 0 invariant violations

### Selection and Interaction
- **SelectionService** — pure C# selection state with events
- **SelectionBridge** — selection ring, highlight, camera focus
- **BodyClickHandler** — raycast click selection (UI Toolkit aware)

### UI Panels
- **ObjectListPanelController** — hierarchical body list with PNG icon support (32x32, fallback colored circles), hover highlight with border, dynamic refresh, collapsible panel header (shrinks to header-only width)
- **ObjectDetailsPanelController** — compact body properties panel with collapsible header (shrinks to header-only), large 300x300 icon display, ship/station fields, "Детальней" button opens modal
- **DetailModalController** — full-screen modal window with 48x48 icon in header, tabbed interface: Детали/Рынок/Задания/Модули/Ангар; close via X or overlay click; live-updates details tab; hides IMGUI labels while open via BodyLabelController.Visible
- **TimeControlsPanelController** — pause + x1/x10/x100
- **UIInputBlocker** — blocks camera zoom/pan/rotate when mouse is over UI panels or modal is open; WheelEvent stoppers on all blocking panels; lives in Rendering layer
- **BodyIconResolver** — maps body types to PNG icon paths, loads via Resources.Load from Icons/32x32/ and Icons/300x300/
- **UIStrings** — Russian strings: all UI labels, modal tabs/sections/placeholders
- **OrbitalSandboxScreen.uxml/.uss** — green terminal aesthetic with CSS custom properties for theme colors, collapsible panels, themed scrollbars (8px green), list hover highlight, detail/modal icon elements

### Camera and Labels
- **OrbitalCameraController** — pan/zoom/rotate/smooth focus (Input System), `BlockInput` property suppresses all input when UI has focus
- **BodyLabelController** — IMGUI labels with zoom fade, `Visible` property hides labels when modal is open (prevents IMGUI rendering over UI Toolkit panels)

### World Units and Scaling
- **WorldUnits** — Mm distances, sim-s time
- **SceneScaleConfig** — DistanceScale, BodyRadiusScale, MinBodyDiameter

### Ships Foundation
- CelestialBody with BodyType.Ship + ShipInfo
- **ShipRole**: Player, Trader, Patrol, Civilian
- Visual differentiation by role color
- Full integration: selection, labels, details panel, camera focus

### Ship Movement (Anchored Travel Model)
- **ShipState**: Idle, Orbiting, Travelling, Arrived, ApproachingStation, Docking, Docked
- **RouteFrame**: Global, LocalParent — determines interpolation frame
- **ShipRoute**: origin/destination ids, departure time, duration, frame selection, pre-computed start/arrival positions (world and local), destination orbit parameters, arrival phase
- **ShipMovementSystem**: pure C# simulation, ticked by coordinator
  - Anchored departure from current orbital world position (no snap)
  - Surface station destination → arrival at parent body orbit (r=3.0, P=12.0)
  - Orbital station destination → arrival at small station orbit (r=0.5, P=8.0)
  - Two interpolation modes: Global frame and LocalParent frame
  - Phase-matched orbit assignment on arrival (no visible snap)
  - **OnShipArrived** event with original destination id (surface station id preserved)

### NPC Scheduling
- **NPCShipScheduler** — pure C# scheduler, role-based route selection
  - Trader: prefers station destinations, performs cargo load/unload when docked
  - Civilian: random planet/moon/station destination
  - Patrol: ping-pong between two bodies
  - Player ships ignored
  - Distance-based travel duration
  - Automatic docking at orbital stations (ship orbits station → dock)
  - Automatic docking at surface stations (ship arrives at planet orbit → pending surface dock → dock)
  - Automatic undocking after DockingWaitTime with immediate departure (`_pendingDeparture`)
  - Configurable via Inspector: TravelSpeed, MinTravelDuration, IdleDelay, DockingWaitTime
  - Listens to ShipMovementSystem.OnShipArrived for surface station tracking
  - Injected: DockingSystem, CargoTransferService, PositionResolver

### SOI Foundation (Sphere of Influence)
- **CelestialBody.SOIRadius** — nullable double, defines sphere of influence in Mm
- **ShipInfo.CurrentSOIBodyId** — tracks which body's SOI currently dominates each ship
- **SOIResolver** — pure C# service, dominance rule: smallest SOI wins
- SOI transitions logged via GameDebug (category ORBIT)
- Sample system SOI values: Sol=1000, Terra=60, Ares=40, Venus=30, Luna=8

### Bootstrap and Coordination
- **GameBootstrap** — creates clock, initializes debug (export dir, context updates), calls coordinator
- **OrbitalSandboxCoordinator** — loads system, initializes economy, creates and wires all services (incl. CargoTransferService, StationProductionSystem), registers debug snapshot providers, ticks simulation in order: ShipMovementSystem → DockingSystem → NPCShipScheduler → StationProductionSystem → SOIResolver, logs events via GameDebug (production, cargo, ships, SOI), syncs Inspector params

------------------------------------------------------------------------

## Current Temporary Architectural Decisions

### Ships as CelestialBody + ShipInfo

**Why acceptable now:**
- Ships share capabilities with celestial bodies (hierarchy, orbits, selection, rendering)
- ShipInfo keeps ship-specific data contained including docking + cargo fields
- No code duplication — shared pipeline

**When to change:**
- If ships gain complex unique state (modules, AI trees, damage)

### Stations as CelestialBody + StationInfo

**Why acceptable now:**
- Stations share hierarchy, orbits, rendering, selection
- StationInfo + DockingInfo + StationStorage + StationProductionState keeps station-specific data contained
- Both orbital and surface stations support docking, storage, and production

**When to change:**
- If stations gain complex state (multi-recipe factories, refueling services, complex inventories)

### Transit parenting to root star

During travel, ships are temporarily parented to the root star for hierarchy visibility. Position comes from OverrideWorldPosition.

### Inspector serialization: float not double

NPC scheduling and docking parameters use `float` in SerializeField. Coordinator converts to double.

### SOI values are authored placeholders

Hand-picked for visual testing, not astrophysically accurate.

### Docking port positions are auto-generated

Ports evenly spaced in XZ plane at 0.15 Mm from station center.

### Economy config is hardcoded

StationEconomyConfig maps station localization keys to initial resources. Future: move to ScriptableObjects or JSON.

### Production recipes are hardcoded

StationProductionConfig maps station localization keys to recipes. Future: move to ScriptableObjects or data-driven config.

### No prices or money

Cargo transfer is free and instant. Foundation only — pricing deferred.

### Trader route selection is random

Traders pick a random different station. No demand/supply-driven routing yet.

------------------------------------------------------------------------

## Deferred Systems

| System | Notes |
|---|---|
| Prices / Money | Currency, buy/sell prices, supply/demand |
| Dynamic supply/demand | Production creates natural supply; demand routing deferred |
| Market simulation | Price fluctuation based on supply/demand |
| Player trading UI | Buy/sell interface for player at docked station |
| Complex factories | Multi-input, multi-output production |
| Economic AI | Smart trader routing based on prices/demand |
| Contracts | Task definition, assignment |
| Factions | Entities, relationships, territory |
| AI behavior | Autonomous ship decisions beyond simple scheduling |
| Advanced navigation | Pathfinding, waypoints, SOI-aware routing |
| Orbital transfers | Hohmann, delta-v, patched conics (SOI foundation ready) |
| Combat | Weapons, damage |
| Ship modules | Equipment slots |
| Save/Load | WorldRegistry serialization |
| Procedural planets | PPG Lite integration (documented in PDF) |
| Elliptical orbits | Kepler equation solver |
| Inclined orbits | 3D orbit planes |
| LOD system | CelestialBodyView.SetRepresentationMode() stub exists |
| SOI visualization | Debug gizmo/wireframe spheres for SOI boundaries |
| SOI-based reparenting | Automatic orbit conversion on SOI crossing |
| Docking animations | Visual effects for approach/dock/undock |
| Station interiors | Interior view when docked |
| Fuel/repair | Station services requiring docking |

------------------------------------------------------------------------

## Architecture Health Notes

### Clean boundaries maintained
- Simulation and World layers have zero UnityEngine references (asmdef enforced)
- WorldPositionResolver is pure C# — single source of truth (incl. docked positions)
- DockingSystem is pure C# — uses position resolver delegate, no rendering dependency
- CargoTransferService is pure C# — validates docking state, fires events
- StationProductionSystem is pure C# — uses WorldRegistry and StationStorage only, fires OnProductionCycleCompleted event (coordinator handles logging)
- StationProductionRecipe and StationProductionState are pure data in World layer
- StationProductionConfig is pure C# in Simulation layer
- EconomyInitializer is pure C# — no Unity dependency
- SOIResolver is pure C# — uses WorldPositionResolver
- ShipMovementSystem is pure C# — surface station logic is internal to simulation
- NPCShipScheduler is pure C# with DockingSystem, CargoTransferService, and position resolver injected
- OrbitalMapRenderer contains zero simulation logic
- All rendering in Rendering layer, all UI in UI layer
- Debug system exports to disk via DebugExportUtility (no Unity serialization dependency for JSON)

### Known technical debt
- SampleStarSystemFactory uses Russian display names (should use localization keys)
- Unity 6 EntityId conflict requires using-alias in Rendering/UI files
- OnGUI labels — acceptable for MVP, hidden when modal is open via Visible flag
- Ship travel is linear interpolation (not physically realistic)
- Docking approach is linear interpolation in local space (no curved paths)
- Transit parenting is a UI convenience hack
- ObjectListPanelController.Refresh() rebuilds entire list (fine at current scale)
- SOI values are placeholder approximations
- Surface station position is fixed relative to parent (no spin coupling)
- Docking port positions are auto-generated (not authored)
- Docked ship visual is just positioned at port — no special docking visual feedback
- DebugExportUtility uses manual JSON builder (no third-party JSON library)
- Debug JSON locale issue: double formatting uses system locale (comma vs dot) — needs CultureInfo.InvariantCulture fix
- Trader NPC route selection is random among stations — can ping-pong between same two stations
- Production recipes are hardcoded in StationProductionConfig (should be ScriptableObjects)
- StationStorage has no per-resource capacity limit (CapacityPerResource defaults to 0 = unlimited)

------------------------------------------------------------------------

## Next Recommended Development Phase

**Option A: Prices & Money** — add currency, buy/sell prices at stations, trader profit motive. Creates real economic incentive loop.

**Option B: Player Trading UI** — buy/sell interface when player ship is docked. First player interaction with economy.

**Option C: Storage Capacity Limits** — per-resource caps on stations to create real scarcity and trade pressure.

**Option D: More Ships / Civilian Traffic** — more trader ships, civilian behavior, busier sandbox with visible trade traffic.

**Option E: Demand-Driven Routing** — traders prefer stations that need their cargo (stations consuming inputs).

Recommendation: **Option B or E** — player trading UI closes the first gameplay loop; demand-driven routing makes the production chain visibly useful as traders intelligently carry Food to Орбита-1, Electronics to Арес-1, etc.
