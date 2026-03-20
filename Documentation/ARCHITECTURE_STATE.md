# ARCHITECTURE_STATE.md

Current snapshot of project implementation status.
Last updated after: Step 20 — Symmetric SOI Navigation (SOI Exit + Outward Frame Switching).

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

### Fully Symmetric SOI Navigation — Patched-Conics Lite (Step 19 + 20)

#### HandleSOITransition() — updated signature (Step 20)

```csharp
public void HandleSOITransition(
    EntityId shipId,
    EntityId previousSOIBodyId,   // NEW in Step 20
    EntityId newSOIBodyId,
    double simTime,
    Func<EntityId, double, SimVec3> positionResolver)
```

Previously only `newSOIBodyId` was passed. Now both are forwarded from `OrbitalSandboxCoordinator.HandleSOITransitionEvent()` via `t.PreviousBodyId` and `t.NewBodyId`.

#### Three-Case SOI Frame Switching

**Case 1 — Entered destination SOI:**
- `newSOIBodyId == arrivalParentId`
- `StartInsertionPhase()` triggered immediately
- Progress < 0.95 guard prevents double-triggering near end of travel
- Highest priority — returns early, no other case fires

**Case 2 — Entered intermediate SOI (inward reframe):**
- `newSOIBodyId` is valid, route is currently Global frame
- `IsBodyRelatedToDestination()` confirms relevance
- `ReframeRoute()` — re-anchors remaining leg in new body's local frame
- Only fires for Global → LocalParent transitions

**Case 3 — Left the active local frame body's SOI (outward reframe) [NEW]:**
- `previousSOIBodyId == route.LocalFrameBodyId` — the exited body IS the current frame
- Route is currently in LocalParent frame
- `ReframeRouteOutward()` — re-anchors outward into parent or global frame
- Fires for LocalParent → broader LocalParent or LocalParent → Global transitions

#### ReframeRouteOutward() — new in Step 20

New frame selection logic:
```
if newSOIBodyId is valid AND is NOT a star:
    → switch to LocalParent frame of newSOIBodyId
else (newSOI is star, or no SOI at all):
    → switch to Global frame
```

In both cases:
- Current world position captured exactly (no teleport)
- Approach point recomputed from current position toward destination
- Route.DepartureTime = simTime, TravelDuration = remaining duration
- ship.ShipInfo.OverrideWorldPosition unchanged

Navigation log examples:
```
[Nav] Транспорт «Карго-7»: left Luna SOI, reframing outward → Terra frame at t=142.3
[Nav] Транспорт «Карго-7»: left Terra SOI, reframing outward → global frame at t=188.7
```

#### Anti-Jitter Mechanism

`_lastFrameSwitchTime` dictionary (per ship, sim-time of last switch).  
`MinFrameSwitchInterval = 2.0` sim-seconds.

If a ship oscillates near an SOI boundary:
- SOIResolver fires a transition each time the dominant body changes
- The cooldown prevents HandleSOITransition from firing every single tick
- After 2.0 sim-seconds, normal detection resumes

Additional stability: `StartRoute()` clears the cooldown entry for the ship, so new routes always start fresh. `CompleteArrival()` also clears the entry.

#### Full Transition Matrix

| Frame before | SOI event | Condition | Action |
|---|---|---|---|
| Any | newSOI = destination | progress < 0.95 | Case 1: early insertion |
| Global | newSOI enters hierarchy | IsBodyRelated() | Case 2: inward reframe |
| LocalParent | previousSOI = LocalFrameBodyId | remaining ≥ 5% | Case 3: outward reframe |
| LocalParent | neither of the above | — | no-op |

#### Coordinator Change (Step 20)

`OrbitalSandboxCoordinator.HandleSOITransitionEvent()` now calls:
```csharp
_shipMovement.HandleSOITransition(
    t.ShipId,
    t.PreviousBodyId,   // ← new argument
    t.NewBodyId,
    t.SimTime,
    ...);
```

Previously only `t.NewBodyId` was passed. `t.PreviousBodyId` was already available on `SOITransition` (unchanged struct).

### Ship Travel Flow (Step 20)

```
StartRoute():
    DetermineRouteFrame() → LocalParent or Global
    Build route with approach point
    _lastFrameSwitchTime.Remove(shipId)   ← cooldown reset
    ship.State = Travelling
    ↓
Update() each tick (Travelling):
    lerp position in current frame
    ↓ [SOI boundary crossed → HandleSOITransition called]:
        Case 1: destination SOI → StartInsertionPhase()
        Case 2: intermediate SOI, Global → ReframeRoute() inward
        Case 3: left frame body SOI → ReframeRouteOutward()
    ↓ [progress >= 1.0]:
    StartInsertionPhase()
    ↓
Update() each tick (InsertingIntoOrbit):
    smooth-step local interpolation to orbit point
    ↓
CompleteArrival():
    set orbit, clear route
    _lastFrameSwitchTime.Remove(shipId)   ← cooldown cleanup
    Orbiting
```

### All Previous Systems (unchanged)
- Phased orbit insertion (Step 18)
- Elliptical orbit foundation, KeplerSolver, OrbitalPositionCalculator
- WorldPositionResolver, Star System Data Loading
- Stations, Docking, Economy, Production, Demand/Trade
- Selection, UI, Camera, SOI, Debug

------------------------------------------------------------------------

## Deferred Systems

| System | Notes |
|---|---|
| Impact / collision checks | Route/body intersection checks for stars, planets, moons |
| Transfer planning lite | Safe direct-route planning before Hohmann-level navigation |
| Hohmann / burn windows | Transfer timing and simplified maneuver planning |
| Patched conics full | True conic sections per SOI segment |
| SOI exit for non-frame bodies | Currently only reframes when the ACTIVE frame body's SOI is exited |
| Insertion burn scaling | Duration proportional to SOI radius or route distance |
| Prices / Money | Currency, buy/sell prices |
| Player trading UI | Buy/sell interface |
| Save/Load | WorldRegistry serialization |
| Multi-system support | Multiple star systems |
| Orbital transfers | Hohmann, delta-v |
| Combat, Ship modules, Factions, Contracts | Future systems |

------------------------------------------------------------------------

## Architecture Health Notes

### Clean boundaries maintained
- ShipMovementSystem is pure C# — no UnityEngine reference
- `previousSOIBodyId` arrives as a plain `EntityId` via delegate — no coupling to SOIResolver
- `_lastFrameSwitchTime` dictionary is private to ShipMovementSystem — no leak to other layers
- `SOITransition` struct already had `PreviousBodyId` field; no World/Simulation changes needed
- NPCShipScheduler, DockingSystem, Economy, SOIResolver — NOT modified

### Known limitations
- Outward reframe only fires when the exited body was the **active local frame** body.  
  If a ship is in Terra-local frame and happens to cross Venus's SOI boundary during a long global leg that was later reframed, the irrelevant crossing is ignored (correct behavior).
- SOI exit for ships in Global frame is not handled (no local frame to leave).
- MinFrameSwitchInterval (2.0s) is a fixed constant; not exposed to Inspector.

------------------------------------------------------------------------

## Next Recommended Development Phase

### New Major Goal: Road to Full Physics

The nearest large-scale project goal is now **transition from hybrid gameplay navigation toward full physics-inspired orbital flight**.

This does **not** mean jumping directly to full n-body simulation. The recommended path is staged and architecture-safe.

### Recommended Roadmap

**Phase 21 — Impact / Collision Check Foundation**
- Detect route segments intersecting stars / planets / moons
- Reject unsafe routes before launch or flag in-flight impact
- Keep routing deterministic and cheap

**Phase 22 — Transfer Planning Lite**
- Add simple transfer planning for direct routes
- Prefer safe arcs over obviously colliding lines
- Prepare route representation for maneuver-driven travel

**Phase 23 — Hohmann Transfer Lite**
- Add approximate transfer-orbit planning for coplanar interplanetary travel
- Use gameplay-friendly simplifications
- Keep compatibility with existing NPC scheduler

**Phase 24 — Burn Windows / Phase Alignment**
- Ships should not depart at arbitrary times for all destinations
- Add wait-for-window behavior and target phase checks

**Phase 25 — Patched Conics Full**
- Replace current route approximation with explicit conic segments per SOI region
- True transfer leg + SOI crossing + local conic continuation

**Phase 26 — Delta-v / Energy Model**
- Introduce maneuver cost
- Burn budget / propulsion capability
- Capture / escape become energy-dependent rather than purely state-driven

**Phase 27 — Gravity Assist / Capture / Escape Mechanics**
- Flyby behavior
- Assisted trajectory changes
- More believable interplanetary navigation

### Notes

- Current system is already a strong hybrid navigation model: Keplerian body motion, SOI-aware reframing, phased insertion.
- The biggest remaining realism gap is that ships still do not follow maneuver-derived transfer trajectories.
- Player-specific systems are no longer the immediate priority until the full-physics roadmap reaches a more mature state.

Recommendation: **Phase 21 — Impact / Collision Check Foundation** first, then move step-by-step toward transfer planning and maneuver-based navigation.
