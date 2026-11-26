using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectoRTS.UI
{
    /// <summary>
    /// Single floating tooltip shared by every HUD icon. It follows the
    /// mouse, flips near the screen edges and hides as soon as the pointer
    /// leaves the element that requested it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiTooltip : MonoBehaviour
    {
        private const float CursorOffset = 18f;
        private const float EdgeMargin = 8f;
        private const float MaxWidth = 340f;

        private static UiTooltip _instance;
        private static object _owner;

        private Canvas _canvas;
        private RectTransform _root;
        private Text _title;
        private Text _body;
        private bool _visible;

        public static void Show(object owner, string title, string body)
        {
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body))
            {
                return;
            }

            EnsureInstance();
            _owner = owner;
            _instance.Apply(title, body);
        }

        public static void Hide(object owner)
        {
            if (_instance == null || (owner != null && _owner != null && _owner != owner))
            {
                return;
            }

            _owner = null;
            _instance.HideInternal();
        }

        private static void EnsureInstance()
        {
            if (_instance != null)
            {
                return;
            }

            GameObject host = new GameObject("Ui Tooltip");
            _instance = host.AddComponent<UiTooltip>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            Build();
            HideInternal();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                _owner = null;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                _owner = null;
                HideInternal();
            }
        }

        private void LateUpdate()
        {
            if (_visible)
            {
                UpdatePosition();
            }
        }

        private void Apply(string title, string body)
        {
            bool hasTitle = !string.IsNullOrEmpty(title);
            _title.text = hasTitle ? title : string.Empty;
            _title.gameObject.SetActive(hasTitle);

            _body.text = string.IsNullOrEmpty(body) ? string.Empty : body;
            _body.gameObject.SetActive(!string.IsNullOrEmpty(body));

            _root.gameObject.SetActive(true);
            _visible = true;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_root);
            UpdatePosition();
        }

        private void HideInternal()
        {
            _visible = false;

            if (_root != null)
            {
                _root.gameObject.SetActive(false);
            }
        }

        private void UpdatePosition()
        {
            if (_root == null || _canvas == null)
            {
                return;
            }

            RectTransform canvasRect = _canvas.transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            Vector2 screenPoint = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : Vector2.zero;

            Camera camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _canvas.worldCamera;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPoint,
                    camera,
                    out Vector2 local))
            {
                return;
            }

            Vector2 size = _root.rect.size;
            Vector2 canvasSize = canvasRect.rect.size;
            float halfWidth = canvasSize.x * 0.5f;
            float halfHeight = canvasSize.y * 0.5f;

            float x = local.x + CursorOffset;
            if (x + size.x > halfWidth - EdgeMargin)
            {
                x = local.x - CursorOffset - size.x;
            }

            float y = local.y - CursorOffset;
            if (y - size.y < -halfHeight + EdgeMargin)
            {
                y = local.y + CursorOffset + size.y;
            }

            x = Mathf.Clamp(x, -halfWidth + EdgeMargin, halfWidth - size.x - EdgeMargin);
            y = Mathf.Clamp(y, -halfHeight + size.y + EdgeMargin, halfHeight - EdgeMargin);

            _root.anchoredPosition = new Vector2(x, y);
        }

        private void Build()
        {
            GameObject canvasObject = new GameObject("Tooltip Canvas");
            canvasObject.transform.SetParent(transform, false);

            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 300;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject rootObject = new GameObject("Tooltip");
            rootObject.transform.SetParent(canvasObject.transform, false);

            _root = rootObject.AddComponent<RectTransform>();
            _root.anchorMin = new Vector2(0.5f, 0.5f);
            _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0f, 1f);
            _root.sizeDelta = new Vector2(MaxWidth, 0f);

            Image background = rootObject.AddComponent<Image>();
            background.color = new Color(0.06f, 0.07f, 0.09f, 0.96f);
            background.raycastTarget = false;

            VerticalLayoutGroup layout = rootObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = rootObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _title = CreateText(rootObject.transform, "Title", 15, FontStyle.Bold,
                new Color(1f, 0.87f, 0.55f, 1f));
            _body = CreateText(rootObject.transform, "Body", 12, FontStyle.Normal,
                new Color(0.93f, 0.93f, 0.9f, 1f));
        }

        private static Text CreateText(
            Transform parent,
            string name,
            int fontSize,
            FontStyle style,
            Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            Text text = go.AddComponent<Text>();
            text.font = UiFonts.Default;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.1f;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }
    }
}
