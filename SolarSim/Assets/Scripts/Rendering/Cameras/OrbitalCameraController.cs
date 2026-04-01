using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceSim.Rendering.Cameras
{
    /// <summary>
    /// Minimal orbital camera controller for the sandbox view.
    /// Supports pan, zoom, focus on target, and smooth focus transitions.
    ///
    /// maxDistance increased to 8000 to accommodate real-scale solar system
    /// with DistanceScale=0.0002 (Pluto ~1181 scene units from Sol).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class OrbitalCameraController : MonoBehaviour
    {
        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 50f;
        [SerializeField] private float minDistance = 0.1f;
        [SerializeField] private float maxDistance = 8000f;

        [Header("Pan")]
        [SerializeField] private float panSpeed = 0.5f;

        [Header("Rotation")]
        [SerializeField] private float rotateSpeed = 2f;

        [Header("Smooth Focus")]
        [SerializeField] private float focusLerpSpeed = 20f;

        [Header("State")]
        [SerializeField] private float currentDistance = 80f;
        [SerializeField] private float pitch = 45f;
        [SerializeField] private float yaw = 0f;

        private Vector3 _focusPoint = Vector3.zero;
        private Transform _focusTarget;
        private Mouse _mouse;

        private bool _isSmoothFocusing;
        private Vector3 _smoothTargetPoint;

        public float CurrentDistance => currentDistance;
        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;

        public bool BlockInput { get; set; }

        public void SetFocusTarget(Transform target)
        {
            _focusTarget = target;
            if (target != null) _focusPoint = target.position;
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

        private void OnEnable() { _mouse = Mouse.current; }

        private void LateUpdate()
        {
            _mouse = Mouse.current;
            if (_mouse == null) return;

            if (_focusTarget != null) _smoothTargetPoint = _focusTarget.position;

            if (_isSmoothFocusing)
            {
                _focusPoint = Vector3.Lerp(_focusPoint, _smoothTargetPoint, focusLerpSpeed * Time.deltaTime);
                if (Vector3.Distance(_focusPoint, _smoothTargetPoint) < 0.001f)
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
                _focusPoint += transform.right * dx + transform.up * dy;
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
