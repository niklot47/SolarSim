# ARCHITECTURE_STATE.md

Current snapshot of project implementation status.
Last updated after: Step 23 — Maneuver Planning Foundation, Iteration 3 + Bugfixes (ImmediateDirectScore + PlannedDepartureTime).

------------------------------------------------------------------------

## Currently Implemented Systems

### Project Foundation
- **EntityId** — immutable ulong identifier with thread-safe generation
- **SimulationClock** — pure C# time manager (pause/resume/timeScale)
- **WorldEntity** — base class for all domain entities
- **Assembly Definitions** — 7 asmdef files enforcing layer boundaries

### Debug Infrastructure
- **GameDebug** — static API: Log, CaptureSnapshot, ExportBundle, RunInvariantChecks, GetStatus, BuildBundle
- **DebugEvent / RingBuffer** — 1000 events, 200 errors, 20 snapshots
- **DebugSnapshot** — full world state capture via provider pattern
- **IDebugSnapshotProvider** — interface for subsystem contributions
- **BuiltInSnapshotProviders** — 5 providers: World, Ships, Economy, Docking, SOI
- **DebugInvariantChecker** — 7 automated checks
- **DebugExportUtility** — JSON serialization + file export
- **DebugBundle** — complete export package

### Orbital Sandbox
- **CelestialBody** — domain entity with orbital/spin data, parent-child hierarchy, ShipInfo, StationInfo, SOIRadius
- **StarSystem** — container for body ids
- **WorldRegistry** — central entity lookup
- **OrbitalPositionCalculator** — full Keplerian orbit position calculation
- **KeplerSolver** — Newton-Raphson solver for Kepler's equation
- **OrbitSampler** — adaptive subdivision and uniform fallback
- **OrbitalMapRenderer** — scene visuals, orbit lines via OrbitSampler
- **CelestialBodyView** — visual binding

### Fully Symmetric SOI Navigation — Patched-Conics Lite (Steps 19 + 20)

Three-case SOI frame switching. `HandleSOITransition()` receives both `previousSOIBodyId` and `newSOIBodyId`.
Anti-jitter via `_lastFrameSwitchTime` (MinFrameSwitchInterval = 2.0 sim-s).

### Route Safety Check (Step 21)

**RouteSafetyChecker** — pure C# static helper. Validates route against large bodies. Called inside ManeuverPlanner.

### Transfer Planning Lite (Step 22)

**TransferPlannerLite** — retained for reference; superseded by ManeuverPlanner.

### Maneuver Planning Foundation (Step 23, Iteration 3 + Bugfixes)

**ManeuverPlanner** — pure C# static helper in Simulation.Ships.

#### Bugfix 1 — misleading "poor alignment" log

**Root cause:** When `ImmediateWasAvailable == true` the returned `ManeuverPlan.DirectScore`
contained the *delayed* plan's score (e.g. 0.07) rather than the *immediate* plan's score
(e.g. -0.45) that actually triggered the delay condition.

**Fix:** Added `ImmediateDirectScore` field to `ManeuverPlan`.
- Populated to `bestImmPlan.DirectScore` in the `ImmediateWasAvailable` return path.
- `ShipMovementSystem` now logs `mPlan.ImmediateDirectScore` in the "poor alignment" message.
- `double.MinValue` when `ImmediateWasAvailable == false`.

#### Bugfix 2 — NPCShipScheduler retry spam

**Root cause:** `ScheduleNewRouteIfReady()` called `StartRoute()` every simulation tick because
`_arrivalTimes` was never reset on failure. Each call found the same delayed window and logged
the same message.

**Fix (two-part):**

1. **`PlannedDepartureTime` on `ShipInfo`:** When `StartRoute()` returns false with `IsDelayed == true`,
   `ShipMovementSystem` sets `ship.ShipInfo.PlannedDepartureTime = mPlan.DepartureTime`.
   Cleared to 0.0 when a route successfully starts.

2. **Guard in NPCShipScheduler:** Both `ScheduleNewRouteIfReady` and `ScheduleDepartureFromStation`
   check `ship.ShipInfo.PlannedDepartureTime` at entry and skip until that sim-time is reached.
   On `!started`, `_arrivalTimes[ship.Id] = simTime` is also reset so `IdleDelay` acts as a
   minimum retry interval for the all-blocked case (no planned window).

#### ManeuverPlan struct fields summary

```
Score                — best-candidate score ∈ [0.94, 1.0] (always near ceiling)
DirectScore          — candidate 0 alignment: immediate window when Success, delayed when IsDelayed
ImmediateDirectScore — candidate 0 alignment of window 0 only; triggers the delay decision
                       double.MinValue when ImmediateWasAvailable == false
```

#### Log messages (corrected)

```
[Nav] X: selected maneuver score 0.99 (direct 0.73) to Y
[Nav] X: using offset variant #2 to Y (approach 60°, score 0.97, direct -0.41, blocked by Sol)
[Nav] X: poor alignment (direct -0.52), searching better window to Y ~15s away   ← ImmediateDirectScore
[Nav] X: waiting for better window to Y (blocked by Terra), delayed departure by 20s
[Nav] X: all maneuver plans failed to Y (blocked by Terra)
```

#### PlannedDepartureTime flow

```
StartRoute() → ManeuverPlanner returns IsDelayed:
  ship.ShipInfo.PlannedDepartureTime = mPlan.DepartureTime   (set)
  return false

StartRoute() → ManeuverPlanner returns Success:
  ship.ShipInfo.PlannedDepartureTime = 0.0                   (clear)
  commit route

NPCShipScheduler.ScheduleNewRouteIfReady():
  if PlannedDepartureTime > 0 && simTime < PlannedDepartureTime → skip
  if !started → _arrivalTimes[ship.Id] = simTime             (reset idle delay)
```

#### Architecture health

- `ManeuverPlanner` is pure C# in Simulation.Ships — no UnityEngine reference
- `RouteSafetyChecker` unchanged — called inside ManeuverPlanner per candidate
- `TransferPlannerLite` retained but not called from ShipMovementSystem
- `BuildGlobalRoute` / `BuildLocalRoute` signatures unchanged from Phase 22
- `ShipMovementSystem` is the sole integration point for planning
- `ShipInfo.PlannedDepartureTime` is the contract between ShipMovementSystem and NPCShipScheduler
- No LINQ in any planning path; zero heap allocations per candidate evaluation

------------------------------------------------------------------------

## Deferred Systems

| System | Notes |
|---|---|
| Burn Windows / Phase Alignment (Phase 24) | PlannedDepartureTime already on ShipInfo; NPCShipScheduler auto-depart at window |
| Patched Conics Full (Phase 25) | True conic sections per SOI segment |
| Delta-v / Energy Model (Phase 26) | Maneuver cost and route comparison |
| Hohmann Helper (Phase 27) | Optional baseline estimator |
| Advanced Transfers (Phase 28) | Lambert-lite, intercept, non-coplanar |
| Accurate delayed ship position | Orbit integration for ManeuverPlanner delayed windows |
| Adaptive DelayPreferenceThreshold | Per-ship or distance-based |
| Prices / Money | Currency, buy/sell prices |
| Player trading UI | Buy/sell interface |
| Save/Load | WorldRegistry serialization |
| Multi-system support | Multiple star systems |
| Combat, Ship modules, Factions, Contracts | Future systems |

------------------------------------------------------------------------

## Next Recommended Development Phase

### Phase 24 — Burn Windows / Phase Alignment

`PlannedDepartureTime` is already stored on `ShipInfo` (added in bugfix).

Goals:
- NPCShipScheduler: detect when `PlannedDepartureTime` is reached and depart automatically
  without waiting for the next IdleDelay cycle.
- ManeuverPlanner: phase-angle heuristic to estimate time-to-favorable-window more precisely.
- Ships visibly wait for windows then depart at the predicted time.
- Reduces retry noise compared to current periodic-check approach.

### Phase 25–28 — (unchanged from previous plan)
