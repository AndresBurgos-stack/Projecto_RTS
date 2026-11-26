using System.Collections.Generic;
using ProjectoRTS.Buildings;
using ProjectoRTS.CameraControl;
using ProjectoRTS.Core;
using ProjectoRTS.Resources;
using ProjectoRTS.Units;
using ProjectoRTS.Waves;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectoRTS.UI
{
    /// <summary>
    /// Circular minimap medallion built at runtime, in the same style as the
    /// other HUDs. It is north-up, shows the camera footprint, lets the player
    /// click or drag to move the camera and shows Roman units only while a
    /// wave is active. A single visibility hook is left ready for the future
    /// fog-of-war grid.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MinimapHud : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const float PanelSize = 240f;
        private const float MapSize = 208f;

        // The camera can pan over a 300x300 square (limits of +/-150). Its
        // corners are ~212 from the centre, so a 215 radius keeps the whole
        // playable area inside the round medallion.
        private const float WorldExtent = 215f;
        private const float RefreshInterval = 0.1f;

        private static readonly Vector2 WorldCenter = Vector2.zero;

        private static readonly Color AllyColor = new Color(0.40f, 0.82f, 1f, 0.95f);
        private static readonly Color WorkerColor = new Color(0.72f, 0.92f, 1f, 0.90f);
        private static readonly Color EnemyColor = new Color(1f, 0.30f, 0.24f, 0.98f);
        private static readonly Color BuildingColor = new Color(0.55f, 0.75f, 1f, 0.95f);
        private static readonly Color ConstructionColor = new Color(1f, 0.78f, 0.30f, 0.95f);
        private static readonly Color PantheonColor = new Color(1f, 0.84f, 0.38f, 1f);
        private static readonly Color WoodColor = new Color(0.42f, 0.85f, 0.42f, 0.80f);
        private static readonly Color CopperColor = new Color(1f, 0.55f, 0.20f, 0.95f);
        private static readonly Color TinColor = new Color(0.78f, 0.83f, 0.88f, 0.95f);
        private static readonly Color RingColor = new Color(0.69f, 0.55f, 0.34f, 1f);
        private static readonly Color InnerRingColor = new Color(0.88f, 0.74f, 0.46f, 0.55f);
        private static readonly Color CompassColor = new Color(0.94f, 0.86f, 0.66f, 1f);
        private static readonly Color MapBaseColor = new Color(0.08f, 0.10f, 0.09f, 1f);
        private static readonly Color MapGridColor = new Color(0.20f, 0.25f, 0.21f, 1f);
        private static readonly Color MapEdgeColor = new Color(0.03f, 0.04f, 0.04f, 1f);

        private static MinimapHud _instance;

        private RtsCameraController _cameraController;
        private Camera _raycastCamera;
        private WaveManager _waves;
        private UnitSelectionController _selection;
        private PantheonObjective _pantheon;

        private RectTransform _mapRect;
        private MinimapFrustumGraphic _frustum;
        private Image _cameraDot;
        private Image _ring;
        private Image _background;

        private IconPool _units;
        private IconPool _buildings;
        private IconPool _resources;
        private IconPool _landmarks;

        private Sprite _circleSprite;
        private Sprite _squareSprite;
        private Sprite _diamondSprite;
        private Sprite _ringSprite;
        private Sprite _innerRingSprite;
        private Sprite _mapBackgroundSprite;

        private bool _showResources;
        private bool _dragging;
        private float _nextRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!SceneFlow.IsGameplaySceneName(scene.name) || _instance != null)
            {
                return;
            }

            GameObject host = new GameObject("Minimap HUD");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<MinimapHud>();
        }

        private void Awake()
        {
            _instance = this;
            _cameraController = FindFirstObjectByType<RtsCameraController>();
            _raycastCamera = Camera.main;
            if (_raycastCamera == null)
            {
                _raycastCamera = FindFirstObjectByType<Camera>();
            }

            _waves = FindFirstObjectByType<WaveManager>();
            _selection = FindFirstObjectByType<UnitSelectionController>();
            _pantheon = FindFirstObjectByType<PantheonObjective>();

            CreateSprites();
            BuildUi();
            UpdateCameraDot();
            RefreshIcons();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            DestroySprite(_circleSprite);
            DestroySprite(_squareSprite);
            DestroySprite(_diamondSprite);
            DestroySprite(_ringSprite);
            DestroySprite(_innerRingSprite);
            DestroySprite(_mapBackgroundSprite);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M))
            {
                _showResources = !_showResources;
            }

            UpdateCameraDot();

            if (_frustum != null)
            {
                _frustum.Refresh();
            }

            if (Time.time < _nextRefresh)
            {
                return;
            }

            _nextRefresh = Time.time + RefreshInterval;
            RefreshIcons();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!TryGetPointerUv(eventData, out Vector2 uv))
            {
                return;
            }

            Vector2 centered = uv - new Vector2(0.5f, 0.5f);
            if (centered.sqrMagnitude > 0.25f)
            {
                return;
            }

            _dragging = true;
            FocusOnUv(uv);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || !TryGetPointerUv(eventData, out Vector2 uv))
            {
                return;
            }

            Vector2 centered = uv - new Vector2(0.5f, 0.5f);
            if (centered.sqrMagnitude > 0.25f)
            {
                centered = centered.normalized * 0.5f;
                uv = centered + new Vector2(0.5f, 0.5f);
            }

            FocusOnUv(uv);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _dragging = false;
        }

        private void BuildUi()
        {
            GameObject canvasObject = new GameObject("Minimap Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 35;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject rootObject = new GameObject("Medallion");
            rootObject.transform.SetParent(canvasObject.transform, false);

            RectTransform root = rootObject.AddComponent<RectTransform>();
            root.anchorMin = new Vector2(1f, 0f);
            root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(1f, 0f);
            root.anchoredPosition = new Vector2(-28f, 28f);
            root.sizeDelta = new Vector2(PanelSize, PanelSize);

            Image shadow = CreateImage(
                root,
                "Shadow",
                _circleSprite,
                new Color(0f, 0f, 0f, 0.40f),
                new Vector2(PanelSize + 8f, PanelSize + 8f));
            shadow.raycastTarget = false;

            _background = CreateImage(
                root,
                "Map",
                _mapBackgroundSprite,
                Color.white,
                new Vector2(MapSize, MapSize));
            _background.raycastTarget = true;
            _background.alphaHitTestMinimumThreshold = 0.1f;
            _mapRect = _background.rectTransform;

            GameObject maskObject = new GameObject("Mask");
            maskObject.transform.SetParent(root, false);

            RectTransform maskRect = maskObject.AddComponent<RectTransform>();
            maskRect.anchorMin = new Vector2(0.5f, 0.5f);
            maskRect.anchorMax = new Vector2(0.5f, 0.5f);
            maskRect.pivot = new Vector2(0.5f, 0.5f);
            maskRect.anchoredPosition = Vector2.zero;
            maskRect.sizeDelta = new Vector2(MapSize, MapSize);

            // UGUI's Graphic base class does not require a CanvasRenderer (only
            // the built-in Image/Text do), so custom graphics must add it here.
            maskObject.AddComponent<CanvasRenderer>();

            MinimapCircleGraphic maskGraphic = maskObject.AddComponent<MinimapCircleGraphic>();
            maskGraphic.color = Color.white;
            maskGraphic.raycastTarget = false;

            Mask mask = maskObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject contentObject = new GameObject("Content");
            contentObject.transform.SetParent(maskObject.transform, false);

            RectTransform content = contentObject.AddComponent<RectTransform>();
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            GameObject frustumObject = new GameObject("Camera View");
            frustumObject.transform.SetParent(contentObject.transform, false);

            RectTransform frustumRect = frustumObject.AddComponent<RectTransform>();
            frustumRect.anchorMin = Vector2.zero;
            frustumRect.anchorMax = Vector2.one;
            frustumRect.offsetMin = Vector2.zero;
            frustumRect.offsetMax = Vector2.zero;

            frustumObject.AddComponent<CanvasRenderer>();

            _frustum = frustumObject.AddComponent<MinimapFrustumGraphic>();
            _frustum.raycastTarget = false;
            _frustum.color = new Color(0.95f, 0.93f, 0.82f, 0.12f);
            _frustum.Configure(_raycastCamera, WorldCenter, WorldExtent);

            _cameraDot = CreateImage(
                contentObject.transform,
                "Camera",
                _circleSprite,
                new Color(1f, 1f, 1f, 0.55f),
                new Vector2(4f, 4f));
            _cameraDot.raycastTarget = false;

            _resources = new IconPool(contentObject.transform, "Resources");
            _buildings = new IconPool(contentObject.transform, "Buildings");
            _units = new IconPool(contentObject.transform, "Units");
            _landmarks = new IconPool(contentObject.transform, "Landmarks");

            _ring = CreateImage(
                root,
                "Ring",
                _ringSprite,
                RingColor,
                new Vector2(PanelSize, PanelSize));
            _ring.raycastTarget = false;

            Image innerRing = CreateImage(
                root,
                "Inner Ring",
                _innerRingSprite,
                InnerRingColor,
                new Vector2(MapSize, MapSize));
            innerRing.raycastTarget = false;

            CreateCompass(root);
        }

        private void CreateCompass(RectTransform root)
        {
            GameObject compassObject = new GameObject("North");
            compassObject.transform.SetParent(root, false);

            RectTransform rect = compassObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 6f);
            rect.sizeDelta = new Vector2(24f, 18f);

            Text label = compassObject.AddComponent<Text>();
            label.font = ResolveFont();
            label.text = "N";
            label.fontSize = 13;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = CompassColor;
            label.raycastTarget = false;

            Outline outline = compassObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private void UpdateCameraDot()
        {
            if (_cameraDot == null)
            {
                return;
            }

            Vector3 source;
            if (_raycastCamera != null)
            {
                source = _raycastCamera.transform.position;
            }
            else if (_cameraController != null)
            {
                source = _cameraController.TargetPosition;
            }
            else
            {
                _cameraDot.enabled = false;
                return;
            }

            if (TryWorldToMap(source, out Vector2 position))
            {
                _cameraDot.enabled = true;
                _cameraDot.rectTransform.anchoredPosition = position;
            }
            else
            {
                _cameraDot.enabled = false;
            }
        }

        private void RefreshIcons()
        {
            if (_resources == null)
            {
                return;
            }

            _resources.Begin();
            _buildings.Begin();
            _units.Begin();
            _landmarks.Begin();

            if (_showResources)
            {
                IReadOnlyList<ResourceNode> nodes = ResourceNode.All;
                for (int i = 0; i < nodes.Count; i++)
                {
                    ResourceNode node = nodes[i];
                    if (node == null || node.IsDepleted || !IsVisibleToPlayer(node.transform.position))
                    {
                        continue;
                    }

                    Color color;
                    float size;
                    switch (node.Type)
                    {
                        case ResourceType.Wood:
                            color = WoodColor;
                            size = 2.4f;
                            break;
                        case ResourceType.Copper:
                            color = CopperColor;
                            size = 2.8f;
                            break;
                        case ResourceType.Tin:
                            color = TinColor;
                            size = 2.8f;
                            break;
                        default:
                            continue;
                    }

                    if (TryWorldToMap(node.transform.position, out Vector2 position))
                    {
                        _resources.Add(_circleSprite, color, new Vector2(size, size), position);
                    }
                }
            }

            IReadOnlyList<BuildingRuntime> buildings = BuildingRuntime.All;
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingRuntime building = buildings[i];
                if (building == null || !building.IsAlive || !IsVisibleToPlayer(building.transform.position))
                {
                    continue;
                }

                Color color = BuildingColor;
                if (building.TryGetComponent<ConstructionSite>(out ConstructionSite site) &&
                    site != null &&
                    !site.IsComplete)
                {
                    color = ConstructionColor;
                }

                if (TryWorldToMap(building.transform.position, out Vector2 position))
                {
                    _buildings.Add(_squareSprite, color, new Vector2(6f, 6f), position);
                }
            }

            if (_pantheon != null &&
                _pantheon.IsAlive &&
                TryWorldToMap(_pantheon.transform.position, out Vector2 pantheonPosition))
            {
                _landmarks.Add(_diamondSprite, PantheonColor, new Vector2(12f, 12f), pantheonPosition);
            }

            bool showEnemies = _waves != null && _waves.HasActiveWave;
            IReadOnlyList<UnitRuntime> units = UnitRegistry.All;
            for (int i = 0; i < units.Count; i++)
            {
                UnitRuntime unit = units[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                if (unit.Faction == Faction.Roman)
                {
                    if (!showEnemies || !IsVisibleToPlayer(unit.transform.position))
                    {
                        continue;
                    }
                }
                else if (unit.Faction != Faction.Greek)
                {
                    continue;
                }

                if (!TryWorldToMap(unit.transform.position, out Vector2 position))
                {
                    continue;
                }

                bool isWorker = unit.TryGetComponent<WorkerBuilder>(out _);
                bool isSelected = IsSelected(unit);

                Color color;
                float size;
                if (unit.Faction == Faction.Roman)
                {
                    color = EnemyColor;
                    size = 5f;
                }
                else if (isWorker)
                {
                    color = WorkerColor;
                    size = 3.4f;
                }
                else
                {
                    color = AllyColor;
                    size = 4.6f;
                }

                if (isSelected)
                {
                    color = Color.white;
                    size += 1.6f;
                }

                _units.Add(_circleSprite, color, new Vector2(size, size), position);
            }

            _resources.End();
            _buildings.End();
            _units.End();
            _landmarks.End();
        }

        private bool IsSelected(UnitRuntime unit)
        {
            if (_selection == null || unit == null)
            {
                return false;
            }

            UnitMovement movement = unit.Movement;
            if (movement == null)
            {
                return false;
            }

            IReadOnlyList<UnitMovement> selected = _selection.SelectedUnits;
            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i] == movement)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Single hook for the future fog-of-war grid. Everything is visible
        /// for now; when the fog exists, return false for unexplored cells.
        /// </summary>
        private static bool IsVisibleToPlayer(Vector3 worldPosition)
        {
            return true;
        }

        private static bool TryWorldToMap(Vector3 worldPosition, out Vector2 anchoredPosition)
        {
            float u = (worldPosition.x - WorldCenter.x) / (2f * WorldExtent) + 0.5f;
            float v = (worldPosition.z - WorldCenter.y) / (2f * WorldExtent) + 0.5f;

            anchoredPosition = new Vector2(
                (u - 0.5f) * MapSize,
                (v - 0.5f) * MapSize);

            return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
        }

        private bool TryGetPointerUv(PointerEventData eventData, out Vector2 uv)
        {
            uv = Vector2.zero;
            if (_mapRect == null)
            {
                return false;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _mapRect,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 local))
            {
                return false;
            }

            Vector2 size = _mapRect.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                return false;
            }

            uv = new Vector2(local.x / size.x + 0.5f, local.y / size.y + 0.5f);
            return true;
        }

        private void FocusOnUv(Vector2 uv)
        {
            if (_cameraController == null)
            {
                return;
            }

            Vector3 world = new Vector3(
                (uv.x - 0.5f) * 2f * WorldExtent + WorldCenter.x,
                0f,
                (uv.y - 0.5f) * 2f * WorldExtent + WorldCenter.y);

            _cameraController.FocusOn(world);
        }

        private static Image CreateImage(
            Transform parent,
            string name,
            Sprite sprite,
            Color color,
            Vector2 size)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            Image image = obj.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
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

        private void CreateSprites()
        {
            _circleSprite = CreateCircleSprite(128);
            _squareSprite = CreateSquareSprite(32);
            _diamondSprite = CreateDiamondSprite(64);
            _ringSprite = CreateRingSprite(256, 0.90f);
            _innerRingSprite = CreateRingSprite(256, 0.96f);
            _mapBackgroundSprite = CreateMapBackgroundSprite(256);
        }

        private static Sprite CreateCircleSprite(int size)
        {
            Texture2D texture = NewTexture(size);
            Color32[] pixels = new Color32[size * size];
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            return FinishTexture(texture, pixels, size);
        }

        private static Sprite CreateRingSprite(int size, float innerRadiusNormalized)
        {
            Texture2D texture = NewTexture(size);
            Color32[] pixels = new Color32[size * size];
            float outerRadius = size * 0.5f - 1f;
            float innerRadius = outerRadius * Mathf.Clamp01(innerRadiusNormalized);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f;
                    float dy = y + 0.5f - size * 0.5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    float outerAlpha = Mathf.Clamp01(outerRadius - distance + 0.5f);
                    float innerAlpha = Mathf.Clamp01(distance - innerRadius + 0.5f);
                    float alpha = Mathf.Min(outerAlpha, innerAlpha);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            return FinishTexture(texture, pixels, size);
        }

        private static Sprite CreateSquareSprite(int size)
        {
            Texture2D texture = NewTexture(size);
            Color32[] pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }

            return FinishTexture(texture, pixels, size);
        }

        private static Sprite CreateDiamondSprite(int size)
        {
            Texture2D texture = NewTexture(size);
            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - half) / half;
                    float dy = Mathf.Abs(y + 0.5f - half) / half;
                    float alpha = Mathf.Clamp01((1f - (dx + dy)) * half + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            return FinishTexture(texture, pixels, size);
        }

        private static Sprite CreateMapBackgroundSprite(int size)
        {
            Texture2D texture = NewTexture(size);
            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f - half) / half;
                    float ny = (y + 0.5f - half) / half;
                    float distance = Mathf.Sqrt(nx * nx + ny * ny);

                    float alpha = Mathf.Clamp01((1f - distance) * half + 0.5f);
                    if (alpha <= 0f)
                    {
                        pixels[y * size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    float vignette = Mathf.Clamp01(1f - 0.45f * distance * distance);
                    Color color = new Color(
                        MapBaseColor.r * vignette,
                        MapBaseColor.g * vignette,
                        MapBaseColor.b * vignette,
                        1f);

                    float gridX = Mathf.Abs(Mathf.Repeat(nx * 4f + 0.5f, 1f) - 0.5f);
                    float gridY = Mathf.Abs(Mathf.Repeat(ny * 4f + 0.5f, 1f) - 0.5f);
                    if (Mathf.Min(gridX, gridY) < 0.008f)
                    {
                        color = Color.Lerp(color, MapGridColor, 0.45f);
                    }

                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, distance));
                    color = Color.Lerp(color, MapEdgeColor, edge * 0.75f);

                    pixels[y * size + x] = new Color32(
                        (byte)(Mathf.Clamp01(color.r) * 255f),
                        (byte)(Mathf.Clamp01(color.g) * 255f),
                        (byte)(Mathf.Clamp01(color.b) * 255f),
                        (byte)(alpha * 255f));
                }
            }

            return FinishTexture(texture, pixels, size);
        }

        private static Texture2D NewTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        private static Sprite FinishTexture(Texture2D texture, Color32[] pixels, int size)
        {
            return FinishTexture(texture, pixels, size, SpriteMeshType.FullRect);
        }

        private static Sprite FinishTexture(
            Texture2D texture,
            Color32[] pixels,
            int size,
            SpriteMeshType meshType)
        {
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                meshType);
        }

        private static void DestroySprite(Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            Texture2D texture = sprite.texture;
            UnityEngine.Object.Destroy(sprite);
            if (texture != null)
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        private sealed class IconPool
        {
            private readonly RectTransform _layer;
            private readonly List<Image> _icons = new List<Image>(64);
            private int _used;

            public IconPool(Transform parent, string name)
            {
                GameObject layerObject = new GameObject(name);
                layerObject.transform.SetParent(parent, false);

                _layer = layerObject.AddComponent<RectTransform>();
                _layer.anchorMin = Vector2.zero;
                _layer.anchorMax = Vector2.one;
                _layer.offsetMin = Vector2.zero;
                _layer.offsetMax = Vector2.zero;
            }

            public void Begin()
            {
                _used = 0;
            }

            public void Add(Sprite sprite, Color color, Vector2 size, Vector2 position)
            {
                Image icon;
                if (_used < _icons.Count)
                {
                    icon = _icons[_used];
                }
                else
                {
                    GameObject iconObject = new GameObject("Icon");
                    iconObject.transform.SetParent(_layer, false);

                    RectTransform rect = iconObject.AddComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);

                    icon = iconObject.AddComponent<Image>();
                    icon.raycastTarget = false;
                    _icons.Add(icon);
                }

                _used++;
                icon.gameObject.SetActive(true);
                icon.sprite = sprite;
                icon.color = color;
                icon.rectTransform.sizeDelta = size;
                icon.rectTransform.anchoredPosition = position;
                icon.transform.SetAsLastSibling();
            }

            public void End()
            {
                for (int i = _used; i < _icons.Count; i++)
                {
                    _icons[i].gameObject.SetActive(false);
                }
            }
        }
    }
}
