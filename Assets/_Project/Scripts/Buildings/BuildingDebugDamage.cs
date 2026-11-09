using ProjectoRTS.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectoRTS.Buildings
{
    /// <summary>
    /// Temporary debug helper: point at a finished building and press K to
    /// damage it. Used to test the structure health bar before combat exists.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingDebugDamage : MonoBehaviour
    {
        [SerializeField] private float damageAmount = 100f;
        [SerializeField] private float maxRayDistance = 500f;

        private Camera _camera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!SceneFlow.IsGameplayScene || Object.FindFirstObjectByType<BuildingDebugDamage>() != null)
            {
                return;
            }

            GameObject host = new GameObject("Building Debug Damage");
            host.AddComponent<BuildingDebugDamage>();
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.kKey.wasPressedThisFrame)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera == null)
            {
                return;
            }

            Vector2 screen = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            Ray ray = _camera.ScreenPointToRay(screen);

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                BuildingRuntime runtime = hit.collider.GetComponentInParent<BuildingRuntime>();

                if (runtime != null)
                {
                    runtime.ApplyDamage(damageAmount);
                    Debug.Log("[BuildingDebugDamage] " + runtime.gameObject.name + " health " + runtime.CurrentHealth + "/" + runtime.MaxHealth);
                    return;
                }
            }

            Debug.Log("[BuildingDebugDamage] No building under the cursor");
        }
    }
}
