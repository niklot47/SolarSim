# ARCHITECTURE_STATE.md

Current snapshot of project implementation status.
Last updated after: Step 23 — Maneuver Planning Foundation, Iteration 3 (DirectScore fix).

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

Three-case SOI frame switching (Case 1: early insertion, Case 2: inward reframe, Case 3: outward reframe).
`HandleSOITransition()` receives both `previousSOIBodyId` and `newSOIBodyId`.
Anti-jitter via `_lastFrameSwitchTime` (MinFrameSwitchInterval = 2.0 sim-s).

### Route Safety Check — Impact / Collision Check Foundation (Step 21)

**RouteSafetyChecker** — pure C# static helper. Validates a route's straight-line path against
all large celestial bodies (Star, Planet, Moon, Asteroid). Uses closest-point-on-segment geometry
plus N sampled points. No allocations. Called inside ManeuverPlanner per candidate.

Inspector fields on OrbitalSandboxCoordinator (wired to ShipMovementSystem):
- `routeSafetyEnabled` (bool, default true)
- `routeSafetyMargin` (float Mm, default 0.1)
- `routeSafetyCheckSamples` (int, default 20)

### Transfer Planning Lite (Step 22)

**TransferPlannerLite** — retained for reference; no longer called from ShipMovementSystem.
Superseded by ManeuverPlanner in Phase 23.

### Maneuver Planning Foundation (Step 23, Iteration 2)

**ManeuverPlanner** — pure C# static helper in Simulation.Ships. Phase-aware maneuver planning
abstraction replacing TransferPlannerLite in ShipMovementSystem.StartRoute().

#### Iteration summary

| Feature | Iteration 1 | Iteration 2 | Iteration 3 |
|---|---|---|---|
| Candidate selection | First safe | Best scored (all candidates) | Best scored (unchanged) |
| Scoring | None | alignment dot − offset penalty | unchanged |
| Score range | — | always 0.94–1.00 (bug) | unchanged (acknowledged, documented) |
| DirectScore | None | None | **New** — candidate 0 alignment, [-1, +1] |
| Window interval | Fixed 20 sim-s | 8% of period, clamped [5, 30] | unchanged |
| Delay condition | Any future safe window | bestDelScore > bestImmScore + 0.5 (broken) | **DirectScore-based (fixed)** |
| Log: score | Not present | Added | `score X (direct Y)` — both shown |
| Log: "poor alignment" | Not present | Used Score (always ~1.0, broken) | Uses DirectScore (correct) |

#### ManeuverPlan struct (Iteration 3)

```
ManeuverPlan {
  bool    Success                — true = depart now; false = wait
  double  DepartureTime          — currentSimTime (immediate) or future estimate (delayed)
  double  ApproachAngleDeg       — chosen approach angle (world XZ plane)
  double  ApproachRadius         — approach distance in Mm
  SimVec3 ApproachWorldPos       — pre-computed approach point
  int     StrategyType           — 0=Direct, 1=Offset, 2=Delayed, -1=AllFailed
  int     VariantIndex           — angle-offset table index; -1=failed
  double  Score                  — best-candidate score ∈ [0.94, 1.0]; always near ceiling
  double  DirectScore            — NEW: candidate 0 alignment ∈ [-1.0, +1.0]; true geometry metric
  bool    IsDelayed              — true when StrategyType == StrategyDelayed
  bool    ImmediateWasAvailable  — true when immediate existed but delayed was preferred
  string  DirectRouteBlocker     — first blocking body on direct candidate, for logs
}
```

#### Strategy types (unchanged)

```
StrategyDirect  (0) — immediate departure, direct approach angle.
StrategyOffset  (1) — immediate departure, rotated approach angle.
StrategyDelayed (2) — ship must wait. Success=false.
-1              — no viable window found at all. Success=false.
```

#### Two score values (Iteration 3)

```
Score (best candidate — unchanged from Iteration 2):
  = dot(approachDir_best, destVelDir) - offsetPenalty
  Always ∈ [0.94, 1.0] in practice (10 evenly-spaced candidates guarantee
  a near-optimal angle). Logged to show the chosen approach quality.

DirectScore (NEW in Iteration 3 — candidate 0 only):
  directApproachDir = normalize(destPos(t) + approachRadius*dir(baseAngle) - destPos(t))
  DirectScore = dot(directApproachDir, destVelDir)   ← no penalty
  Range: [-1.0, +1.0]
  +1.0 = approach is directly ahead of target's orbital motion (ideal intercept)
  -1.0 = approach is directly behind (chasing — poor geometry)
  Computed unconditionally even when the direct route is safety-blocked.
  Varies freely with orbital phase — the meaningful diagnostic metric.
```

#### Delay decision (fixed in Iteration 3)

```
OLD (broken, Iteration 2):
  bestDelScore > bestImmScore + 0.5    → never true (both ≈ 0.97–1.0)

NEW (Iteration 3):
  currentGeometryPoor      = bestImmPlan.DirectScore < -0.2
  delayedSignificantlyBetter = bestDelPlan.DirectScore > bestImmPlan.DirectScore + 0.5
  wait = currentGeometryPoor AND delayedSignificantlyBetter
```

#### Adaptive window interval (new in Iteration 2)

```
destPeriod     = destination body's OrbitalPeriod (DefaultOrbitalPeriod=120 if no orbit)
windowInterval = clamp(destPeriod × 0.08, 5, 30) sim-s
velDt          = clamp(destPeriod × 0.005, 0.1, 2.0) sim-s   (velocity estimate dt)
```

Bodies with period 20 s → windowInterval = 5 s (minimum).
Bodies with period 375 s → windowInterval = 30 s (maximum).

#### Selection algorithm (Iteration 2)

```
ManeuverPlanner.Plan():
  Window 0 (immediate):
    for each angle offset [0°, ±30°, ±60°, ±90°, ±120°, +150°]:
      compute approachPos, run RouteSafetyChecker
      if safe: score = alignment - offsetPenalty
    → track best-scored safe candidate → bestImmediate

  Windows 1…4 (delayed, Δt = adaptive interval):
    same candidate loop, body positions at futureDepTime
    → track best-scored safe candidate across all windows → bestDelayed

  Decision:
    if bestImmediate found AND bestDelayed.score > bestImmediate.score + 0.5:
        Success=false, StrategyDelayed, ImmediateWasAvailable=true
        Log: "poor alignment, searching better window"

    elif bestImmediate found:
        Success=true, StrategyDirect or StrategyOffset
        Log: "selected maneuver score X" or "using offset variant #N"

    elif bestDelayed found:
        Success=false, StrategyDelayed, ImmediateWasAvailable=false
        Log: "delayed departure by Y seconds"

    else:
        Success=false, StrategyType=-1
        Log: "all maneuver plans failed"
```

#### Log messages (Iteration 3)

```
[Nav] X: selected maneuver score 0.99 (direct 0.73) to Y       ← direct route, both scores shown
[Nav] X: using offset variant #2 to Y (approach 60°, score 0.97, direct -0.41, blocked by Sol)
[Nav] X: poor alignment (direct -0.52), searching better window to Y ~15s away
[Nav] X: waiting for better window to Y (blocked by Terra), delayed departure by 20s
[Nav] X: all maneuver plans failed to Y (blocked by Terra)
```

DirectScore in the first two cases reveals orbital geometry quality even when the ship departs immediately: positive = favorable, negative = ship is chasing the target.

#### Integration in ShipMovementSystem.StartRoute() (unchanged from Iteration 1)

```
mPlan = ManeuverPlanner.Plan(shipWorldPos, arrivalParentId, approachRadius, ...)
if !mPlan.Success → log → return false
BuildGlobalRoute / BuildLocalRoute (unchanged signatures)
commit ship state
```

#### Architecture health

- `ManeuverPlanner` is pure C# in Simulation.Ships — no UnityEngine reference
- `RouteSafetyChecker` unchanged — called inside ManeuverPlanner per candidate
- `TransferPlannerLite` retained but not called from ShipMovementSystem
- `BuildGlobalRoute` / `BuildLocalRoute` signatures unchanged from Phase 22
- `ShipMovementSystem` is the sole integration point
- No LINQ in any planning path
- Zero heap allocations per candidate evaluation in EvaluateWindow()

#### Performance analysis

Per StartRoute() call (infrequent — on ship departure only):
- Windows evaluated: 5 (1 immediate + 4 delayed)
- Candidates per window: 10
- Calls per candidate: 1 RouteSafetyChecker + 1 position resolver (approach point)
- Additional per window: 2 position resolver calls (velocity direction estimate)
- Total: ≈ 60 RouteSafetyChecker calls + ≈ 60 position resolver calls + 10 velocity samples
- All stack-local — zero heap allocations in the hot path

#### Known limitations (MVP)

- Ship world position approximated as constant across delayed windows.
  Accurate position at future departure time requires orbit integration (Phase 24).
- Delayed window detection signals the caller (returns false); scheduler retries naturally.
  No explicit departure timer is stored on the ship.
- Velocity direction uses forward finite difference (one extra position resolver call per window).
  A central difference would be slightly more accurate but doubles the calls.
- DelayPreferenceThreshold (0.5) is a global constant. Future: per-ship or distance-based.

------------------------------------------------------------------------

## Deferred Systems

| System | Notes |
|---|---|
| Burn Windows / Phase Alignment | Scheduled departure (Phase 24): store planned departure time on ship, depart automatically at window |
| Patched Conics Full | True conic sections per SOI segment (Phase 25) |
| Delta-v / Energy Model | Maneuver cost and route comparison (Phase 26) |
| Hohmann Helper | Optional baseline estimator (Phase 27) |
| Advanced Transfers | Lambert-lite, intercept, non-coplanar (Phase 28) |
| Accurate delayed ship position | Orbit integration for ManeuverPlanner delayed windows |
| Adaptive DelayPreferenceThreshold | Per-ship or distance-based threshold tuning |
| Central-difference velocity estimate | More accurate than forward finite difference |
| Prices / Money | Currency, buy/sell prices |
| Player trading UI | Buy/sell interface |
| Save/Load | WorldRegistry serialization |
| Multi-system support | Multiple star systems |
| Combat, Ship modules, Factions, Contracts | Future systems |

------------------------------------------------------------------------

## Route Planning and Safety Pipeline (Steps 21 + 22 + 23 Iteration 2)

```
ShipMovementSystem.StartRoute()
    ↓
ComputeShipWorldPosition()
approachRadius = destOrbitRadius × OrbitApproachMultiplier

if ImpactSafetyEnabled:
    ManeuverPlanner.Plan(shipWorldPos, arrivalParentId, approachRadius, ...)
    ↓
    destPeriod = arrivalBody.Orbit.OrbitalPeriod  (or 120s default)
    windowInterval = clamp(destPeriod × 0.08, 5, 30)
    velDt = clamp(destPeriod × 0.005, 0.1, 2.0)

    ── EvaluateWindow(window=0, immediate) ──────────────────────────────
    |  destVelDir = ComputeVelocityDirection(dest, t0, velDt)
    |  for each offset in [0°, ±30°, ±60°, ±90°, ±120°, +150°]:
    |    approachPos = destPos(arrivalTime) + approachRadius × dir(baseAngle + offset)
    |    RouteSafetyChecker.IsSafe(shipPos, approachPos, departureTime, ...)
    |    if safe: score = dot(approachDir, destVelDir) - offsetPenalty
    |  → bestImmediate (highest-scoring safe candidate)
    ─────────────────────────────────────────────────────────────────────

    ── EvaluateWindow(windows 1…4, delayed) ────────────────────────────
    |  same loop, body positions at futureDepTime
    |  → bestDelayed (highest-scoring across all delay steps)
    ─────────────────────────────────────────────────────────────────────

    Decision:
      bestImmediate AND bestDelayed.score > bestImmediate.score + 0.5
        → StrategyDelayed, ImmediateWasAvailable=true → return false
      bestImmediate found
        → StrategyDirect/Offset → return plan
      bestDelayed found (no immediate)
        → StrategyDelayed, ImmediateWasAvailable=false → return false
      nothing
        → StrategyType=-1 → return false
else:
    Direct angle, no validation

BuildGlobalRoute(approachAngleDeg, approachRadius, shipWorldPos)  or
BuildLocalRoute( approachAngleDeg, approachRadius, shipWorldPos)
    ↓
commit ship state → ShipState.Travelling
```

------------------------------------------------------------------------

## Next Recommended Development Phase

### Phase 24 — Burn Windows / Phase Alignment

Extend ManeuverPlanner with explicit departure scheduling.

Goals:
- Store planned departure time on ShipInfo when a delayed window is chosen.
- NPCShipScheduler: hold ships until planned departure time instead of retrying blind.
- ManeuverPlanner: add WaitForWindow strategy — compute time until alignment improves.
- Phase-angle estimator: approximate how far orbital configuration must advance
  to produce favorable alignment (no full orbit mechanics required).

Outcome:
- Ships visibly "wait for a window", then depart at the predicted time.
- Reduces unnecessary retry noise in NPCShipScheduler logs.
- Natural progression: ships become intentional rather than reactive.

---

### Phase 25 — Patched Conics Full
### Phase 26 — Delta-v / Energy Model
### Phase 27 — Hohmann Helper (Optional)
### Phase 28 — Advanced Transfers (Lambert-lite / Intercept)
