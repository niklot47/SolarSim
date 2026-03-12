using System.Collections.Generic;
using UnityEngine;
using SpaceSim.Shared.Math;
using SpaceSim.Simulation.Orbits;
using SpaceSim.Simulation.Time;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;

// Resolve ambiguity with UnityEngine.EntityId (Unity 6+).
using EntityId = SpaceSim.Shared.Identifiers.EntityId;

namespace SpaceSim.Rendering.Orbits
{
    /// <summary>
    /// Creates and updates placeholder scene visuals for the orbital map.
    /// Reads world data from WorldRegistry, positions objects using OrbitalPositionCalculator.
    /// </summary>
    public class OrbitalMapRenderer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Material defaultBodyMaterial;

        [Header("Orbit Lines")]
        [SerializeField] private float baseLineWidth = 0.05f;
        [SerializeField] private float maxLineWidthMultiplier = 2.0f;
        [SerializeField] private float nearDistance = 20f;
        [SerializeField] private float farDistance = 150f;

        // External dependencies (set via Initialize).
        private WorldRegistry _registry;
        private StarSystem _system;
        private SimulationClock _clock;
        private Camera _mainCamera;

        // Entity id -> view mapping.
        private readonly Dictionary<EntityId, Planets.CelestialBodyView> _views =
            new Dictionary<EntityId, Planets.CelestialBodyView>();

        // Entity id -> orbit line renderer.
        private readonly Dictionary<EntityId, LineRenderer> _orbitLines =
            new Dictionary<EntityId, LineRenderer>();

        private const int OrbitLineSegments = 64;

        /// <summary>
        /// Initialize with required services. Called from bootstrap coordinator.
        /// </summary>
        public void Initialize(WorldRegistry registry, StarSystem system, SimulationClock clock)
        {
            _registry = registry;
            _system = system;
            _clock = clock;
        }

        /// <summary>
        /// Build scene objects for all bodies in the current system.
        /// </summary>
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

        /// <summary>
        /// Get the view for a specific entity id.
        /// </summary>
        public Planets.CelestialBodyView GetView(EntityId id)
        {
            _views.TryGetValue(id, out var view);
            return view;
        }

        private void Update()
        {
            if (_registry == null || _system == null || _clock == null) return;

            double simTime = _clock.CurrentTime;

            foreach (var bodyId in _system.AllBodyIds)
            {
                var body = _registry.GetCelestialBody(bodyId);
                if (body == null) continue;

                if (!_views.TryGetValue(bodyId, out var view)) continue;

                SimVec3 worldPos = CalculateWorldPosition(body, simTime);
                view.SetWorldPosition(SimVecToUnity(worldPos));

                UpdateOrbitLineCenter(body, simTime);
            }

            UpdateOrbitLineThickness();
        }

        private SimVec3 CalculateWorldPosition(CelestialBody body, double simTime)
        {
            if (body.Orbit == null || !body.ParentId.IsValid)
                return SimVec3.Zero;

            var parent = _registry.GetCelestialBody(body.ParentId);
            SimVec3 parentPos = SimVec3.Zero;

            if (parent != null)
                parentPos = CalculateWorldPosition(parent, simTime);

            return OrbitalPositionCalculator.CalculateAbsolutePosition(
                body.Orbit, simTime, parentPos);
        }

        private void CreateBodyView(CelestialBody body)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var sphereCollider = go.AddComponent<SphereCollider>();
            sphereCollider.isTrigger = true;

            var renderer = go.GetComponent<MeshRenderer>();
            if (defaultBodyMaterial != null)
                renderer.material = new Material(defaultBodyMaterial);

            var view = go.AddComponent<Planets.CelestialBodyView>();
            view.Bind(body);

            go.AddComponent<Cameras.CameraFocusTarget>();

            go.transform.SetParent(transform);
            _views[body.Id] = view;
        }

        private void CreateOrbitLine(CelestialBody body)
        {
            if (body.Orbit == null) return;

            // ASCII-safe name: no localized text in scene hierarchy.
            var lineGo = new GameObject($"OrbitLine_{body.BodyType}_{body.Id}");
            lineGo.transform.SetParent(transform);

            var lr = lineGo.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = OrbitLineSegments;
            lr.startWidth = baseLineWidth;
            lr.endWidth = baseLineWidth;

            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = new Color(1f, 1f, 1f, 0.2f);
            lr.endColor = new Color(1f, 1f, 1f, 0.2f);

            float radius = (float)body.Orbit.SemiMajorAxis;
            for (int i = 0; i < OrbitLineSegments; i++)
            {
                float angle = (float)i / OrbitLineSegments * Mathf.PI * 2f;
                float x = radius * Mathf.Cos(angle);
                float z = radius * Mathf.Sin(angle);
                lr.SetPosition(i, new Vector3(x, 0f, z));
            }

            _orbitLines[body.Id] = lr;
        }

        private void UpdateOrbitLineCenter(CelestialBody body, double simTime)
        {
            if (!_orbitLines.TryGetValue(body.Id, out var lr)) return;
            if (body.Orbit == null || !body.ParentId.IsValid) return;

            var parent = _registry.GetCelestialBody(body.ParentId);
            if (parent == null) return;

            SimVec3 parentPos = CalculateWorldPosition(parent, simTime);
            lr.transform.position = SimVecToUnity(parentPos);
        }

        private void UpdateOrbitLineThickness()
        {
            if (_orbitLines.Count == 0) return;

            if (_mainCamera == null)
                _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            float camDistance = _mainCamera.transform.position.magnitude;
            float t = Mathf.Clamp01((camDistance - nearDistance) / (farDistance - nearDistance));
            float multiplier = Mathf.Lerp(1.0f, maxLineWidthMultiplier, t);
            float width = baseLineWidth * multiplier;

            foreach (var lr in _orbitLines.Values)
            {
                if (lr == null) continue;
                lr.startWidth = width;
                lr.endWidth = width;
            }
        }

        public void ClearSceneObjects()
        {
            foreach (var kvp in _views)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);
            }
            _views.Clear();

            foreach (var kvp in _orbitLines)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);
            }
            _orbitLines.Clear();
        }

        private static Vector3 SimVecToUnity(SimVec3 v)
        {
            return new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        }
    }
}
