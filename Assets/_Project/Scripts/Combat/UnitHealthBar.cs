using ProjectoRTS.Units;
using UnityEngine;

namespace ProjectoRTS.Combat
{
    /// <summary>
    /// World-space health bar that always faces the main camera. The fill is
    /// a simple quad whose width follows the unit's health.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitHealthBar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UnitRuntime runtime;
        [SerializeField] private Transform fill;

        [Header("Display")]
        [SerializeField] private bool hideWhenFull;

        private Camera _camera;
        private float _fullWidth = 1f;

        private void Awake()
        {
            if (runtime == null)
            {
                runtime = GetComponentInParent<UnitRuntime>();
            }

            if (fill != null)
            {
                _fullWidth = Mathf.Max(0.01f, fill.localScale.x);
            }
        }

        private void OnEnable()
        {
            if (runtime == null)
            {
                return;
            }

            runtime.HealthChanged += HandleHealthChanged;
            runtime.Died += HandleDied;
        }

        private void OnDisable()
        {
            if (runtime == null)
            {
                return;
            }

            runtime.HealthChanged -= HandleHealthChanged;
            runtime.Died -= HandleDied;
        }

        private void Start()
        {
            _camera = Camera.main;
            Refresh();
        }

        private void LateUpdate()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera != null)
            {
                transform.rotation = _camera.transform.rotation;
            }
        }

        private void HandleHealthChanged(UnitRuntime unit)
        {
            Refresh();
        }

        private void HandleDied(UnitRuntime unit)
        {
            gameObject.SetActive(false);
        }

        private void Refresh()
        {
            if (runtime == null || fill == null)
            {
                return;
            }

            float normalized = Mathf.Clamp01(runtime.HealthNormalized);
            Vector3 scale = fill.localScale;
            scale.x = _fullWidth * normalized;
            fill.localScale = scale;

            Vector3 position = fill.localPosition;
            position.x = -(1f - normalized) * _fullWidth * 0.5f;
            fill.localPosition = position;

            if (hideWhenFull)
            {
                bool visible = normalized < 1f;
                if (fill.gameObject.activeSelf != visible)
                {
                    fill.gameObject.SetActive(visible);
                }
            }
        }
    }
}
