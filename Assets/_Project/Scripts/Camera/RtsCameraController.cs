using Unity.Cinemachine;
using UnityEngine;

namespace ProjectoRTS.CameraControl
{
    /// <summary>
    /// Angled elevated RTS camera driven through Cinemachine, in the style of
    /// city builders like Diplomacy is Not an Option: you see both the ground
    /// and the facades of the buildings.
    /// The script never moves the Camera transform directly: it places a follow
    /// target on the ground plane and writes the FollowOffset of the
    /// CinemachineFollow component to apply zoom and pitch.
    /// Only the vertical axis (pitch) is rotatable. There is no yaw control, so
    /// the camera never orbits from left to right.
    /// Panning: edge scroll, middle mouse drag or WASD.
    /// Pitch: right mouse drag vertical, or PageUp / PageDown.
    /// Zoom: mouse wheel.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RtsCameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CinemachineCamera virtualCamera;
        [SerializeField] private CinemachineFollow follow;
        [SerializeField] private Transform followTarget;
        [SerializeField] private Transform initialFocus;

        [Header("Pan")]
        [SerializeField] private bool edgeScrollEnabled = true;
        [SerializeField, Min(0f)] private float edgeThickness = 12f;
        [SerializeField, Min(0f)] private float panSpeed = 34f;
        [SerializeField] private bool middleDragEnabled = true;
        [SerializeField, Min(0f)] private float dragSpeed = 0.55f;
        [SerializeField] private bool keyboardEnabled = true;

        [Header("Zoom")]
        [SerializeField, Min(0.1f)] private float zoomStep = 4f;
        [SerializeField, Min(0.1f)] private float minDistance = 14f;
        [SerializeField, Min(0.1f)] private float maxDistance = 70f;
        [SerializeField, Min(0f)] private float zoomSmoothing = 12f;
        [SerializeField, Min(0.1f)] private float startDistance = 38f;

        [Header("Pitch")]
        [SerializeField, Range(15f, 89f)] private float startPitch = 50f;
        [SerializeField, Range(15f, 89f)] private float minPitch = 25f;
        [SerializeField, Range(15f, 89f)] private float maxPitch = 85f;
        [SerializeField] private bool rightDragPitchEnabled = true;
        [SerializeField, Min(0f)] private float mousePitchSpeed = 130f;
        [SerializeField] private bool keyboardPitchEnabled = true;
        [SerializeField, Min(0f)] private float keyboardPitchSpeed = 60f;
        [SerializeField, Min(0f)] private float pitchSmoothing = 14f;
        [SerializeField, Min(0f)] private float targetHeight = 2f;

        [Header("Limits")]
        [SerializeField] private Vector2 limitMin = new Vector2(-52f, -52f);
        [SerializeField] private Vector2 limitMax = new Vector2(52f, 52f);

        private Vector3 _targetPosition;
        private float _targetDistance;
        private float _currentDistance;
        private float _pitch;
        private float _targetPitch;
        private Vector3 _dragOrigin;
        private bool _dragging;
        private bool _pitching;

        /// <summary>Current follow point on the ground plane.</summary>
        public Vector3 TargetPosition => _targetPosition;

        /// <summary>Current distance between the camera and the follow point.</summary>
        public float CurrentDistance => _currentDistance;

        /// <summary>Minimum distance reached when zooming in.</summary>
        public float MinDistance => minDistance;

        /// <summary>Maximum distance reached when zooming out.</summary>
        public float MaxDistance => maxDistance;

        /// <summary>Current vertical tilt in degrees. Higher values look more top-down.</summary>
        public float CurrentPitch => _pitch;

        private void Awake()
        {
            if (virtualCamera == null)
            {
                virtualCamera = GetComponentInChildren<CinemachineCamera>();
            }

            if (follow == null && virtualCamera != null)
            {
                follow = virtualCamera.GetComponent<CinemachineFollow>();
            }

            if (followTarget == null && virtualCamera != null)
            {
                followTarget = virtualCamera.Follow;
            }

            // The Pantheon is only used once to decide where the camera starts
            // looking. It is not a tracked target and it is not an orbit pivot,
            // so the player can tilt freely afterwards.
            if (initialFocus != null)
            {
                _targetPosition = initialFocus.position;
            }
            else if (followTarget != null)
            {
                _targetPosition = followTarget.position;
            }
            else
            {
                _targetPosition = transform.position;
            }

            _targetPosition = ClampToLimits(_targetPosition);
            _targetDistance = Mathf.Clamp(startDistance, minDistance, maxDistance);
            _currentDistance = _targetDistance;
            _pitch = Mathf.Clamp(startPitch, minPitch, maxPitch);
            _targetPitch = _pitch;

            if (virtualCamera != null && followTarget != null)
            {
                virtualCamera.Follow = followTarget;
                virtualCamera.LookAt = followTarget;
            }

            ApplyPosition(_targetPosition);
            ApplyPitch(_currentDistance);
        }

        /// <summary>
        /// Sets the map bounds used to clamp the camera follow point.
        /// </summary>
        public void SetLimits(Vector2 min, Vector2 max)
        {
            limitMin = min;
            limitMax = max;
            _targetPosition = ClampToLimits(_targetPosition);
            ApplyPosition(_targetPosition);
        }

        /// <summary>
        /// Enables or disables edge scrolling at runtime.
        /// </summary>
        public void SetEdgeScrollEnabled(bool value)
        {
            edgeScrollEnabled = value;
        }

        /// <summary>
        /// Sets the vertical tilt in degrees, clamped to the configured range.
        /// </summary>
        public void SetPitch(float degrees)
        {
            _targetPitch = Mathf.Clamp(degrees, minPitch, maxPitch);
            _pitch = _targetPitch;
            ApplyPitch(_currentDistance);
        }

        /// <summary>
        /// Moves the camera to look at the given world position, clamped to the
        /// map limits. This is a one time placement, not a tracked target.
        /// </summary>
        public void FocusOn(Vector3 worldPosition)
        {
            _targetPosition = ClampToLimits(new Vector3(worldPosition.x, 0f, worldPosition.z));
            ApplyPosition(_targetPosition);
        }

        private void Update()
        {
            Vector3 delta = Vector3.zero;

            if (keyboardEnabled)
            {
                delta += ReadKeyboardPan();
            }

            if (edgeScrollEnabled)
            {
                delta += ReadEdgePan();
            }

            if (middleDragEnabled)
            {
                delta += ReadMiddleDrag();
            }

            if (delta.sqrMagnitude > 0f)
            {
                _targetPosition = ClampToLimits(_targetPosition + delta);
            }

            ReadZoom();
            ReadPitch();
            SmoothZoom();
            SmoothPitch();

            ApplyPosition(_targetPosition);
            ApplyPitch(_currentDistance);
        }

        private Vector3 ReadKeyboardPan()
        {
            float x = 0f;
            float z = 0f;

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                x -= 1f;
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                x += 1f;
            }

            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                z -= 1f;
            }

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                z += 1f;
            }

            Vector3 input = new Vector3(x, 0f, z);
            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            return input * (panSpeed * Time.deltaTime);
        }

        private Vector3 ReadEdgePan()
        {
            Vector3 mouse = Input.mousePosition;
            if (mouse.x < 0f || mouse.y < 0f || mouse.x > Screen.width || mouse.y > Screen.height)
            {
                return Vector3.zero;
            }

            float x = 0f;
            float z = 0f;

            if (mouse.x <= edgeThickness)
            {
                x -= 1f;
            }
            else if (mouse.x >= Screen.width - edgeThickness)
            {
                x += 1f;
            }

            if (mouse.y <= edgeThickness)
            {
                z -= 1f;
            }
            else if (mouse.y >= Screen.height - edgeThickness)
            {
                z += 1f;
            }

            Vector3 input = new Vector3(x, 0f, z);
            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            return input * (panSpeed * Time.deltaTime);
        }

        private Vector3 ReadMiddleDrag()
        {
            if (Input.GetMouseButtonDown(2))
            {
                _dragging = true;
                _dragOrigin = Input.mousePosition;
            }

            if (Input.GetMouseButtonUp(2))
            {
                _dragging = false;
            }

            if (!_dragging)
            {
                return Vector3.zero;
            }

            Vector3 current = Input.mousePosition;
            Vector3 screenDelta = current - _dragOrigin;
            _dragOrigin = current;

            float scale = dragSpeed * (_currentDistance / Mathf.Max(1f, maxDistance));
            return new Vector3(-screenDelta.x, 0f, -screenDelta.y) * scale;
        }

        private void ReadZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 0.01f)
            {
                return;
            }

            _targetDistance = Mathf.Clamp(
                _targetDistance - scroll * zoomStep,
                minDistance,
                maxDistance);
        }

        private void ReadPitch()
        {
            float keyboard = 0f;
            if (keyboardPitchEnabled)
            {
                if (Input.GetKey(KeyCode.PageDown))
                {
                    keyboard += 1f;
                }

                if (Input.GetKey(KeyCode.PageUp))
                {
                    keyboard -= 1f;
                }

                if (Mathf.Abs(keyboard) > 0f)
                {
                    _targetPitch = Mathf.Clamp(
                        _targetPitch + keyboard * keyboardPitchSpeed * Time.deltaTime,
                        minPitch,
                        maxPitch);
                }
            }

            if (!rightDragPitchEnabled)
            {
                return;
            }

            if (Input.GetMouseButtonDown(1))
            {
                _pitching = true;
            }

            if (Input.GetMouseButtonUp(1))
            {
                _pitching = false;
            }

            if (!_pitching)
            {
                return;
            }

            float mouseY = Input.GetAxis("Mouse Y");
            if (Mathf.Abs(mouseY) > 0.001f)
            {
                _targetPitch = Mathf.Clamp(
                    _targetPitch - mouseY * mousePitchSpeed * Time.deltaTime,
                    minPitch,
                    maxPitch);
            }
        }

        private void SmoothZoom()
        {
            if (zoomSmoothing > 0f)
            {
                _currentDistance = Mathf.Lerp(
                    _currentDistance,
                    _targetDistance,
                    1f - Mathf.Exp(-zoomSmoothing * Time.deltaTime));
            }
            else
            {
                _currentDistance = _targetDistance;
            }
        }

        private void SmoothPitch()
        {
            if (pitchSmoothing > 0f)
            {
                _pitch = Mathf.Lerp(
                    _pitch,
                    _targetPitch,
                    1f - Mathf.Exp(-pitchSmoothing * Time.deltaTime));
            }
            else
            {
                _pitch = _targetPitch;
            }
        }

        private void ApplyPosition(Vector3 position)
        {
            if (followTarget == null)
            {
                return;
            }

            followTarget.position = new Vector3(position.x, targetHeight, position.z);
        }

        private void ApplyPitch(float distance)
        {
            if (follow == null)
            {
                return;
            }

            // Only the vertical axis moves. The horizontal component always
            // points along the same world axis, so the view never rotates left
            // or right on its own.
            float pitchRadians = _pitch * Mathf.Deg2Rad;
            float safeDistance = Mathf.Max(0.1f, Mathf.Abs(distance));
            float horizontal = safeDistance * Mathf.Cos(pitchRadians);

            Vector3 offset = follow.FollowOffset;
            offset.x = 0f;
            offset.y = safeDistance * Mathf.Sin(pitchRadians);
            offset.z = -horizontal;
            follow.FollowOffset = offset;
        }

        private Vector3 ClampToLimits(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, limitMin.x, limitMax.x);
            position.z = Mathf.Clamp(position.z, limitMin.y, limitMax.y);
            position.y = 0f;
            return position;
        }
    }
}