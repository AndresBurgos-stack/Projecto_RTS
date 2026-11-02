using UnityEngine;
using UnityEngine.UI;

namespace ProjectoRTS.Buildings
{
    /// <summary>
    /// Small world-space health bar for buildings. It stays hidden while the
    /// structure is at full health or still under construction, and only
    /// appears once it takes damage.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StructureHealthBar : MonoBehaviour
    {
        [SerializeField] private BuildingRuntime runtime;
        [SerializeField] private float barWidth = 1.6f;
        [SerializeField] private float barHeight = 0.28f;
        [SerializeField] private float heightPadding = 0.35f;
        [SerializeField] private bool hideWhenFull = true;

        private Canvas _canvas;
        private Image _fill;
        private Camera _camera;
        private ConstructionSite _site;

        private void Awake()
        {
            if (runtime == null)
            {
                runtime = GetComponent<BuildingRuntime>();
            }

            _site = GetComponent<ConstructionSite>();
        }

        private void OnEnable()
        {
            if (runtime != null)
            {
                runtime.HealthChanged += HandleHealthChanged;
            }
        }

        private void OnDisable()
        {
            if (runtime != null)
            {
                runtime.HealthChanged -= HandleHealthChanged;
            }
        }

        private void Start()
        {
            BuildBar();
            Refresh();
        }

        private void LateUpdate()
        {
            if (_canvas == null)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera != null)
            {
                _canvas.transform.rotation = _camera.transform.rotation;
            }

            bool visible = !(_site != null && !_site.IsComplete);

            if (hideWhenFull && runtime != null && runtime.HealthNormalized >= 1f)
            {
                visible = false;
            }

            if (_canvas.gameObject.activeSelf != visible)
            {
                _canvas.gameObject.SetActive(visible);
            }
        }

        private void HandleHealthChanged(BuildingRuntime target)
        {
            Refresh();
        }

        private float GetTopY()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            float top = transform.position.y;
            bool found = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                float y = renderers[i].bounds.max.y;

                if (!found || y > top)
                {
                    top = y;
                    found = true;
                }
            }

            return top;
        }

        private void BuildBar()
        {
            Vector3 center = transform.position;
            Vector3 position = new Vector3(center.x, GetTopY() + heightPadding, center.z);

            GameObject canvasObject = new GameObject(
                "Structure Health Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));

            canvasObject.transform.SetParent(transform, false);
            canvasObject.transform.position = position;
            canvasObject.transform.rotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one * 0.01f;

            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 150;

            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(barWidth * 100f, barHeight * 100f);

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            GameObject backgroundObject = new GameObject(
                "Background",
                typeof(RectTransform),
                typeof(Image));

            backgroundObject.transform.SetParent(canvasObject.transform, false);
            RectTransform backRect = backgroundObject.GetComponent<RectTransform>();
            backRect.anchorMin = Vector2.zero;
            backRect.anchorMax = Vector2.one;
            backRect.offsetMin = Vector2.zero;
            backRect.offsetMax = Vector2.zero;

            Image backImage = backgroundObject.GetComponent<Image>();
            backImage.color = new Color(0.05f, 0.06f, 0.08f, 0.85f);
            backImage.raycastTarget = false;

            GameObject fillObject = new GameObject(
                "Fill",
                typeof(RectTransform),
                typeof(Image));

            fillObject.transform.SetParent(backgroundObject.transform, false);
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);

            _fill = fillObject.GetComponent<Image>();
            _fill.color = new Color(0.45f, 0.78f, 0.42f);
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Horizontal;
            _fill.fillOrigin = 0;
            _fill.fillAmount = 1f;
            _fill.raycastTarget = false;
        }

        private void Refresh()
        {
            if (_fill == null)
            {
                return;
            }

            float normalized = runtime != null ? Mathf.Clamp01(runtime.HealthNormalized) : 1f;
            _fill.fillAmount = normalized;

            if (normalized < 0.35f)
            {
                _fill.color = new Color(0.85f, 0.2f, 0.2f);
            }
            else if (normalized < 0.7f)
            {
                _fill.color = new Color(0.9f, 0.65f, 0.2f);
            }
            else
            {
                _fill.color = new Color(0.45f, 0.78f, 0.42f);
            }
        }
    }
}
