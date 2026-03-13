using System.Collections.Generic;
using UnityEngine;
using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;
using SpaceSim.Simulation.Orbits;
using SpaceSim.Simulation.Time;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;
using SpaceSim.Data.Config;

using EntityId = SpaceSim.Shared.Identifiers.EntityId;

namespace SpaceSim.Rendering.Orbits
{
    public class OrbitalMapRenderer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Material defaultBodyMaterial;
        [SerializeField] private SceneScaleConfig scaleConfig;

        [Header("Orbit Lines (camera distance scaling)")]
        [SerializeField] private float nearDistance = 20f;
        [SerializeField] private float farDistance = 150f;

        private WorldRegistry _registry;
        private StarSystem _system;
        private SimulationClock _clock;
        private Camera _mainCamera;

        private readonly Dictionary<EntityId, Planets.CelestialBodyView> _views =
            new Dictionary<EntityId, Planets.CelestialBodyView>();
        private readonly Dictionary<EntityId, LineRenderer> _orbitLines =
            new Dictionary<EntityId, LineRenderer>();

        private const int OrbitLineSegments = 64;

        public void Initialize(WorldRegistry registry, StarSystem system, SimulationClock clock)
        {
            _registry = registry;
            _system = system;
            _clock = clock;
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

        /// <summary>
        /// Resolve absolute world position for any body at given simulation time.
        /// Public so ShipMovementSystem can use it via delegate.
        /// </summary>
        public SimVec3 ResolveWorldPosition(EntityId bodyId, double simTime)
        {
            var body = _registry.GetCelestialBody(bodyId);
            if (body == null) return SimVec3.Zero;
            return CalculateWorldPosition(body, simTime);
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

                // Check if this is a travelling ship with a position override.
                if (body.BodyType == CelestialBodyType.Ship
                    && body.ShipInfo != null
                    && body.ShipInfo.OverrideWorldPosition.HasValue)
                {
                    // Use override position from ShipMovementSystem.
                    view.SetWorldPosition(WorldToScene(body.ShipInfo.OverrideWorldPosition.Value));

                    // Hide orbit line while travelling.
                    SetOrbitLineVisible(bodyId, false);
                }
                else
                {
                    // Standard orbital position.
                    SimVec3 worldPos = CalculateWorldPosition(body, simTime);
                    view.SetWorldPosition(WorldToScene(worldPos));
                    UpdateOrbitLineCenter(body, simTime);

                    // Re-create orbit line if ship arrived and has orbit again but no line yet.
                    if (body.BodyType == CelestialBodyType.Ship
                        && body.Orbit != null
                        && body.AttachmentMode == AttachmentMode.Orbit)
                    {
                        EnsureOrbitLine(body);
                        SetOrbitLineVisible(bodyId, true);
                    }
                }
            }
            UpdateOrbitLineThickness();
        }

        private SimVec3 CalculateWorldPosition(CelestialBody body, double simTime)
        {
            if (body.Orbit == null || !body.ParentId.IsValid) return SimVec3.Zero;
            var parent = _registry.GetCelestialBody(body.ParentId);
            SimVec3 parentPos = SimVec3.Zero;
            if (parent != null) parentPos = CalculateWorldPosition(parent, simTime);
            return OrbitalPositionCalculator.CalculateAbsolutePosition(body.Orbit, simTime, parentPos);
        }

        private void CreateBodyView(CelestialBody body)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            var sc = go.AddComponent<SphereCollider>();
            sc.isTrigger = true;
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
            if (_orbitLines.ContainsKey(body.Id)) return; // Already exists.

            var lineGo = new GameObject($"OrbitLine_{body.BodyType}_{body.Id}");
            lineGo.transform.SetParent(transform);
            float baseWidth = scaleConfig != null ? scaleConfig.OrbitLineBaseWidth : 0.05f;
            var lr = lineGo.AddComponent<LineRenderer>();

            // Use local space so that transform.position acts as the orbit center.
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = OrbitLineSegments;
            lr.startWidth = baseWidth;
            lr.endWidth = baseWidth;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = new Color(1f, 1f, 1f, 0.2f);
            lr.endColor = new Color(1f, 1f, 1f, 0.2f);
            float sceneRadius = scaleConfig != null
                ? scaleConfig.WorldToSceneDistance(body.Orbit.SemiMajorAxis)
                : (float)body.Orbit.SemiMajorAxis;

            // Positions are in local space — circle centered at (0,0,0).
            for (int i = 0; i < OrbitLineSegments; i++)
            {
                float angle = (float)i / OrbitLineSegments * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(sceneRadius * Mathf.Cos(angle), 0f, sceneRadius * Mathf.Sin(angle)));
            }
            _orbitLines[body.Id] = lr;
        }

        /// <summary>
        /// Ensure orbit line exists for a body. Creates one if missing.
        /// Used when a ship arrives at a new destination and needs a fresh orbit line.
        /// </summary>
        private void EnsureOrbitLine(CelestialBody body)
        {
            if (_orbitLines.ContainsKey(body.Id))
            {
                // Orbit line exists but may have wrong radius — rebuild.
                var existingLr = _orbitLines[body.Id];
                if (existingLr != null && body.Orbit != null)
                {
                    float sceneRadius = scaleConfig != null
                        ? scaleConfig.WorldToSceneDistance(body.Orbit.SemiMajorAxis)
                        : (float)body.Orbit.SemiMajorAxis;

                    for (int i = 0; i < OrbitLineSegments; i++)
                    {
                        float angle = (float)i / OrbitLineSegments * Mathf.PI * 2f;
                        existingLr.SetPosition(i, new Vector3(
                            sceneRadius * Mathf.Cos(angle), 0f, sceneRadius * Mathf.Sin(angle)));
                    }
                }
                return;
            }

            CreateOrbitLine(body);
        }

        private void SetOrbitLineVisible(EntityId bodyId, bool visible)
        {
            if (_orbitLines.TryGetValue(bodyId, out var lr) && lr != null)
            {
                lr.enabled = visible;
            }
        }

        private void UpdateOrbitLineCenter(CelestialBody body, double simTime)
        {
            if (!_orbitLines.TryGetValue(body.Id, out var lr)) return;
            if (body.Orbit == null || !body.ParentId.IsValid) return;
            var parent = _registry.GetCelestialBody(body.ParentId);
            if (parent == null) return;

            // Move the orbit line GameObject to the parent's scene position.
            // Since useWorldSpace=false, this centers the local-space circle on the parent.
            lr.transform.position = WorldToScene(CalculateWorldPosition(parent, simTime));
        }

        private void UpdateOrbitLineThickness()
        {
            if (_orbitLines.Count == 0) return;
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;
            float baseWidth = scaleConfig != null ? scaleConfig.OrbitLineBaseWidth : 0.05f;
            float maxMul = scaleConfig != null ? scaleConfig.OrbitLineMaxWidthMultiplier : 2.0f;
            float camDist = _mainCamera.transform.position.magnitude;
            float t = Mathf.Clamp01((camDist - nearDistance) / (farDistance - nearDistance));
            float width = baseWidth * Mathf.Lerp(1.0f, maxMul, t);
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
