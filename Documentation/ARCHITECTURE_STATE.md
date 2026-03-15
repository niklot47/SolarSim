# ARCHITECTURE_STATE.md

Current snapshot of project implementation status.
Last updated after: Step 8 — Docking Foundation.

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
  - Resolution priority for ships: (1) Docked → station + port offset, (2) Override → travel/approach, (3) Orbital → parent chain
  - Orbital bodies: recursive parent chain + OrbitalPositionCalculator
  - Surface stations: parent position + lat/lon surface offset
  - Travelling/approaching ships: OverrideWorldPosition passthrough
  - Docked ships: station world position + port local offset (dynamic each tick)
  - Root bodies: position at origin
- OrbitalMapRenderer is a thin consumer — no simulation logic in Rendering

### Star System Data Loading
- **CelestialBodyDefinition / ShipDefinition / StationDefinition** — serializable Inspector-authored data
- **StarSystemDefinition** — ScriptableObject with body list + ship list + station list
- **StarSystemBuilder** — pure C# builder (5 phases: create, parents, register, ships, stations + docking ports for any station type)
- **StarSystemLoader** — Unity-side adapter, converts all definition types to build data (includes DockingPortCount)
- **SampleSystemAssetCreator** — editor menu to create sample asset (Sol system with 4 stations incl. Ares)
- **SampleStarSystemFactory** — hardcoded fallback (Sol, Terra, Ares, Venus, Luna + 3 ships + 4 stations)

### Stations Foundation
- Two station kinds: **Orbital** (AttachmentMode.Orbit) and **Surface** (AttachmentMode.Surface)
- **StationInfo** — station metadata: StationKind, SurfaceLatitudeDeg, SurfaceLongitudeDeg, DockingInfo
- **StationDefinition** — Inspector-authored station data (includes DockingPortCount)
- Orbital stations: orbit + optional spin, orbit lines, cube visual (cyan), docking ports
- Surface stations: fixed position on parent surface via lat/lon, no orbit line, cube visual (orange), docking ports
- Station cubes rendered at ⅓ scale for visual clarity
- Sample stations: Орбита-1 (Terra, 3 ports), Терра-1 (Terra surface, 2 ports), Фобос (Ares, 2 ports), Арес-1 (Ares surface, 2 ports)

### Docking Foundation
- **DockingPort** — single port model: PortId, LocalPosition (SimVec3), OccupiedShipId, Occupy/Release
- **DockingInfo** — port container on StationInfo: GeneratePorts, RequestPort, ReleasePort, GetPortForShip
- **DockingSystem** — pure C# simulation service in Simulation layer
  - Orbital docking: `RequestOrbitalDocking()` — ship on station orbit → approach in station-local space → dock
  - Surface docking: `RequestSurfaceDocking()` — ship on planet orbit → approach in planet-local space → dock at surface station
  - `Update(simTime, posResolver)` — interpolates approaching ships using DockingReferenceBodyId local frame
  - `Undock(shipId, simTime)` — orbital: orbit around station; surface: orbit around parent planet
  - `OnShipDocked` / `OnShipUndocked` events
- **ShipInfo docking fields**: DockedAtStationId, DockedPortId, DockingStartTime/Duration/StartPosition, DockingReferenceBodyId, DockedAtTime
- **ShipMovementSystem surface station handling**: when destination is surface station, arrival body = station's parent planet, orbit = default (r=3.0), OnShipArrived fires with original station id
- **NPCShipScheduler docking behavior**:
  - `_pendingSurfaceDock`: tracks ships arriving at planet whose destination was a surface station
  - `_pendingDeparture`: prevents undocked ships from immediately re-docking (anti-loop)
  - Orbital stations: auto-dock after 0.5s delay when ship orbits station
  - Surface stations: auto-dock after 0.5s delay via pending surface dock tracking
  - After DockingWaitTime: undock → immediate departure
- Default timing: 3 ports per orbital station, 2 ports per surface station, 0.15 Mm port offset, 2 sim-s approach, 5 sim-s docked wait
- Docked ships visible and selectable, position follows station via WorldPositionResolver

### Selection and Interaction
- **SelectionService** — pure C# selection state with events
- **SelectionBridge** — selection ring, highlight, camera focus
- **BodyClickHandler** — raycast click selection (UI Toolkit aware)

### UI Panels
- **ObjectListPanelController** — hierarchical body list, dynamic refresh on hierarchy changes
- **ObjectDetailsPanelController** — body properties, ship role/class/state/destination/SOI body/docking info (docked at, port), station kind/attachment/docking ports/occupancy, SOI radius for bodies
- **TimeControlsPanelController** — pause + x1/x10/x100
- **UIStrings** — Russian strings: body types, ship roles, ship states (incl. ApproachingStation/Docking/Docked), station kinds, attachment modes, SOI labels, docking labels (Пристыкован, Порт, Стыковочные порты, Занято, Сближение, Стыковка)
- **OrbitalSandboxScreen.uxml/.uss** — root layout with station/SOI/docking detail rows

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
- **ShipState**: Idle, Orbiting, Travelling, Arrived, ApproachingStation, Docking, Docked
- **RouteFrame**: Global, LocalParent — determines interpolation frame
- **ShipRoute**: origin/destination ids, departure time, duration, frame selection, pre-computed start/arrival positions (world and local), destination orbit parameters, arrival phase
- **ShipMovementSystem**: pure C# simulation, ticked by coordinator
  - Anchored departure from current orbital world position (no snap)
  - Surface station destination → arrival at parent body orbit (r=3.0, P=12.0)
  - Orbital station destination → arrival at small station orbit (r=0.5, P=8.0)
  - Two interpolation modes: Global frame and LocalParent frame
  - Phase-matched orbit assignment on arrival (no visible snap)
  - **OnShipArrived** event with original destination id (surface station id preserved)

### NPC Scheduling
- **NPCShipScheduler** — pure C# scheduler, role-based route selection
  - Trader/Civilian: random planet/moon/station destination
  - Patrol: ping-pong between two bodies
  - Player ships ignored
  - Distance-based travel duration
  - Automatic docking at orbital stations (ship orbits station → dock)
  - Automatic docking at surface stations (ship arrives at planet orbit → pending surface dock → dock)
  - Automatic undocking after DockingWaitTime with immediate departure (`_pendingDeparture`)
  - Configurable via Inspector: TravelSpeed, MinTravelDuration, IdleDelay, DockingWaitTime
  - Listens to ShipMovementSystem.OnShipArrived for surface station tracking

### SOI Foundation (Sphere of Influence)
- **CelestialBody.SOIRadius** — nullable double, defines sphere of influence in Mm
- **ShipInfo.CurrentSOIBodyId** — tracks which body's SOI currently dominates each ship
- **SOIResolver** — pure C# service, dominance rule: smallest SOI wins
- SOI transitions logged to Unity console
- Sample system SOI values: Sol=1000, Terra=60, Ares=40, Venus=30, Luna=8

### Bootstrap and Coordination
- **GameBootstrap** — creates clock, initializes debug, calls coordinator
- **OrbitalSandboxCoordinator** — loads system, creates and wires all services, ticks simulation in order: ShipMovementSystem → DockingSystem → NPCShipScheduler → SOIResolver, logs events, syncs Inspector params

------------------------------------------------------------------------

## Current Temporary Architectural Decisions

### Ships as CelestialBody + ShipInfo

**Why acceptable now:**
- Ships share capabilities with celestial bodies (hierarchy, orbits, selection, rendering)
- ShipInfo keeps ship-specific data contained including docking fields
- No code duplication — shared pipeline

**When to change:**
- If ships gain complex unique state (cargo, modules, AI trees, damage)

### Stations as CelestialBody + StationInfo

**Why acceptable now:**
- Stations share hierarchy, orbits, rendering, selection
- StationInfo + DockingInfo keeps station-specific data contained
- Both orbital and surface stations support docking

**When to change:**
- If stations gain complex state (cargo storage, production, refueling services)

### Transit parenting to root star

During travel, ships are temporarily parented to the root star for hierarchy visibility. Position comes from OverrideWorldPosition.

### Inspector serialization: float not double

NPC scheduling and docking parameters use `float` in SerializeField. Coordinator converts to double.

### SOI values are authored placeholders

Hand-picked for visual testing, not astrophysically accurate.

### Docking port positions are auto-generated

Ports evenly spaced in XZ plane at 0.15 Mm from station center.

------------------------------------------------------------------------

## Deferred Systems

| System | Notes |
|---|---|
| Economy | Resources, production, consumption, pricing |
| Cargo | Ship inventory, trade goods |
| Cargo transfer | Docking foundation ready to support it |
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
| Docking animations | Visual effects for approach/dock/undock |
| Station interiors | Interior view when docked |
| Fuel/repair | Station services requiring docking |

------------------------------------------------------------------------

## Architecture Health Notes

### Clean boundaries maintained
- Simulation and World layers have zero UnityEngine references (asmdef enforced)
- WorldPositionResolver is pure C# — single source of truth (incl. docked positions)
- DockingSystem is pure C# — uses position resolver delegate, no rendering dependency
- SOIResolver is pure C# — uses WorldPositionResolver
- ShipMovementSystem is pure C# — surface station logic is internal to simulation
- NPCShipScheduler is pure C# with DockingSystem and position resolver injected
- OrbitalMapRenderer contains zero simulation logic
- All rendering in Rendering layer, all UI in UI layer

### Known technical debt
- SampleStarSystemFactory uses Russian display names (should use localization keys)
- Unity 6 EntityId conflict requires using-alias in Rendering/UI files
- OnGUI labels — acceptable for MVP
- Ship travel is linear interpolation (not physically realistic)
- Docking approach is linear interpolation in local space (no curved paths)
- Transit parenting is a UI convenience hack
- ObjectListPanelController.Refresh() rebuilds entire list (fine at current scale)
- SOI values are placeholder approximations
- Surface station position is fixed relative to parent (no spin coupling)
- Docking port positions are auto-generated (not authored)
- Docked ship visual is just positioned at port — no special docking visual feedback

------------------------------------------------------------------------

## Next Recommended Development Phase

**Option A: Economy Foundation** — resource types, production, cargo. Opens core gameplay. Docking is ready for cargo transfer.

**Option B: SOI-Aware Navigation** — use SOI data to improve route frame selection, capture/escape transitions.

**Option C: More Ship Types / Civilian Traffic** — more ships, civilian behavior, busier sandbox.

**Option D: Cargo & Trade** — ship inventory, station inventory, buy/sell at docked stations. Natural extension of docking.

Recommendation: **Option D** — cargo and trade is the natural next step. Ships dock at stations; adding cargo transfer creates the first economic interaction loop.
