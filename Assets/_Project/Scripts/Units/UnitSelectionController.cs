using System.Collections.Generic;
using ProjectoRTS.Buildings;
using ProjectoRTS.Combat;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectoRTS.Units
{
    /// <summary>
    /// Selects friendly units with click or drag, moves them with a right
    /// click and issues attack orders when the right click hits an enemy.
    /// A drag box is drawn over the recruitment canvas.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitSelectionController : MonoBehaviour
    {
        private const int RaycastBufferSize = 16;

        [Header("References")]
        [SerializeField] private Camera raycastCamera;
        [SerializeField] private BuildingPlacementController placement;

        [Header("Selection")]
        [SerializeField] private LayerMask selectionMask = ~0;
        [SerializeField, Min(1f)] private float maxRayDistance = 500f;
        [SerializeField, Min(0f)] private float dragThreshold = 8f;
        [SerializeField, Min(0.1f)] private float formationSpacing = 0.9f;

        [Header("Selection Box")]
        [SerializeField] private Color selectionBoxColor = new Color(0.25f, 0.8f, 1f, 0.18f);

        private readonly List<UnitMovement> _selected = new List<UnitMovement>();
        private readonly RaycastHit[] _raycastHits = new RaycastHit[RaycastBufferSize];
        private Image _selectionBox;
        private RectTransform _canvasRect;
        private Vector2 _dragStart;
        private bool _dragging;
        private bool _dragMoved;
        private bool _wasPlacing;

        public IReadOnlyList<UnitMovement> SelectedUnits => _selected;

        private void Awake()
        {
            if (raycastCamera == null)
            {
                raycastCamera = Camera.main;
            }

            if (placement == null)
            {
                placement = FindFirstObjectByType<BuildingPlacementController>();
            }

            CreateSelectionBox();
        }

        private void OnDestroy()
        {
            if (_selectionBox != null)
            {
                Destroy(_selectionBox.gameObject);
            }
        }

        private void Update()
        {
            if (placement != null && placement.IsPlacing)
            {
                if (!_wasPlacing)
                {
                    CancelDrag();
                    ClearSelection();
                }

                _wasPlacing = true;
                return;
            }

            _wasPlacing = false;
            PruneSelection();

            if (Mouse.current == null)
            {
                return;
            }

            Vector2 mousePosition = Mouse.current.position.ReadValue();

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelDrag();
                ClearSelection();
            }

            if (Mouse.current.leftButton.wasPressedThisFrame && !IsPointerOverUI())
            {
                _dragStart = mousePosition;
                _dragging = true;
                _dragMoved = false;
            }

            if (_dragging && Mouse.current.leftButton.isPressed)
            {
                if ((mousePosition - _dragStart).sqrMagnitude >= dragThreshold * dragThreshold)
                {
                    _dragMoved = true;
                    UpdateSelectionBox(_dragStart, mousePosition);
                }
            }

            if (_dragging && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                _dragging = false;
                HideSelectionBox();

                if (_dragMoved)
                {
                    SelectUnitsInScreenRect(_dragStart, mousePosition);
                }
                else if (!IsPointerOverUI())
                {
                    SelectSingleAtScreenPosition(mousePosition);
                }
            }

            if (!_dragging &&
                Mouse.current.rightButton.wasPressedThisFrame &&
                !IsPointerOverUI())
            {
                HandleRightClick(mousePosition);
            }
        }

        /// <summary>Selects one unit and clears the previous selection.</summary>
        public void SelectUnit(UnitMovement unit)
        {
            if (!IsSelectable(unit))
            {
                ClearSelection();
                return;
            }

            ClearSelection();
            AddToSelection(unit);
        }

        /// <summary>Clears the current unit selection.</summary>
        public void ClearSelection()
        {
            for (int i = _selected.Count - 1; i >= 0; i--)
            {
                if (_selected[i] != null)
                {
                    _selected[i].SetSelected(false);
                }
            }

            _selected.Clear();
        }

        /// <summary>Moves every selected unit to the supplied world position.</summary>
        public void MoveSelectedTo(Vector3 destination)
        {
            if (_selected.Count == 0)
            {
                return;
            }

            if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                return;
            }

            Vector3 target = hit.position;
            int count = _selected.Count;

            for (int i = 0; i < count; i++)
            {
                UnitMovement unit = _selected[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                WallArcher archer = unit.GetComponent<WallArcher>();
                if (archer != null)
                {
                    archer.Dismount();
                }

                UnitCombat combat = unit.GetComponent<UnitCombat>();
                if (combat != null)
                {
                    combat.ClearTarget();
                }

                if (count == 1)
                {
                    unit.MoveTo(target);
                    continue;
                }

                Vector3 offset = FormationOffset(i, count);
                if (NavMesh.SamplePosition(target + offset, out NavMeshHit unitHit, 1.5f, NavMesh.AllAreas))
                {
                    unit.MoveTo(unitHit.position);
                }
                else
                {
                    unit.MoveTo(target);
                }
            }
        }

        private void HandleRightClick(Vector2 screenPosition)
        {
            if (_selected.Count == 0)
            {
                return;
            }

            if (TryGetEnemyUnit(screenPosition, out UnitRuntime enemy))
            {
                AttackSelected(enemy);
                return;
            }

            if (TryGetWallMount(screenPosition, out WallMountPoint wall))
            {
                MountSelected(wall);
                return;
            }

            if (TryGetGroundPoint(screenPosition, out Vector3 destination))
            {
                MoveSelectedTo(destination);
            }
        }

        private void AttackSelected(UnitRuntime enemy)
        {
            for (int i = 0; i < _selected.Count; i++)
            {
                UnitMovement unit = _selected[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                WallArcher archer = unit.GetComponent<WallArcher>();
                if (archer != null)
                {
                    archer.Dismount();
                }

                UnitCombat combat = unit.GetComponent<UnitCombat>();
                if (combat != null)
                {
                    combat.CommandAttack(enemy);
                }
            }
        }

        private bool TryGetWallMount(Vector2 screenPosition, out WallMountPoint wall)
        {
            wall = null;
            if (!TryGetNearestHit(screenPosition, out RaycastHit hit))
            {
                return false;
            }

            wall = hit.collider.GetComponentInParent<WallMountPoint>();
            return wall != null;
        }

        private void MountSelected(WallMountPoint wall)
        {
            if (wall == null)
            {
                return;
            }

            for (int i = 0; i < _selected.Count; i++)
            {
                UnitMovement unit = _selected[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                WallArcher archer = unit.GetComponent<WallArcher>();
                if (archer != null)
                {
                    archer.RequestMoveTo(wall);
                    continue;
                }

                UnitRuntime runtime = unit.Runtime != null
                    ? unit.Runtime
                    : unit.GetComponent<UnitRuntime>();

                if (runtime == null || runtime.Role != UnitRole.Ranged)
                {
                    continue;
                }

                wall.RequestMount(unit);
            }
        }

        private void SelectSingleAtScreenPosition(Vector2 screenPosition)
        {
            if (TryGetUnit(screenPosition, out UnitMovement unit))
            {
                SelectUnit(unit);
            }
            else
            {
                ClearSelection();
            }
        }

        private bool TryGetUnit(Vector2 screenPosition, out UnitMovement unit)
        {
            unit = null;
            if (!TryGetNearestHit(screenPosition, out RaycastHit hit))
            {
                return false;
            }

            UnitMovement candidate = hit.collider.GetComponentInParent<UnitMovement>();
            if (candidate == null || !IsSelectable(candidate))
            {
                return false;
            }

            unit = candidate;
            return true;
        }

        private bool TryGetEnemyUnit(Vector2 screenPosition, out UnitRuntime enemy)
        {
            enemy = null;
            if (!TryGetNearestHit(screenPosition, out RaycastHit hit))
            {
                return false;
            }

            UnitRuntime candidate = hit.collider.GetComponentInParent<UnitRuntime>();
            if (candidate == null ||
                !candidate.IsAlive ||
                candidate.Faction == Faction.Greek ||
                candidate.Faction == Faction.None)
            {
                return false;
            }

            enemy = candidate;
            return true;
        }

        private bool TryGetNearestHit(Vector2 screenPosition, out RaycastHit nearest)
        {
            nearest = default;
            if (raycastCamera == null)
            {
                return false;
            }

            Ray ray = raycastCamera.ScreenPointToRay(screenPosition);
            int count = Physics.RaycastNonAlloc(
                ray,
                _raycastHits,
                maxRayDistance,
                selectionMask,
                QueryTriggerInteraction.Ignore);

            if (count == 0)
            {
                return false;
            }

            float nearestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_raycastHits[i].distance < nearestDistance)
                {
                    nearestDistance = _raycastHits[i].distance;
                    nearest = _raycastHits[i];
                }
            }

            return true;
        }

        private void SelectUnitsInScreenRect(Vector2 start, Vector2 end)
        {
            ClearSelection();
            if (raycastCamera == null)
            {
                return;
            }

            Rect rect = ScreenRect(start, end);
            IReadOnlyList<UnitRuntime> units = UnitRegistry.All;

            for (int i = 0; i < units.Count; i++)
            {
                UnitRuntime runtime = units[i];
                if (runtime == null || !runtime.IsAlive || runtime.Faction != Faction.Greek)
                {
                    continue;
                }

                UnitMovement unit = runtime.Movement;
                if (unit == null)
                {
                    continue;
                }

                Vector3 screen = raycastCamera.WorldToScreenPoint(
                    unit.transform.position + Vector3.up * 0.5f);
                if (screen.z <= 0f)
                {
                    continue;
                }

                if (rect.Contains(new Vector2(screen.x, screen.y)))
                {
                    AddToSelection(unit);
                }
            }
        }

        private void AddToSelection(UnitMovement unit)
        {
            if (!IsSelectable(unit) || _selected.Contains(unit))
            {
                return;
            }

            _selected.Add(unit);
            unit.SetSelected(true);
        }

        private void PruneSelection()
        {
            for (int i = _selected.Count - 1; i >= 0; i--)
            {
                UnitMovement unit = _selected[i];
                if (unit != null && unit.IsAlive)
                {
                    continue;
                }

                if (unit != null)
                {
                    unit.SetSelected(false);
                }

                _selected.RemoveAt(i);
            }
        }

        private bool TryGetGroundPoint(Vector2 screenPosition, out Vector3 point)
        {
            point = default;
            if (raycastCamera == null)
            {
                return false;
            }

            Ray ray = raycastCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    maxRayDistance,
                    selectionMask,
                    QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }

            Plane plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            return false;
        }

        private Vector3 FormationOffset(int index, int count)
        {
            int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt(count / (float)columns);
            int column = index % columns;
            int row = index / columns;

            float x = (column - (columns - 1) * 0.5f) * formationSpacing;
            float z = (row - (rows - 1) * 0.5f) * formationSpacing;
            return new Vector3(x, 0f, z);
        }

        private static bool IsSelectable(UnitMovement unit)
        {
            if (unit == null || !unit.IsAlive)
            {
                return false;
            }

            UnitRuntime runtime = unit.Runtime;
            return runtime == null || runtime.Faction == Faction.Greek;
        }

        private static Rect ScreenRect(Vector2 start, Vector2 end)
        {
            Vector2 min = Vector2.Min(start, end);
            Vector2 max = Vector2.Max(start, end);
            return new Rect(min, max - min);
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void CreateSelectionBox()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            _canvasRect = canvas.GetComponent<RectTransform>();
            if (_canvasRect == null)
            {
                return;
            }

            GameObject box = new GameObject("Unit Selection Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(canvas.transform, false);

            RectTransform rect = box.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            _selectionBox = box.GetComponent<Image>();
            _selectionBox.color = selectionBoxColor;
            _selectionBox.raycastTarget = false;

            box.SetActive(false);
        }

        private void UpdateSelectionBox(Vector2 start, Vector2 end)
        {
            if (_selectionBox == null || _canvasRect == null)
            {
                return;
            }

            Vector2 min = Vector2.Min(start, end);
            Vector2 max = Vector2.Max(start, end);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                min,
                null,
                out Vector2 localMin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                max,
                null,
                out Vector2 localMax);

            RectTransform rect = _selectionBox.rectTransform;
            rect.anchoredPosition = (localMin + localMax) * 0.5f;
            rect.sizeDelta = localMax - localMin;

            if (!_selectionBox.gameObject.activeSelf)
            {
                _selectionBox.gameObject.SetActive(true);
            }
        }

        private void HideSelectionBox()
        {
            if (_selectionBox != null && _selectionBox.gameObject.activeSelf)
            {
                _selectionBox.gameObject.SetActive(false);
            }
        }

        private void CancelDrag()
        {
            _dragging = false;
            _dragMoved = false;
            HideSelectionBox();
        }
    }
}
