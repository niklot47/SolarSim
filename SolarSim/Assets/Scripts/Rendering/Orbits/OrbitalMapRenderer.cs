using System.Collections.Generic;
using UnityEngine;
using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;
using SpaceSim.Simulation.Core;
using SpaceSim.Simulation.Orbits;
using SpaceSim.Simulation.Time;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;
using SpaceSim.Data.Config;
using SpaceSim.Rendering.Cameras;

using EntityId = SpaceSim.Shared.Identifiers.EntityId;

namespace SpaceSim.Rendering.Orbits
{
    public class OrbitalMapRenderer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Material defaultBodyMaterial;
        [SerializeField] private SceneScaleConfig scaleConfig;

        [Header("Orbit Lines")]
        [Tooltip("Material for orbit lines. If null, uses Sprites/Default with white color.")]
        [SerializeField] private Material orbitLineMaterial;

        [Tooltip("Base width of orbit lines at closest zoom.")]
        [SerializeField] private float orbitLineBaseWidth = 0.01f;

        [Tooltip("Maximum width multiplier at farthest zoom.")]
        [Min(1f)]
        [SerializeField] private float orbitLineMaxMultiplier = 80f;

        [Tooltip("Gaussian curve sharpness. Low = thin most of range then steep ramp. High = gradual growth.")]
        [Range(1f, 6f)]
        [SerializeField] private float orbitLineCurveSigma = 2.5f;

        [Header("Orbit Line Quality")]
        [Tooltip("Use adaptive subdivision (curvature-based). If false, uses fixed uniform segments.")]
        [SerializeField] private bool adaptiveOrbitLines = true;

        [Tooltip("Adaptive mode: max allowed chord-to-curve deviation in world units (Mm). Lower = smoother.")]
        [Range(0.001f, 1.0f)]
        [SerializeField] private float orbitLineTolerance = 0.05f;

        [Tooltip("Adaptive mode: max recursion depth per seed segment. Higher = finer detail.")]
        [Range(1, 10)]
        [SerializeField] private int orbitLineMaxDepth = 7;

        [Tooltip("Adaptive mode: initial evenly-spaced seed segments before subdivision.")]
        [Range(4, 64)]
        [SerializeField] private int orbitLineSeedSegments = 16;

        [Tooltip("Uniform fallback mode: fixed number of orbit line segments.")]
        [Range(32, 512)]
        [SerializeField] private int orbitLineFixedSegments = 128;

        private WorldRegistry _registry;
        private StarSystem _system;
        private SimulationClock _clock;
        private WorldPositionResolver _positionResolver;
        private OrbitalCameraController _cameraController;

        private readonly Dictionary<EntityId, Planets.CelestialBodyView> _views =
            new Dictionary<EntityId, Planets.CelestialBodyView>();
        private readonly Dictionary<EntityId, LineRenderer> _orbitLines =
            new Dictionary<EntityId, LineRenderer>();

        private static readonly Color DefaultOrbitLineColor = new Color(1f, 1f, 1f, 0.2f);

        public void Initialize(WorldRegistry registry, StarSystem system, SimulationClock clock,
            WorldPositionResolver positionResolver)
        {
            _registry = registry;
            _system = system;
            _clock = clock;
            _positionResolver = positionResolver;
        }

        /// <summary>
        /// Set the camera controller reference so orbit line thickness
        /// can read actual zoom level (CurrentDistance) instead of guessing
        /// from camera world position.
        /// </summary>
        public void SetCameraController(OrbitalCameraController cam)
        {
            _cameraController = cam;
        }

        public void BuildSceneObjects()
        {
            if (_registry == null || _system == null) return;
            foreach (var bodyId in _system.AllBodyIds)
            {
                var body = _registry.GetCelestialBody(bodyId);
                if (body == null) continue;
                CreateBodyView(body);
                if (body.Orbit != null && body.AttachmentMode == AttachmentMode.Orbit)
                    CreateOrbitLine(body);
            }
        }

        public Planets.CelestialBodyView GetView(EntityId id)
        {
            _views.TryGetValue(id, out var view);
            return view;
        }

        public SceneScaleConfig ScaleConfig => scaleConfig;

        public SimVec3 ResolveWorldPosition(EntityId bodyId, double simTime)
        {
            if (_positionResolver == null) return SimVec3.Zero;
            return _positionResolver.Resolve(bodyId, simTime);
        }

        private void Update()
        {
            if (_registry == null || _system == null || _clock == null || _positionResolver == null) return;
            double simTime = _clock.CurrentTime;

            foreach (var bodyId in _system.AllBodyIds)
            {
                var body = _registry.GetCelestialBody(bodyId);
                if (body == null) continue;
                if (!_views.TryGetValue(bodyId, out var view)) continue;

                SimVec3 worldPos = _positionResolver.Resolve(body, simTime);
                view.SetWorldPosition(WorldToScene(worldPos));

                if (body.BodyType == CelestialBodyType.Ship && body.ShipInfo != null)
                {
                    if (body.ShipInfo.OverrideWorldPosition.HasValue)
                    {
                        SetOrbitLineVisible(bodyId, false);
                    }
                    else if (body.Orbit != null && body.AttachmentMode == AttachmentMode.Orbit)
                    {
                        EnsureOrbitLine(body);
                        SetOrbitLineVisible(bodyId, true);
                    }
                }

                UpdateOrbitLineCenter(body, simTime);
            }
            UpdateOrbitLineThickness();
        }

        private void UpdateOrbitLineCenter(CelestialBody body, double simTime)
        {
            if (!_orbitLines.TryGetValue(body.Id, out var lr)) return;
            if (body.Orbit == null || !body.ParentId.IsValid) return;

            SimVec3 parentWorldPos = _positionResolver.Resolve(body.ParentId, simTime);
            lr.transform.position = WorldToScene(parentWorldPos);
        }

        // ---------------------------------------------------------------
        // Scene object creation
        // ---------------------------------------------------------------

        private void CreateBodyView(CelestialBody body)
        {
            GameObject go;
            if (body.BodyType == CelestialBodyType.Station)
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            else
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            if (body.BodyType == CelestialBodyType.Station)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.isTrigger = true;
            }
            else
            {
                var sc = go.AddComponent<SphereCollider>();
                sc.isTrigger = true;
            }

            var rend = go.GetComponent<MeshRenderer>();
            if (defaultBodyMaterial != null) rend.material = new Material(defaultBodyMaterial);
            var view = go.AddComponent<Planets.CelestialBodyView>();
            view.Bind(body, scaleConfig);
            go.AddComponent<Cameras.CameraFocusTarget>();
            go.transform.SetParent(transform);
            _views[body.Id] = view;
        }

        private void CreateOrbitLine(CelestialBody body)
        {
            if (body.Orbit == null) return;
            if (_orbitLines.ContainsKey(body.Id)) return;

            var lineGo = new GameObject($"OrbitLine_{body.BodyType}_{body.Id}");
            lineGo.transform.SetParent(transform);

            var lr = lineGo.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;

            if (orbitLineMaterial != null)
            {
                lr.material = new Material(orbitLineMaterial);
            }
            else
            {
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.startColor = DefaultOrbitLineColor;
                lr.endColor = DefaultOrbitLineColor;
            }

            lr.startWidth = orbitLineBaseWidth;
            lr.endWidth = orbitLineBaseWidth;

            SetOrbitLinePoints(lr, body.Orbit);
            _orbitLines[body.Id] = lr;
        }

        private void EnsureOrbitLine(CelestialBody body)
        {
            if (_orbitLines.ContainsKey(body.Id))
            {
                var existingLr = _orbitLines[body.Id];
                if (existingLr != null && body.Orbit != null)
                {
                    SetOrbitLinePoints(existingLr, body.Orbit);
                }
                return;
            }
            CreateOrbitLine(body);
        }

        /// <summary>
        /// Set orbit line points using OrbitSampler from the Simulation layer.
        /// This is the ONLY place orbit line geometry is generated.
        ///
        /// OrbitSampler delegates all position evaluation to
        /// OrbitalPositionCalculator.CalculateOrbitPoint() — the same function
        /// used by runtime body position calculation.
        /// This guarantees bodies move exactly on the rendered orbit line.
        ///
        /// Two modes:
        ///   Adaptive (default): curvature-based subdivision, few points for circles,
        ///     many points near periapsis of eccentric orbits.
        ///   Uniform (fallback): fixed segment count, evenly spaced in mean anomaly.
        /// </summary>
        private void SetOrbitLinePoints(LineRenderer lr, World.ValueTypes.OrbitDefinition orbit)
        {
            if (lr == null || orbit == null) return;

            float distScale = scaleConfig != null ? scaleConfig.DistanceScale : 1f;

            // Get orbit sample points from Simulation layer.
            List<SimVec3> points;

            if (adaptiveOrbitLines)
            {
                points = OrbitSampler.SampleAdaptive(
                    orbit,
                    orbitLineTolerance,
                    orbitLineMaxDepth,
                    orbitLineSeedSegments);
            }
            else
            {
                points = OrbitSampler.SampleUniform(orbit, orbitLineFixedSegments);
            }

            // Apply to LineRenderer.
            lr.positionCount = points.Count;

            for (int i = 0; i < points.Count; i++)
            {
                lr.SetPosition(i, new Vector3(
                    (float)(points[i].X * distScale),
                    (float)(points[i].Y * distScale),
                    (float)(points[i].Z * distScale)));
            }
        }

        private void SetOrbitLineVisible(EntityId bodyId, bool visible)
        {
            if (_orbitLines.TryGetValue(bodyId, out var lr) && lr != null)
            {
                lr.enabled = visible;
            }
        }

        /// <summary>
        /// Update orbit line width = baseWidth * multiplier.
        /// Multiplier goes from 1 to maxMultiplier based on camera zoom level
        /// using a half-Gaussian curve.
        ///
        /// Uses OrbitalCameraController.CurrentDistance directly (the actual
        /// zoom distance from the focus point), and its minDistance/maxDistance
        /// as the range endpoints. No hardcoded near/far values.
        ///
        /// Half-Gaussian: g(t) = 1 - exp(-t^2 / (2 * sigma^2))
        ///   t=0 (closest zoom) => multiplier = 1
        ///   t=1 (farthest zoom) => multiplier ≈ maxMultiplier
        /// </summary>
        private void UpdateOrbitLineThickness()
        {
            if (_orbitLines.Count == 0) return;
            if (_cameraController == null) return;

            // Read actual zoom level and camera range directly.
            float zoomDist = _cameraController.CurrentDistance;
            float minDist = _cameraController.MinDistance;
            float maxDist = _cameraController.MaxDistance;

            // Normalize zoom to [0..1] across the full camera range.
            float range = maxDist - minDist;
            float t = range > 0.001f
                ? Mathf.Clamp01((zoomDist - minDist) / range)
                : 0f;

            // Map inspector sigma [1..6] to internal [0.15..1.0].
            float sigma = Mathf.Lerp(0.15f, 1.0f, (orbitLineCurveSigma - 1f) / 5f);

            // Half-Gaussian.
            float g = 1f - Mathf.Exp(-(t * t) / (2f * sigma * sigma));

            float multiplier = 1f + (orbitLineMaxMultiplier - 1f) * g;
            float width = orbitLineBaseWidth * multiplier;

            foreach (var lr in _orbitLines.Values)
            {
                if (lr == null) continue;
                lr.startWidth = width;
                lr.endWidth = width;
            }
        }

        public void ClearSceneObjects()
        {
            foreach (var kvp in _views) { if (kvp.Value != null) Destroy(kvp.Value.gameObject); }
            _views.Clear();
            foreach (var kvp in _orbitLines) { if (kvp.Value != null) Destroy(kvp.Value.gameObject); }
            _orbitLines.Clear();
        }

        private Vector3 WorldToScene(SimVec3 v)
        {
            if (scaleConfig != null) return scaleConfig.WorldToScenePosition(v.X, v.Y, v.Z);
            return new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        }
    }
}
