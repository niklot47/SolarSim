using UnityEngine;
using UnityEngine.UIElements;
using SpaceSim.Rendering.Cameras;
using SpaceSim.UI.Panels;

namespace SpaceSim.Rendering.Selection
{
    /// <summary>
    /// Blocks camera zoom/pan/rotate when the mouse cursor is over
    /// UI panels (left, right, time controls) or when a modal is open.
    /// Attaches WheelEvent callbacks to prevent scroll bleed-through
    /// and sets OrbitalCameraController.BlockInput each frame.
    /// </summary>
    public class UIInputBlocker : MonoBehaviour
    {
        private OrbitalCameraController _camera;
        private UIDocument _uiDocument;
        private DetailModalController _modalController;

        // Cached UI elements to check mouse hover.
        private VisualElement _leftPanel;
        private VisualElement _rightPanel;
        private VisualElement _timeControls;
        private VisualElement _modalOverlay;

        // Names of UI containers that block camera input.
        private static readonly string[] _blockingPanelNames = {
            "left-panel", "right-panel", "time-controls", "modal-overlay"
        };

        public void Initialize(
            OrbitalCameraController camera,
            UIDocument uiDocument,
            DetailModalController modalController)
        {
            _camera = camera;
            _uiDocument = uiDocument;
            _modalController = modalController;

            if (_uiDocument == null || _uiDocument.rootVisualElement == null) return;

            var root = _uiDocument.rootVisualElement;

            _leftPanel = root.Q<VisualElement>("left-panel");
            _rightPanel = root.Q<VisualElement>("right-panel");
            _timeControls = root.Q<VisualElement>("time-controls");
            _modalOverlay = root.Q<VisualElement>("modal-overlay");

            // Register WheelEvent stoppers on all blocking panels.
            // This prevents the scroll event from propagating through
            // UI Toolkit to the Input System mouse scroll.
            RegisterWheelBlocker(_leftPanel);
            RegisterWheelBlocker(_rightPanel);
            RegisterWheelBlocker(_timeControls);
            RegisterWheelBlocker(_modalOverlay);
        }

        private void Update()
        {
            if (_camera == null) return;

            // If modal is open, always block.
            if (_modalController != null && _modalController.IsOpen)
            {
                _camera.BlockInput = true;
                return;
            }

            // Check if mouse is over any blocking panel.
            _camera.BlockInput = IsMouseOverBlockingPanel();
        }

        /// <summary>
        /// Check if the mouse cursor is currently over any UI panel.
        /// Uses UI Toolkit panel picking.
        /// </summary>
        private bool IsMouseOverBlockingPanel()
        {
            if (_uiDocument == null) return false;

            var root = _uiDocument.rootVisualElement;
            if (root == null) return false;

            var panel = root.panel;
            if (panel == null) return false;

            Vector2 mousePos = UnityEngine.InputSystem.Mouse.current?.position.ReadValue() ?? Vector2.zero;
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(
                panel, new Vector2(mousePos.x, Screen.height - mousePos.y));

            var picked = panel.Pick(panelPos);
            if (picked == null) return false;

            // Walk up from picked element to see if it belongs to a blocking panel.
            var current = picked;
            int safety = 0;
            while (current != null && safety < 30)
            {
                for (int i = 0; i < _blockingPanelNames.Length; i++)
                {
                    if (current.name == _blockingPanelNames[i])
                        return true;
                }
                current = current.parent;
                safety++;
            }

            return false;
        }

        /// <summary>
        /// Register a WheelEvent callback on a visual element that stops
        /// the event from propagating, preventing camera zoom.
        /// </summary>
        private static void RegisterWheelBlocker(VisualElement element)
        {
            if (element == null) return;
            element.RegisterCallback<WheelEvent>(evt =>
            {
                evt.StopPropagation();
            }, TrickleDown.TrickleDown);
        }
    }
}
