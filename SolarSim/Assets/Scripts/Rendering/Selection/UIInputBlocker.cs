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

            RegisterWheelBlocker(root.Q<VisualElement>("left-panel"));
            RegisterWheelBlocker(root.Q<VisualElement>("right-panel"));
            RegisterWheelBlocker(root.Q<VisualElement>("time-controls"));
            RegisterWheelBlocker(root.Q<VisualElement>("modal-overlay"));
        }

        private void Update()
        {
            if (_camera == null) return;

            if (_modalController != null && _modalController.IsOpen)
            {
                _camera.BlockInput = true;
                return;
            }

            _camera.BlockInput = IsMouseOverBlockingPanel();
        }

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
