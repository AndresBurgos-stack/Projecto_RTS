using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ProjectoRTS.Buildings
{
    /// <summary>
    /// Selects placed buildings with the left mouse button. Clicking a
    /// barracks opens its recruitment panel; clicking empty ground or
    /// pressing Escape closes it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingSelectionController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera raycastCamera;
        [SerializeField] private BuildingPlacementController placement;
        [SerializeField] private RecruitmentManager recruitment;
        [SerializeField] private LayerMask buildingMask = ~0;

        [Header("Input")]
        [SerializeField, Min(1f)] private float maxRayDistance = 500f;

        private bool _wasPlacing;
        private int _ignoreClickFrame = -1;

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

            if (recruitment == null)
            {
                recruitment = FindFirstObjectByType<RecruitmentManager>(FindObjectsInactive.Include);
            }

            if (placement != null)
            {
                placement.BuildingPlaced += HandleBuildingPlaced;
            }
        }

        private void OnDestroy()
        {
            if (placement != null)
            {
                placement.BuildingPlaced -= HandleBuildingPlaced;
            }
        }

        private void Update()
        {
            if (placement != null && placement.IsPlacing)
            {
                if (!_wasPlacing && recruitment != null)
                {
                    recruitment.ClearSelection();
                }

                _wasPlacing = true;
                return;
            }

            _wasPlacing = false;

            if (Mouse.current == null)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (recruitment != null)
                {
                    recruitment.ClearSelection();
                }

                return;
            }

            if (!Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (Time.frameCount == _ignoreClickFrame || IsPointerOverUI())
            {
                return;
            }

            SelectAtScreenPosition(Mouse.current.position.ReadValue());
        }

        private void HandleBuildingPlaced(GameObject instance, BuildingDefinition definition)
        {
            // Do not treat the click that placed the building as a selection.
            _ignoreClickFrame = Time.frameCount;
        }

        /// <summary>
        /// Selects the barracks under the given screen position. Returns true
        /// when a barracks was selected; clicking anything else clears it.
        /// </summary>
        public bool SelectAtScreenPosition(Vector2 screenPosition)
        {
            if (raycastCamera == null)
            {
                return false;
            }

            return SelectFromRay(raycastCamera.ScreenPointToRay(screenPosition));
        }

        /// <summary>Selects the barracks hit by the supplied ray.</summary>
        public bool SelectFromRay(Ray ray)
        {
            BarracksBuilding barracks = null;

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, buildingMask, QueryTriggerInteraction.Ignore))
            {
                barracks = hit.collider.GetComponentInParent<BarracksBuilding>();
            }

            if (barracks != null)
            {
                if (recruitment != null)
                {
                    recruitment.SelectBarracks(barracks);
                }

                return true;
            }

            if (recruitment != null)
            {
                recruitment.ClearSelection();
            }

            return false;
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
