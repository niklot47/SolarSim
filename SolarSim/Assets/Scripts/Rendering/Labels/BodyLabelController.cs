using System.Collections.Generic;
using UnityEngine;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;
using SpaceSim.Rendering.Orbits;
using SpaceSim.Rendering.Cameras;
using SpaceSim.Rendering.Planets;

// Resolve ambiguity with UnityEngine.EntityId (Unity 6+).
using EntityId = SpaceSim.Shared.Identifiers.EntityId;

namespace SpaceSim.Rendering.Labels
{
    /// <summary>
    /// Creates and manages floating name labels above celestial bodies.
    /// Labels are screen-space GUI rendered on top of the 3D scene.
    /// Visibility is controlled by camera distance thresholds.
    /// </summary>
    public class BodyLabelController : MonoBehaviour
    {
        [Header("Visibility")]
        [SerializeField] private float showLabelsUnderDistance = 120f;
        [SerializeField] private float fadeStartDistance = 90f;
        [SerializeField] private float labelOffsetPixels = 20f;

        [Header("Style")]
        [SerializeField] private int fontSize = 13;
        [SerializeField] private Color labelColor = new Color(0.85f, 0.85f, 0.95f, 1f);
        [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.6f);

        private WorldRegistry _registry;
        private StarSystem _system;
        private OrbitalMapRenderer _mapRenderer;
        private OrbitalCameraController _cameraController;
        private Camera _camera;

        private GUIStyle _labelStyle;
        private GUIStyle _shadowStyle;

        // Cached body data for label rendering.
        private struct LabelEntry
        {
            public EntityId Id;
            public string DisplayName;
        }
        private List<LabelEntry> _entries = new List<LabelEntry>();

        public void Initialize(
            WorldRegistry registry,
            StarSystem system,
            OrbitalMapRenderer mapRenderer,
            OrbitalCameraController cameraController)
        {
            _registry = registry;
            _system = system;
            _mapRenderer = mapRenderer;
            _cameraController = cameraController;

            BuildEntryList();
        }

        private void BuildEntryList()
        {
            _entries.Clear();
            if (_registry == null || _system == null) return;

            foreach (var bodyId in _system.AllBodyIds)
            {
                var body = _registry.GetCelestialBody(bodyId);
                if (body == null) continue;

                _entries.Add(new LabelEntry
                {
                    Id = body.Id,
                    DisplayName = body.DisplayName
                });
            }
        }

        private void OnGUI()
        {
            if (_entries.Count == 0 || _mapRenderer == null) return;

            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null) return;

            // Determine visibility based on camera distance.
            float camDist = _cameraController != null
                ? _cameraController.CurrentDistance
                : _camera.transform.position.magnitude;

            if (camDist > showLabelsUnderDistance) return;

            // Calculate alpha for fade.
            float alpha = 1f;
            if (camDist > fadeStartDistance)
            {
                alpha = 1f - Mathf.InverseLerp(fadeStartDistance, showLabelsUnderDistance, camDist);
            }

            EnsureStyles();

            Color baseColor = labelColor;
            baseColor.a *= alpha;
            _labelStyle.normal.textColor = baseColor;

            Color baseShadow = shadowColor;
            baseShadow.a *= alpha;
            _shadowStyle.normal.textColor = baseShadow;

            foreach (var entry in _entries)
            {
                var view = _mapRenderer.GetView(entry.Id);
                if (view == null) continue;

                Vector3 worldPos = view.transform.position;
                Vector3 screenPos = _camera.WorldToScreenPoint(worldPos);

                // Skip if behind camera.
                if (screenPos.z < 0f) continue;

                // Convert to GUI coordinates (Y is inverted).
                float guiY = Screen.height - screenPos.y - labelOffsetPixels;
                float guiX = screenPos.x;

                var content = new GUIContent(entry.DisplayName);
                Vector2 size = _labelStyle.CalcSize(content);

                // Center horizontally.
                float x = guiX - size.x * 0.5f;
                Rect rect = new Rect(x, guiY, size.x, size.y);
                Rect shadowRect = new Rect(x + 1f, guiY + 1f, size.x, size.y);

                // Drop shadow.
                GUI.Label(shadowRect, content, _shadowStyle);
                GUI.Label(rect, content, _labelStyle);
            }
        }

        private void EnsureStyles()
        {
            if (_labelStyle != null) return;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Normal
            };
            _labelStyle.normal.textColor = labelColor;

            _shadowStyle = new GUIStyle(_labelStyle);
            _shadowStyle.normal.textColor = shadowColor;
        }
    }
}
