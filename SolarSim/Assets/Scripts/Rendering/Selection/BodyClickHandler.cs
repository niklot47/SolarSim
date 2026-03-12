using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using SpaceSim.Simulation.Core;
using SpaceSim.Rendering.Planets;

// Resolve ambiguity with UnityEngine.EntityId (Unity 6+).
using EntityId = SpaceSim.Shared.Identifiers.EntityId;

namespace SpaceSim.Rendering.Selection
{
    /// <summary>
    /// Handles mouse click raycasting to select celestial bodies in the scene.
    /// Uses UI Toolkit panel picking to distinguish real UI panels from the 3D viewport.
    /// </summary>
    public class BodyClickHandler : MonoBehaviour
    {
        private SelectionService _selectionService;
        private Camera _camera;
        private Mouse _mouse;
        private UIDocument _uiDocument;

        [SerializeField] private float maxRayDistance = 500f;

        // Names of UI containers that should block 3D raycasting.
        private static readonly string[] _uiPanelNames = {
            "left-panel", "right-panel", "time-controls"
        };

        public void Initialize(SelectionService selectionService, UIDocument uiDocument)
        {
            _selectionService = selectionService;
            _uiDocument = uiDocument;
        }

        private void Update()
        {
            _mouse = Mouse.current;
            if (_mouse == null) return;

            if (!_mouse.leftButton.wasPressedThisFrame) return;

            // Check if click landed on a real UI panel (not the transparent viewport area).
            if (IsClickOnUIPanel()) return;

            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null) return;

            Vector2 screenPos = _mouse.position.ReadValue();
            Ray ray = _camera.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, ~0, QueryTriggerInteraction.Collide))
            {
                var view = hit.collider.GetComponentInParent<CelestialBodyView>();
                if (view != null && view.BoundEntityId.IsValid)
                {
                    _selectionService?.Select(view.BoundEntityId);
                    return;
                }
            }

            // Click on empty space — clear selection.
            _selectionService?.ClearSelection();
        }

        /// <summary>
        /// Check if the mouse click hit a real UI panel element.
        /// Walks up the picked element's parent chain looking for known panel names
        /// (left-panel, right-panel, time-controls). If found — click is on UI.
        /// If we reach the root without finding a known panel — click is in the 3D viewport.
        /// </summary>
        private bool IsClickOnUIPanel()
        {
            if (_uiDocument == null) return false;

            var root = _uiDocument.rootVisualElement;
            if (root == null) return false;

            var panel = root.panel;
            if (panel == null) return false;

            Vector2 mousePos = _mouse.position.ReadValue();
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(
                panel, new Vector2(mousePos.x, Screen.height - mousePos.y));

            var picked = panel.Pick(panelPos);
            if (picked == null) return false;

            // Walk up from picked element to see if it belongs to a known UI panel.
            var current = picked;
            int safety = 0;
            while (current != null && safety < 30)
            {
                for (int i = 0; i < _uiPanelNames.Length; i++)
                {
                    if (current.name == _uiPanelNames[i])
                        return true; // Click is on a real UI panel — block raycast.
                }
                current = current.parent;
                safety++;
            }

            // Reached root without finding a UI panel — click is in the 3D viewport area.
            return false;
        }
    }
}
