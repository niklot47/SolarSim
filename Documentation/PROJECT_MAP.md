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
- `Scripts/Simulation/Orbits/OrbitalPositionCalculator.cs` — full Keplerian orbit position
- `Scripts/Simulation/Orbits/KeplerSolver.cs` — Newton-Raphson solver for Kepler's equation
- `Scripts/Simulation/Orbits/OrbitSampler.cs` — adaptive orbit geometry sampling
- `Scripts/Simulation/Ships/ShipMovementSystem.cs` — SOI-aware travel; phased orbit insertion; HandleSOITransition(); anti-jitter cooldown; Phase 21: RouteSafetyChecker; Phase 22: TransferPlannerLite; Phase 23: ManeuverPlanner; **Bugfix: logs ImmediateDirectScore in "poor alignment" message; sets PlannedDepartureTime on delayed plan, clears on success**
- `Scripts/Simulation/Ships/RouteSafetyChecker.cs` — **(Phase 21)** pure C# static helper; validates planned route segment; no allocations; called inside ManeuverPlanner
- `Scripts/Simulation/Ships/TransferPlannerLite.cs` — **(Phase 22)** retained reference; superseded by ManeuverPlanner
- `Scripts/Simulation/Ships/ManeuverPlanner.cs` — **(Phase 23 + Bugfix)** pure C# static helper; evaluates ALL safe candidates across immediate + delayed windows; scores by alignment with target velocity; **Bugfix: adds ImmediateDirectScore field to ManeuverPlan (immediate window's DirectScore, used in "poor alignment" log); adds GetWindowInterval() helper**
- `Scripts/Simulation/Ships/NPCShipScheduler.cs` — demand-driven trader routing; **Bugfix: checks ship.ShipInfo.PlannedDepartureTime before calling StartRoute(); resets _arrivalTimes on !started to prevent tight retry loop**
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

Key files (changed):
- `Scripts/World/Entities/ShipInfo.cs` — **Bugfix: added `PlannedDepartureTime` (double, default 0.0); set by ShipMovementSystem when ManeuverPlanner returns a delayed window; read by NPCShipScheduler to prevent retry spam**

### 3. Rendering

Key files:
- `Scripts/Rendering/Bootstrap/GameBootstrap.cs` — Unity entry point
- `Scripts/Rendering/Bootstrap/OrbitalSandboxCoordinator.cs` — wires all services; Phase 21 inspector fields unchanged
- `Scripts/Rendering/Bootstrap/StarSystemLoader.cs` — converts ScriptableObject definitions to build data
- `Scripts/Rendering/Orbits/OrbitalMapRenderer.cs` — scene visuals, orbit lines via OrbitSampler
- `Scripts/Rendering/Planets/CelestialBodyView.cs` — body visual representation
- `Scripts/Rendering/Cameras/OrbitalCameraController.cs` — camera controls
- `Scripts/Rendering/Selection/SelectionBridge.cs` — selection ring + highlight
- `Scripts/Rendering/Selection/BodyClickHandler.cs` — raycast click selection
- `Scripts/Rendering/Selection/UIInputBlocker.cs` — blocks camera input over UI
- `Scripts/Rendering/Labels/BodyLabelController.cs` — IMGUI labels clipped to viewport

### 4. UI

Key files: unchanged.

### 5. Data / Shared / Debug

Unchanged.

------------------------------------------------------------------------

## Assembly Definitions

| Assembly | noEngineReferences | Dependencies |
|---|---|---|
| SpaceSim.Shared | true | (none) |
| SpaceSim.World | true | Shared |
| SpaceSim.Simulation | true | Shared, World |
| SpaceSim.Debug | false | Shared, Simulation, World |
| SpaceSim.Data | false | Shared, World, Simulation |
| SpaceSim.UI | false | Shared, World, Simulation |
| SpaceSim.Rendering | false | Shared, World, Simulation, Debug, UI, Data, Unity.InputSystem |

------------------------------------------------------------------------

## Route Planning and Safety Pipeline (Phases 21 + 22 + 23 + Bugfixes)

```
ShipMovementSystem.StartRoute()
    ↓
ComputeShipWorldPosition()
approachRadius = destOrbitRadius × OrbitApproachMultiplier

if ImpactSafetyEnabled:
    ManeuverPlanner.Plan(...)
    [5 windows × 10 candidates — see ManeuverPlanner docs]

    if !mPlan.Success:
      if IsDelayed:
        ship.ShipInfo.PlannedDepartureTime = mPlan.DepartureTime    ← NEW
        if ImmediateWasAvailable:
          log "poor alignment (direct {ImmediateDirectScore})"       ← FIXED
        else:
          log "delayed departure by Y seconds"
      else:
        log "all maneuver plans failed"
      return false

    ship.ShipInfo.PlannedDepartureTime = 0.0                         ← NEW
    log success

BuildGlobalRoute / BuildLocalRoute
commit ship state → ShipState.Travelling

NPCShipScheduler.ScheduleNewRouteIfReady():
    if PlannedDepartureTime > 0 && simTime < PlannedDepartureTime:
        return                                                        ← NEW
    ... idle delay check ...
    bool started = StartRoute(...)
    if !started:
        _arrivalTimes[ship.Id] = simTime                             ← NEW
```

------------------------------------------------------------------------

## Simulation Tick Order

```
ShipMovementSystem.Update()
DockingSystem.Update()
NPCShipScheduler.Update()
    → ScheduleNewRouteIfReady / ScheduleDepartureFromStation
        → PlannedDepartureTime guard (skip if window not yet open)
        → ShipMovementSystem.StartRoute()
            → ManeuverPlanner.Plan()
                EvaluateWindow() × 5 (1 imm + 4 del)
                    → RouteSafetyChecker.IsSafe() × ≤50
            → set/clear PlannedDepartureTime on ShipInfo
        → Build route with chosen approach geometry
StationProductionSystem.Update()
[Periodic] StationDemandEvaluator + TradeOpportunityResolver
SOIResolver.UpdateAllShips()
    → ShipMovementSystem.HandleSOITransition()
```

------------------------------------------------------------------------

## Coding Standards

- Code comments: English only
- Project documentation: English
- Integration instructions: Russian
- UI strings: localization-ready via UIStrings (current: Russian)
- Composition over inheritance, explicit dependencies, single responsibility
- Inspector-serialized fields: use float (not double) for Unity compatibility

------------------------------------------------------------------------

## Strategic Direction — Road to Full Physics

Completed:
1. Impact / Collision Check Foundation ✓ (Phase 21)
2. Transfer Planning Lite ✓ (Phase 22)
3. Maneuver Planning Foundation ✓ (Phase 23, Iterations 1–3 + Bugfixes)

### Phase 24 — Burn Windows / Phase Alignment

`PlannedDepartureTime` is already on `ShipInfo`. Phase 24 promotes it to a proper scheduling mechanism:
- NPCShipScheduler departs automatically when the window opens (no manual retry polling).
- ManeuverPlanner phase-angle heuristic for better window time estimation.
- Ships intentionally wait, then depart at the predicted time.

### Phase 25 — Patched Conics Full
### Phase 26 — Delta-v / Energy Model
### Phase 27 — Hohmann Helper (Optional)
### Phase 28 — Advanced Transfers (Lambert-lite / Intercept)
