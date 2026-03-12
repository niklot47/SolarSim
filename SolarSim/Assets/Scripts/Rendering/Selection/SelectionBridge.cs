using UnityEngine;
using SpaceSim.Simulation.Core;
using SpaceSim.Rendering.Orbits;
using SpaceSim.Rendering.Cameras;
using SpaceSim.Rendering.Planets;

// Resolve ambiguity with UnityEngine.EntityId (Unity 6+).
using EntityId = SpaceSim.Shared.Identifiers.EntityId;

namespace SpaceSim.Rendering.Selection
{
    /// <summary>
    /// Bridges SelectionService to rendering: creates a visible selection ring,
    /// highlights the selected body, and focuses the camera.
    /// </summary>
    public class SelectionBridge : MonoBehaviour
    {
        private SelectionService _selectionService;
        private OrbitalMapRenderer _mapRenderer;
        private OrbitalCameraController _camera;

        private EntityId _lastHighlightedId = EntityId.None;
        private GameObject _selectionRing;

        [SerializeField] private bool autoFocusOnSelect = true;

        public void Initialize(
            SelectionService selectionService,
            OrbitalMapRenderer mapRenderer,
            OrbitalCameraController camera)
        {
            _selectionService = selectionService;
            _mapRenderer = mapRenderer;
            _camera = camera;

            _selectionService.OnSelectionChanged += OnSelectionChanged;
        }

        private void OnDestroy()
        {
            if (_selectionService != null)
                _selectionService.OnSelectionChanged -= OnSelectionChanged;

            if (_selectionRing != null)
                Destroy(_selectionRing);
        }

        private void OnSelectionChanged(EntityId previousId, EntityId newId)
        {
            // Remove old highlight.
            if (_lastHighlightedId.IsValid)
            {
                var oldView = _mapRenderer?.GetView(_lastHighlightedId);
                if (oldView != null)
                    oldView.SetHighlight(false);
            }

            // Apply new highlight and ring.
            if (newId.IsValid)
            {
                var newView = _mapRenderer?.GetView(newId);
                if (newView != null)
                {
                    newView.SetHighlight(true);
                    AttachRingTo(newView);

                    if (autoFocusOnSelect && _camera != null)
                        _camera.FocusSmooth(newView.transform);
                }
            }
            else
            {
                HideRing();
            }

            _lastHighlightedId = newId;
        }

        private void AttachRingTo(CelestialBodyView view)
        {
            if (_selectionRing == null)
                _selectionRing = CreateRingObject();

            _selectionRing.SetActive(true);
            _selectionRing.transform.SetParent(view.transform, false);
            _selectionRing.transform.localPosition = Vector3.zero;

            // Scale ring slightly larger than the body (body scale = diameter).
            float ringScale = 1.3f;
            _selectionRing.transform.localScale = Vector3.one * ringScale;
            _selectionRing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private void HideRing()
        {
            if (_selectionRing != null)
            {
                _selectionRing.transform.SetParent(null);
                _selectionRing.SetActive(false);
            }
        }

        private static GameObject CreateRingObject()
        {
            // Create a thin torus-like ring using a LineRenderer circle.
            var go = new GameObject("SelectionRing");

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;

            int segments = 48;
            lr.positionCount = segments;
            lr.startWidth = 0.04f;
            lr.endWidth = 0.04f;

            lr.material = new Material(Shader.Find("Sprites/Default"));
            Color ringColor = new Color(0.3f, 0.8f, 1.0f, 0.8f);
            lr.startColor = ringColor;
            lr.endColor = ringColor;

            float radius = 0.55f; // Slightly beyond unit sphere radius of 0.5.
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float x = radius * Mathf.Cos(angle);
                float y = radius * Mathf.Sin(angle);
                lr.SetPosition(i, new Vector3(x, y, 0f));
            }

            go.SetActive(false);
            return go;
        }
    }
}
