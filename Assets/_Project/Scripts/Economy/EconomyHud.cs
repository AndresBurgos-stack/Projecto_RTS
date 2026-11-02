using ProjectoRTS.Buildings;
using ProjectoRTS.Resources;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectoRTS.Economy
{
    /// <summary>
    /// Small always-on HUD that shows the Greek resource pool and the number
    /// of living workers. It builds its own UI at runtime so the scene does
    /// not need extra wiring.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EconomyHud : MonoBehaviour
    {
        private static EconomyHud _instance;

        private Text _label;
        private float _nextRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null)
            {
                return;
            }

            GameObject host = new GameObject("Economy HUD");
            host.AddComponent<EconomyHud>();
        }

        private void Awake()
        {
            _instance = this;
            BuildUi();
            Refresh();
        }

        private void OnEnable()
        {
            PlayerEconomy.Changed += Refresh;
        }

        private void OnDisable()
        {
            PlayerEconomy.Changed -= Refresh;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            if (Time.time < _nextRefresh)
            {
                return;
            }

            _nextRefresh = Time.time + 0.5f;
            Refresh();
        }

        private void Refresh()
        {
            if (_label == null)
            {
                return;
            }

            _label.text =
                "Madera: " + PlayerEconomy.Wood +
                "\nBronce: " + PlayerEconomy.Bronze +
                "\nTrabajadores: " + WorkerBuilder.Count +
                "\nPriorizan madera: " + WorkerBuilder.CountPreferred(ResourceType.Wood) +
                "\nPriorizan bronce: " + WorkerBuilder.CountPreferred(ResourceType.Bronze) +
                "\nSin prioridad: " + (
                    WorkerBuilder.Count -
                    WorkerBuilder.CountPreferred(ResourceType.Wood) -
                    WorkerBuilder.CountPreferred(ResourceType.Bronze));
        }

        private void BuildUi()
        {
            GameObject canvasObject = new GameObject("Economy Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 25;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(canvasObject.transform, false);

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -80f);
            rect.sizeDelta = new Vector2(250f, 172f);

            Image background = panel.AddComponent<Image>();
            background.color = new Color(0.08f, 0.09f, 0.12f, 0.9f);
            background.raycastTarget = false;

            GameObject labelObject = new GameObject("Text");
            labelObject.transform.SetParent(panel.transform, false);

            RectTransform labelRect = labelObject.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(14f, 8f);
            labelRect.offsetMax = new Vector2(-14f, -8f);

            _label = labelObject.AddComponent<Text>();
            _label.font = ResolveFont();
            _label.fontSize = 18;
            _label.lineSpacing = 1.15f;
            _label.alignment = TextAnchor.MiddleLeft;
            _label.color = new Color(0.94f, 0.92f, 0.86f, 1f);
            _label.raycastTarget = false;
        }

        private static Font ResolveFont()
        {
            Font font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = UnityEngine.Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return font;
        }
    }
}
