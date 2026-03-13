# ARCHITECTURE_STATE.md

Current snapshot of project implementation status.
Last updated after: Ship Movement System + documentation update step.

------------------------------------------------------------------------

## Currently Implemented Systems

### Project Foundation
- **EntityId** — immutable ulong identifier with thread-safe generation
- **SimulationClock** — pure C# time manager (pause/resume/timeScale)
- **WorldEntity** — base class for all domain entities
- **Assembly Definitions** — 7 asmdef files enforcing layer boundaries

### Debug Infrastructure
- **GameDebug** — static API: Log, CaptureSnapshot, ExportBundle, GetStatus
- **DebugEvent / RingBuffer** — bounded structured event storage (1000 events, 200 errors)
- Context menu actions on GameBootstrap for editor debugging

### Orbital Sandbox
- **CelestialBody** — domain entity with orbital/spin data, parent-child hierarchy, ShipInfo
- **StarSystem** — container for body ids
- **WorldRegistry** — central entity lookup
- **OrbitalPositionCalculator** — circular orbit positions in XZ plane (MVP)
- **OrbitalMapRenderer** — sphere primitives, orbit line renderers (local space), travelling ship support
- **CelestialBodyView** — visual binding with role-based ship colors

### Star System Data Loading
- **CelestialBodyDefinition / ShipDefinition** — serializable Inspector-authored data
- **StarSystemDefinition** — ScriptableObject with body list + ship list
- **StarSystemBuilder** — pure C# builder (4 phases: create, parents, register, ships)
- **StarSystemLoader** — Unity-side adapter
- **SampleSystemAssetCreator** — editor menu to create sample asset
- **SampleStarSystemFactory** — hardcoded fallback (Sol, Terra, Ares, Luna + 3 ships)

### Selection and Interaction
- **SelectionService** — pure C# selection state with events
- **SelectionBridge** — selection ring, highlight, camera focus
- **BodyClickHandler** — raycast click selection (UI Toolkit aware)

### UI Panels
- **ObjectListPanelController** — hierarchical body list, dynamic refresh on hierarchy changes
- **ObjectDetailsPanelController** — body properties + ship role/class/state/destination
- **TimeControlsPanelController** — pause + x1/x10/x100
- **UIStrings** — Russian strings: body types, ship roles, ship states
- **OrbitalSandboxScreen.uxml/.uss** — root layout

### Camera and Labels
- **OrbitalCameraController** — pan/zoom/rotate/smooth focus (Input System)
- **BodyLabelController** — IMGUI labels with zoom fade

### World Units and Scaling
- **WorldUnits** — Mm distances, sim-s time
- **SceneScaleConfig** — DistanceScale, BodyRadiusScale, MinBodyDiameter

### Ships Foundation
- CelestialBody with BodyType.Ship + ShipInfo
- **ShipRole**: Player, Trader, Patrol, Civilian
- Visual differentiation by role color
- Full integration: selection, labels, details panel, camera focus

### Ship Movement
- **ShipState**: Idle, Orbiting, Travelling, Arrived
- **ShipRoute**: origin/destination ids, departure time, duration
- **ShipMovementSystem**: pure C# simulation, ticked by coordinator
- Linear interpolation during travel, OverrideWorldPosition for renderer
- Departure: detach from parent, parent to root star for list visibility
- Arrival: re-parent to destination, new orbit, cleanup transit parent, refresh list
- **OnShipArrived** event for UI refresh
- Sample route: Карго-7 auto-flies Terra → Ares (60 sim-s, starts at 2 sim-s)

### Bootstrap and Coordination
- **GameBootstrap** — creates clock, initializes debug, calls coordinator
- **OrbitalSandboxCoordinator** — loads system, creates ShipMovementSystem, wires everything, manages transit parenting and UI refresh

------------------------------------------------------------------------

## Current Temporary Architectural Decisions

### Ships as CelestialBody + ShipInfo

**Why acceptable now:**
- Ships need the same capabilities as celestial bodies (hierarchy, orbits, selection, rendering)
- ShipInfo keeps ship-specific data contained as a clean attachment
- No code duplication — shared rendering/selection/UI pipeline

**When to change:**
- If ships gain complex unique state (cargo, modules, AI trees, damage)
- If ShipInfo grows beyond a clean data attachment

**How to change:**
- Extract Ship class (extending WorldEntity or CelestialBody)
- Move ShipInfo fields into Ship directly
- Add WorldRegistry.GetShip() accessor
- Straightforward refactor, no architectural breakage

### Transit parenting to root star

During travel, ships are temporarily parented to the system's root star so they remain visible in the hierarchical object list. This is a UI convenience hack. The actual position comes from ShipMovementSystem via OverrideWorldPosition, not from orbital calculation. On arrival, transit parent is cleaned up.

------------------------------------------------------------------------

## Deferred Systems

| System | Notes |
|---|---|
| Stations | CelestialBodyType.Station exists in enum |
| Docking | Requires stations + proximity |
| Economy | Resources, production, consumption, pricing |
| Cargo | Ship inventory, trade goods |
| Contracts | Task definition, assignment |
| Factions | Entities, relationships, territory |
| AI behavior | Autonomous ship decisions |
| Advanced navigation | Pathfinding, waypoints |
| Orbital transfers | Hohmann, delta-v |
| Combat | Weapons, damage |
| Ship modules | Equipment slots |
| Save/Load | WorldRegistry serialization |
| Procedural planets | PPG Lite integration (documented in PDF) |
| Elliptical orbits | Kepler equation solver |
| Inclined orbits | 3D orbit planes |
| LOD system | CelestialBodyView.SetRepresentationMode() stub exists |

------------------------------------------------------------------------

## Architecture Health Notes

### Clean boundaries maintained
- Simulation and World layers have zero UnityEngine references (asmdef enforced)
- ShipMovementSystem is pure C# despite coordinating with rendering via delegate
- All rendering in Rendering layer, all UI in UI layer

### Known technical debt
- SampleStarSystemFactory uses Russian display names (should use localization keys)
- Unity 6 EntityId conflict requires using-alias in Rendering/UI files
- OnGUI labels — acceptable for MVP
- Ship travel is linear interpolation (not physically realistic)
- Transit parenting is a UI convenience hack
- ObjectListPanelController.Refresh() rebuilds entire list (fine at current scale)

------------------------------------------------------------------------

## Next Recommended Development Phase

**Option A: Route Loop / NPC Scheduling** — extend ShipMovementSystem with multi-leg routes (Terra → Ares → Terra → repeat). Makes sandbox feel alive with continuous traffic. Builds directly on existing movement system.

**Option B: Stations Foundation** — introduce stations as orbital structures distinct from planets. Enables future trade/logistics.

**Option C: Economy Foundation** — resource types, production, cargo. Larger step but opens core gameplay.

Recommendation: **Option A** — minimal scope, maximum visual impact, validates sustained movement pipeline.
