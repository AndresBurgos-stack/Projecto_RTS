using Unity.Cinemachine;
using UnityEngine;

namespace ProjectoRTS.CameraControl
{
    /// <summary>
    /// RTS camera rig driven through Cinemachine. The script never moves the
    /// Camera transform directly: it moves a follow target on the ground plane
    /// and changes the FollowOffset of the CinemachineFollow component to zoom
    /// and to tilt the view from flat to almost top-down.
    /// There is no horizontal yaw control: the only rotation the player can
    /// drive is the vertical pitch.
    /// The camera starts looking toward the Pantheon, but the Pantheon is only
    /// used to place the follow point once at startup. It is not a dynamic
    /// Look At target and it does not act as an orbit pivot.
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
        [SerializeField, Min(0.1f)] private float maxDistance = 65f;
        [SerializeField, Min(0f)] private float zoomSmoothing = 12f;
        [SerializeField, Min(0.1f)] private float startDistance = 34f;

        [Header("Tilt")]
        [SerializeField, Range(15f, 89f)] private float startPitch = 55f;
        [SerializeField, Range(15f, 89f)] private float minPitch = 25f;
        [SerializeField, Range(15f, 89f)] private float maxPitch = 89f;
        [SerializeField, Min(0f)] private float keyboardTiltSpeed = 55f;
        [SerializeField, Min(0f)] private float dragTiltSpeed = 0.28f;
        [SerializeField, Min(0f)] private float tiltSmoothing = 10f;
        [SerializeField] private bool rightDragTiltEnabled = true;
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
        private bool _tilting;

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

            // The follow point is the only thing the camera tracks. The Pantheon
            // is used once here to decide where the camera starts looking, and
            // is then ignored so panning and tilting stay independent.
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
            ReadTilt();
            SmoothZoom();
            SmoothTilt();

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

        private void ReadTilt()
        {
            float keyboard = 0f;
            if (Input.GetKey(KeyCode.Q))
            {
                keyboard -= 1f;
            }

            if (Input.GetKey(KeyCode.E))
            {
                keyboard += 1f;
            }

            if (Mathf.Abs(keyboard) > 0f)
            {
                _targetPitch = Mathf.Clamp(
                    _targetPitch + keyboard * keyboardTiltSpeed * Time.deltaTime,
                    minPitch,
                    maxPitch);
            }

            if (!rightDragTiltEnabled)
            {
                return;
            }

            if (Input.GetMouseButtonDown(1))
            {
                _tilting = true;
            }

            if (Input.GetMouseButtonUp(1))
            {
                _tilting = false;
            }

            if (!_tilting)
            {
                return;
            }

            float mouseY = Input.GetAxis("Mouse Y");
            if (Mathf.Abs(mouseY) > 0.001f)
            {
                _targetPitch = Mathf.Clamp(
                    _targetPitch + mouseY * dragTiltSpeed * 60f * Time.deltaTime,
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

        private void SmoothTilt()
        {
            if (tiltSmoothing > 0f)
            {
                _pitch = Mathf.Lerp(
                    _pitch,
                    _targetPitch,
                    1f - Mathf.Exp(-tiltSmoothing * Time.deltaTime));
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

        private void ApplyDistance(float distance)
        {
            if (follow == null)
            {
                return;
            }

            // No yaw is applied: the camera only changes how much it looks down.
            // The horizontal component stays on the same world axis, so the view
            // never rotates left or right on its own.
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