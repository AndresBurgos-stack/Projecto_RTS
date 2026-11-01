using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace ProjectoRTS.Buildings
{
    /// <summary>
    /// Previews, validates and confirms building placement on a
    /// <see cref="BuildGrid"/> using a camera raycast.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingPlacementController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BuildGrid grid;
        [SerializeField] private Camera raycastCamera;
        [SerializeField] private Transform buildingParent;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Preview")]
        [SerializeField] private Material validPreviewMaterial;
        [SerializeField] private Material invalidPreviewMaterial;
        [SerializeField] private Color validPreviewColor = new Color(0.2f, 0.9f, 0.3f, 0.45f);
        [SerializeField] private Color invalidPreviewColor = new Color(0.95f, 0.2f, 0.15f, 0.45f);
        [SerializeField] private bool alignToGround = true;

        [Header("Input")]
        [SerializeField, Min(1f)] private float maxRayDistance = 500f;
        [SerializeField] private bool centerOnCursor = true;
        [SerializeField] private bool cancelWithRightClick = true;
        [SerializeField] private bool keepPlacingWithShift = true;

        private BuildingDefinition _definition;
        private GameObject _preview;
        private PreviewGhost _ghost;
        private Material _validMaterial;
        private Material _invalidMaterial;
        private bool _ownsValidMaterial;
        private bool _ownsInvalidMaterial;
        private float _bottomOffset;
        private Vector2Int _originCell;
        private bool _canPlace;
        private int _startFrame;

        /// <summary>Raised after a building is successfully placed.</summary>
        public event Action<GameObject, BuildingDefinition> BuildingPlaced;

        public bool IsPlacing => _definition != null;
        public BuildingDefinition CurrentDefinition => _definition;

        private void Awake()
        {
            if (grid == null)
            {
                grid = FindFirstObjectByType<BuildGrid>();
            }

            if (raycastCamera == null)
            {
                raycastCamera = Camera.main;
            }

            EnsurePreviewMaterials();
        }

        private void OnDestroy()
        {
            CancelPlacement();

            if (_ownsValidMaterial && _validMaterial != null)
            {
                Destroy(_validMaterial);
            }

            if (_ownsInvalidMaterial && _invalidMaterial != null)
            {
                Destroy(_invalidMaterial);
            }
        }

        private void Update()
        {
            if (_definition == null)
            {
                return;
            }

            if (!TryGetGroundPoint(out Vector3 groundPoint))
            {
                SetPreviewVisible(false);
                return;
            }

            SetPreviewVisible(true);
            UpdatePreview(groundPoint);

            if (Time.frameCount == _startFrame)
            {
                return;
            }

            HandleInput();
        }

        /// <summary>Starts placing the supplied building definition.</summary>
        public void BeginPlacement(BuildingDefinition definition)
        {
            if (definition == null || definition.Prefab == null)
            {
                Debug.LogWarning("BuildingPlacementController: the definition or its prefab is missing.", this);
                return;
            }

            if (grid == null)
            {
                Debug.LogError("BuildingPlacementController: no BuildGrid assigned or found.", this);
                return;
            }

            CancelPlacement();
            EnsurePreviewMaterials();

            _definition = definition;
            _startFrame = Time.frameCount;

            _preview = Instantiate(definition.Prefab, Vector3.zero, Quaternion.identity);
            _preview.name = $"{definition.DisplayName} (Preview)";
            _ghost = _preview.AddComponent<PreviewGhost>();
            _ghost.Initialize(_validMaterial);
            _bottomOffset = alignToGround ? ComputeBottomOffset(_preview) : 0f;
            _preview.SetActive(false);
        }

        /// <summary>Cancels the current placement and destroys the preview.</summary>
        public void CancelPlacement()
        {
            if (_preview != null)
            {
                Destroy(_preview);
            }

            _preview = null;
            _ghost = null;
            _definition = null;
            _canPlace = false;
        }

        private void HandleInput()
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (!IsPointerOverUI())
                {
                    ConfirmPlacement();
                }
                return;
            }

            if (cancelWithRightClick && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                CancelPlacement();
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelPlacement();
            }
        }

        private void ConfirmPlacement()
        {
            if (!_canPlace || _definition == null || grid == null)
            {
                return;
            }

            if (!grid.TryPlace(_definition, _originCell))
            {
                return;
            }

            GameObject instance = SpawnBuilding(_definition, _originCell);
            if (instance != null)
            {
                BuildingPlaced?.Invoke(instance, _definition);
            }

            bool keepPlacing = keepPlacingWithShift
                && Keyboard.current != null
                && Keyboard.current.shiftKey.isPressed;

            if (!keepPlacing)
            {
                CancelPlacement();
            }
        }

        private GameObject SpawnBuilding(BuildingDefinition definition, Vector2Int originCell)
        {
            if (definition.Prefab == null)
            {
                return null;
            }

            Vector3 position = grid.FootprintCenterWorld(originCell, definition.Footprint);
            position.y = grid.Origin.y + _bottomOffset;

            GameObject instance = buildingParent != null
                ? Instantiate(definition.Prefab, position, Quaternion.identity, buildingParent)
                : Instantiate(definition.Prefab, position, Quaternion.identity);

            instance.name = definition.DisplayName;
            return instance;
        }

        private void UpdatePreview(Vector3 groundPoint)
        {
            Vector2Int cursorCell = grid.WorldToCell(groundPoint);
            Vector2Int footprint = _definition.Footprint;
            Vector2Int originCell = centerOnCursor
                ? grid.FootprintOriginFromCenter(cursorCell, footprint)
                : cursorCell;

            _originCell = originCell;
            _canPlace = grid.CanPlace(_definition, originCell);

            Vector3 position = grid.FootprintCenterWorld(originCell, footprint);
            position.y = grid.Origin.y + _bottomOffset;
            _preview.transform.SetPositionAndRotation(position, Quaternion.identity);

            _ghost.SetMaterial(_canPlace ? _validMaterial : _invalidMaterial);
        }

        private bool TryGetGroundPoint(out Vector3 groundPoint)
        {
            groundPoint = default;
            if (raycastCamera == null || Mouse.current == null)
            {
                return false;
            }

            Ray ray = raycastCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                groundPoint = hit.point;
                return true;
            }

            float groundY = grid != null ? grid.Origin.y : 0f;
            Plane plane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
            if (plane.Raycast(ray, out float distance))
            {
                groundPoint = ray.GetPoint(distance);
                return true;
            }

            return false;
        }

        private void SetPreviewVisible(bool visible)
        {
            if (_preview != null && _preview.activeSelf != visible)
            {
                _preview.SetActive(visible);
            }
        }

        private static float ComputeBottomOffset(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return 0f;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return -bounds.min.y;
        }

        private void EnsurePreviewMaterials()
        {
            if (validPreviewMaterial == null && _validMaterial == null)
            {
                _validMaterial = CreateGhostMaterial("Valid Placement Preview", validPreviewColor);
                _ownsValidMaterial = true;
            }
            else if (validPreviewMaterial != null)
            {
                _validMaterial = validPreviewMaterial;
            }

            if (invalidPreviewMaterial == null && _invalidMaterial == null)
            {
                _invalidMaterial = CreateGhostMaterial("Invalid Placement Preview", invalidPreviewColor);
                _ownsInvalidMaterial = true;
            }
            else if (invalidPreviewMaterial != null)
            {
                _invalidMaterial = invalidPreviewMaterial;
            }
        }

        private static Material CreateGhostMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                Debug.LogWarning("BuildingPlacementController: no shader found for the placement preview.");
                return null;
            }

            Material material = new Material(shader)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void OnValidate()
        {
            maxRayDistance = Mathf.Max(1f, maxRayDistance);
        }
    }
}
