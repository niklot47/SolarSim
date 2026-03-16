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
- `Scripts/Simulation/Orbits/OrbitalPositionCalculator.cs` — circular orbit position in XZ plane
- `Scripts/Simulation/Ships/ShipMovementSystem.cs` — anchored travel with Global/LocalParent frame selection
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
- `Scripts/World/ValueTypes/OrbitDefinition.cs` — Keplerian orbital elements
- `Scripts/World/ValueTypes/SpinDefinition.cs` — axial tilt, rotation period
- `Scripts/World/Systems/WorldRegistry.cs` — central entity registry

### 3. Rendering

Key files:
- `Scripts/Rendering/Bootstrap/GameBootstrap.cs` — Unity entry point
- `Scripts/Rendering/Bootstrap/OrbitalSandboxCoordinator.cs` — wires all services, **external JSON import support** via `externalSystemFile` field, fallback chain: JSON → ScriptableObject → sample
- `Scripts/Rendering/Bootstrap/StarSystemLoader.cs` — converts ScriptableObject definitions to build data
- `Scripts/Rendering/Orbits/OrbitalMapRenderer.cs` — scene visuals
- `Scripts/Rendering/Planets/CelestialBodyView.cs` — body visual representation
- `Scripts/Rendering/Cameras/OrbitalCameraController.cs` — camera controls
- `Scripts/Rendering/Selection/SelectionBridge.cs` — selection ring + highlight
- `Scripts/Rendering/Selection/BodyClickHandler.cs` — raycast click selection
- `Scripts/Rendering/Selection/UIInputBlocker.cs` — blocks camera input over UI
- `Scripts/Rendering/Labels/BodyLabelController.cs` — IMGUI labels

### 4. UI

Key files:
- `Scripts/UI/Panels/ObjectListPanelController.cs` — hierarchical body list
- `Scripts/UI/Panels/ObjectDetailsPanelController.cs` — compact body properties
- `Scripts/UI/Panels/DetailModalController.cs` — full-screen modal
- `Scripts/UI/Panels/TimeControlsPanelController.cs` — pause + speed buttons
- `Scripts/UI/Core/BodyIconResolver.cs` — icon resolution
- `Scripts/UI/Localization/UIStrings.cs` — centralized Russian string provider

### 5. Data

ScriptableObjects for static configuration, content authoring, **and external data import**.

Rules:
- Configuration only
- Runtime mutable state must not live in ScriptableObjects
- External data import adapters live here (allowed to use Unity types)

Key files:
- `Scripts/Data/Config/SceneScaleConfig.cs` — world-to-scene scaling parameters
- `Scripts/Data/Definitions/CelestialBodyDefinition.cs` — serializable body definition
- `Scripts/Data/Definitions/ShipDefinition.cs` — serializable ship definition
- `Scripts/Data/Definitions/StationDefinition.cs` — serializable station definition
- `Scripts/Data/Definitions/StarSystemDefinition.cs` — ScriptableObject containing body/ship/station lists
- `Scripts/Data/Editor/SampleSystemAssetCreator.cs` — editor menu to create sample asset
- `Scripts/Data/Import/StarSystemJsonDto.cs` — **NEW** JSON DTO classes (StarSystemJson, BodyJson, OrbitJson, SpinJson, StationJson, ShipJson)
- `Scripts/Data/Import/StarSystemJsonValidator.cs` — **NEW** validates JSON structure before build
- `Scripts/Data/Import/JsonStarSystemImporter.cs` — **NEW** adapter: JSON → StarSystemBuildData → StarSystemBuilder

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
| SpaceSim.Data | false | Shared, World |
| SpaceSim.UI | false | Shared, World, Simulation |
| SpaceSim.Rendering | false | Shared, World, Simulation, Debug, UI, Data, Unity.InputSystem |

Note: SpaceSim.Data already references SpaceSim.Simulation (for StarSystemBuilder/StarSystemBuildData). JsonStarSystemImporter uses StarSystemBuilder.Build() and build data types from Simulation.Core.

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

### Pipeline 2: External JSON (NEW)
```
JSON file (TextAsset or file path)
    → JsonStarSystemImporter.LoadFromTextAsset() / LoadFromFile() / LoadFromJson()
    → StarSystemJson (DTO deserialization via JsonUtility)
    → StarSystemJsonValidator.Validate() (errors → abort, warnings → log)
    → ConvertToBuildData() → StarSystemBuildData
    → StarSystemBuilder.Build()
    → WorldRegistry + StarSystem
    → EconomyInitializer.Initialize()
```

### Coordinator fallback chain
```
1. externalSystemFile (TextAsset) assigned? → JSON import
2. starSystemDefinition (ScriptableObject) assigned? → asset import
3. SampleStarSystemFactory.Create() → built-in fallback
```

Both pipelines produce identical runtime entities through the shared StarSystemBuilder.

------------------------------------------------------------------------

## JSON Schema

```json
{
  "systemName": "string",
  "systemKey": "string",
  "localizationKey": "string",

  "bodies": [
    {
      "key": "string (unique)",
      "name": "string (display name)",
      "localizationKey": "string",
      "type": "Star|Planet|Moon|Asteroid|SurfaceSite",
      "parent": "string (key of parent body, empty for roots)",
      "attachmentMode": "None|Orbit|Surface (optional, auto-detected)",
      "radius": 1.0,
      "isSelectable": true,
      "hasSurface": false,
      "soi": 0.0,
      "orbit": {
        "semiMajorAxis": 150.0,
        "period": 120.0,
        "eccentricity": 0.0,
        "inclinationDeg": 0.0,
        "longitudeOfAscendingNodeDeg": 0.0,
        "argumentOfPeriapsisDeg": 0.0,
        "meanAnomalyAtEpochDeg": 0.0,
        "epochTime": 0.0,
        "isPrograde": true
      },
      "spin": {
        "axialTiltDeg": 0.0,
        "rotationPeriod": 60.0,
        "initialRotationDeg": 0.0
      }
    }
  ],

  "stations": [
    {
      "key": "string (unique)",
      "name": "string",
      "localizationKey": "string",
      "kind": "Orbital|Surface",
      "parentBody": "string (key of parent body)",
      "radius": 0.06,
      "orbitalRadius": 2.0,
      "orbitalPeriod": 15.0,
      "startAngleDeg": 0.0,
      "rotationPeriod": 0.0,
      "surfaceLatitudeDeg": 0.0,
      "surfaceLongitudeDeg": 0.0,
      "dockingPortCount": 0
    }
  ],

  "ships": [
    {
      "key": "string (unique)",
      "name": "string",
      "localizationKey": "string",
      "role": "Player|Trader|Patrol|Civilian",
      "shipClass": "string",
      "parentBody": "string (key of parent body)",
      "radius": 0.03,
      "orbitalRadius": 3.0,
      "orbitalPeriod": 12.0,
      "startAngleDeg": 0.0
    }
  ]
}
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
```

------------------------------------------------------------------------

## Coding Standards

- Code comments: English only
- Project documentation: English
- Integration instructions: Russian
- UI strings: localization-ready via UIStrings (current: Russian)
- Composition over inheritance, explicit dependencies, single responsibility
- Inspector-serialized fields: use float (not double) for Unity compatibility
