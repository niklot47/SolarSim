# ARCHITECTURE_STATE.md

Current snapshot of project implementation status.
Last updated after: Step 25 — Burn Windows / Phase Alignment (Persistent Plans, WaitingForWindow state).

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
- **BuiltInSnapshotProviders** — 5 providers: World, Ships (with WaitingForWindow count), Economy, Docking, SOI
- **DebugInvariantChecker** — 7 automated checks
- **DebugExportUtility** — JSON serialization + file export; filter state included in metadata
- **DebugBundle** — complete export package with filter metadata

### Debug Log Filter System (Step 24)
- **DebugFilter** — pure C# runtime filter; checks category + source tag + severity; errors always pass
- **DebugFilterProfile** — ScriptableObject with hierarchical filter groups
- **DebugFilterGroup** — master toggle + list of DebugCategory names + child DebugTagToggle entries
- **DebugFilterProfileEditor** — custom Inspector rendering groups as collapsible tree
- **DebugFilterProfileCreator** — editor menu to create pre-configured default profile asset

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
- ImmediateDirectScore, PlannedDepartureTime, retry spam guard — see Step 23 docs.

### Burn Windows / Persistent Plans (Step 25)

**PlannedManeuver** — pure C# data class in World.Entities. Persists maneuver decision on ShipInfo.
Fields: PlannedDepartureTime, TargetBodyId, Score, DirectScore, EstimatedTravelDuration, CreatedAtSimTime.
MaxPlanAge = 120 sim-s (stale plans are invalidated).

**ShipState.WaitingForWindow** — new ship state. Ship has a PlannedManeuver and continues orbiting
while waiting for the departure window to open. Visually identical to Orbiting (orbit line visible,
standard position via WorldPositionResolver). Transitions to Travelling when window opens,
or back to Orbiting if plan is invalidated.

**ShipInfo.CurrentPlan** — nullable PlannedManeuver field. Non-null when ship is waiting for a window.
**ShipInfo.HasPlannedManeuver** — convenience property checking CurrentPlan != null && target valid.
**ShipInfo.ClearPlannedManeuver()** — clears plan + PlannedDepartureTime + reverts state if WaitingForWindow.

#### NPCShipScheduler behavior change (Step 25)

OLD (Phase 23 — stateless):
- Every tick → compute route → ManeuverPlanner → if delayed, store PlannedDepartureTime → next tick retry

NEW (Phase 25 — stateful):
1. Ship picks destination → calls StartRoute() → ManeuverPlanner evaluates
2. If immediate window → execute now, no plan stored
3. If delayed window → ShipMovementSystem stores PlannedDepartureTime → NPCShipScheduler creates
   PlannedManeuver on ShipInfo → ship enters WaitingForWindow state
4. While WaitingForWindow: validate plan each tick (target exists, plan not stale)
5. When PlannedDepartureTime reached → attempt StartRoute with stored target + duration
6. If plan invalid → ClearPlannedManeuver(), revert to Orbiting, reschedule after idle delay

#### Plan validation (Step 25)
- Target body removed from registry → plan invalidated, log "[Nav] plan invalidated — target body no longer exists"
- Plan older than MaxPlanAge (120s) → plan invalidated, log "[Nav] plan invalidated — stale"
- StartRoute fails at planned time → either creates new plan (if ManeuverPlanner finds another window) or falls back to idle

#### Plan event logging (Step 25)
- `"[Nav] planned maneuver to X at T+Ys (waiting for burn window)"` — plan created
- `"[Nav] executing planned maneuver to X (planned score=Y)"` — window arrived
- `"[Nav] plan invalidated — target body no longer exists"` — target removed
- `"[Nav] plan invalidated — stale (age=Xs)"` — plan too old
All events use source tag "Navigation" (existing filter group).

------------------------------------------------------------------------

## Debug Filter Coding Standards

(unchanged from Step 24)

------------------------------------------------------------------------

## Deferred Systems

| System | Notes |
|---|---|
| Patched Conics Full (Phase 26) | True conic sections per SOI segment |
| Delta-v / Energy Model (Phase 27) | Maneuver cost and route comparison |
| Hohmann Helper (Phase 28) | Optional baseline estimator |
| Advanced Transfers (Phase 29) | Lambert-lite, intercept, non-coplanar |
| Prices / Money | Currency, buy/sell prices |
| Player trading UI | Buy/sell interface |
| Save/Load | WorldRegistry serialization |
| Multi-system support | Multiple star systems |
| Combat, Ship modules, Factions, Contracts | Future systems |

------------------------------------------------------------------------

## Architecture Health Notes

### Clean boundaries maintained
- PlannedManeuver is pure C# in World layer — no UnityEngine dependency
- ShipState.WaitingForWindow has no special rendering behavior — standard orbit rendering applies
- NPCShipScheduler plan logic is pure C# — no Unity dependency
- Plan event callback (OnPlanEvent) follows existing event pattern (coordinator routes to GameDebug)
- All existing simulation/world layer code unchanged — plan system is additive

### Known technical debt (inherited + new)
- PlannedManeuver.Score/DirectScore are 0.0 when created from PlannedDepartureTime (ManeuverPlanner doesn't expose these on the delayed path currently)
- PlannedManeuver MaxPlanAge is a hardcoded constant (120s) — should be configurable via Inspector in future
- WaitingForWindow ship continues to be subject to SOI transitions and docking logic — these should clear the plan if they change the ship's state unexpectedly (not implemented yet, low risk with current NPC patterns)

------------------------------------------------------------------------

## Next Recommended Development Phase

### Phase 26 — Patched Conics Full
### Phase 27 — Delta-v / Energy Model
### Phase 28 — Hohmann Helper (Optional)
### Phase 29 — Advanced Transfers (Lambert-lite / Intercept)
