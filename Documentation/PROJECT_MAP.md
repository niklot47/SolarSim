# PROJECT_MAP.md

## Project Overview

Modular sandbox space simulation built in Unity LTS using URP and UI Toolkit.

Long-term goals: orbital simulation sandbox, ships and stations, NPC traffic and tasks, economy and trade, faction and clan politics, maintainable architecture for humans and AI assistants.

------------------------------------------------------------------------

## Architecture Layers

### 1. Simulation

Pure game simulation logic. Time progression, orbital calculations, selection state, star system building, ship movement, NPC scheduling, world position resolution, SOI resolution, docking, economy/cargo transfer.

Rules:
- Must NOT depend on UnityEngine
- Pure C# only
- Deterministic and testable
- Enforced via asmdef with `noEngineReferences: true`

Key files:
- `Scripts/Simulation/Time/SimulationClock.cs` — simulation clock (pause, resume, time scale)
- `Scripts/Simulation/Core/SelectionService.cs` — tracks selected entity id, fires events
- `Scripts/Simulation/Core/SampleStarSystemFactory.cs` — hardcoded fallback sample system (Sol, Terra, Ares, Venus, Luna + 3 ships + 4 stations with docking)
- `Scripts/Simulation/Core/StarSystemBuilder.cs` — builds runtime entities from pure build data (bodies + ships + stations + docking ports for any station type)
- `Scripts/Simulation/Core/WorldPositionResolver.cs` — single source of truth for body world positions (orbital, surface, override, docked — docked check has priority over override)
- `Scripts/Simulation/Orbits/OrbitalPositionCalculator.cs` — circular orbit position in XZ plane (MVP) + surface position from lat/lon
- `Scripts/Simulation/Ships/ShipMovementSystem.cs` — anchored travel with Global/LocalParent frame selection, predictive arrival, phase-matched orbit assignment; surface station destinations arrive at parent body orbit
- `Scripts/Simulation/Ships/NPCShipScheduler.cs` — auto-assigns routes to NPC ships, role-based destination selection (traders prefer stations), distance-based duration, automatic docking/undocking, cargo load/unload for traders, _pendingDeparture prevents re-docking loop, _pendingSurfaceDock tracks surface station arrivals, _cargoHandled prevents repeated cargo ops
- `Scripts/Simulation/SOI/SOIResolver.cs` — sphere of influence containment resolution, dominant body detection, ship SOI tracking with transition detection
- `Scripts/Simulation/Docking/DockingSystem.cs` — docking lifecycle: orbital approach (station-local interpolation), surface approach (planet-local interpolation), dock completion, undocking to appropriate orbit
- `Scripts/Simulation/Economy/CargoTransferService.cs` — cargo transfer operations between docked ships and stations: LoadFromStation, UnloadToStation, UnloadAll, LoadAny; validates Docked state; fires OnCargoTransferred event
- `Scripts/Simulation/Economy/StationEconomyConfig.cs` — hardcoded initial resource loadouts per station (by localization key), cargo capacity per ship role
- `Scripts/Simulation/Economy/EconomyInitializer.cs` — initializes StationStorage on all stations and ShipCargo on all ships after star system build

### 2. World

Game domain entities and shared world state models.

Rules:
- Independent from rendering, no MonoBehaviours
- `noEngineReferences: true` enforced via asmdef
- References only SpaceSim.Shared

Key files:
- `Scripts/World/Entities/WorldEntity.cs` — base class (EntityId + DisplayName)
- `Scripts/World/Entities/CelestialBody.cs` — body type, parent/child ids, orbit, spin, radius, SOIRadius, ShipInfo, StationInfo
- `Scripts/World/Entities/CelestialBodyType.cs` — enum: Star, Planet, Moon, Asteroid, Station, Ship, SurfaceSite
- `Scripts/World/Entities/AttachmentMode.cs` — enum: None, Orbit, Surface, LocalSpace
- `Scripts/World/Entities/ShipRole.cs` — enum: Player, Trader, Patrol, Civilian
- `Scripts/World/Entities/ShipState.cs` — enum: Idle, Orbiting, Travelling, Arrived, ApproachingStation, Docking, Docked
- `Scripts/World/Entities/ShipInfo.cs` — ship data: role, key, class, state, route, override position, currentSOIBodyId, Cargo, docking fields (DockedAtStationId, DockedPortId, DockingStartTime, DockingDuration, DockingStartPosition, DockingReferenceBodyId, DockedAtTime)
- `Scripts/World/Entities/ShipRoute.cs` — travel route: RouteFrame (Global/LocalParent), origin/destination, start/arrival world+local positions, destination orbit params, arrival phase
- `Scripts/World/Entities/ShipCargo.cs` — ship cargo hold: Dictionary<ResourceType, double>, Capacity, Add/Remove/FreeSpace/TotalUsed/IsEmpty/IsFull
- `Scripts/World/Entities/StationInfo.cs` — station data: StationKind (Orbital/Surface), surface lat/lon, DockingInfo, Storage
- `Scripts/World/Entities/StationStorage.cs` — station resource storage: Dictionary<ResourceType, double>, Add/Remove/GetAmount, optional capacity
- `Scripts/World/Entities/ResourceType.cs` — enum: Food, Metals, Fuel, Electronics
- `Scripts/World/Entities/DockingPort.cs` — single docking port: PortId, LocalPosition, OccupiedShipId
- `Scripts/World/Entities/DockingInfo.cs` — docking capability container: port list, GeneratePorts, RequestPort, ReleasePort
- `Scripts/World/Entities/StarSystem.cs` — container with root body ids and all body ids
- `Scripts/World/ValueTypes/OrbitDefinition.cs` — full Keplerian orbital elements
- `Scripts/World/ValueTypes/SpinDefinition.cs` — axial tilt, rotation period
- `Scripts/World/Systems/WorldRegistry.cs` — central entity registry (add/get/enumerate/children)

### 3. Rendering

Unity MonoBehaviour layer. Visualization, camera, scene object management, selection bridging, bootstrap coordination.

Rules:
- MonoBehaviours belong here
- Reads state from World and Simulation
- Must not contain core simulation logic — position resolution delegated to WorldPositionResolver
- Coordinates system lifecycle and wiring

Key files:
- `Scripts/Rendering/Bootstrap/GameBootstrap.cs` — Unity entry point, DontDestroyOnLoad singleton, creates SimulationClock, sets debug export directory, updates debug context each frame
- `Scripts/Rendering/Bootstrap/OrbitalSandboxCoordinator.cs` — wires all services (WorldPositionResolver, SOIResolver, DockingSystem, ShipMovementSystem, NPCShipScheduler, CargoTransferService), initializes economy, registers debug snapshot providers, loads star system, ticks simulation, manages transit parenting, logs events via GameDebug, syncs NPC/docking params from Inspector
- `Scripts/Rendering/Bootstrap/StarSystemLoader.cs` — converts ScriptableObject definitions (bodies + ships + stations + docking ports) to pure build data, calls StarSystemBuilder
- `Scripts/Rendering/Orbits/OrbitalMapRenderer.cs` — creates/updates scene visuals (spheres for bodies, cubes for stations), manages orbit lines, delegates all position resolution to WorldPositionResolver
- `Scripts/Rendering/Planets/CelestialBodyView.cs` — body visual representation with role-based ship colors, station kind colors (cyan orbital, orange surface), station scale ×⅓
- `Scripts/Rendering/Cameras/OrbitalCameraController.cs` — pan (MMB), zoom (scroll), rotate (RMB), smooth focus (Input System)
- `Scripts/Rendering/Cameras/CameraFocusTarget.cs` — marks objects as focusable
- `Scripts/Rendering/Selection/SelectionBridge.cs` — selection ring + highlight + camera focus
- `Scripts/Rendering/Selection/BodyClickHandler.cs` — raycast click selection (UI Toolkit aware)
- `Scripts/Rendering/Labels/BodyLabelController.cs` — IMGUI screen-space name labels with zoom-based fade

### 4. UI

UI Toolkit screens and panels. Localization-ready string provider.

Rules:
- No hardcoded user-facing strings
- Thin controllers calling domain services
- Uses UIStrings for all localizable text

Key files:
- `Scripts/UI/Panels/ObjectListPanelController.cs` — hierarchical body list with selection sync, supports dynamic refresh
- `Scripts/UI/Panels/ObjectDetailsPanelController.cs` — body properties, ship state/destination/SOI body/docking info/cargo contents, station kind/attachment/docking ports/occupancy/storage contents
- `Scripts/UI/Panels/TimeControlsPanelController.cs` — pause + x1/x10/x100 speed buttons
- `Scripts/UI/Localization/UIStrings.cs` — centralized Russian string provider (body types, ship roles, ship states incl. docking states, station kinds, attachment modes, SOI labels, docking labels, resource names, cargo/storage labels)
- `UI/UXML/OrbitalSandboxScreen.uxml` — root layout with left panel, viewport, right panel, time strip, station/SOI/docking/cargo/storage detail rows
- `UI/USS/OrbitalSandboxScreen.uss` — styling

### 5. Data

ScriptableObjects for static configuration and content authoring.

Rules:
- Configuration only
- Runtime mutable state must not live in ScriptableObjects

Key files:
- `Scripts/Data/Config/SceneScaleConfig.cs` — world-to-scene scaling parameters
- `Scripts/Data/Definitions/CelestialBodyDefinition.cs` — serializable body definition for Inspector (includes SOIRadius)
- `Scripts/Data/Definitions/ShipDefinition.cs` — serializable ship definition for Inspector
- `Scripts/Data/Definitions/StationDefinition.cs` — serializable station definition for Inspector (orbital/surface, lat/lon, DockingPortCount)
- `Scripts/Data/Definitions/StarSystemDefinition.cs` — ScriptableObject containing body list + ship list + station list
- `Scripts/Data/Editor/SampleSystemAssetCreator.cs` — editor menu to auto-create sample asset (with ships, 4 stations incl. Ares, SOI values, docking ports)

### Shared

Cross-cutting utilities shared by all layers.

Rules:
- No UnityEngine dependency (`noEngineReferences: true`)
- No dependencies on other project assemblies

Key files:
- `Scripts/Shared/Identifiers/EntityId.cs` — immutable ulong identifier with thread-safe generation
- `Scripts/Shared/Math/SimVec3.cs` — double-precision 3D vector for simulation
- `Scripts/Shared/Units/WorldUnits.cs` — unit policy constants and documentation

### Debug

Structured debug event and snapshot system for AI-assisted debugging. Exports JSON bundles to disk for offline analysis.

Key files:
- `Scripts/Debug/GameDebug.cs` — static API: Log, CaptureSnapshot, ExportBundle, BuildBundle, RunInvariantChecks, GetStatus, SetContext, SetWorldRegistry, RegisterSnapshotProvider
- `Scripts/Debug/DebugEvent.cs` — structured event model with category, severity, timestamp
- `Scripts/Debug/DebugModels.cs` — DebugSnapshot, SubsystemSnapshot, DebugBundle, BundleMetadata, InvariantViolation
- `Scripts/Debug/RingBuffer.cs` — bounded collection (events: 1000, errors: 200, snapshots: 20)
- `Scripts/Debug/DebugSnapshotProviders.cs` — IDebugSnapshotProvider interface + SnapshotProviderRegistry
- `Scripts/Debug/DebugInvariantChecker.cs` — 7 invariant checks: NaN/Inf positions, duplicate ids, negative cargo/storage, orphaned children, docking consistency, self-parenting
- `Scripts/Debug/DebugExportUtility.cs` — manual JSON serialization + file export to persistentDataPath/debug_bundles/
- `Scripts/Debug/BuiltInSnapshotProviders.cs` — 5 providers: WorldSnapshotProvider, ShipSnapshotProvider, EconomySnapshotProvider, DockingSnapshotProvider, SOISnapshotProvider

------------------------------------------------------------------------

## Assembly Definitions

| Assembly | noEngineReferences | Dependencies |
|---|---|---|
| SpaceSim.Shared | true | (none) |
| SpaceSim.World | true | Shared |
| SpaceSim.Simulation | true | Shared, World |
| SpaceSim.Debug | false | Shared, Simulation, World |
| SpaceSim.Data | false | Shared, World |
| SpaceSim.UI | false | Shared, World, Simulation |
| SpaceSim.Rendering | false | Shared, World, Simulation, Debug, UI, Data, Unity.InputSystem |

------------------------------------------------------------------------

## World Model

### Runtime Entities

All runtime entities are pure C# objects with no connection to Unity GameObjects.

**WorldEntity** — base class. Contains EntityId (immutable ulong) and DisplayName.

**CelestialBody** (extends WorldEntity) — primary world entity for all celestial objects, ships, and stations. Fields: BodyType, ParentId, ChildIds, AttachmentMode, OrbitDefinition, SpinDefinition, Radius, SOIRadius, IsSelectable, HasSurface, LocalizationKeyName, ShipInfo, StationInfo.

**StarSystem** — container holding root body ids (stars) and all body ids. Not a WorldEntity, just an id registry for one system.

**WorldRegistry** — central runtime storage. Dictionary of EntityId to WorldEntity. Provides: Add, Remove, GetEntity, GetCelestialBody, Contains, AllEntities, AllCelestialBodies, GetChildren.

**SelectionService** — tracks selected EntityId, fires OnSelectionChanged. Pure C#, authoritative selection source.

### Ship Representation (Current Implementation)

Ships are CelestialBody instances with `BodyType == Ship` and non-null ShipInfo. This bridge solution works because ships share hierarchy, orbits, rendering, and selection with celestial bodies.

**ShipInfo**: ShipRole, ShipKey, ShipClass, ShipState, CurrentRoute, OverrideWorldPosition, CurrentSOIBodyId, Cargo, DockedAtStationId, DockedPortId, DockingStartTime, DockingDuration, DockingStartPosition, DockingReferenceBodyId, DockedAtTime.

**ShipCargo**: Capacity, Dictionary<ResourceType, double>, Add, Remove, RemoveAll, FreeSpace, TotalUsed, IsEmpty, IsFull.

**ShipRoute**: RouteFrame (Global/LocalParent), OriginBodyId, DestinationBodyId, DepartureTime, TravelDuration, StartWorldPosition, ArrivalWorldPosition, StartLocalPosition, ArrivalLocalPosition, LocalFrameBodyId, DestinationOrbitRadius, DestinationOrbitPeriod, ArrivalOrbitPhaseDeg. Methods: GetProgress(time), IsComplete(time).

### Station Representation (Current Implementation)

Stations are CelestialBody instances with `BodyType == Station` and non-null StationInfo. Distinguished by AttachmentMode: Orbit (orbital) or Surface (surface).

**StationInfo**: StationKind (Orbital/Surface), SurfaceLatitudeDeg, SurfaceLongitudeDeg, DockingInfo (nullable), Storage (nullable).

**StationStorage**: Dictionary<ResourceType, double>, CapacityPerResource, Add, Remove, GetAmount, GetAll, GetNonEmptyTypes.

**DockingInfo**: List of DockingPort, GeneratePorts(), RequestPort(), ReleasePort(), GetPortForShip(), TotalPorts, OccupiedCount, FreeCount, HasFreePort.

**DockingPort**: PortId, LocalPosition (SimVec3), OccupiedShipId, IsFree, Occupy(), Release().

### SOI Model

Bodies with non-null SOIRadius define a sphere of influence. SOIResolver determines the deepest (smallest SOI) containing body for any world position.

### Economy Model

**ResourceType**: Food, Metals, Fuel, Electronics.

Stations have StationStorage initialized by EconomyInitializer with resources defined in StationEconomyConfig. Ships have ShipCargo initialized with capacity based on role. CargoTransferService handles all transfers, enforcing docking requirement and capacity limits. NPCShipScheduler drives trader behavior: unload all → load any → depart.

------------------------------------------------------------------------

## Star System Loading Pipeline

```
StarSystemDefinition (ScriptableObject)
    contains: List<CelestialBodyDefinition> + List<ShipDefinition> + List<StationDefinition>
    |
    v
StarSystemLoader.Load()
    CelestialBodyDefinition -> CelestialBodyBuildData (includes SOIRadius)
    ShipDefinition -> ShipBuildData
    StationDefinition -> StationBuildData (includes DockingPortCount)
    |
    v
StarSystemBuilder.Build()
    Phase 1: create CelestialBody entities, map Key -> EntityId
    Phase 2: resolve parent-child by ParentKey
    Phase 3: register bodies in WorldRegistry + StarSystem
    Phase 4: build ships from ShipBuildData, attach to parents
    Phase 5: build stations from StationBuildData, attach to parents, initialize docking ports (any station type)
    |
    v
EconomyInitializer.Initialize()
    Phase 6: create StationStorage on all stations (initial resources from StationEconomyConfig)
    Phase 7: create ShipCargo on all ships (capacity from StationEconomyConfig)
    |
    v
WorldRegistry + WorldPositionResolver + SOIResolver + DockingSystem + CargoTransferService + OrbitalMapRenderer + UI
```

------------------------------------------------------------------------

## Ship Movement Pipeline

### States

| State | Meaning |
|---|---|
| Idle | No orbit, no route |
| Orbiting | Normal orbit around parent body |
| Travelling | In transit between bodies |
| Arrived | Momentary (transitions to Orbiting immediately) |
| ApproachingStation | Moving toward docking port (local-space interpolation) |
| Docking | Reserved for future use (currently instant after approach) |
| Docked | Attached to station at docking port |

### Route Frame Selection

| Route type | Frame | Example |
|---|---|---|
| Planet ↔ its Moon | LocalParent (planet) | Terra ↔ Luna |
| Moon ↔ Moon (same parent) | LocalParent (planet) | future Moon1 ↔ Moon2 |
| Planet ↔ Planet | Global | Terra ↔ Ares |
| Moon ↔ other Planet | Global | Luna ↔ Ares |
| Any ↔ Star child | Global | anything involving star-level siblings |
| Body ↔ Station of same parent | LocalParent (parent) | Terra ↔ Orbita-1 |
| Station ↔ sibling body | LocalParent (parent) | Orbita-1 ↔ Luna |

### Anchored Travel Flow

1. `ShipMovementSystem.StartRoute()`: compute ship's current orbital world position BEFORE detaching
2. If destination is a surface station: redirect arrival to station's parent body with default orbit (r=3.0)
3. Determine RouteFrame (Global or LocalParent) based on origin/arrival body hierarchy
4. Compute arrival time, predict arrival body position at arrival, compute near-side orbital insertion point
5. For orbital station destinations: use small orbit (radius=0.5, period=8)
6. For LocalParent: compute start/arrival in local coordinates relative to dominant parent
7. Store pre-computed positions in ShipRoute; set OverrideWorldPosition to start position (no snap)
8. Detach from parent, clear orbit, parent to root star for list visibility
9. Each tick:
   - Global: lerp between fixed world start/arrival positions
   - LocalParent: lerp in local space, add current frame body world position
10. On arrival: assign orbit with phase-matched MeanAnomalyAtEpoch (no visible snap), re-parent to arrival body
11. `OnShipArrived` event fires with original destination id (station id for surface stations)

### Docking Flow — Orbital Station

1. Ship arrives at orbital station → enters small orbit (r=0.5, period=8) around station
2. After 0.5 sim-s, NPCShipScheduler requests docking via DockingSystem
3. `DockingSystem.RequestOrbitalDocking()`: reserves port, records LOCAL start offset relative to station
4. `DockingSystem.Update()`: interpolates in station-local space, converts to world each tick
5. On completion: clear OverrideWorldPosition, set Docked, WorldPositionResolver handles position via ResolveDocked()
6. **Trader cargo ops**: NPCShipScheduler calls UnloadAll → LoadAny via CargoTransferService (once per docking)
7. After DockingWaitTime: undock → small orbit around station
8. `_pendingDeparture` flag → immediate route departure (no re-docking loop)

### Docking Flow — Surface Station

1. Ship travels to surface station → ShipMovementSystem redirects arrival to parent planet orbit (r=3.0)
2. `OnShipArrived` fires with surface station id → NPCShipScheduler records `_pendingSurfaceDock`
3. After 0.5 sim-s, NPCShipScheduler requests docking via DockingSystem
4. `DockingSystem.RequestSurfaceDocking()`: ship re-parented from planet to station, reserves port, records LOCAL start offset relative to **planet** (reference body = planet)
5. `DockingSystem.Update()`: interpolates in planet-local space (station surface offset + port offset as target)
6. On completion: Docked at surface station, WorldPositionResolver handles position
7. **Trader cargo ops**: same as orbital — UnloadAll → LoadAny
8. After DockingWaitTime: undock → orbit around parent planet (r=3.0)
9. `_pendingDeparture` → immediate route departure

### NPC Scheduling Flow

1. NPCShipScheduler.Update() checks all NPC ships (Player ships skipped)
2. Docked ships: perform cargo ops (once), wait DockingWaitTime → undock → add to `_pendingDeparture`
3. `_pendingDeparture` ships: schedule route immediately, skip idle delay, clear flag
4. `_pendingSurfaceDock` ships: request surface docking after 0.5s delay
5. Ships orbiting orbital station with docking: request docking after 0.5s delay
6. Ships orbiting non-station bodies: idle delay → pick destination → compute duration → start route
7. Traders prefer station destinations via `_stationCandidates` list
8. Fires OnRouteScheduled → coordinator handles transit parenting + UI refresh

### SOI Tracking Flow

1. After ShipMovementSystem.Update(), DockingSystem.Update(), and NPCShipScheduler.Update()
2. SOIResolver.UpdateAllShips(simTime) checks all ships against all SOI bodies
3. For each ship: resolve world position, find deepest containing SOI
4. If dominant body changed: record SOITransition, update ShipInfo.CurrentSOIBodyId
5. Coordinator logs transitions via GameDebug (category ORBIT)

------------------------------------------------------------------------

## Debug System

### Export workflow
1. Right-click GameBootstrap → `Debug/Экспорт + Инварианты`
2. System runs invariant checks, captures snapshot, builds bundle, writes JSON
3. File saved to: `Application.persistentDataPath/debug_bundles/debug_bundle_YYYYMMDD_HHmmss.json`
4. Human copies JSON → sends to Claude for structured analysis

### Bundle contents
- **metadata**: sessionId, exportedAtUtc, sceneName, frame, simulationTime, buffer capacities, totals
- **statusSummary**: one-line human-readable status
- **recentEvents**: last 1000 structured events (category, severity, message, source)
- **recentErrors**: last 200 error events
- **snapshots**: last 20 world state snapshots with subsystem data
- **violations**: invariant check results

### Snapshot providers
| Provider | Data |
|---|---|
| World | Entity counts by type |
| Ships | Ship counts by state and role |
| Economy | Station resource totals, ship cargo totals, per-resource breakdown |
| Docking | Port counts, occupancy, approaching ships |
| SOI | Ships per SOI body |

### Invariant checks
| Check | What it detects |
|---|---|
| NaN/Inf Orbit | NaN or Infinity in orbital parameters |
| NaN/Inf Position | NaN or Infinity in ship override positions |
| Invalid Radius | Negative or NaN body radius |
| Duplicate EntityId | Duplicate ids in registry |
| Negative Cargo | Negative resource amounts in ship cargo, or cargo over capacity |
| Negative Storage | Negative resource amounts in station storage |
| Orphaned Child | Child id not found in registry |
| Docking Consistency | Docked ship referencing invalid station or mismatched port |
| Self-Parent | Body parented to itself |

------------------------------------------------------------------------

## Scaling Policy

| Quantity | Unit | Examples |
|---|---|---|
| Orbital distances | Mm (megameter) | Terra orbit: 150 Mm |
| Body radii | Mm | Sol: 1.0, Terra: 0.3, Luna: 0.08 |
| Ship radii | Mm | Corvette: 0.03 (placeholder) |
| Station radii | Mm | Orbital: 0.05–0.06, Surface: 0.035–0.04 |
| SOI radii | Mm | Terra: 60, Luna: 8 |
| Docking port offset | Mm | 0.15 from station center |
| Time | sim-s | Terra period: 120 sim-s |
| Ship speed | Mm/sim-s | NPC default: 2.0 |

Scene conversion via SceneScaleConfig: `scenePos = worldPos * DistanceScale`, `sceneDiameter = max(radius * BodyRadiusScale * 2, MinBodyDiameter)`.
Station cubes: visual scale ×⅓.

------------------------------------------------------------------------

## Sample Star System Content

| Body | Type | Parent | Orbit | SOI | Docking | Economy |
|---|---|---|---|---|---|---|
| Sol | Star | — | — | 1000 | — | — |
| Terra | Planet | Sol | r=150, P=120 | 60 | — | — |
| Ares | Planet | Sol | r=275, P=240 | 40 | — | — |
| Venus | Planet | Sol | r=75, P=80 | 30 | — | — |
| Luna | Moon | Terra | r=25, P=20 | 8 | — | — |
| Станция «Орбита-1» | Orbital Station | Terra | r=8, P=18 | — | 3 ports | Electronics 200, Fuel 60 |
| База «Терра-1» | Surface Station | Terra | lat=30° lon=45° | — | 2 ports | Food 200, Fuel 100 |
| Станция «Фобос» | Orbital Station | Ares | r=6, P=14 | — | 2 ports | Fuel 200, Metals 60 |
| База «Арес-1» | Surface Station | Ares | lat=-15° lon=120° | — | 2 ports | Metals 200, Fuel 100 |
| Корвет «Аврора» | Ship (Player) | Terra | r=3, P=12 | — | — | Cargo cap 100 |
| Транспорт «Карго-7» | Ship (Trader) | Terra | r=4, P=15 | — | — | Cargo cap 100 |
| Патруль «Страж-3» | Ship (Patrol) | Ares | r=3, P=10 | — | — | Cargo cap 25 |

------------------------------------------------------------------------

## Sandbox Scene Structure

```
OrbitalSandbox (Scene)
  Main Camera               — OrbitalCameraController
  Directional Light
  GameBootstrap              — creates SimulationClock, sets debug export dir, calls coordinator.Setup()
  OrbitalMapRenderer         — creates body views + orbit lines, delegates to WorldPositionResolver
  OrbitalSandboxCoordinator  — wires all services, ticks simulation + SOI + docking + economy
    Inspector params: npcTravelSpeed, npcMinTravelDuration, npcIdleDelay,
                      npcDockingWaitTime, dockingApproachDuration
    (runtime) WorldPositionResolver
    (runtime) SOIResolver
    (runtime) DockingSystem
    (runtime) ShipMovementSystem
    (runtime) NPCShipScheduler
    (runtime) CargoTransferService
    (runtime) SelectionBridge
    (runtime) BodyClickHandler
    (runtime) BodyLabelController
    (runtime) ObjectListPanelController
    (runtime) ObjectDetailsPanelController
    (runtime) TimeControlsPanelController
  UIDocument                 — OrbitalSandboxScreen.uxml
  EventSystem                — InputSystemUIInputModule
```

------------------------------------------------------------------------

## Implemented vs Deferred

### Implemented
- Debug infrastructure (full: events, snapshots, providers, invariants, JSON export, editor commands)
- Assembly definitions, orbital sandbox
- Celestial body hierarchy, orbit rendering, camera controls
- Selection, labels, object list, details panel, time controls
- World units, scaling, star system asset loading, builder pipeline
- Ships foundation, anchored ship movement with Global/LocalParent frames
- NPC scheduling with role-based routing, distance-based duration, trader station preference
- Dynamic object list refresh on hierarchy changes
- Stations foundation (orbital + surface), station definitions, cube visuals
- World position resolver (pure C#, single source of truth)
- SOI foundation: SOI radius on bodies, SOI resolver, ship SOI tracking, transition detection
- Docking foundation: docking ports on orbital and surface stations, approach/dock/undock lifecycle, NPC auto-dock/undock, surface station arrival via parent body orbit, UI display of docking state
- Economy foundation: resource types, station storage, ship cargo, cargo transfer service, economy initializer, NPC trader cargo loop, UI display of cargo/storage

### Deferred
Prices/money, production chains, contracts, factions, AI behavior, advanced navigation, orbital transfers, combat, ship modules, save/load, procedural planets, elliptical orbits, inclined orbits, LOD system, SOI visualization, SOI-based reparenting, patched conics, docking animations, station interiors, player trading UI.

------------------------------------------------------------------------

## Coding Standards

- Code comments: English only
- Project documentation: English
- Integration instructions: Russian
- UI strings: localization-ready via UIStrings (current: Russian)
- Composition over inheritance, explicit dependencies, single responsibility
- Inspector-serialized fields: use float (not double) for Unity compatibility
