using System;
using System.Collections.Generic;
using ProjectoRTS.Economy;
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
        [SerializeField] private bool continuousPlacement = false;

        [SerializeField] private bool dragPlacementEnabled = true;

        private readonly List<GameObject> _dragPreviews = new List<GameObject>(16);
        private readonly List<Vector2Int> _dragCells = new List<Vector2Int>(16);
        private bool _dragging;
        private Vector2Int _dragStartCell;

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
        private float _placementRotation;

        /// <summary>Raised after a building is successfully placed.</summary>
        public event Action<GameObject, BuildingDefinition> BuildingPlaced;

        public bool IsPlacing => _definition != null;

        /// <summary>When enabled, the placement preview stays active after a
        /// successful placement without needing to hold Shift.</summary>
        public bool ContinuousPlacement
        {
            get => continuousPlacement;
            set => continuousPlacement = value;
        }
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
            _dragging = false;
            _placementRotation = 0f;
            _dragCells.Clear();
            ClearDragPreviews();
        }

        private bool CanRotate()
        {
            return _definition != null &&
                _definition.Footprint.x == _definition.Footprint.y;
        }

        private Quaternion PlacementRotation =>
            CanRotate() ? Quaternion.Euler(0f, _placementRotation, 0f) : Quaternion.identity;

        private void HandleInput()
        {
            bool shiftHeld = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && CanRotate())
            {
                _placementRotation = (_placementRotation + 90f) % 360f;
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (!IsPointerOverUI())
                {
                    if (dragPlacementEnabled && shiftHeld && TryGetGroundPoint(out Vector3 startPoint))
                    {
                        BeginDrag(startPoint);
                    }
                    else
                    {
                        ConfirmPlacement();
                    }
                }

                return;
            }

            if (_dragging && Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                if (TryGetGroundPoint(out Vector3 groundPoint))
                {
                    UpdateDrag(groundPoint);
                }

                return;
            }

            if (_dragging && Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                CommitDrag();
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

            if (!PlayerEconomy.TrySpend(_definition.WoodCost, _definition.BronzeCost))
            {
                return;
            }

            if (!grid.TryPlace(_definition, _originCell))
            {
                PlayerEconomy.Refund(_definition.WoodCost, _definition.BronzeCost);
                return;
            }

            GameObject instance = SpawnBuilding(_definition, _originCell);
            if (instance == null)
            {
                PlayerEconomy.Refund(_definition.WoodCost, _definition.BronzeCost);
                return;
            }

            BuildingPlaced?.Invoke(instance, _definition);

            bool keepPlacing = continuousPlacement
                || (keepPlacingWithShift
                    && Keyboard.current != null
                    && Keyboard.current.shiftKey.isPressed);

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
                ? Instantiate(definition.Prefab, position, PlacementRotation, buildingParent)
                : Instantiate(definition.Prefab, position, PlacementRotation);

            instance.name = definition.DisplayName;

            BuildingRuntime runtime = instance.AddComponent<BuildingRuntime>();
            runtime.Configure(definition);

            ConstructionSite site = instance.AddComponent<ConstructionSite>();
            site.BeginConstruction();

            if (instance.GetComponent<StructureHealthBar>() == null)
            {
                instance.AddComponent<StructureHealthBar>();
            }

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
            _canPlace = grid.CanPlace(_definition, originCell) &&
                PlayerEconomy.CanAfford(_definition.WoodCost, _definition.BronzeCost);

            Vector3 position = grid.FootprintCenterWorld(originCell, footprint);
            position.y = grid.Origin.y + _bottomOffset;
            _preview.transform.SetPositionAndRotation(position, PlacementRotation);

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

        private void BeginDrag(Vector3 groundPoint)
        {
            if (_definition == null || grid == null)
            {
                return;
            }

            Vector2Int cursorCell = grid.WorldToCell(groundPoint);
            Vector2Int footprint = _definition.Footprint;
            _dragStartCell = centerOnCursor
                ? grid.FootprintOriginFromCenter(cursorCell, footprint)
                : cursorCell;
            _dragging = true;
            ClearDragPreviews();
            UpdateDrag(groundPoint);
        }

        private void UpdateDrag(Vector3 groundPoint)
        {
            if (_definition == null || grid == null)
            {
                return;
            }

            Vector2Int cursorCell = grid.WorldToCell(groundPoint);
            Vector2Int footprint = _definition.Footprint;
            Vector2Int endCell = centerOnCursor
                ? grid.FootprintOriginFromCenter(cursorCell, footprint)
                : cursorCell;

            _dragCells.Clear();
            CollectLine(_dragStartCell, endCell, _dragCells);

            while (_dragPreviews.Count < _dragCells.Count)
            {
                GameObject ghost = Instantiate(_definition.Prefab, Vector3.zero, Quaternion.identity);
                ghost.name = _definition.DisplayName + " (Drag Preview)";
                PreviewGhost preview = ghost.AddComponent<PreviewGhost>();
                preview.Initialize(_validMaterial);
                ghost.SetActive(false);
                _dragPreviews.Add(ghost);
            }

            for (int i = 0; i < _dragPreviews.Count; i++)
            {
                GameObject ghost = _dragPreviews[i];
                if (ghost == null)
                {
                    continue;
                }

                if (i >= _dragCells.Count)
                {
                    if (ghost.activeSelf)
                    {
                        ghost.SetActive(false);
                    }

                    continue;
                }

                Vector2Int origin = _dragCells[i];
                bool canPlace = grid.CanPlace(_definition, origin) &&
                    PlayerEconomy.CanAfford(_definition.WoodCost, _definition.BronzeCost);
                Vector3 position = grid.FootprintCenterWorld(origin, footprint);
                position.y = grid.Origin.y + _bottomOffset;
                ghost.transform.SetPositionAndRotation(position, PlacementRotation);

                PreviewGhost preview = ghost.GetComponent<PreviewGhost>();
                if (preview != null)
                {
                    preview.SetMaterial(canPlace ? _validMaterial : _invalidMaterial);
                }

                if (!ghost.activeSelf)
                {
                    ghost.SetActive(true);
                }
            }
        }

        private void CommitDrag()
        {
            _dragging = false;
            ClearDragPreviews();

            if (_definition == null || grid == null)
            {
                CancelPlacement();
                return;
            }

            for (int i = 0; i < _dragCells.Count; i++)
            {
                Vector2Int origin = _dragCells[i];

                if (!grid.CanPlace(_definition, origin))
                {
                    continue;
                }

                if (!PlayerEconomy.TrySpend(_definition.WoodCost, _definition.BronzeCost))
                {
                    break;
                }

                if (!grid.TryPlace(_definition, origin))
                {
                    PlayerEconomy.Refund(_definition.WoodCost, _definition.BronzeCost);
                    continue;
                }

                GameObject instance = SpawnBuilding(_definition, origin);
                if (instance == null)
                {
                    PlayerEconomy.Refund(_definition.WoodCost, _definition.BronzeCost);
                    continue;
                }

                BuildingPlaced?.Invoke(instance, _definition);
            }

            _dragCells.Clear();
            CancelPlacement();
        }

        private void ClearDragPreviews()
        {
            for (int i = 0; i < _dragPreviews.Count; i++)
            {
                if (_dragPreviews[i] != null)
                {
                    Destroy(_dragPreviews[i]);
                }
            }

            _dragPreviews.Clear();
        }

        private static void CollectLine(Vector2Int start, Vector2Int end, List<Vector2Int> output)
        {
            int x0 = start.x;
            int y0 = start.y;
            int x1 = end.x;
            int y1 = end.y;

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                output.Add(new Vector2Int(x0, y0));

                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                int e2 = err * 2;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private void OnValidate()
        {
            maxRayDistance = Mathf.Max(1f, maxRayDistance);
        }
    }
}
