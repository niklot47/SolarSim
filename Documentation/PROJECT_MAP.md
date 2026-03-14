# PROJECT_MAP.md

## Project Overview

Modular sandbox space simulation built in Unity LTS using URP and UI Toolkit.

Long-term goals: orbital simulation sandbox, ships and stations, NPC traffic and tasks, economy and trade, faction and clan politics, maintainable architecture for humans and AI assistants.

------------------------------------------------------------------------

## Architecture Layers

### 1. Simulation

Pure game simulation logic. Time progression, orbital calculations, selection state, star system building, ship movement, NPC scheduling, world position resolution, SOI resolution.

Rules:
- Must NOT depend on UnityEngine
- Pure C# only
- Deterministic and testable
- Enforced via asmdef with `noEngineReferences: true`

Key files:
- `Scripts/Simulation/Time/SimulationClock.cs` — simulation clock (pause, resume, time scale)
- `Scripts/Simulation/Core/SelectionService.cs` — tracks selected entity id, fires events
- `Scripts/Simulation/Core/SampleStarSystemFactory.cs` — hardcoded fallback sample system (with ships, stations, SOI)
- `Scripts/Simulation/Core/StarSystemBuilder.cs` — builds runtime entities from pure build data (bodies + ships + stations)
- `Scripts/Simulation/Core/WorldPositionResolver.cs` — single source of truth for body world positions (orbital, surface, override)
- `Scripts/Simulation/Orbits/OrbitalPositionCalculator.cs` — circular orbit position in XZ plane (MVP) + surface position from lat/lon
- `Scripts/Simulation/Ships/ShipMovementSystem.cs` — anchored travel with Global/LocalParent frame selection, predictive arrival, phase-matched orbit assignment, station orbit support
- `Scripts/Simulation/Ships/NPCShipScheduler.cs` — auto-assigns routes to NPC ships, role-based destination selection (planets, moons, stations), distance-based duration
- `Scripts/Simulation/SOI/SOIResolver.cs` — sphere of influence containment resolution, dominant body detection, ship SOI tracking with transition detection

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
- `Scripts/World/Entities/ShipState.cs` — enum: Idle, Orbiting, Travelling, Arrived
- `Scripts/World/Entities/ShipInfo.cs` — ship data: role, key, class, state, route, override position, currentSOIBodyId
- `Scripts/World/Entities/ShipRoute.cs` — travel route: RouteFrame (Global/LocalParent), origin/destination, start/arrival world+local positions, destination orbit params, arrival phase
- `Scripts/World/Entities/StationInfo.cs` — station data: StationKind (Orbital/Surface), surface lat/lon
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
- `Scripts/Rendering/Bootstrap/GameBootstrap.cs` — Unity entry point, DontDestroyOnLoad singleton, creates SimulationClock
- `Scripts/Rendering/Bootstrap/OrbitalSandboxCoordinator.cs` — wires all services (WorldPositionResolver, SOIResolver, ShipMovementSystem, NPCShipScheduler), loads star system, ticks simulation, manages transit parenting, logs SOI transitions, syncs NPC params from Inspector
- `Scripts/Rendering/Bootstrap/StarSystemLoader.cs` — converts ScriptableObject definitions (bodies + ships + stations) to pure build data, calls StarSystemBuilder
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
- `Scripts/UI/Panels/ObjectDetailsPanelController.cs` — selected body properties, ship state/destination/SOI body, station kind/attachment, SOI radius for bodies
- `Scripts/UI/Panels/TimeControlsPanelController.cs` — pause + x1/x10/x100 speed buttons
- `Scripts/UI/Localization/UIStrings.cs` — centralized Russian string provider (body types, ship roles, ship states, station kinds, attachment modes, SOI labels)
- `UI/UXML/OrbitalSandboxScreen.uxml` — root layout with left panel, viewport, right panel, time strip, station/SOI detail rows
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
- `Scripts/Data/Definitions/StationDefinition.cs` — serializable station definition for Inspector (orbital/surface, lat/lon)
- `Scripts/Data/Definitions/StarSystemDefinition.cs` — ScriptableObject containing body list + ship list + station list
- `Scripts/Data/Editor/SampleSystemAssetCreator.cs` — editor menu to auto-create sample asset (with ships, stations, SOI values)

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

Structured debug event system for AI-assisted debugging.

Key files:
- `Scripts/Debug/GameDebug.cs` — static API: Log, Snapshot, ExportBundle, GetStatus
- `Scripts/Debug/DebugEvent.cs` — structured event model with category, severity, timestamp
- `Scripts/Debug/RingBuffer.cs` — bounded collection (events: 1000, errors: 200)

------------------------------------------------------------------------

## Assembly Definitions

| Assembly | noEngineReferences | Dependencies |
|---|---|---|
| SpaceSim.Shared | true | (none) |
| SpaceSim.World | true | Shared |
| SpaceSim.Simulation | true | Shared, World |
| SpaceSim.Debug | false | Shared, Simulation |
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

**ShipInfo**: ShipRole, ShipKey, ShipClass, ShipState, CurrentRoute (ShipRoute or null), OverrideWorldPosition (SimVec3? for travel), CurrentSOIBodyId.

**ShipRoute**: RouteFrame (Global/LocalParent), OriginBodyId, DestinationBodyId, DepartureTime, TravelDuration, StartWorldPosition, ArrivalWorldPosition, StartLocalPosition, ArrivalLocalPosition, LocalFrameBodyId, DestinationOrbitRadius, DestinationOrbitPeriod, ArrivalOrbitPhaseDeg. Methods: GetProgress(time), IsComplete(time).

### Station Representation (Current Implementation)

Stations are CelestialBody instances with `BodyType == Station` and non-null StationInfo. Distinguished by AttachmentMode: Orbit (orbital) or Surface (surface).

**StationInfo**: StationKind (Orbital/Surface), SurfaceLatitudeDeg, SurfaceLongitudeDeg.

### SOI Model

Bodies with non-null SOIRadius define a sphere of influence. SOIResolver determines the deepest (smallest SOI) containing body for any world position.

When ship complexity grows (modules, cargo, AI), a dedicated Ship entity class may be extracted.

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
    StationDefinition -> StationBuildData
    |
    v
StarSystemBuilder.Build()
    Phase 1: create CelestialBody entities, map Key -> EntityId
    Phase 2: resolve parent-child by ParentKey
    Phase 3: register bodies in WorldRegistry + StarSystem
    Phase 4: build ships from ShipBuildData, attach to parents
    Phase 5: build stations from StationBuildData, attach to parents
    |
    v
WorldRegistry + WorldPositionResolver + SOIResolver + OrbitalMapRenderer + UI
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
2. Determine RouteFrame (Global or LocalParent) based on origin/destination hierarchy (extended for stations)
3. Compute arrival time, predict destination body position at arrival, compute near-side orbital insertion point
4. For station destinations: use small orbit (radius=0.5, period=8)
5. For LocalParent: compute start/arrival in local coordinates relative to dominant parent
6. Store pre-computed positions in ShipRoute; set OverrideWorldPosition to start position (no snap)
7. Detach from parent, clear orbit, parent to root star for list visibility
8. Each tick:
   - Global: lerp between fixed world start/arrival positions
   - LocalParent: lerp in local space, add current frame body world position
9. On arrival: assign orbit with phase-matched MeanAnomalyAtEpoch (no visible snap), re-parent to destination

### NPC Scheduling Flow

1. NPCShipScheduler.Update() checks all NPC ships in Orbiting state
2. Applies idle delay (configurable, default 3 sim-s)
3. Picks destination by role (Trader/Civilian: random planet/moon/station, Patrol: ping-pong, Player: skip)
4. Computes distance-based duration: `distance / travelSpeed` (local distance for local routes)
5. Calls ShipMovementSystem.StartRoute() with position resolver
6. Fires OnRouteScheduled → coordinator handles transit parenting + UI refresh

### SOI Tracking Flow

1. After ShipMovementSystem.Update() and NPCShipScheduler.Update()
2. SOIResolver.UpdateAllShips(simTime) checks all ships against all SOI bodies
3. For each ship: resolve world position, find deepest containing SOI
4. If dominant body changed: record SOITransition, update ShipInfo.CurrentSOIBodyId
5. Coordinator logs transitions to console

------------------------------------------------------------------------

## Scaling Policy

| Quantity | Unit | Examples |
|---|---|---|
| Orbital distances | Mm (megameter) | Terra orbit: 150 Mm |
| Body radii | Mm | Sol: 1.0, Terra: 0.3, Luna: 0.08 |
| Ship radii | Mm | Corvette: 0.03 (placeholder) |
| Station radii | Mm | Orbital: 0.06, Surface: 0.04 |
| SOI radii | Mm | Terra: 60, Luna: 8 |
| Time | sim-s | Terra period: 120 sim-s |
| Ship speed | Mm/sim-s | NPC default: 2.0 |

Scene conversion via SceneScaleConfig: `scenePos = worldPos * DistanceScale`, `sceneDiameter = max(radius * BodyRadiusScale * 2, MinBodyDiameter)`.
Station cubes: visual scale ×⅓.

------------------------------------------------------------------------

## Sandbox Scene Structure

```
OrbitalSandbox (Scene)
  Main Camera               — OrbitalCameraController
  Directional Light
  GameBootstrap              — creates SimulationClock, calls coordinator.Setup()
  OrbitalMapRenderer         — creates body views + orbit lines, delegates to WorldPositionResolver
  OrbitalSandboxCoordinator  — wires all services, ticks simulation + SOI
    Inspector params: npcTravelSpeed, npcMinTravelDuration, npcIdleDelay
    (runtime) WorldPositionResolver
    (runtime) SOIResolver
    (runtime) ShipMovementSystem
    (runtime) NPCShipScheduler
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
- Debug infrastructure, assembly definitions, orbital sandbox
- Celestial body hierarchy, orbit rendering, camera controls
- Selection, labels, object list, details panel, time controls
- World units, scaling, star system asset loading, builder pipeline
- Ships foundation, anchored ship movement with Global/LocalParent frames
- NPC scheduling with role-based routing and distance-based duration
- Dynamic object list refresh on hierarchy changes
- Stations foundation (orbital + surface), station definitions, cube visuals
- World position resolver (pure C#, single source of truth)
- SOI foundation: SOI radius on bodies, SOI resolver, ship SOI tracking, transition detection

### Deferred
Docking, economy, cargo, contracts, factions, AI, advanced navigation, orbital transfers, combat, ship modules, save/load, procedural planets, elliptical orbits, inclined orbits, LOD system, SOI visualization, SOI-based reparenting, patched conics.

------------------------------------------------------------------------

## Coding Standards

- Code comments: English only
- Project documentation: English
- Integration instructions: Russian
- UI strings: localization-ready via UIStrings (current: Russian)
- Composition over inheritance, explicit dependencies, single responsibility
- Inspector-serialized fields: use float (not double) for Unity compatibility
