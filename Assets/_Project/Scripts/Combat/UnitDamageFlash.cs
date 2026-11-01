using ProjectoRTS.Units;
using UnityEngine;

namespace ProjectoRTS.Combat
{
    /// <summary>
    /// Briefly tints a unit white when it takes damage. Uses a material
    /// property block so the shared materials are never modified.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitDamageFlash : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("References")]
        [SerializeField] private UnitRuntime runtime;
        [SerializeField] private Renderer[] renderers;

        [Header("Feedback")]
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField, Min(0.01f)] private float flashDuration = 0.12f;

        private MaterialPropertyBlock _block;
        private float _flashUntil;
        private bool _flashing;

        private void Awake()
        {
            if (runtime == null)
            {
                runtime = GetComponent<UnitRuntime>();
            }

            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
            }

            _block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (runtime != null)
            {
                runtime.Damaged += HandleDamaged;
            }
        }

        private void OnDisable()
        {
            if (runtime != null)
            {
                runtime.Damaged -= HandleDamaged;
            }

            if (_flashing)
            {
                _flashing = false;
                ClearFlash();
            }
        }

        private void Update()
        {
            if (_flashing && Time.time >= _flashUntil)
            {
                _flashing = false;
                ClearFlash();
            }
        }

        private void HandleDamaged(UnitRuntime unit, float amount)
        {
            _flashUntil = Time.time + flashDuration;

            if (_flashing)
            {
                return;
            }

            _flashing = true;
            ApplyFlash();
        }

        private void ApplyFlash()
        {
            if (renderers == null)
            {
                return;
            }

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, flashColor);
                _block.SetColor(ColorId, flashColor);
                renderer.SetPropertyBlock(_block);
            }
        }

        private void ClearFlash()
        {
            if (renderers == null)
            {
                return;
            }

            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.SetPropertyBlock(null);
                }
            }
        }
    }
}
