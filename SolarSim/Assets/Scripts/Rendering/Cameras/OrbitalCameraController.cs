using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceSim.Rendering.Cameras
{
    /// <summary>
    /// Minimal orbital camera controller for the sandbox view.
    /// Supports pan, zoom, focus on target, and smooth focus transitions.
    /// Uses the new Input System package.
    ///
    /// BlockInput property allows UI controllers to suppress camera input
    /// when the mouse is over UI panels or a modal is open.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class OrbitalCameraController : MonoBehaviour
    {
        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 10f;
        [SerializeField] private float minDistance = 5f;
        [SerializeField] private float maxDistance = 200f;

        [Header("Pan")]
        [SerializeField] private float panSpeed = 0.5f;

        [Header("Rotation")]
        [SerializeField] private float rotateSpeed = 2f;

        [Header("Smooth Focus")]
        [SerializeField] private float focusLerpSpeed = 5f;

        [Header("State")]
        [SerializeField] private float currentDistance = 80f;
        [SerializeField] private float pitch = 45f;
        [SerializeField] private float yaw = 0f;

        private Vector3 _focusPoint = Vector3.zero;
        private Transform _focusTarget;
        private Mouse _mouse;

        private bool _isSmoothFocusing;
        private Vector3 _smoothTargetPoint;

        /// <summary>Current camera distance from focus point.</summary>
        public float CurrentDistance => currentDistance;

        /// <summary>
        /// When true, all camera input (zoom, pan, rotate) is suppressed.
        /// Set by UIInputBlocker when mouse is over panels or modal is open.
        /// </summary>
        public bool BlockInput { get; set; }

        public void SetFocusTarget(Transform target)
        {
            _focusTarget = target;
            if (target != null)
                _focusPoint = target.position;
            _isSmoothFocusing = false;
        }

        public void FocusSmooth(Transform target)
        {
            _focusTarget = target;
            if (target != null)
            {
                _smoothTargetPoint = target.position;
                _isSmoothFocusing = true;
            }
        }

        public void SetFocusPoint(Vector3 point)
        {
            _focusTarget = null;
            _focusPoint = point;
            _isSmoothFocusing = false;
        }

        private void OnEnable()
        {
            _mouse = Mouse.current;
        }

        private void LateUpdate()
        {
            _mouse = Mouse.current;
            if (_mouse == null) return;

            if (_focusTarget != null)
                _smoothTargetPoint = _focusTarget.position;

            if (_isSmoothFocusing)
            {
                _focusPoint = Vector3.Lerp(_focusPoint, _smoothTargetPoint, focusLerpSpeed * Time.deltaTime);
                if (Vector3.Distance(_focusPoint, _smoothTargetPoint) < 0.01f)
                {
                    _focusPoint = _smoothTargetPoint;
                    _isSmoothFocusing = false;
                }
            }
            else if (_focusTarget != null)
            {
                _focusPoint = _focusTarget.position;
            }

            if (!BlockInput)
            {
                HandleZoom();
                HandlePan();
                HandleRotation();
            }

            ApplyCameraTransform();
        }

        private void HandleZoom()
        {
            float scroll = _mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float normalizedScroll = scroll / 120f;
                currentDistance -= normalizedScroll * zoomSpeed * (currentDistance * 0.1f);
                currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
            }
        }

        private void HandlePan()
        {
            if (_mouse.middleButton.isPressed)
            {
                Vector2 delta = _mouse.delta.ReadValue();
                float dx = -delta.x * panSpeed * (currentDistance * 0.001f);
                float dy = -delta.y * panSpeed * (currentDistance * 0.001f);

                Vector3 right = transform.right;
                Vector3 up = transform.up;

                _focusPoint += right * dx + up * dy;
                _focusTarget = null;
                _isSmoothFocusing = false;
            }
        }

        private void HandleRotation()
        {
            if (_mouse.rightButton.isPressed)
            {
                Vector2 delta = _mouse.delta.ReadValue();
                yaw += delta.x * rotateSpeed * 0.1f;
                pitch -= delta.y * rotateSpeed * 0.1f;
                pitch = Mathf.Clamp(pitch, 5f, 89f);
            }
        }

        private void ApplyCameraTransform()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 offset = rotation * new Vector3(0f, 0f, -currentDistance);
            transform.position = _focusPoint + offset;
            transform.LookAt(_focusPoint);
        }
    }
}
