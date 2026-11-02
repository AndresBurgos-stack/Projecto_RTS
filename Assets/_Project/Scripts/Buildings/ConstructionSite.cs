using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectoRTS.Buildings
{
    /// <summary>
    /// Marks a placed building that is still being built. While the site is
    /// incomplete the renderers use a translucent material and the colliders are
    /// disabled, so the building does not block anything until it is finished.
    /// A small world space bar floats above the building with the percentage
    /// completed and disappears once the work is done.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ConstructionSite : MonoBehaviour
    {
        private static readonly List<ConstructionSite> Sites = new List<ConstructionSite>(32);

        [Header("Build")]
        [SerializeField, Min(0.1f)] private float buildDuration = 6f;
        [SerializeField] private Color ghostColor = new Color(0.55f, 0.62f, 0.72f, 0.45f);

        [Header("Progress Bar")]
        [SerializeField] private Color fillColor = new Color(0.95f, 0.72f, 0.2f, 1f);
        [SerializeField] private float barHeightOffset = 0.8f;

        private readonly List<Renderer> _renderers = new List<Renderer>(8);
        private readonly List<Material[]> _originalMaterials = new List<Material[]>(8);
        private readonly List<Collider> _colliders = new List<Collider>(4);
        private Material _ghostMaterial;
        private GameObject _barRoot;
        private Image _fill;
        private Text _label;
        private Camera _camera;
        private float _progress;
        private bool _completed;

        /// <summary>Raised once when the site reaches full progress.</summary>
        public event Action<ConstructionSite> Completed;

        /// <summary>Progress from 0 to 1.</summary>
        public float Progress => _progress;

        /// <summary>True once the building is finished.</summary>
        public bool IsComplete => _completed;

        /// <summary>All the sites that are still being built.</summary>
        public static IReadOnlyList<ConstructionSite> Active => Sites;

        private void Awake()
        {
            CacheParts();
        }

        private void OnEnable()
        {
            if (!Sites.Contains(this))
            {
                Sites.Add(this);
            }
        }

        private void OnDisable()
        {
            Sites.Remove(this);
        }

        private void OnDestroy()
        {
            if (_ghostMaterial != null)
            {
                Destroy(_ghostMaterial);
            }
        }

        /// <summary>
        /// Puts the building in its under construction state: translucent look,
        /// no collision and a progress bar above it.
        /// </summary>
        public void BeginConstruction()
        {
            _progress = 0f;
            _completed = false;

            if (_ghostMaterial == null)
            {
                _ghostMaterial = CreateGhostMaterial();
            }

            for (int i = 0; i < _renderers.Count; i++)
            {
                Material[] ghost = new Material[_originalMaterials[i].Length];
                for (int m = 0; m < ghost.Length; m++)
                {
                    ghost[m] = _ghostMaterial;
                }

                _renderers[i].sharedMaterials = ghost;
            }

            for (int i = 0; i < _colliders.Count; i++)
            {
                _colliders[i].enabled = false;
            }

            EnsureBar();
            RefreshBar();
        }

        /// <summary>
        /// Adds work to the site. Delta is in seconds of one worker's effort.
        /// </summary>
        public void AddProgress(float delta)
        {
            if (_completed || delta <= 0f)
            {
                return;
            }

            _progress = Mathf.Clamp01(_progress + delta / Mathf.Max(0.1f, buildDuration));
            RefreshBar();

            if (_progress >= 1f)
            {
                Complete();
            }
        }

        /// <summary>
        /// Returns the closest active site to the given position, or null.
        /// </summary>
        public static ConstructionSite FindClosest(Vector3 position, float maxDistance)
        {
            ConstructionSite best = null;
            float bestSqr = maxDistance * maxDistance;

            for (int i = Sites.Count - 1; i >= 0; i--)
            {
                ConstructionSite site = Sites[i];
                if (site == null)
                {
                    Sites.RemoveAt(i);
                    continue;
                }

                if (site._completed)
                {
                    continue;
                }

                float sqr = (site.transform.position - position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = site;
                }
            }

            return best;
        }

        private void LateUpdate()
        {
            if (_barRoot == null || !_barRoot.activeSelf)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera != null)
            {
                _barRoot.transform.rotation = _camera.transform.rotation;
            }
        }

        private void Complete()
        {
            _completed = true;
            _progress = 1f;

            for (int i = 0; i < _renderers.Count; i++)
            {
                _renderers[i].sharedMaterials = _originalMaterials[i];
            }

            for (int i = 0; i < _colliders.Count; i++)
            {
                _colliders[i].enabled = true;
            }

            if (_barRoot != null)
            {
                Destroy(_barRoot);
                _barRoot = null;
            }

            Sites.Remove(this);
            Completed?.Invoke(this);
        }

        private void CacheParts()
        {
            _renderers.Clear();
            _originalMaterials.Clear();
            _colliders.Clear();

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                _renderers.Add(renderers[i]);
                _originalMaterials.Add(renderers[i].sharedMaterials);
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                _colliders.Add(colliders[i]);
            }
        }

        private void EnsureBar()
        {
            if (_barRoot != null)
            {
                return;
            }

            float top = 2f;
            if (_renderers.Count > 0 && _renderers[0] != null)
            {
                Bounds bounds = _renderers[0].bounds;
                for (int i = 1; i < _renderers.Count; i++)
                {
                    if (_renderers[i] != null)
                    {
                        bounds.Encapsulate(_renderers[i].bounds);
                    }
                }

                top = bounds.max.y - transform.position.y;
            }

            _barRoot = new GameObject("Construction Bar");
            _barRoot.transform.SetParent(transform, false);
            _barRoot.transform.localPosition = new Vector3(0f, top + barHeightOffset, 0f);
            _barRoot.transform.localScale = Vector3.one * 0.01f;

            Canvas canvas = _barRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 5;

            RectTransform rect = _barRoot.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = _barRoot.AddComponent<RectTransform>();
            }

            rect.sizeDelta = new Vector2(140f, 26f);

            GameObject background = new GameObject("Background");
            background.transform.SetParent(_barRoot.transform, false);
            RectTransform backRect = background.AddComponent<RectTransform>();
            backRect.anchorMin = Vector2.zero;
            backRect.anchorMax = Vector2.one;
            backRect.offsetMin = Vector2.zero;
            backRect.offsetMax = Vector2.zero;
            Image backImage = background.AddComponent<Image>();
            backImage.color = new Color(0.05f, 0.06f, 0.08f, 0.85f);
            backImage.raycastTarget = false;

            GameObject fillObject = new GameObject("Fill");
            fillObject.transform.SetParent(background.transform, false);
            RectTransform fillRect = fillObject.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);

            _fill = fillObject.AddComponent<Image>();
            _fill.color = fillColor;
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Horizontal;
            _fill.fillOrigin = 0;
            _fill.fillAmount = 0f;
            _fill.raycastTarget = false;

            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(_barRoot.transform, false);
            RectTransform labelRect = labelObject.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            _label = labelObject.AddComponent<Text>();
            _label.font = ResolveFont();
            _label.fontSize = 14;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = Color.white;
            _label.raycastTarget = false;
        }

        private void RefreshBar()
        {
            if (_fill != null)
            {
                _fill.fillAmount = _progress;
            }

            if (_label != null)
            {
                _label.text = Mathf.RoundToInt(_progress * 100f) + "%";
            }
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

        private Material CreateGhostMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material material = new Material(shader != null ? shader : Shader.Find("Standard"));
            material.name = "Construction Ghost";

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", ghostColor);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", ghostColor);
            }

            return material;
        }
    }
}