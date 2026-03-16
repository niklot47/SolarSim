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
- `Scripts/Simulation/Time/SimulationClock.cs` — simulation clock (pause, resume, time scale)
- `Scripts/Simulation/Core/SelectionService.cs` — tracks selected entity id, fires events
- `Scripts/Simulation/Core/SampleStarSystemFactory.cs` — hardcoded fallback sample system
- `Scripts/Simulation/Core/StarSystemBuilder.cs` — builds runtime entities from pure build data
- `Scripts/Simulation/Core/WorldPositionResolver.cs` — single source of truth for body world positions
- `Scripts/Simulation/Orbits/OrbitalPositionCalculator.cs` — circular orbit position in XZ plane
- `Scripts/Simulation/Ships/ShipMovementSystem.cs` — anchored travel with Global/LocalParent frame selection
- `Scripts/Simulation/Ships/NPCShipScheduler.cs` — auto-assigns routes to NPC ships, **demand-driven trader routing** via TradeOpportunityResolver, targeted cargo ops based on TraderJob phase, OnTradeRouteSelected callback for debug logging
- `Scripts/Simulation/SOI/SOIResolver.cs` — sphere of influence resolution
- `Scripts/Simulation/Docking/DockingSystem.cs` — docking lifecycle
- `Scripts/Simulation/Economy/CargoTransferService.cs` — cargo transfer operations
- `Scripts/Simulation/Economy/StationEconomyConfig.cs` — hardcoded initial resource loadouts
- `Scripts/Simulation/Economy/EconomyInitializer.cs` — initializes storage, production, and cargo
- `Scripts/Simulation/Economy/StationProductionConfig.cs` — hardcoded production recipes
- `Scripts/Simulation/Economy/StationProductionSystem.cs` — ticks production on all stations
- `Scripts/Simulation/Economy/StationDemandEvaluator.cs` — **NEW** evaluates demand/surplus scores for all stations based on storage levels and production input requirements
- `Scripts/Simulation/Economy/TradeOpportunityResolver.cs` — **NEW** scans station pairs for matching surplus/demand, produces scored trade route list

### 2. World

Game domain entities and shared world state models.

Key files:
- `Scripts/World/Entities/WorldEntity.cs` — base class (EntityId + DisplayName)
- `Scripts/World/Entities/CelestialBody.cs` — body type, parent/child ids, orbit, spin, radius, SOIRadius, ShipInfo, StationInfo
- `Scripts/World/Entities/ShipInfo.cs` — ship data including **CurrentTradeJob** field for trade assignment tracking
- `Scripts/World/Entities/ShipRoute.cs` — travel route data
- `Scripts/World/Entities/ShipCargo.cs` — ship cargo hold
- `Scripts/World/Entities/StationInfo.cs` — station data including **Demand** field (StationDemand) for demand/surplus tracking
- `Scripts/World/Entities/StationStorage.cs` — station resource storage
- `Scripts/World/Entities/StationProductionRecipe.cs` — production recipe
- `Scripts/World/Entities/StationProductionState.cs` — production progress tracker
- `Scripts/World/Entities/StationDemand.cs` — **NEW** demand and surplus scores per resource type
- `Scripts/World/Entities/TradeOpportunity.cs` — **NEW** single trade route opportunity (resource, source, destination, score)
- `Scripts/World/Entities/TraderJob.cs` — **NEW** trader's current trade assignment with phase tracking (GoingToSource/LoadingAtSource/GoingToDestination/UnloadingAtDestination)
- `Scripts/World/Entities/ResourceType.cs` — enum: Food, Metals, Fuel, Electronics
- `Scripts/World/Entities/DockingPort.cs` — single docking port
- `Scripts/World/Entities/DockingInfo.cs` — docking capability container
- `Scripts/World/Entities/StarSystem.cs` — container with root body ids and all body ids
- `Scripts/World/ValueTypes/OrbitDefinition.cs` — full Keplerian orbital elements
- `Scripts/World/ValueTypes/SpinDefinition.cs` — axial tilt, rotation period
- `Scripts/World/Systems/WorldRegistry.cs` — central entity registry

### 3. Rendering

Key files:
- `Scripts/Rendering/Bootstrap/GameBootstrap.cs` — Unity entry point
- `Scripts/Rendering/Bootstrap/OrbitalSandboxCoordinator.cs` — wires all services including **StationDemandEvaluator** and **TradeOpportunityResolver**, periodic demand evaluation, trade route logging
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
- `Scripts/UI/Panels/ObjectDetailsPanelController.cs` — compact body properties with demand/surplus and trade job display
- `Scripts/UI/Panels/DetailModalController.cs` — full-screen modal
- `Scripts/UI/Panels/TimeControlsPanelController.cs` — pause + speed buttons
- `Scripts/UI/Core/BodyIconResolver.cs` — icon resolution
- `Scripts/UI/Localization/UIStrings.cs` — centralized Russian string provider including demand/surplus/trade labels

### 5. Data

Unchanged from previous step.

### Shared

Unchanged from previous step.

### Debug

Unchanged from previous step.

------------------------------------------------------------------------

## Assembly Definitions

Unchanged from previous step.

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

Note: Demand evaluation runs every `demandEvalInterval` sim-seconds (default 5s), not every tick.

------------------------------------------------------------------------

## Trade System Pipeline

```
StationProductionSystem produces/consumes resources
    ↓
StationDemandEvaluator.EvaluateAll() (periodic)
    - Scans all stations
    - Computes demand scores from production input needs + low storage
    - Computes surplus scores from high storage + production output
    - Stores on StationInfo.Demand
    ↓
TradeOpportunityResolver.Resolve() (periodic)
    - For each station pair: match surplus at A with demand at B
    - Score = (demand × surplus) / distance
    - Sorted by score descending
    ↓
NPCShipScheduler.PickTraderDestination()
    - Asks TradeOpportunityResolver.FindBestForTrader()
    - Creates TraderJob (resource, source, destination)
    - Phase: GoingToSource → LoadingAtSource → GoingToDestination → UnloadingAtDestination
    ↓
Trader travels to source station → docks → loads target resource
    ↓
Trader travels to destination station → docks → unloads target resource
    ↓
Job complete → TraderJob cleared → next opportunity requested
```

Fallback: if no trade opportunities exist, traders use random station selection (legacy behavior).

------------------------------------------------------------------------

## Coding Standards

- Code comments: English only
- Project documentation: English
- Integration instructions: Russian
- UI strings: localization-ready via UIStrings (current: Russian)
- Composition over inheritance, explicit dependencies, single responsibility
- Inspector-serialized fields: use float (not double) for Unity compatibility
