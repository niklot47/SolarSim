# ARCHITECTURE_STATE.md

Current snapshot of project implementation status.
Last updated after: Phase 26d — Smooth Orbit Insertion (Bézier curve for OrbitInsertion segments).

------------------------------------------------------------------------

## Currently Implemented Systems

(All systems from Phase 26c remain unchanged. Only additions listed below.)

### Smooth Orbit Insertion (Phase 26d) — NEW

**Problem solved**: Ships used to "snap" into orbit with a sharp ~180° direction change
at the end of the OrbitInsertion segment, because linear interpolation between the
approach point and orbit point doesn't respect orbital tangent direction.

**Solution**: Quadratic Bézier curve interpolation for OrbitInsertion segments.

#### RouteSegment additions:
- `InsertionControlPoint` (SimVec3) — Bézier control point in local coords
- `UseCurvedInsertion` (bool) — enables Bézier interpolation for this segment
- `OrbitTangentAtEnd` (SimVec3) — orbit tangent at insertion point (debug/validation)

#### RouteSegmentBuilder.ConfigureInsertionCurve():
- Called after segment list is built
- Computes orbit tangent at the insertion endpoint (perpendicular to radial in XZ plane)
- Chooses prograde direction based on approach side (CW vs CCW)
- Places control point: C = P1 - k * orbitTangent (k = orbit radius)
- This guarantees: tangent at t=1 is parallel to orbit motion

#### ShipMovementSystem.InterpolateInsertionCurve():
- B(t) = (1-t)²·P0 + 2(1-t)t·C + t²·P1
- Uses smoothstep easing: t' = t²(3-2t) for natural deceleration
- Ship sweeps in a smooth arc arriving tangent to the orbit
- Zero allocations per tick

#### Visual result:
- Ship approaches along a gentle curve
- Arrives aligned with orbit direction
- No visible snap or teleport
- Seamless transition to circular orbit

------------------------------------------------------------------------

## Architecture Health Notes

- All new code pure C# — no UnityEngine dependency
- No changes to StartRoute() signature — NPCShipScheduler compatible
- No changes to CompleteArrival() logic — orbit phase matching unchanged
- Bézier computation is 6 multiplications + 3 additions per tick — negligible cost
- ConfigureInsertionCurve runs once per route build, not per tick

------------------------------------------------------------------------

## Next Recommended Development Phase

### Phase 27 — Delta-v / Energy Model
### Phase 28 — Hohmann Helper (Optional)
### Phase 29 — Advanced Transfers
