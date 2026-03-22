# PROJECT_MAP.md

## Project Overview

Modular sandbox space simulation built in Unity LTS using URP and UI Toolkit.

Long-term goals: orbital simulation sandbox, ships and stations, NPC traffic and tasks, economy and trade, faction and clan politics, maintainable architecture for humans and AI assistants.

------------------------------------------------------------------------

## Architecture Layers

### 1. Simulation

Pure game simulation logic. Time progression, orbital calculations, selection state, star system building, ship movement (segmented patched-conics), NPC scheduling, world position resolution, SOI resolution, docking, economy/cargo transfer, station production, station demand evaluation, trade opportunity resolution, route segment building.

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
- `Scripts/Simulation/Orbits/KeplerSolver.cs` — Newton-Raphson solver
- `Scripts/Simulation/Orbits/OrbitSampler.cs` — adaptive orbit geometry sampling
- `Scripts/Simulation/Ships/ShipMovementSystem.cs` — **(Phase 26c)** segmented-only navigation; legacy reframe removed
- `Scripts/Simulation/Ships/RouteSafetyChecker.cs` — route collision validation
- `Scripts/Simulation/Ships/TransferPlannerLite.cs` — retained reference; superseded by ManeuverPlanner
- `Scripts/Simulation/Ships/ManeuverPlanner.cs` — multi-window maneuver planning with geometry scoring
- `Scripts/Simulation/Ships/NPCShipScheduler.cs` — demand-driven trader routing; persistent burn windows
- `Scripts/Simulation/Ships/RouteSegmentBuilder.cs` — builds patched-conics segment lists
- `Scripts/Simulation/SOI/SOIResolver.cs` — sphere of influence resolution (debug/telemetry only for navigation)
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
- `Scripts/World/Entities/ShipInfo.cs` — PlannedDepartureTime, CurrentPlan, HasPlannedManeuver, ClearPlannedManeuver
- `Scripts/World/Entities/ShipState.cs` — includes WaitingForWindow; InsertingIntoOrbit retained but no longer entered
- `Scripts/World/Entities/PlannedManeuver.cs` — persistent maneuver plan
- `Scripts/World/Entities/ShipRoute.cs` — **(Phase 26c)** segments primary, legacy fields marked [DEPRECATED]
- `Scripts/World/Entities/SegmentType.cs` — enum for segment types
- `Scripts/World/Entities/RouteSegment.cs` — one patched-conics segment

### 3. Rendering

- `Scripts/Rendering/Bootstrap/OrbitalSandboxCoordinator.cs` — wires all services; still forwards SOI transitions (HandleSOITransition is no-op)

### 4. UI, 5. Data / Shared, Debug

Unchanged.

------------------------------------------------------------------------

## Simulation Tick Order

```
ShipMovementSystem.Update()     ← segmented-only execution
DockingSystem.Update()
NPCShipScheduler.Update()       ← handles WaitingForWindow ships
StationProductionSystem.Update()
[Periodic] StationDemandEvaluator + TradeOpportunityResolver
SOIResolver.UpdateAllShips()    ← HandleSOITransition is debug-only no-op
```

------------------------------------------------------------------------

## Ship State Machine (Phase 26c)

```
                    ┌──────────────────────┐
                    │      Orbiting        │◄─────────────────┐
                    └──────┬───────────────┘                  │
                           │                                  │
              ┌────────────┼────────────────┐                 │
              │ idle delay │ ManeuverPlanner │                 │
              │ + pick     │ returns delayed │                 │
              │ destination│ window          │                 │
              ▼            ▼                 │                 │
     ┌────────────┐  ┌──────────────────┐   │                 │
     │ Travelling │  │ WaitingForWindow │───┘ (plan invalid)  │
     │ (segments) │  └────────┬─────────┘                     │
     └─────┬──────┘           │ (window arrives)              │
           │                  ▼                               │
           │         ┌────────────┐                           │
           │         │ Travelling │                           │
           │         │ (segments) │                           │
           │         └─────┬──────┘                           │
           ▼               ▼                                  │
     ┌──────────────────────┐                                 │
     │   Orbiting (arrived) │─────────────────────────────────┘
     └──────────────────────┘
     Note: InsertingIntoOrbit state no longer entered.
     Segmented routes handle insertion as their final OrbitInsertion segment.
```

------------------------------------------------------------------------

## Strategic Direction — Road to Full Physics

Completed:
1. Impact / Collision Check Foundation ✓ (Phase 21)
2. Transfer Planning Lite ✓ (Phase 22)
3. Maneuver Planning Foundation ✓ (Phase 23)
4. Debug Log Filter System ✓ (Step 24)
5. Burn Windows / Persistent Plans ✓ (Phase 25)
6. Patched Conics Full ✓ (Phase 26a/b/c)

### Phase 27 — Delta-v / Energy Model
### Phase 28 — Hohmann Helper (Optional)
### Phase 29 — Advanced Transfers (Lambert-lite / Intercept)
