# ARCHITECTURE_STATE.md

Current snapshot of project implementation status.
Last updated after: Step 4 — Anchored Travel + Local-Frame Routes + NPC Scheduling.

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

### Ship Movement (Anchored Travel Model)
- **ShipState**: Idle, Orbiting, Travelling, Arrived
- **RouteFrame**: Global, LocalParent — determines interpolation frame
- **ShipRoute**: origin/destination ids, departure time, duration, frame selection, pre-computed start/arrival positions (world and local), destination orbit parameters, arrival phase
- **ShipMovementSystem**: pure C# simulation, ticked by coordinator
  - Anchored departure: ship leaves from current orbital world position (no snap to body center)
  - Predictive arrival: arrival point computed at predicted future destination position + orbital offset on near side
  - Two interpolation modes:
    - **Global frame**: lerp between fixed world positions (interplanetary travel)
    - **LocalParent frame**: lerp in local coordinates relative to dominant parent, converted to world each tick (local transfers like Planet↔Moon)
  - Frame auto-selection: Star children use Global, Planet children use LocalParent
  - Phase-matched orbit assignment on arrival (no visible snap)
  - **OnShipArrived** event for UI refresh

### NPC Scheduling
- **NPCShipScheduler** — pure C# scheduler, role-based route selection
  - Trader/Civilian: random planet/moon destination
  - Patrol: ping-pong between two bodies
  - Player ships ignored
  - Distance-based travel duration: `duration = distance / travelSpeed`
  - Local-frame distance computation for local routes
  - Configurable via Inspector: TravelSpeed, MinTravelDuration, IdleDelay
  - Live tuning: coordinator syncs Inspector values every frame
  - Position resolver for anchored travel computation
  - Ticked by OrbitalSandboxCoordinator after ShipMovementSystem

### Bootstrap and Coordination
- **GameBootstrap** — creates clock, initializes debug, calls coordinator
- **OrbitalSandboxCoordinator** — loads system, creates ShipMovementSystem + NPCShipScheduler, wires everything, manages transit parenting and UI refresh, syncs NPC parameters from Inspector

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

During travel, ships are temporarily parented to the system's root star so they remain visible in the hierarchical object list. The actual position comes from ShipMovementSystem via OverrideWorldPosition, not from orbital calculation. On arrival, transit parent is cleaned up.

### Inspector serialization: float not double

NPC scheduling parameters (travelSpeed, minTravelDuration, idleDelay) use `float` in SerializeField because Unity Inspector does not serialize `double`. Coordinator converts to double when passing to pure C# systems.

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
- NPCShipScheduler is pure C# with position resolver injected via delegate
- All rendering in Rendering layer, all UI in UI layer

### Known technical debt
- SampleStarSystemFactory uses Russian display names (should use localization keys)
- Unity 6 EntityId conflict requires using-alias in Rendering/UI files
- OnGUI labels — acceptable for MVP
- Ship travel is linear interpolation (not physically realistic, but visually correct)
- Transit parenting is a UI convenience hack
- ObjectListPanelController.Refresh() rebuilds entire list (fine at current scale)

------------------------------------------------------------------------

## Next Recommended Development Phase

**Option A: Stations Foundation** — introduce stations as orbital structures distinct from planets. Enables future trade/logistics. Ships can travel to stations.

**Option B: Economy Foundation** — resource types, production, cargo. Larger step but opens core gameplay.

**Option C: More Ship Types / Civilian Traffic** — add more ships, civilian role behavior, make the sandbox feel busier.

Recommendation: **Option A** — stations are the next natural docking/trade target and validate the movement system with a new entity type.
