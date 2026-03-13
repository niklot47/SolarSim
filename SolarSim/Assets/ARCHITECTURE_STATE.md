# ARCHITECTURE_STATE.md

Current snapshot of project implementation status.
Last updated after: Star System Data Loading step.

------------------------------------------------------------------------

## Currently Implemented Systems

### Project Foundation
- **EntityId** — immutable ulong identifier with thread-safe generation
- **SimulationClock** — pure C# time manager (pause/resume/timeScale)
- **WorldEntity** — base class for all domain entities
- **Assembly Definitions** — 7 asmdef files enforcing layer boundaries

### Debug Infrastructure
- **GameDebug** — static API with structured event logging
- **DebugEvent** / **RingBuffer** — bounded event storage
- **Snapshot/Export** — placeholder methods ready for full implementation

### Orbital Sandbox
- **CelestialBody** — domain entity with full orbital/spin data, parent-child hierarchy
- **StarSystem** — container for body ids
- **WorldRegistry** — central entity lookup service
- **OrbitalPositionCalculator** — circular orbit positions in XZ plane (MVP)
- **OrbitalMapRenderer** — creates sphere primitives and orbit line renderers
- **CelestialBodyView** — MonoBehaviour visual binding

### Star System Data Loading
- **CelestialBodyDefinition** — serializable Inspector-authored body data
- **StarSystemDefinition** — ScriptableObject asset for star system authoring
- **StarSystemBuilder** — pure C# builder: definition data → runtime entities
- **StarSystemLoader** — Unity-side adapter: ScriptableObject → build data → builder
- **SampleSystemAssetCreator** — editor menu to create sample asset
- **SampleStarSystemFactory** — hardcoded fallback if no asset assigned

### Selection and Interaction
- **SelectionService** — pure C# selection state with events
- **SelectionBridge** — renders selection ring, triggers highlight and camera focus
- **BodyClickHandler** — raycast click selection with UI Toolkit awareness
- **ObjectListPanelController** — bidirectional selection sync with ListView

### UI Panels
- **ObjectListPanelController** — hierarchical body list (DFS order, indentation by depth)
- **ObjectDetailsPanelController** — selected body properties display
- **TimeControlsPanelController** — pause/resume + x1/x10/x100 speed buttons
- **UIStrings** — centralized localization-ready string provider (Russian)
- **OrbitalSandboxScreen.uxml/.uss** — root layout with left panel, viewport, right panel, time strip

### Camera
- **OrbitalCameraController** — pan (MMB), zoom (scroll), rotate (RMB), smooth focus
- **CameraFocusTarget** — marks objects as focusable
- Uses new Input System package

### Body Labels
- **BodyLabelController** — IMGUI screen-space labels above bodies
- Zoom-based fade: visible under 90 units, hidden beyond 120 units

### World Units and Scaling
- **WorldUnits** — unit policy: Mm for distances, sim-s for time
- **SceneScaleConfig** — ScriptableObject with DistanceScale, BodyRadiusScale, MinBodyDiameter
- Conversion in OrbitalMapRenderer and CelestialBodyView

### Bootstrap and Coordination
- **GameBootstrap** — creates clock, initializes debug, calls coordinator
- **OrbitalSandboxCoordinator** — loads star system (asset or fallback), wires renderer/selection/UI

------------------------------------------------------------------------

## Deferred Systems

### Ships
- Ship as a runtime entity (CelestialBodyType.Ship exists in enum)
- Ship definition in data assets
- Ship spawning and orbital attachment
- Ship movement and navigation

### Stations
- Station as a runtime entity (CelestialBodyType.Station exists in enum)
- Station definition in data assets
- Orbital and surface attachment

### Economy
- Resources and commodities
- Production and consumption
- Trade routes and pricing

### Factions and Politics
- Faction entities and relationships
- Territory and influence
- Diplomacy mechanics

### Contracts
- Contract definition and lifecycle
- Task assignment to ships/NPCs

### Route Navigation
- Pathfinding between bodies
- Transfer orbit calculation
- Waypoint system

### NPC Traffic
- NPC ship spawning
- Autonomous task execution
- Traffic patterns

### Procedural Planets
- Integration with Procedural Planet Generation Lite
- Planet spec pipeline (documented in project PDF)
- Material and prefab generation

### Save/Load
- Serialization of WorldRegistry state
- Session persistence

### Advanced Orbital Mechanics
- Elliptical orbit solver (Kepler equation)
- Inclination-based rendered orbit planes
- Orbital transfer calculations

### LOD System
- CelestialBodyView.SetRepresentationMode() stub exists
- Future: mesh/material swap by camera distance
- Future: procedural planet detail levels

------------------------------------------------------------------------

## Next Development Phase: Ships Foundation

### Goal
Introduce ships as world entities that can exist in the orbital sandbox.

### Expected scope
- Ship runtime entity (extending WorldEntity or CelestialBody)
- Ship definition in data assets
- Ship spawning at a body's orbit
- Ship visual representation (simple placeholder)
- Ship appearing in object list and selection system
- Ship position update (stationary or simple orbital)

### Prerequisites (all met)
- Parent-child hierarchy supports Ship body type
- WorldRegistry can hold any WorldEntity subclass
- Data definition pipeline is in place
- Rendering pipeline creates views from registry data
- Selection and UI work with any CelestialBody

### What ships should NOT include yet
- Autonomous navigation
- Economy interaction
- Combat
- Docking
- NPC behavior
- Route planning

------------------------------------------------------------------------

## Architecture Health Notes

### Clean boundaries maintained
- Simulation layer has zero UnityEngine references (enforced by asmdef)
- World layer has zero UnityEngine references (enforced by asmdef)
- All rendering logic is in Rendering layer
- All UI logic is in UI layer
- Data definitions are in Data layer

### Known technical debt
- `SampleStarSystemFactory` uses Russian display names — should use localization keys
- `StarSystem.DisplayName` in sample factory is Russian — cosmetic only
- Unity 6 `EntityId` conflict requires `using EntityId = SpaceSim.Shared.Identifiers.EntityId;` alias in all Rendering and UI files
- `OnGUI` labels (BodyLabelController) — acceptable for MVP, may migrate to UI Toolkit overlay later
- SceneScaleConfig changes require Play Mode restart — acceptable for authoring workflow
