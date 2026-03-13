# PROJECT_MAP.md

## Project Overview

Modular sandbox space simulation built in Unity LTS using URP and UI Toolkit.

Long-term goals: orbital simulation sandbox, ships and stations, NPC traffic and tasks, economy and trade, faction and clan politics, maintainable architecture for humans and AI assistants.

------------------------------------------------------------------------

## Architecture Layers

### 1. Simulation

Pure game simulation logic. Time progression, orbital calculations, selection state, star system building, ship movement.

Rules:
- Must NOT depend on UnityEngine
- Pure C# only
- Deterministic and testable
- Enforced via asmdef with `noEngineReferences: true`

Key files:
- `Scripts/Simulation/Time/SimulationClock.cs` — simulation clock (pause, resume, time scale)
- `Scripts/Simulation/Core/SelectionService.cs` — tracks selected entity id, fires events
- `Scripts/Simulation/Core/SampleStarSystemFactory.cs` — hardcoded fallback sample system (with ships)
- `Scripts/Simulation/Core/StarSystemBuilder.cs` — builds runtime entities from pure build data (bodies + ships)
- `Scripts/Simulation/Orbits/OrbitalPositionCalculator.cs` — circular orbit position in XZ plane (MVP)
- `Scripts/Simulation/Ships/ShipMovementSystem.cs` — updates ship travel positions, handles departure and arrival

### 2. World

Game domain entities and shared world state models.

Rules:
- Independent from rendering, no MonoBehaviours
- `noEngineReferences: true` enforced via asmdef
- References only SpaceSim.Shared

Key files:
- `Scripts/World/Entities/WorldEntity.cs` — base class (EntityId + DisplayName)
- `Scripts/World/Entities/CelestialBody.cs` — body type, parent/child ids, orbit, spin, radius, ShipInfo
- `Scripts/World/Entities/CelestialBodyType.cs` — enum: Star, Planet, Moon, Asteroid, Station, Ship, SurfaceSite
- `Scripts/World/Entities/AttachmentMode.cs` — enum: None, Orbit, Surface, LocalSpace
- `Scripts/World/Entities/ShipRole.cs` — enum: Player, Trader, Patrol, Civilian
- `Scripts/World/Entities/ShipState.cs` — enum: Idle, Orbiting, Travelling, Arrived
- `Scripts/World/Entities/ShipInfo.cs` — ship data: role, key, class, state, route, override position
- `Scripts/World/Entities/ShipRoute.cs` — travel route: origin, destination, departure time, duration
- `Scripts/World/Entities/StarSystem.cs` — container with root body ids and all body ids
- `Scripts/World/ValueTypes/OrbitDefinition.cs` — full Keplerian orbital elements
- `Scripts/World/ValueTypes/SpinDefinition.cs` — axial tilt, rotation period
- `Scripts/World/Systems/WorldRegistry.cs` — central entity registry (add/get/enumerate/children)

### 3. Rendering

Unity MonoBehaviour layer. Visualization, camera, scene object management, selection bridging, bootstrap coordination.

Rules:
- MonoBehaviours belong here
- Reads state from World and Simulation
- Must not contain core simulation logic
- Coordinates system lifecycle and wiring

Key files:
- `Scripts/Rendering/Bootstrap/GameBootstrap.cs` — Unity entry point, DontDestroyOnLoad singleton, creates SimulationClock
- `Scripts/Rendering/Bootstrap/OrbitalSandboxCoordinator.cs` — wires all services, loads star system, ticks ShipMovementSystem, manages UI refresh
- `Scripts/Rendering/Bootstrap/StarSystemLoader.cs` — converts ScriptableObject definitions to pure build data, calls StarSystemBuilder
- `Scripts/Rendering/Orbits/OrbitalMapRenderer.cs` — creates/updates scene visuals, supports travelling ship override positions, manages orbit line visibility
- `Scripts/Rendering/Planets/CelestialBodyView.cs` — body visual representation with role-based ship colors
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
- `Scripts/UI/Panels/ObjectDetailsPanelController.cs` — selected body properties, ship state/destination when travelling
- `Scripts/UI/Panels/TimeControlsPanelController.cs` — pause + x1/x10/x100 speed buttons
- `Scripts/UI/Localization/UIStrings.cs` — centralized Russian string provider (body types, ship roles, ship states)
- `UI/UXML/OrbitalSandboxScreen.uxml` — root layout with left panel, viewport, right panel, time strip
- `UI/USS/OrbitalSandboxScreen.uss` — styling

### 5. Data

ScriptableObjects for static configuration and content authoring.

Rules:
- Configuration only
- Runtime mutable state must not live in ScriptableObjects

Key files:
- `Scripts/Data/Config/SceneScaleConfig.cs` — world-to-scene scaling parameters
- `Scripts/Data/Definitions/CelestialBodyDefinition.cs` — serializable body definition for Inspector
- `Scripts/Data/Definitions/ShipDefinition.cs` — serializable ship definition for Inspector
- `Scripts/Data/Definitions/StarSystemDefinition.cs` — ScriptableObject containing body list + ship list
- `Scripts/Data/Editor/SampleSystemAssetCreator.cs` — editor menu to auto-create sample asset (with ships)

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

**CelestialBody** (extends WorldEntity) — primary world entity for all celestial objects and ships. Fields: BodyType, ParentId, ChildIds, AttachmentMode, OrbitDefinition, SpinDefinition, Radius, IsSelectable, HasSurface, LocalizationKeyName, ShipInfo.

**StarSystem** — container holding root body ids (stars) and all body ids. Not a WorldEntity, just an id registry for one system.

**WorldRegistry** — central runtime storage. Dictionary of EntityId to WorldEntity. Provides: Add, Remove, GetEntity, GetCelestialBody, Contains, AllEntities, AllCelestialBodies, GetChildren.

**SelectionService** — tracks selected EntityId, fires OnSelectionChanged. Pure C#, authoritative selection source.

### Ship Representation (Current Implementation)

Ships are CelestialBody instances with `BodyType == Ship` and non-null ShipInfo. This bridge solution works because ships share hierarchy, orbits, rendering, and selection with celestial bodies.

**ShipInfo**: ShipRole, ShipKey, ShipClass, ShipState, CurrentRoute (ShipRoute or null), OverrideWorldPosition (SimVec3? for travel).

**ShipRoute**: OriginBodyId, DestinationBodyId, DepartureTime, TravelDuration. Methods: GetProgress(time), IsComplete(time).

When ship complexity grows (modules, cargo, AI), a dedicated Ship entity class may be extracted.

------------------------------------------------------------------------

## Star System Loading Pipeline

```
StarSystemDefinition (ScriptableObject)
    contains: List<CelestialBodyDefinition> + List<ShipDefinition>
    |
    v
StarSystemLoader.Load()
    CelestialBodyDefinition -> CelestialBodyBuildData
    ShipDefinition -> ShipBuildData
    |
    v
StarSystemBuilder.Build()
    Phase 1: create CelestialBody entities, map Key -> EntityId
    Phase 2: resolve parent-child by ParentKey
    Phase 3: register bodies in WorldRegistry + StarSystem
    Phase 4: build ships from ShipBuildData, attach to parents
    |
    v
WorldRegistry + OrbitalMapRenderer + UI
```

Parent-child resolution: Key/ParentKey string matching. Root bodies have empty ParentKey.

Fallback: if no StarSystemDefinition asset assigned, SampleStarSystemFactory.Create() provides Sol + Terra + Ares + Luna + 3 ships.

------------------------------------------------------------------------

## Ship Movement Pipeline

### States

| State | Meaning |
|---|---|
| Idle | No orbit, no route |
| Orbiting | Normal orbit around parent body |
| Travelling | In transit between bodies |
| Arrived | Momentary (transitions to Orbiting immediately) |

### Travel Flow

1. `ShipMovementSystem.StartRoute()`: detach from origin, clear orbit, state → Travelling
2. Coordinator parents ship to root star (visible in list during transit)
3. Each tick: progress = elapsed / duration, position = lerp(origin, destination), stored as OverrideWorldPosition
4. Renderer uses OverrideWorldPosition instead of orbit calculation; orbit line hidden
5. progress >= 1.0: re-parent to destination, new orbit assigned, state → Orbiting
6. Coordinator cleans up transit parent, refreshes object list

### Position Resolution

ShipMovementSystem receives `Func<EntityId, double, SimVec3>` delegate from coordinator, calling OrbitalMapRenderer.ResolveWorldPosition() for consistent body positions.

------------------------------------------------------------------------

## Scaling Policy

| Quantity | Unit | Examples |
|---|---|---|
| Orbital distances | Mm (megameter) | Terra orbit: 30 Mm |
| Body radii | Mm | Sol: 5.0, Terra: 1.5 |
| Ship radii | Mm | Corvette: 0.15 (placeholder) |
| Time | sim-s | Terra period: 120 sim-s |

Scene conversion via SceneScaleConfig: `scenePos = worldPos * DistanceScale`, `sceneDiameter = max(radius * BodyRadiusScale * 2, MinBodyDiameter)`.

MinBodyDiameter guarantees small ships remain visible.

------------------------------------------------------------------------

## Sandbox Scene Structure

```
OrbitalSandbox (Scene)
  Main Camera               — OrbitalCameraController
  Directional Light
  GameBootstrap              — creates SimulationClock, calls coordinator.Setup()
  OrbitalMapRenderer         — creates body views + orbit lines
  OrbitalSandboxCoordinator  — wires services, ticks ShipMovementSystem
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
- Ships foundation, ship movement with departure/travel/arrival
- Dynamic object list refresh on hierarchy changes

### Deferred
Stations, docking, economy, cargo, contracts, factions, AI, advanced navigation, orbital transfers, combat, ship modules, save/load, procedural planets, elliptical orbits, inclined orbits, LOD system.

------------------------------------------------------------------------

## Coding Standards

- Code comments: English only
- Project documentation: English
- Integration instructions: Russian
- UI strings: localization-ready via UIStrings (current: Russian)
- Composition over inheritance, explicit dependencies, single responsibility
