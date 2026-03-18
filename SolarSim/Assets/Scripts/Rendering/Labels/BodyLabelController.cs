using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
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
    /// Labels are screen-space IMGUI rendered on top of the 3D scene.
    /// Visibility is controlled by camera distance thresholds.
    ///
    /// The Visible property allows external controllers (e.g. modal dialog)
    /// to temporarily hide all labels so they don't render on top of UI panels.
    ///
    /// Labels are clipped to the viewport rect between the two side panels.
    /// Panel widths are read automatically from the UIDocument's resolved layout
    /// each frame, so they adapt to any panel width changes (collapse, resize).
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

        // UI Toolkit references for reading resolved panel widths.
        private VisualElement _leftPanel;
        private VisualElement _rightPanel;
        // Panel body elements — when display:none, the panel is collapsed (header-only).
        // In that case we treat the panel as taking zero horizontal space for clipping.
        private VisualElement _leftPanelBody;
        private VisualElement _rightPanelBody;

        private GUIStyle _labelStyle;
        private GUIStyle _shadowStyle;

        /// <summary>
        /// When false, all IMGUI labels are hidden.
        /// Set to false when a modal overlay is open to prevent labels
        /// from rendering on top of the UI.
        /// </summary>
        public bool Visible { get; set; } = true;

        // Cached body data for label rendering.
        private struct LabelEntry
        {
            public EntityId Id;
            public string DisplayName;
        }
        private List<LabelEntry> _entries = new List<LabelEntry>();

        /// <summary>
        /// Initialize the label controller.
        /// Pass the UIDocument so panel widths are read automatically from resolved layout.
        /// </summary>
        public void Initialize(
            WorldRegistry registry,
            StarSystem system,
            OrbitalMapRenderer mapRenderer,
            OrbitalCameraController cameraController,
            UIDocument uiDocument = null)
        {
            _registry = registry;
            _system = system;
            _mapRenderer = mapRenderer;
            _cameraController = cameraController;

            if (uiDocument != null)
            {
                var root = uiDocument.rootVisualElement;
                _leftPanel     = root?.Q<VisualElement>("left-panel");
                _rightPanel    = root?.Q<VisualElement>("right-panel");
                _leftPanelBody  = root?.Q<VisualElement>("left-panel-body");
                _rightPanelBody = root?.Q<VisualElement>("right-panel-body");
            }

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

        /// <summary>
        /// Convert a UI Toolkit point coordinate to IMGUI physical screen pixels.
        /// </summary>
        private float UIToScreenX(float uiX)
        {
            if (_leftPanel == null) return uiX;
            float scale = _leftPanel.panel?.scaledPixelsPerPoint ?? 1f;
            return uiX * scale;
        }

        /// <summary>
        /// Returns true when the panel body has display:none — i.e. the panel is collapsed
        /// to just its header bar. In this state the panel takes no useful horizontal space
        /// for label clipping purposes.
        /// </summary>
        private static bool IsPanelCollapsed(VisualElement panelBody)
        {
            if (panelBody == null) return false;
            return panelBody.resolvedStyle.display == DisplayStyle.None;
        }

        private void OnGUI()
        {
            // Respect visibility flag (hidden when modal is open).
            if (!Visible) return;

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

            // Determine clip edges from actual panel positions.
            // When a panel is collapsed (panel-body display:none), its header stays full-width
            // but occupies only ~28px of height — no horizontal space is blocked, so we use 0.
            float leftEdge;
            if (_leftPanel == null || IsPanelCollapsed(_leftPanelBody))
            {
                leftEdge = 0f;
            }
            else
            {
                leftEdge = UIToScreenX(_leftPanel.worldBound.xMax);
                if (float.IsNaN(leftEdge) || leftEdge < 0f) leftEdge = 0f;
            }

            float rightEdge;
            if (_rightPanel == null || IsPanelCollapsed(_rightPanelBody))
            {
                rightEdge = Screen.width;
            }
            else
            {
                rightEdge = UIToScreenX(_rightPanel.worldBound.xMin);
                if (float.IsNaN(rightEdge) || rightEdge <= 0f) rightEdge = Screen.width;
            }

            float clipX     = leftEdge;
            float clipWidth = Mathf.Max(0f, rightEdge - leftEdge);
            Rect clipRect   = new Rect(clipX, 0f, clipWidth, Screen.height);

            // GUI.BeginClip clips rendering AND shifts coordinate origin to clipRect's top-left,
            // so all label X positions must be offset by -clipX.
            GUI.BeginClip(clipRect);

            foreach (var entry in _entries)
            {
                var view = _mapRenderer.GetView(entry.Id);
                if (view == null) continue;

                Vector3 worldPos = view.transform.position;
                Vector3 screenPos = _camera.WorldToScreenPoint(worldPos);

                // Skip if behind camera.
                if (screenPos.z < 0f) continue;

                // Convert to GUI coordinates (Y is inverted), shift X to clip-space.
                float guiY = Screen.height - screenPos.y - labelOffsetPixels;
                float guiX = screenPos.x - clipX;

                var content = new GUIContent(entry.DisplayName);
                Vector2 size = _labelStyle.CalcSize(content);

                // Center horizontally.
                float x = guiX - size.x * 0.5f;
                Rect rect       = new Rect(x,        guiY,        size.x, size.y);
                Rect shadowRect = new Rect(x + 1f,   guiY + 1f,   size.x, size.y);

                GUI.Label(shadowRect, content, _shadowStyle);
                GUI.Label(rect,       content, _labelStyle);
            }

            GUI.EndClip();
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
