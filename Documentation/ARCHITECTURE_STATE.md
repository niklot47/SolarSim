# ARCHITECTURE_STATE.md

Current snapshot of project implementation status.
Last updated after: Phase 26c — Patched Conics Cleanup (legacy reframe removed, segmented-only navigation).

------------------------------------------------------------------------

## Currently Implemented Systems

### Project Foundation
- **EntityId** — immutable ulong identifier with thread-safe generation
- **SimulationClock** — pure C# time manager (pause/resume/timeScale)
- **WorldEntity** — base class for all domain entities
- **Assembly Definitions** — 7 asmdef files enforcing layer boundaries

### Debug Infrastructure
- **GameDebug**, **DebugFilter**, **DebugFilterProfile**, **DebugExportUtility**
- **BuiltInSnapshotProviders** — 5 providers: World, Ships (segmentedRoutes/totalSegments/activeSegmented), Economy, Docking, SOI
- **DebugInvariantChecker** — 7 automated checks

### Orbital Sandbox
- Standard body hierarchy, Keplerian orbits, orbit rendering

### Patched-Conics Navigation (Phase 26 — COMPLETE)

**Segmented routes are the ONLY active navigation path.**

Data model (Phase 26a):
- **SegmentType** — enum: LocalOrbitDeparture, SOIExit, HeliocentricTransfer, SOIEntry, LocalTransfer, OrbitInsertion
- **RouteSegment** — one segment with reference body, timing, local positions
- **ShipRoute.Segments** — ordered list, CurrentSegmentIndex, UseSegmentedRoute
- **RouteSegmentBuilder** — builds segment chains (3 local, 5 interplanetary)

Movement execution (Phase 26b):
- **ShipMovementSystem.StartRoute()** builds segments via RouteSegmentBuilder
- **UpdateShipSegmented()** interpolates per-segment position each tick
- **CompleteArrival()** reads orbit params from last OrbitInsertion segment

Cleanup (Phase 26c):
- **Removed**: ReframeRoute(), ReframeRouteOutward(), UpdateShipLegacy(), StartInsertionPhaseLegacy(), UpdateInsertionPhaseLegacy(), CompleteArrivalLegacy(), DetermineRouteFrame(), BuildGlobalRoute(), BuildLocalRoute(), IsBodyRelatedToDestination(), _lastFrameSwitchTime dictionary, MinFrameSwitchInterval constant
- **HandleSOITransition()** — retained for coordinator compatibility but is now an empty no-op (debug/telemetry only, no route mutation)
- **OrbitInsertionDuration** — retained as public property (Inspector compatibility) but unused by segmented routes
- **ShipRoute legacy fields** (Frame, LocalFrameBodyId, StartLocalPosition, ArrivalLocalPosition, InsertionPhase* fields) — marked [DEPRECATED], retained for save/load compatibility
- **InsertingIntoOrbit ship state** — no longer entered by ShipMovementSystem; segmented routes handle insertion as their final segment
- StartRoute() now rejects routes if segment build fails (no legacy fallback)

### Route Safety (Step 21), Maneuver Planning (Step 23), Burn Windows (Step 25)
Unchanged.

------------------------------------------------------------------------

## Phase 26 Summary — What Was Removed vs Retained

### Removed (dead code)
| Item | Reason |
|---|---|
| ReframeRoute() | Replaced by per-segment reference frames |
| ReframeRouteOutward() | Replaced by per-segment reference frames |
| UpdateShipLegacy() | Replaced by UpdateShipSegmented() |
| StartInsertionPhaseLegacy() | Replaced by OrbitInsertion segment |
| UpdateInsertionPhaseLegacy() | Replaced by OrbitInsertion segment |
| CompleteArrivalLegacy() | Replaced by CompleteArrival() (reads segments) |
| DetermineRouteFrame() | Segments define frames at build time |
| BuildGlobalRoute() | Route is built by RouteSegmentBuilder |
| BuildLocalRoute() | Route is built by RouteSegmentBuilder |
| IsBodyRelatedToDestination() | Only used by reframe logic |
| _lastFrameSwitchTime dict | Anti-jitter for reframe — not needed |
| MinFrameSwitchInterval const | Anti-jitter for reframe — not needed |

### Retained (compatibility / future use)
| Item | Reason |
|---|---|
| HandleSOITransition() signature | Coordinator wiring — now empty no-op |
| OrbitInsertionDuration property | Inspector serialization — unused |
| ShipRoute.Frame, LocalFrameBodyId | Save/load compatibility |
| ShipRoute.InsertionPhase* fields | Save/load compatibility |
| ShipRoute.GetProgress() | Used by GetOverallSegmentProgress fallback |
| RouteFrame enum | Referenced by ShipRoute.Frame |
| SOIResolver | Debug, telemetry, snapshots |

------------------------------------------------------------------------

## Deferred Systems

| System | Notes |
|---|---|
| Delta-v / Energy Model (Phase 27) | Maneuver cost and route comparison |
| Hohmann Helper (Phase 28) | Optional baseline estimator |
| Advanced Transfers (Phase 29) | Lambert-lite, intercept, non-coplanar |
| Prices / Money | Currency, buy/sell prices |
| Save/Load | WorldRegistry serialization |
| Combat, Ship modules, Factions, Contracts | Future systems |

------------------------------------------------------------------------

## Architecture Health Notes

### Clean boundaries maintained
- All navigation code pure C# — no UnityEngine in Simulation
- NPCShipScheduler unchanged — StartRoute() signature preserved
- Coordinator still forwards SOI transitions — HandleSOITransition is safe no-op
- ShipMovementSystem reduced from ~1200 lines to ~530 lines

### Known technical debt
- PlannedManeuver.Score/DirectScore 0.0 from PlannedDepartureTime path
- ShipRoute deprecated fields should be removed when save/load is implemented
- InsertingIntoOrbit ShipState no longer entered — could be removed when safe
- HandleSOITransition is an empty method — coordinator call could be removed later

------------------------------------------------------------------------

## Next Recommended Development Phase

### Phase 27 — Delta-v / Energy Model
### Phase 28 — Hohmann Helper (Optional)
### Phase 29 — Advanced Transfers
