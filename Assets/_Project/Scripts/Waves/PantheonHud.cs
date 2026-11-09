using ProjectoRTS.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectoRTS.Waves
{
    /// <summary>
    /// World-space health bar that floats above the Pantheon. The height is
    /// computed from the building renderers, so it always sits on top of the
    /// mesh. The bar faces the camera and reads PantheonObjective health.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PantheonHud : MonoBehaviour
    {
        [SerializeField] private PantheonObjective objective;
        [SerializeField] private float heightPadding = 1.5f;
        [SerializeField] private float barWidth = 6f;
        [SerializeField] private float barHeight = 0.9f;

        private Canvas _canvas;
        private Image _fill;
        private Text _label;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!SceneFlow.IsGameplayScene || Object.FindFirstObjectByType<PantheonHud>() != null)
            {
                return;
            }

            GameObject host = new GameObject("Pantheon HUD");
            host.AddComponent<PantheonHud>();
        }

        private void Start()
        {
            if (objective == null)
            {
                objective = Object.FindFirstObjectByType<PantheonObjective>();
            }

            if (objective == null)
            {
                Debug.Log("[PantheonHud] No PantheonObjective found");
                return;
            }

            BuildBar();
            Refresh();

            objective.HealthChanged += HandleHealthChanged;
        }

        private void OnDestroy()
        {
            if (objective != null)
            {
                objective.HealthChanged -= HandleHealthChanged;
            }
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;

            if (cam == null)
            {
                cam = Object.FindFirstObjectByType<Camera>();
            }

            if (cam != null && _canvas != null)
            {
                _canvas.transform.rotation = cam.transform.rotation;
            }
        }

        private void HandleHealthChanged(PantheonObjective target)
        {
            Refresh();
        }

        private float GetTopY()
        {
            Renderer[] renderers = objective.GetComponentsInChildren<Renderer>();
            float top = objective.transform.position.y;
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
            Vector3 center = objective.transform.position;
            Vector3 position = new Vector3(center.x, GetTopY() + heightPadding, center.z);

            GameObject canvasObject = new GameObject(
                "Pantheon Health Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));

            canvasObject.transform.SetParent(transform, false);
            canvasObject.transform.position = position;
            canvasObject.transform.rotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one * 0.01f;

            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 200;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(barWidth * 100f, barHeight * 100f);

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            GameObject backgroundObject = new GameObject(
                "Background",
                typeof(RectTransform),
                typeof(Image));

            backgroundObject.transform.SetParent(canvasObject.transform, false);
            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            Image backgroundImage = backgroundObject.GetComponent<Image>();
            backgroundImage.color = new Color(0.05f, 0.05f, 0.08f, 0.9f);
            backgroundImage.raycastTarget = false;

            GameObject fillObject = new GameObject(
                "Fill",
                typeof(RectTransform),
                typeof(Image));

            fillObject.transform.SetParent(backgroundObject.transform, false);
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 0f);
            fillRect.pivot = new Vector2(0f, 0f);
            fillRect.anchoredPosition = new Vector2(6f, 6f);
            fillRect.sizeDelta = new Vector2(-12f, 18f);

            _fill = fillObject.GetComponent<Image>();
            _fill.color = new Color(0.45f, 0.78f, 0.42f);
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Horizontal;
            _fill.fillOrigin = 0;
            _fill.fillAmount = 1f;
            _fill.raycastTarget = false;

            GameObject labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(Text));

            labelObject.transform.SetParent(backgroundObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            _label = labelObject.GetComponent<Text>();
            _label.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 40;
            _label.fontStyle = FontStyle.Bold;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = new Color(1f, 0.93f, 0.78f);
            _label.text = "Panteon 100%";
            _label.raycastTarget = false;
        }

        private void Refresh()
        {
            if (_label == null || _fill == null)
            {
                return;
            }

            float normalized = objective != null ? Mathf.Clamp01(objective.HealthNormalized) : 1f;
            int percent = Mathf.RoundToInt(normalized * 100f);

            _fill.fillAmount = normalized;
            _label.text = "Panteon " + percent + "%";

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
