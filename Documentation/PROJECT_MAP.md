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
- `Scripts/Simulation/Ships/ShipMovementSystem.cs` — SOI-aware travel; phased orbit insertion; HandleSOITransition(); anti-jitter cooldown; **Phase 21: RouteSafetyChecker; Phase 22: TransferPlannerLite; Phase 23 Iter 1: ManeuverPlanner; Phase 23 Iter 2: scoring logs; Phase 23 Iter 3: logs now show score + direct, poor-alignment log uses DirectScore**
- `Scripts/Simulation/Ships/RouteSafetyChecker.cs` — **(Phase 21)** pure C# static helper; validates planned route segment; closest-point-on-segment + sampled points; no allocations; called inside ManeuverPlanner
- `Scripts/Simulation/Ships/TransferPlannerLite.cs` — **(Phase 22)** retained reference; superseded by ManeuverPlanner
- `Scripts/Simulation/Ships/ManeuverPlanner.cs` — **(Phase 23 Iter 3)** pure C# static helper; evaluates ALL safe candidates across immediate + delayed windows; scores by alignment with target velocity; **Iteration 3 fix: adds DirectScore (candidate 0 alignment, [-1,+1]) alongside Score (best candidate, always ~1.0); delay decision now uses DirectScore so poor-geometry waiting actually fires**
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
- `Scripts/Rendering/Bootstrap/OrbitalSandboxCoordinator.cs` — wires all services; **Phase 21 inspector fields**: routeSafetyEnabled, routeSafetyMargin, routeSafetyCheckSamples (no changes in Phases 22–23)
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

## Route Planning and Safety Pipeline (Phases 21 + 22 + 23 Iter 2)

```
ShipMovementSystem.StartRoute()
    ↓
ComputeShipWorldPosition(ship, currentSimTime)
approachRadius = destOrbitRadius × OrbitApproachMultiplier

if ImpactSafetyEnabled:
    ── ManeuverPlanner.Plan() (Phase 23 Iter 2) ─────────────────────────
    |
    |  destPeriod = arrivalBody.Orbit.OrbitalPeriod (default 120)
    |  windowInterval = clamp(destPeriod × 0.08, 5, 30)
    |  velDt = clamp(destPeriod × 0.005, 0.1, 2.0)
    |
    |  Window 0 (immediate):
    |    destVelDir = (destPos(t+velDt) - destPos(t)).normalized
    |    for each offset [0°, ±30°, ±60°, ±90°, ±120°, +150°]:
    |      approachPos = destPos(arrivalTime) + approachRadius × dir(baseAngle+offset)
    |      RouteSafetyChecker.IsSafe(...)
    |      if safe: score = dot(approachDir, destVelDir) - offsetPenalty
    |    → bestImmediate (highest score among safe candidates)
    |
    |  Windows 1…4 (delayed, Δt = windowInterval):
    |    same loop, body positions at futureDepTime
    |    → bestDelayed (highest score across all windows)
    |
    |  Decision:
    |    bestImm AND bestDel.score > bestImm.score + 0.5
    |      → StrategyDelayed, ImmediateWasAvailable=true → return false
    |    bestImm found
    |      → StrategyDirect/Offset → return plan
    |    bestDel found (no immediate)
    |      → StrategyDelayed, ImmediateWasAvailable=false → return false
    |    nothing → StrategyType=-1 → return false
    ───────────────────────────────────────────────────────────────────────
    if !Success:
      IsDelayed + ImmediateWasAvailable → "poor alignment, searching better window"
      IsDelayed (no immediate)          → "delayed departure by Y seconds"
      else                              → "all maneuver plans failed"
    if StrategyOffset  → "using offset variant #N (score X)"
    if StrategyDirect  → "selected maneuver score X"
else:
    Direct angle, no validation

BuildGlobalRoute(approachAngleDeg, approachRadius, shipWorldPos)  or
BuildLocalRoute( approachAngleDeg, approachRadius, shipWorldPos)
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
        → ManeuverPlanner.Plan()                   ← Phase 23 Iter 2
            EvaluateWindow() × 5 (1 imm + 4 del)
                → ComputeVelocityDirection() × 5   (2 resolver calls each)
                → RouteSafetyChecker.IsSafe() × ≤50 (per safe candidate)
                → ScoreCandidate() × ≤50
            → Decision → ManeuverPlan
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
3. Maneuver Planning Foundation ✓ (Phase 23, Iterations 1 + 2)

### Phase 24 — Burn Windows / Phase Alignment

- Store planned departure time on ShipInfo when StrategyDelayed is returned.
- NPCShipScheduler holds ship until planned departure — no blind retry loop.
- ManeuverPlanner extended: phase-angle heuristic to estimate time-to-favorable-window.
- Ships visibly wait for windows then depart intentionally.

### Phase 25 — Patched Conics Full
### Phase 26 — Delta-v / Energy Model
### Phase 27 — Hohmann Helper (Optional)
### Phase 28 — Advanced Transfers (Lambert-lite / Intercept)
