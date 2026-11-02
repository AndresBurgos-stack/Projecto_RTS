using Unity.Cinemachine;
using UnityEngine;

namespace ProjectoRTS.CameraControl
{
    /// <summary>
    /// RTS camera that starts looking at the Pantheon and lets the player turn
    /// and tilt the view in place.
    /// The camera never orbits: CinemachinePanTilt rotates the virtual camera on
    /// its own axis, so tilting behaves like raising or lowering your gaze and
    /// turning behaves like looking left or right from the same spot.
    /// Panning is relative to the current heading: W always moves the camera
    /// away from the viewer and D always moves it to the right, no matter which
    /// way the view is facing.
    /// Pitch is clamped so the view cannot flip upside down or stare at the sky.
    /// Yaw is free and wraps a full 360 degrees.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RtsCameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CinemachineCamera virtualCamera;
        [SerializeField] private CinemachinePanTilt panTilt;
        [SerializeField] private Transform lookTarget;

        [Header("Pan")]
        [SerializeField] private bool edgeScrollEnabled = true;
        [SerializeField, Min(0f)] private float edgeThickness = 12f;
        [SerializeField, Min(0f)] private float panSpeed = 34f;
        [SerializeField] private bool middleDragEnabled = true;
        [SerializeField, Min(0f)] private float dragSpeed = 0.55f;
        [SerializeField] private bool keyboardEnabled = true;

        [Header("Start")]
        [SerializeField] private Vector3 startPosition = new Vector3(0f, 39.94f, -36f);
        [SerializeField, Min(0f)] private float lookHeight = 2f;
        [SerializeField, Min(0.1f)] private float startPitch = 46.528f;
        [SerializeField] private float startYaw = 0f;

        [Header("Pitch")]
        [SerializeField, Range(1f, 89f)] private float minPitch = 10f;
        [SerializeField, Range(1f, 89f)] private float maxPitch = 85f;
        [SerializeField] private bool keyboardPitchEnabled = true;
        [SerializeField, Min(0f)] private float keyboardPitchSpeed = 55f;

        [Header("Yaw")]
        [SerializeField] private bool rightDragRotateEnabled = true;
        [SerializeField, Min(0f)] private float mouseYawSpeed = 160f;
        [SerializeField, Min(0f)] private float mousePitchSpeed = 110f;
        [SerializeField, Min(0f)] private float keyboardYawSpeed = 80f;

        [Header("Zoom")]
        [SerializeField, Min(0.1f)] private float zoomStep = 3f;
        [SerializeField, Min(0.1f)] private float minHeight = 8f;
        [SerializeField, Min(0.1f)] private float maxHeight = 70f;

        [Header("Limits")]
        [SerializeField] private Vector2 limitMin = new Vector2(-52f, -52f);
        [SerializeField] private Vector2 limitMax = new Vector2(52f, 52f);

        private Vector3 _targetPosition;
        private Vector3 _dragOrigin;
        private bool _dragging;
        private bool _rotating;

        /// <summary>Point on the ground the camera is centred on.</summary>
        public Vector3 TargetPosition => _targetPosition;

        /// <summary>Current pitch in degrees, straight from Cinemachine.</summary>
        public float CurrentPitch => panTilt != null ? panTilt.TiltAxis.Value : 0f;

        /// <summary>Current yaw in degrees, straight from Cinemachine.</summary>
        public float CurrentYaw => panTilt != null ? panTilt.PanAxis.Value : 0f;

        /// <summary>Current camera height above the ground.</summary>
        public float CurrentHeight => virtualCamera != null ? virtualCamera.transform.position.y : 0f;

        private void Awake()
        {
            if (virtualCamera == null)
            {
                virtualCamera = GetComponentInChildren<CinemachineCamera>();
            }

            if (panTilt == null && virtualCamera != null)
            {
                panTilt = virtualCamera.GetComponent<CinemachinePanTilt>();
            }

            if (virtualCamera != null)
            {
                virtualCamera.Follow = null;
                virtualCamera.LookAt = null;
                virtualCamera.transform.position = startPosition;
            }

            if (panTilt != null)
            {
                panTilt.PanAxis.Value = startYaw;
                panTilt.PanAxis.Wrap = true;
                panTilt.PanAxis.Range = new Vector2(0f, 360f);

                panTilt.TiltAxis.Value = Mathf.Clamp(startPitch, minPitch, maxPitch);
                panTilt.TiltAxis.Range = new Vector2(minPitch, maxPitch);
                panTilt.TiltAxis.Wrap = false;
            }

            Vector3 focus = lookTarget != null ? lookTarget.position : Vector3.zero;
            _targetPosition = ClampToLimits(new Vector3(focus.x, 0f, focus.z));
        }

        /// <summary>
        /// Sets the map bounds used to clamp the camera position.
        /// </summary>
        public void SetLimits(Vector2 min, Vector2 max)
        {
            limitMin = min;
            limitMax = max;
            _targetPosition = ClampToLimits(_targetPosition);
        }

        /// <summary>
        /// Enables or disables edge scrolling at runtime.
        /// </summary>
        public void SetEdgeScrollEnabled(bool value)
        {
            edgeScrollEnabled = value;
        }

        /// <summary>
        /// Sets the pitch in degrees, clamped to the configured range.
        /// </summary>
        public void SetPitch(float degrees)
        {
            if (panTilt == null)
            {
                return;
            }

            panTilt.TiltAxis.Value = Mathf.Clamp(degrees, minPitch, maxPitch);
        }

        /// <summary>
        /// Sets the yaw in degrees. Yaw wraps around a full circle.
        /// </summary>
        public void SetYaw(float degrees)
        {
            if (panTilt == null)
            {
                return;
            }

            panTilt.PanAxis.Value = Mathf.Repeat(degrees, 360f);
        }

        /// <summary>
        /// Moves the camera over the given world position, clamped to the map.
        /// </summary>
        public void FocusOn(Vector3 worldPosition)
        {
            if (virtualCamera == null)
            {
                return;
            }

            Vector3 position = virtualCamera.transform.position;
            Vector3 flat = ClampToLimits(new Vector3(worldPosition.x, position.y, worldPosition.z));
            virtualCamera.transform.position = flat;
            _targetPosition = new Vector3(flat.x, 0f, flat.z);
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
                Pan(delta);
            }

            ReadRotation();
            ReadZoom();
        }

        private void Pan(Vector3 delta)
        {
            if (virtualCamera == null)
            {
                return;
            }

            Vector3 position = virtualCamera.transform.position + delta;
            position.x = Mathf.Clamp(position.x, limitMin.x, limitMax.x);
            position.z = Mathf.Clamp(position.z, limitMin.y, limitMax.y);
            virtualCamera.transform.position = position;

            _targetPosition = new Vector3(position.x, 0f, position.z);
        }

        private Vector3 HeadingForward()
        {
            float yawRadians = CurrentYaw * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(yawRadians), 0f, Mathf.Cos(yawRadians));
        }

        private Vector3 HeadingRight()
        {
            Vector3 forward = HeadingForward();
            return new Vector3(forward.z, 0f, -forward.x);
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

            Vector3 input = HeadingForward() * z + HeadingRight() * x;

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

            Vector3 input = HeadingForward() * z + HeadingRight() * x;

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

            // Dragging is applied in screen space, then rotated to the current
            // heading so the map keeps following the mouse after turning.
            Vector3 input = HeadingRight() * -screenDelta.x + HeadingForward() * -screenDelta.y;
            return input * dragSpeed;
        }

        private void ReadRotation()
        {
            if (panTilt == null)
            {
                return;
            }

            float keyboardYaw = 0f;
            if (Input.GetKey(KeyCode.Q))
            {
                keyboardYaw -= 1f;
            }

            if (Input.GetKey(KeyCode.E))
            {
                keyboardYaw += 1f;
            }

            if (Mathf.Abs(keyboardYaw) > 0f)
            {
                panTilt.PanAxis.Value = WrapYaw(
                    panTilt.PanAxis.Value + keyboardYaw * keyboardYawSpeed * Time.deltaTime);
            }

            if (keyboardPitchEnabled)
            {
                float keyboardPitch = 0f;

                if (Input.GetKey(KeyCode.PageDown))
                {
                    keyboardPitch += 1f;
                }

                if (Input.GetKey(KeyCode.PageUp))
                {
                    keyboardPitch -= 1f;
                }

                if (Mathf.Abs(keyboardPitch) > 0f)
                {
                    panTilt.TiltAxis.Value = Mathf.Clamp(
                        panTilt.TiltAxis.Value + keyboardPitch * keyboardPitchSpeed * Time.deltaTime,
                        minPitch,
                        maxPitch);
                }
            }

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
                panTilt.PanAxis.Value = WrapYaw(
                    panTilt.PanAxis.Value + mouseX * mouseYawSpeed * Time.deltaTime);
            }

            if (Mathf.Abs(mouseY) > 0.001f)
            {
                panTilt.TiltAxis.Value = Mathf.Clamp(
                    panTilt.TiltAxis.Value - mouseY * mousePitchSpeed * Time.deltaTime,
                    minPitch,
                    maxPitch);
            }
        }

        private void ReadZoom()
        {
            if (virtualCamera == null)
            {
                return;
            }

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 0.01f)
            {
                return;
            }

            // Dolly along the current view direction: the camera moves on the
            // line it is looking along, so zooming keeps both the pitch and the
            // heading instead of changing the distance to a pivot.
            float yawRadians = CurrentYaw * Mathf.Deg2Rad;
            float pitchRadians = CurrentPitch * Mathf.Deg2Rad;

            Vector3 forward = new Vector3(
                Mathf.Sin(yawRadians) * Mathf.Cos(pitchRadians),
                -Mathf.Sin(pitchRadians),
                Mathf.Cos(yawRadians) * Mathf.Cos(pitchRadians));

            Vector3 position = virtualCamera.transform.position;
            Vector3 next = position - forward * (scroll * zoomStep);
            next.y = Mathf.Clamp(next.y, minHeight, maxHeight);
            next.x = Mathf.Clamp(next.x, limitMin.x, limitMax.x);
            next.z = Mathf.Clamp(next.z, limitMin.y, limitMax.y);
            virtualCamera.transform.position = next;

            _targetPosition = new Vector3(next.x, 0f, next.z);
        }

        private static float WrapYaw(float degrees)
        {
            return Mathf.Repeat(degrees, 360f);
        }

        private Vector3 ClampToLimits(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, limitMin.x, limitMax.x);
            position.z = Mathf.Clamp(position.z, limitMin.y, limitMax.y);
            return position;
        }
    }
}