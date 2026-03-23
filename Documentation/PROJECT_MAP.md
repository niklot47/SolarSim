# PROJECT_MAP.md

## Project Overview

Modular sandbox space simulation built in Unity LTS using URP and UI Toolkit.

Long-term goals: orbital simulation sandbox, ships and stations, NPC traffic and tasks, economy and trade, faction and clan politics, maintainable architecture for humans and AI assistants.

------------------------------------------------------------------------

## Architecture Layers

### 1. Simulation

Key files (navigation-related, updated in Phase 26d):
- `Scripts/Simulation/Ships/ShipMovementSystem.cs` — segmented navigation with **Bézier curve orbit insertion**
- `Scripts/Simulation/Ships/RouteSegmentBuilder.cs` — builds segments + **configures insertion curve**
- `Scripts/Simulation/Ships/ManeuverPlanner.cs` — multi-window maneuver planning
- `Scripts/Simulation/Ships/NPCShipScheduler.cs` — demand-driven trader routing
- `Scripts/Simulation/Ships/RouteSafetyChecker.cs` — route collision validation

### 2. World

Key files (updated in Phase 26d):
- `Scripts/World/Entities/RouteSegment.cs` — **added InsertionControlPoint, UseCurvedInsertion, OrbitTangentAtEnd**
- `Scripts/World/Entities/SegmentType.cs` — unchanged
- `Scripts/World/Entities/ShipRoute.cs` — unchanged from 26c

(All other layers unchanged from Phase 26c. See previous PROJECT_MAP for full listing.)

------------------------------------------------------------------------

## Strategic Direction — Road to Full Physics

Completed:
1. Impact / Collision Check Foundation ✓ (Phase 21)
2. Transfer Planning Lite ✓ (Phase 22)
3. Maneuver Planning Foundation ✓ (Phase 23)
4. Debug Log Filter System ✓ (Step 24)
5. Burn Windows / Persistent Plans ✓ (Phase 25)
6. Patched Conics Full ✓ (Phase 26a/b/c)
7. Smooth Orbit Insertion ✓ (Phase 26d)

### Phase 27 — Delta-v / Energy Model
### Phase 28 — Hohmann Helper (Optional)
### Phase 29 — Advanced Transfers (Lambert-lite / Intercept)
