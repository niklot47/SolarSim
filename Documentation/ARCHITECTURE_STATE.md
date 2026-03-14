# ARCHITECTURE_STATE.md

Current snapshot of project implementation status.
Last updated after: Step 7 — SOI Foundation.

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
- **CelestialBody** — domain entity with orbital/spin data, parent-child hierarchy, ShipInfo, StationInfo, SOIRadius
- **StarSystem** — container for body ids
- **WorldRegistry** — central entity lookup
- **OrbitalPositionCalculator** — circular orbit positions in XZ plane (MVP) + surface position from lat/lon
- **OrbitalMapRenderer** — scene visuals (spheres for bodies, cubes for stations), orbit lines, delegates position resolution to WorldPositionResolver
- **CelestialBodyView** — visual binding with role-based ship colors, station kind colors, station scale ×⅓

### World Position Resolution
- **WorldPositionResolver** — single source of truth for body world positions (Simulation layer, pure C#)
  - Orbital bodies: recursive parent chain + OrbitalPositionCalculator
  - Surface stations: parent position + lat/lon surface offset
  - Travelling ships: OverrideWorldPosition passthrough
  - Root bodies: position at origin
- OrbitalMapRenderer is a thin consumer — no simulation logic in Rendering

### Star System Data Loading
- **CelestialBodyDefinition / ShipDefinition / StationDefinition** — serializable Inspector-authored data
- **StarSystemDefinition** — ScriptableObject with body list + ship list + station list
- **StarSystemBuilder** — pure C# builder (5 phases: create, parents, register, ships, stations)
- **StarSystemLoader** — Unity-side adapter, converts all definition types to build data
- **SampleSystemAssetCreator** — editor menu to create sample asset (with ships + stations + SOI values)
- **SampleStarSystemFactory** — hardcoded fallback (Sol, Terra, Ares, Venus, Luna + 3 ships + 2 stations + SOI)

### Stations Foundation
- Two station kinds: **Orbital** (AttachmentMode.Orbit) and **Surface** (AttachmentMode.Surface)
- **StationInfo** — station metadata: StationKind, SurfaceLatitudeDeg, SurfaceLongitudeDeg
- **StationDefinition** — Inspector-authored station data
- Orbital stations: orbit + optional spin, orbit lines, cube visual (cyan)
- Surface stations: fixed position on parent surface via lat/lon, no orbit line, cube visual (orange)
- Station cubes rendered at ⅓ scale for visual clarity
- Ships can travel to stations; on arrival, enter orbit around station (no docking)
- ShipMovementSystem uses small station orbit (radius=0.5, period=8) for station destinations
- NPCShipScheduler includes stations as valid NPC destinations

### Selection and Interaction
- **SelectionService** — pure C# selection state with events
- **SelectionBridge** — selection ring, highlight, camera focus
- **BodyClickHandler** — raycast click selection (UI Toolkit aware)

### UI Panels
- **ObjectListPanelController** — hierarchical body list, dynamic refresh on hierarchy changes
- **ObjectDetailsPanelController** — body properties, ship role/class/state/destination/SOI body, station kind/attachment, SOI radius for bodies
- **TimeControlsPanelController** — pause + x1/x10/x100
- **UIStrings** — Russian strings: body types, ship roles, ship states, station kinds, attachment modes, SOI labels
- **OrbitalSandboxScreen.uxml/.uss** — root layout with station and SOI detail rows

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
  - Extended frame logic for station destinations (Cases 4-5)
  - Phase-matched orbit assignment on arrival (no visible snap)
  - Station arrivals use small orbit (radius=0.5, period=8)
  - **OnShipArrived** event for UI refresh

### NPC Scheduling
- **NPCShipScheduler** — pure C# scheduler, role-based route selection
  - Trader/Civilian: random planet/moon/station destination
  - Patrol: ping-pong between two bodies
  - Player ships ignored
  - Distance-based travel duration: `duration = distance / travelSpeed`
  - Local-frame distance computation for local routes
  - Configurable via Inspector: TravelSpeed, MinTravelDuration, IdleDelay
  - Live tuning: coordinator syncs Inspector values every frame
  - Position resolver delegates to WorldPositionResolver
  - Ticked by OrbitalSandboxCoordinator after ShipMovementSystem

### SOI Foundation (Sphere of Influence)
- **CelestialBody.SOIRadius** — nullable double, defines sphere of influence in Mm
- **ShipInfo.CurrentSOIBodyId** — tracks which body's SOI currently dominates each ship
- **SOIResolver** — pure C# service in Simulation layer
  - `IsInsideSOI(bodyId, worldPosition, simTime)` — point containment check
  - `ResolveDominantBody(worldPosition, simTime)` — find deepest containing SOI body
  - `ResolveDominantBodyForShip(ship, simTime)` — resolve for ship using WorldPositionResolver
  - `UpdateAllShips(simTime)` — batch update all ships, returns list of SOITransition
  - Dominance rule: smallest SOI radius wins (deepest/most specific body)
  - Cached body list with invalidation
- **SOITransition** — struct recording ship SOI changes (previous/new body, sim time)
- SOI transitions logged to Unity console by coordinator
- UI details panel shows dominant SOI body for ships, SOI radius for celestial bodies
- Sample system SOI values: Sol=1000, Terra=60, Ares=40, Venus=30, Luna=8

### Bootstrap and Coordination
- **GameBootstrap** — creates clock, initializes debug, calls coordinator
- **OrbitalSandboxCoordinator** — loads system, creates WorldPositionResolver + SOIResolver + ShipMovementSystem + NPCShipScheduler, wires everything, manages transit parenting, ticks SOI updates, logs SOI transitions, syncs NPC parameters from Inspector

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

### Stations as CelestialBody + StationInfo

**Why acceptable now:**
- Stations share hierarchy, orbits, rendering, selection with celestial bodies
- StationInfo keeps station-specific data contained
- Surface/orbital distinction via AttachmentMode + StationKind

**When to change:**
- If stations gain complex state (docking ports, cargo storage, production)

### Transit parenting to root star

During travel, ships are temporarily parented to the system's root star so they remain visible in the hierarchical object list. The actual position comes from ShipMovementSystem via OverrideWorldPosition, not from orbital calculation. On arrival, transit parent is cleaned up.

### Inspector serialization: float not double

NPC scheduling parameters (travelSpeed, minTravelDuration, idleDelay) use `float` in SerializeField because Unity Inspector does not serialize `double`. Coordinator converts to double when passing to pure C# systems.

### SOI values are authored placeholders

Current SOI radii are hand-picked for visual testing, not astrophysically accurate. Future work may compute SOI from mass ratios or orbital parameters.

------------------------------------------------------------------------

## Deferred Systems

| System | Notes |
|---|---|
| Docking | Requires proximity + station SOI interaction |
| Economy | Resources, production, consumption, pricing |
| Cargo | Ship inventory, trade goods |
| Contracts | Task definition, assignment |
| Factions | Entities, relationships, territory |
| AI behavior | Autonomous ship decisions |
| Advanced navigation | Pathfinding, waypoints, SOI-aware routing |
| Orbital transfers | Hohmann, delta-v, patched conics (SOI foundation ready) |
| Combat | Weapons, damage |
| Ship modules | Equipment slots |
| Save/Load | WorldRegistry serialization |
| Procedural planets | PPG Lite integration (documented in PDF) |
| Elliptical orbits | Kepler equation solver |
| Inclined orbits | 3D orbit planes |
| LOD system | CelestialBodyView.SetRepresentationMode() stub exists |
| SOI visualization | Debug gizmo/wireframe spheres for SOI boundaries |
| SOI-based reparenting | Automatic orbit conversion on SOI crossing |

------------------------------------------------------------------------

## Architecture Health Notes

### Clean boundaries maintained
- Simulation and World layers have zero UnityEngine references (asmdef enforced)
- WorldPositionResolver is pure C# in Simulation — single source of truth for positions
- SOIResolver is pure C# in Simulation — uses WorldPositionResolver, no rendering dependency
- ShipMovementSystem is pure C# despite coordinating with rendering via delegate
- NPCShipScheduler is pure C# with position resolver injected via delegate
- OrbitalMapRenderer contains zero simulation logic — only consumes resolved positions
- All rendering in Rendering layer, all UI in UI layer

### Known technical debt
- SampleStarSystemFactory uses Russian display names (should use localization keys)
- Unity 6 EntityId conflict requires using-alias in Rendering/UI files
- OnGUI labels — acceptable for MVP
- Ship travel is linear interpolation (not physically realistic, but visually correct)
- Transit parenting is a UI convenience hack
- ObjectListPanelController.Refresh() rebuilds entire list (fine at current scale)
- SOI values are placeholder approximations
- Surface station position is fixed relative to parent (no spin coupling)

------------------------------------------------------------------------

## Next Recommended Development Phase

**Option A: Docking Foundation** — ships can dock at orbital stations. Requires proximity detection, docking state, basic docking port concept.

**Option B: Economy Foundation** — resource types, production, cargo. Larger step but opens core gameplay.

**Option C: SOI-Aware Navigation** — use SOI data to improve route frame selection, add capture/escape transitions, prepare for patched conics.

**Option D: More Ship Types / Civilian Traffic** — add more ships, civilian role behavior, make the sandbox feel busier.

Recommendation: **Option A** — docking is the natural next step after stations + SOI. Ships already arrive at stations into orbit; docking adds the first meaningful station interaction.
