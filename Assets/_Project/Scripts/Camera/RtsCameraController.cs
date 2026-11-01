using Unity.Cinemachine;
using UnityEngine;

namespace ProjectoRTS.CameraControl
{
    /// <summary>
    /// Free city-builder style RTS camera driven through Cinemachine.
    /// The script never moves the Camera transform directly: it places a
    /// follow target on the ground plane and writes the FollowOffset of the
    /// CinemachineFollow component to apply zoom, pitch and yaw.
    /// Pitch is clamped so the view cannot flip upside down or stare at the
    /// sky; yaw is unbounded and can spin a full 360 degrees.
    /// Panning: edge scroll, middle mouse drag or WASD.
    /// Rotation: right mouse drag, both axes at the same time.
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

        [Header("Rotation")]
        [SerializeField, Range(15f, 89f)] private float startPitch = 52f;
        [SerializeField, Range(10f, 89f)] private float minPitch = 25f;
        [SerializeField, Range(10f, 89f)] private float maxPitch = 85f;
        [SerializeField] private float startYaw = 0f;
        [SerializeField] private bool rightDragRotateEnabled = true;
        [SerializeField, Min(0f)] private float mouseRotationSpeed = 220f;
        [SerializeField, Min(0f)] private float keyboardRotationSpeed = 90f;
        [SerializeField, Min(0f)] private float rotationSmoothing = 14f;
        [SerializeField, Min(0f)] private float targetHeight = 2f;

        [Header("Limits")]
        [SerializeField] private Vector2 limitMin = new Vector2(-52f, -52f);
        [SerializeField] private Vector2 limitMax = new Vector2(52f, 52f);

        private Vector3 _targetPosition;
        private float _targetDistance;
        private float _currentDistance;
        private float _pitch;
        private float _targetPitch;
        private float _yaw;
        private float _targetYaw;
        private Vector3 _dragOrigin;
        private bool _dragging;
        private bool _rotating;

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

        /// <summary>Current horizontal angle in degrees, wrapped to -180..180.</summary>
        public float CurrentYaw => _yaw;

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
            // looking. It is not a tracked target and it is not an orbit pivot.
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
            _yaw = WrapAngle(startYaw);
            _targetYaw = _yaw;

            if (virtualCamera != null && followTarget != null)
            {
                virtualCamera.Follow = followTarget;
                virtualCamera.LookAt = followTarget;
            }

            ApplyPosition(_targetPosition);
            ApplyDistance(_currentDistance);
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
            ApplyDistance(_currentDistance);
        }

        /// <summary>
        /// Sets the horizontal angle in degrees. Yaw is unbounded and wraps
        /// around a full circle.
        /// </summary>
        public void SetYaw(float degrees)
        {
            _targetYaw = WrapAngle(degrees);
            _yaw = _targetYaw;
            ApplyDistance(_currentDistance);
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
            ReadRotation();
            SmoothZoom();
            SmoothRotation();

            ApplyPosition(_targetPosition);
            ApplyDistance(_currentDistance);
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

            if (x == 0f && z == 0f)
            {
                return Vector3.zero;
            }

            // Pan relative to the current yaw so W always moves away from the
            // camera regardless of where the view is pointing.
            float yawRadians = _yaw * Mathf.Deg2Rad;
            Vector3 forward = new Vector3(Mathf.Sin(yawRadians), 0f, Mathf.Cos(yawRadians));
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            Vector3 input = forward * z + right * x;

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

            if (x == 0f && z == 0f)
            {
                return Vector3.zero;
            }

            float yawRadians = _yaw * Mathf.Deg2Rad;
            Vector3 forward = new Vector3(Mathf.Sin(yawRadians), 0f, Mathf.Cos(yawRadians));
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            Vector3 input = forward * z + right * x;

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

        private void ReadRotation()
        {
            if (!rightDragRotateEnabled)
            {
                return;
            }

            if (Input.GetMouseButtonDown(1))
            {
                _rotating = true;
            }

            if (Input.GetMouseButtonUp(1))
            {
                _rotating = false;
            }

            if (!_rotating)
            {
                return;
            }

            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            if (Mathf.Abs(mouseX) > 0.001f)
            {
                _targetYaw = WrapAngle(_targetYaw + mouseX * mouseRotationSpeed * Time.deltaTime);
            }

            if (Mathf.Abs(mouseY) > 0.001f)
            {
                _targetPitch = Mathf.Clamp(
                    _targetPitch - mouseY * mouseRotationSpeed * 0.6f * Time.deltaTime,
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

        private void SmoothRotation()
        {
            if (rotationSmoothing > 0f)
            {
                float t = 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime);
                _pitch = Mathf.Lerp(_pitch, _targetPitch, t);
                _yaw = Mathf.LerpAngle(_yaw, _targetYaw, t);
            }
            else
            {
                _pitch = _targetPitch;
                _yaw = _targetYaw;
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

        private void ApplyDistance(float distance)
        {
            if (follow == null)
            {
                return;
            }

            // The offset is expressed in world space: pitch decides the height
            // and yaw spins that inclined axis around the target. Pitch is
            // clamped, yaw is free and wraps.
            float pitchRadians = _pitch * Mathf.Deg2Rad;
            float yawRadians = _yaw * Mathf.Deg2Rad;
            float safeDistance = Mathf.Max(0.1f, Mathf.Abs(distance));
            float horizontal = safeDistance * Mathf.Cos(pitchRadians);

            Vector3 offset = follow.FollowOffset;
            offset.y = safeDistance * Mathf.Sin(pitchRadians);
            offset.x = horizontal * Mathf.Sin(yawRadians);
            offset.z = -horizontal * Mathf.Cos(yawRadians);
            follow.FollowOffset = offset;
        }

        private static float WrapAngle(float degrees)
        {
            return Mathf.Repeat(degrees + 180f, 360f) - 180f;
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