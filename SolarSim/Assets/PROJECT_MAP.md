# PROJECT_MAP.md

## Project Overview

Modular sandbox space simulation built in Unity LTS using URP and UI Toolkit.

Long-term goals: orbital simulation sandbox, ships and stations, NPC traffic and tasks, economy and trade, faction and clan politics, maintainable architecture for humans and AI assistants.

------------------------------------------------------------------------

## Architecture Layers

### 1. Simulation

Pure game simulation logic. Time progression, orbital calculations, selection state, star system building.

Rules:
- Must NOT depend on UnityEngine
- Pure C# only
- Deterministic and testable

Key files:
- `Scripts/Simulation/Time/SimulationClock.cs` — simulation clock (pause, resume, time scale)
- `Scripts/Simulation/Core/SelectionService.cs` — tracks selected entity id, fires events
- `Scripts/Simulation/Core/SampleStarSystemFactory.cs` — hardcoded fallback sample system
- `Scripts/Simulation/Core/StarSystemBuilder.cs` — builds runtime entities from pure build data
- `Scripts/Simulation/Orbits/OrbitalPositionCalculator.cs` — circular orbit position (MVP)

### 2. World

Game domain entities. Shared world state models. No rendering, no MonoBehaviours.

Rules:
- Independent from rendering
- noEngineReferences enforced via asmdef

Key files:
- `Scripts/World/Entities/WorldEntity.cs` — base class (EntityId + DisplayName)
- `Scripts/World/Entities/CelestialBody.cs` — body type, parent/child ids, orbit, spin, radius
- `Scripts/World/Entities/CelestialBodyType.cs` — enum: Star, Planet, Moon, Asteroid, Station, Ship, SurfaceSite
- `Scripts/World/Entities/AttachmentMode.cs` — enum: None, Orbit, Surface, LocalSpace
- `Scripts/World/Entities/StarSystem.cs` — container with root body ids and all body ids
- `Scripts/World/ValueTypes/OrbitDefinition.cs` — full Keplerian orbital elements
- `Scripts/World/ValueTypes/SpinDefinition.cs` — axial tilt, rotation period
- `Scripts/World/Systems/WorldRegistry.cs` — central entity registry (add/get/enumerate/children)

### 3. Rendering

Unity MonoBehaviour layer. Visualization, camera, scene object management, selection bridging, bootstrap coordination.

Rules:
- MonoBehaviours belong here
- Reads state from World/Simulation
- No core simulation logic

Key files:
- `Scripts/Rendering/Bootstrap/GameBootstrap.cs` — Unity entry point, DontDestroyOnLoad singleton
- `Scripts/Rendering/Bootstrap/OrbitalSandboxCoordinator.cs` — wires all services, loads star system
- `Scripts/Rendering/Bootstrap/StarSystemLoader.cs` — converts ScriptableObject to build data, calls builder
- `Scripts/Rendering/Orbits/OrbitalMapRenderer.cs` — creates/updates scene visuals, uses SceneScaleConfig
- `Scripts/Rendering/Planets/CelestialBodyView.cs` — body visual representation
- `Scripts/Rendering/Cameras/OrbitalCameraController.cs` — pan/zoom/rotate/focus (Input System)
- `Scripts/Rendering/Cameras/CameraFocusTarget.cs` — marks objects as focusable
- `Scripts/Rendering/Selection/SelectionBridge.cs` — selection ring + highlight + camera focus
- `Scripts/Rendering/Selection/BodyClickHandler.cs` — raycast click selection (UI Toolkit aware)
- `Scripts/Rendering/Labels/BodyLabelController.cs` — IMGUI name labels with zoom-based fade

### 4. UI

UI Toolkit screens and panels. Localization-ready string provider.

Rules:
- No hardcoded user-facing strings
- Thin controllers calling domain services
- Uses UIStrings for localization keys

Key files:
- `Scripts/UI/Panels/ObjectListPanelController.cs` — hierarchical body list with selection sync
- `Scripts/UI/Panels/ObjectDetailsPanelController.cs` — selected body properties
- `Scripts/UI/Panels/TimeControlsPanelController.cs` — pause + x1/x10/x100 speed buttons
- `Scripts/UI/Localization/UIStrings.cs` — centralized Russian string provider
- `UI/UXML/OrbitalSandboxScreen.uxml` — root layout
- `UI/USS/OrbitalSandboxScreen.uss` — styling

### 5. Data

ScriptableObjects for static configuration. Definition assets for content authoring.

Rules:
- Configuration only
- Runtime mutable state must not live in ScriptableObjects

Key files:
- `Scripts/Data/Config/SceneScaleConfig.cs` — world-to-scene scaling parameters
- `Scripts/Data/Definitions/CelestialBodyDefinition.cs` — serializable body definition for Inspector
- `Scripts/Data/Definitions/StarSystemDefinition.cs` — ScriptableObject containing body list
- `Scripts/Data/Editor/SampleSystemAssetCreator.cs` — editor menu to auto-create sample asset

### Shared

Cross-cutting utilities. No UnityEngine dependency.

Key files:
- `Scripts/Shared/Identifiers/EntityId.cs` — immutable ulong identifier
- `Scripts/Shared/Math/SimVec3.cs` — double-precision 3D vector
- `Scripts/Shared/Units/WorldUnits.cs` — unit policy constants and documentation

### Debug

Structured debug event system.

Key files:
- `Scripts/Debug/GameDebug.cs` — static API: Log, Snapshot, ExportBundle, GetStatus
- `Scripts/Debug/DebugEvent.cs` — structured event model
- `Scripts/Debug/RingBuffer.cs` — bounded collection

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

## Star System Loading Pipeline

```
StarSystemDefinition (ScriptableObject, Inspector-authored)
    |
    v
StarSystemLoader.Load()
    converts CelestialBodyDefinition -> CelestialBodyBuildData (pure C#)
    |
    v
StarSystemBuilder.Build()
    Phase 1: create CelestialBody entities, map Key -> EntityId
    Phase 2: resolve parent-child by ParentKey lookup
    Phase 3: register in WorldRegistry, build StarSystem container
    |
    v
Runtime entities: StarSystem + CelestialBody instances in WorldRegistry
    |
    v
OrbitalMapRenderer reads WorldRegistry -> creates scene GameObjects
ObjectListPanelController reads WorldRegistry -> populates UI
```

Fallback: if no StarSystemDefinition is assigned to OrbitalSandboxCoordinator, `SampleStarSystemFactory.Create()` is used instead.

------------------------------------------------------------------------

## Scaling Policy

### World Units

| Quantity | Unit | Examples |
|---|---|---|
| Orbital distances | Mm (megameter = 1000 km) | Terra orbit: 30 Mm |
| Body radii | Mm | Sol: 5.0 Mm, Terra: 1.5 Mm |
| Time | sim-s (simulation seconds) | Terra period: 120 sim-s |
| Speed (future) | Mm/sim-s | — |

Defined in `Scripts/Shared/Units/WorldUnits.cs`.

### Scene Conversion

Controlled by `SceneScaleConfig` ScriptableObject (`Assets/Data/Config/SceneScaleConfig.asset`):

- `scenePosition = worldPosition × DistanceScale`
- `sceneDiameter = max(worldRadius × BodyRadiusScale × 2, MinBodyDiameter)`

DistanceScale and BodyRadiusScale are intentionally separate so bodies can be visually enlarged relative to orbits for readability.

Conversion occurs in:
- `OrbitalMapRenderer` — positions and orbit line radii
- `CelestialBodyView.Bind()` — body scale

------------------------------------------------------------------------

## Sandbox Scene Structure

```
OrbitalSandbox (Scene)
  Main Camera          — OrbitalCameraController
  Directional Light
  GameBootstrap        — creates SimulationClock, initializes coordinator
  OrbitalMapRenderer   — creates body views + orbit lines as children
  OrbitalSandboxCoordinator — wires all services together
    (runtime) SelectionBridge
    (runtime) BodyClickHandler
    (runtime) BodyLabelController
    (runtime) ObjectListPanelController
    (runtime) ObjectDetailsPanelController
    (runtime) TimeControlsPanelController
  UIDocument           — hosts OrbitalSandboxScreen.uxml
  EventSystem          — InputSystemUIInputModule
```

------------------------------------------------------------------------

## World Model

### Runtime Entities (pure C# domain objects)

**StarSystem** — container holding root body ids and all body ids for one star system.

**CelestialBody** (extends WorldEntity) — main domain entity:
- `BodyType` — Star, Planet, Moon, Asteroid, Station, Ship, SurfaceSite
- `ParentId` / `ChildIds` — parent-child hierarchy via EntityId references
- `AttachmentMode` — None, Orbit, Surface, LocalSpace
- `OrbitDefinition` — full Keplerian elements (semiMajorAxis, eccentricity, inclination, etc.)
- `SpinDefinition` — axial tilt, rotation period
- `Radius`, `IsSelectable`, `HasSurface`

**Ship** (planned) — future runtime entity attached to a body or orbit.

**Station** (planned) — future runtime entity, orbital or surface-attached.

**WorldRegistry** — central runtime registry. Add/get/enumerate entities. Resolves children by reading ChildIds and looking up in the registry.

Runtime entities are completely separate from rendering GameObjects. `CelestialBodyView` is a MonoBehaviour that reads data from `CelestialBody` but does not own it.

### Data Model (ScriptableObject definitions)

**CelestialBodyDefinition** — serializable class with all fields needed to construct a CelestialBody. Embedded in StarSystemDefinition's body list.

**StarSystemDefinition** — ScriptableObject asset. Contains system identity + flat list of CelestialBodyDefinition entries. Parent-child resolved by Key/ParentKey at build time.

**SampleSystemAssetCreator** — editor menu (SpaceSim → Create Sample Star System Asset) that creates a pre-populated StarSystemDefinition with Sol, Terra, Ares, Luna.

Conversion path: `CelestialBodyDefinition` → `CelestialBodyBuildData` (pure C#) → `StarSystemBuilder.Build()` → runtime `CelestialBody` + `StarSystem`.

------------------------------------------------------------------------

## Debug and Validation

- Structured debug events via `GameDebug.Log()`
- Ring buffers for recent events (1000) and errors (200)
- Snapshot capture placeholder
- Export bundle placeholder
- Context menu actions on GameBootstrap (editor only)

------------------------------------------------------------------------

## Coding Standards

- Clean, modular, maintainable C#
- Small focused classes
- Composition over inheritance
- Explicit dependencies
- Code comments: English
- Project documentation: English
- Integration instructions: Russian
- UI strings: localization-ready via UIStrings

------------------------------------------------------------------------

## Maintenance Rule

This file must be updated whenever:
- A new subsystem is added
- Major dependencies change
- New root folders appear
- Gameplay pipelines are introduced
