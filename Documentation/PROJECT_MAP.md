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
- `Scripts/Simulation/Orbits/OrbitalPositionCalculator.cs` — **full Keplerian orbit position**: elliptical, inclined, and circular; surface position from lat/lon
- `Scripts/Simulation/Orbits/KeplerSolver.cs` — Newton-Raphson solver for Kepler's equation
- `Scripts/Simulation/Orbits/OrbitSampler.cs` — adaptive orbit geometry sampling
- `Scripts/Simulation/Ships/ShipMovementSystem.cs` — SOI-aware travel with symmetric frame switching; phased orbit insertion; HandleSOITransition() receives both previousSOIBodyId and newSOIBodyId; per-ship anti-jitter cooldown; **Phase 21: RouteSafetyChecker integration; Phase 22: TransferPlannerLite integration — BuildGlobalRoute/BuildLocalRoute now accept pre-computed approach angle and radius**
- `Scripts/Simulation/Ships/RouteSafetyChecker.cs` — **(Phase 21)** pure C# static helper; validates planned route segment against large blocking bodies; closest-point-on-segment + sampled points; no allocations
- `Scripts/Simulation/Ships/TransferPlannerLite.cs` — **(Phase 22)** pure C# static helper; generates up to 10 approach angle variants (0°, ±30°, ±60°, ±90°, ±120°, +150°) for the same destination; tries each with RouteSafetyChecker; returns first safe ApproachPlan or reports failure
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

Game domain entities and shared world state models. Key files: unchanged from Step 22.

### 3. Rendering

Key files:
- `Scripts/Rendering/Bootstrap/GameBootstrap.cs` — Unity entry point
- `Scripts/Rendering/Bootstrap/OrbitalSandboxCoordinator.cs` — wires all services; **Phase 21 inspector fields**: routeSafetyEnabled, routeSafetyMargin, routeSafetyCheckSamples (no changes in Phase 22)
- `Scripts/Rendering/Bootstrap/StarSystemLoader.cs` — converts ScriptableObject definitions to build data
- `Scripts/Rendering/Orbits/OrbitalMapRenderer.cs` — scene visuals, orbit lines via OrbitSampler
- `Scripts/Rendering/Planets/CelestialBodyView.cs` — body visual representation
- `Scripts/Rendering/Cameras/OrbitalCameraController.cs` — camera controls
- `Scripts/Rendering/Selection/SelectionBridge.cs` — selection ring + highlight
- `Scripts/Rendering/Selection/BodyClickHandler.cs` — raycast click selection
- `Scripts/Rendering/Selection/UIInputBlocker.cs` — blocks camera input over UI
- `Scripts/Rendering/Labels/BodyLabelController.cs` — IMGUI labels clipped to viewport

### 4. UI

Key files: unchanged from Step 21.

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

## Route Planning and Safety Pipeline (Phases 21 + 22)

```
ShipMovementSystem.StartRoute()
    ↓
ComputeShipWorldPosition(ship, currentSimTime)
approachRadius = destOrbitRadius × OrbitApproachMultiplier
estimatedArrivalTime = currentSimTime + travelDuration

if ImpactSafetyEnabled:
    ── TransferPlannerLite.FindSafeApproach() ──────────────────────────
    |  destWorldAtArrival = positionResolver(arrivalParentId, arrivalTime)
    |  baseAngle = atan2(ship - dest)
    |  for offset in [0°, +30°, -30°, +60°, -60°, +90°, -90°, +120°, -120°, +150°]:
    |      candidateApproach = dest + approachRadius × direction(baseAngle + offset)
    |      RouteSafetyChecker.IsSafe(shipPos, candidateApproach, ...)
    |          for each Star/Planet/Moon/Asteroid:
    |              PointToSegmentDistanceSq < (radius + margin)²?
    |      → first safe: return ApproachPlan(angle, worldPos, variantIndex)
    |  → all failed: return ApproachPlan(Success=false)
    ──────────────────────────────────────────────────────────────────
    if !Success: log + return false   (ship waits, scheduler retries)
    if variantIndex > 0: log "direct blocked, using variant #N"
else:
    DirectApproach() — computes direct angle, no validation

BuildGlobalRoute(approachAngleDeg, approachRadius, shipWorldPos)  or
BuildLocalRoute(approachAngleDeg, approachRadius, shipWorldPos)
    ↓
commit ship state → ShipState.Travelling
```

------------------------------------------------------------------------

## Simulation Tick Order

```
ShipMovementSystem.Update()
DockingSystem.Update()
NPCShipScheduler.Update()
    → ShipMovementSystem.StartRoute()
        → TransferPlannerLite.FindSafeApproach()   ← Phase 22
            → RouteSafetyChecker.IsSafe()          ← Phase 21 (called per candidate)
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

### New Direction

3. Maneuver Planning Foundation
   - generalized transfer planning
   - departure timing control
   - strategy selection (not fixed geometry)

4. Burn Windows / Phase Alignment
   - phase-based departure
   - scheduler-aware waiting behavior

5. Patched Conics Full
   - explicit trajectory segments per SOI
   - physically consistent transitions

6. Delta-v / Energy Model
   - maneuver cost
   - route comparison

7. Hohmann Helper (Optional)
   - baseline estimator for simple cases
   - not core system

8. Advanced Transfers
   - intercept trajectories
   - moving targets
   - non-coplanar transfers
