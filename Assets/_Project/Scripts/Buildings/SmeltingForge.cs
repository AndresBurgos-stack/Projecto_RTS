using System.Collections.Generic;
using ProjectoRTS.Economy;
using ProjectoRTS.Resources;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectoRTS.Buildings
{
    /// <summary>
    /// Fundicion: los trabajadores le entregan cobre y estano, que se
    /// acumulan en su stock local y se funden en bronce por ciclos.
    /// Mientras funde, el fuego del horno se enciende.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SmeltingForge : MonoBehaviour
    {
        private static readonly List<SmeltingForge> Forges = new List<SmeltingForge>(8);

        [Header("Receta")]
        [SerializeField, Min(1)] private int copperPerSmelt = 2;
        [SerializeField, Min(1)] private int tinPerSmelt = 1;
        [SerializeField, Min(1)] private int bronzePerSmelt = 2;

        [Header("Ritmo")]
        [SerializeField, Min(0.5f)] private float smeltInterval = 7f;

        [Header("Trabajadores")]
        [SerializeField, Min(1)] private int maxWorkers = 2;

        [Header("Feedback")]
        [SerializeField] private GameObject fireGlow;
        [SerializeField, Min(0f)] private float firePulseSpeed = 0f;

        private readonly List<WorkerBuilder> _assignedWorkers = new List<WorkerBuilder>(2);
        private int _copperStock;
        private int _tinStock;
        private float _cooldown;
        private float _workerPruneTimer;
        private ConstructionSite _site;
        private bool _siteCached;
        private bool _fireActive;
        private bool _fireScaleCaptured;
        private float _pulseTime;
        private Vector3 _fireBaseScale = Vector3.one;

        private Canvas _workerCanvas;
        private Image _workerBackground;
        private Text _workerText;
        private Camera _workerCamera;

        /// <summary>Todas las fundiciones activas de la escena.</summary>
        public static IReadOnlyList<SmeltingForge> All => Forges;

        public int CopperStock => _copperStock;
        public int TinStock => _tinStock;

        /// <summary>Trabajadores asignados actualmente a esta fundicion.</summary>
        public int WorkerCount => _assignedWorkers.Count;

        /// <summary>Plazas maximas de trabajador de la fundicion.</summary>
        public int MaxWorkers => maxWorkers;

        public bool HasFreeWorkerSlot => _assignedWorkers.Count < maxWorkers;

        /// <summary>True cuando la fundicion ya esta construida y puede operar.</summary>
        public bool IsOperational
        {
            get
            {
                if (!_siteCached)
                {
                    _site = GetComponent<ConstructionSite>();
                    _siteCached = true;
                }

                return _site == null || _site.IsComplete;
            }
        }

        /// <summary>True si el trabajador ya ocupa una plaza en esta fundicion.</summary>
        public bool IsWorkerAssigned(WorkerBuilder worker)
        {
            return worker != null && _assignedWorkers.Contains(worker);
        }

        /// <summary>
        /// Reserva una plaza de trabajador. Devuelve false cuando la
        /// fundicion ya tiene cubiertas sus dos plazas.
        /// </summary>
        public bool TryAssignWorker(WorkerBuilder worker)
        {
            if (worker == null)
            {
                return false;
            }

            if (_assignedWorkers.Contains(worker))
            {
                return true;
            }

            if (_assignedWorkers.Count >= maxWorkers)
            {
                return false;
            }

            _assignedWorkers.Add(worker);
            RefreshWorkerLabel();
            return true;
        }

        /// <summary>Libera la plaza que ocupaba el trabajador.</summary>
        public void UnassignWorker(WorkerBuilder worker)
        {
            if (worker == null)
            {
                return;
            }

            if (_assignedWorkers.Remove(worker))
            {
                RefreshWorkerLabel();
            }
        }

        private void Awake()
        {
            if (fireGlow == null)
            {
                Transform fire = transform.Find("Fuego");

                if (fire == null)
                {
                    fire = transform.Find("VFX_Fire");
                }

                if (fire != null)
                {
                    fireGlow = fire.gameObject;
                }
            }

            // El fuego solo se enciende cuando la fundicion trabaja.
            if (fireGlow != null)
            {
                fireGlow.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (!Forges.Contains(this))
            {
                Forges.Add(this);
            }
        }

        private void OnDisable()
        {
            Forges.Remove(this);
            SetFire(false);

            // Al destruirse o desactivarse la fundicion, sus trabajadores
            // vuelven a quedar libres para buscar otra fundicion.
            for (int i = _assignedWorkers.Count - 1; i >= 0; i--)
            {
                WorkerBuilder worker = _assignedWorkers[i];
                _assignedWorkers.RemoveAt(i);

                if (worker != null)
                {
                    worker.NotifyForgeUnavailable(this);
                }
            }

            RefreshWorkerLabel();

            if (_workerCanvas != null)
            {
                _workerCanvas.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            // El lienzo vive en la raiz de la escena para no heredar la
            // escala del edificio; hay que destruirlo a mano.
            if (_workerCanvas != null)
            {
                Destroy(_workerCanvas.gameObject);
                _workerCanvas = null;
            }
        }

        /// <summary>
        /// Entrega fisica de un trabajador: acepta solo cobre y estano,
        /// que se acumulan en el stock local de la fundicion.
        /// </summary>
        public void Deposit(ResourceType type, int amount)
        {
            amount = Mathf.Max(0, amount);

            if (type == ResourceType.Copper)
            {
                _copperStock += amount;
            }
            else if (type == ResourceType.Tin)
            {
                _tinStock += amount;
            }
        }

        /// <summary>Fundicion activa mas cercana dentro de la distancia dada.</summary>
        public static SmeltingForge FindNearest(Vector3 position, float maxDistance)
        {
            SmeltingForge best = null;
            float bestSqr = maxDistance * maxDistance;

            for (int i = Forges.Count - 1; i >= 0; i--)
            {
                SmeltingForge forge = Forges[i];

                if (forge == null)
                {
                    Forges.RemoveAt(i);
                    continue;
                }

                if (!forge.isActiveAndEnabled)
                {
                    continue;
                }

                float sqr = (forge.transform.position - position).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = forge;
                }
            }

            return best;
        }

        /// <summary>
        /// Fundicion activa mas cercana que todavia tenga una plaza libre.
        /// </summary>
        public static SmeltingForge FindNearestWithFreeSlot(Vector3 position, float maxDistance)
        {
            SmeltingForge best = null;
            float bestSqr = maxDistance * maxDistance;

            for (int i = Forges.Count - 1; i >= 0; i--)
            {
                SmeltingForge forge = Forges[i];

                if (forge == null)
                {
                    Forges.RemoveAt(i);
                    continue;
                }

                if (!forge.isActiveAndEnabled || !forge.IsOperational || !forge.HasFreeWorkerSlot)
                {
                    continue;
                }

                float sqr = (forge.transform.position - position).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = forge;
                }
            }

            return best;
        }

        private void Update()
        {
            _workerPruneTimer -= Time.deltaTime;

            if (_workerPruneTimer <= 0f)
            {
                _workerPruneTimer = 1f;
                PruneWorkers();
            }

            if (!_siteCached)
            {
                _site = GetComponent<ConstructionSite>();
                _siteCached = true;
            }

            if (_site != null && !_site.IsComplete)
            {
                SetFire(false);
                return;
            }

            // Sin trabajadores la fundicion se apaga y no produce.
            if (_assignedWorkers.Count <= 0)
            {
                SetFire(false);
                return;
            }

            _cooldown -= Time.deltaTime;

            if (_cooldown > 0f)
            {
                return;
            }

            if (_copperStock >= copperPerSmelt && _tinStock >= tinPerSmelt)
            {
                _copperStock -= copperPerSmelt;
                _tinStock -= tinPerSmelt;
                PlayerEconomy.Add(ResourceType.Bronze, bronzePerSmelt);
                _cooldown = GetSmeltInterval();
                SetFire(true);
            }
            else
            {
                SetFire(false);
            }
        }

        /// <summary>
        /// Con dos trabajadores asignados la fundicion funde al doble de
        /// velocidad. Sin trabajadores no produce (Update lo corta antes),
        /// y con uno mantiene el ritmo normal.
        /// </summary>
        private float GetSmeltInterval()
        {
            int workers = Mathf.Clamp(_assignedWorkers.Count, 1, maxWorkers);
            return smeltInterval / workers;
        }

        private void PruneWorkers()
        {
            bool changed = false;

            for (int i = _assignedWorkers.Count - 1; i >= 0; i--)
            {
                WorkerBuilder worker = _assignedWorkers[i];

                if (worker == null || !worker.isActiveAndEnabled)
                {
                    _assignedWorkers.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
            {
                RefreshWorkerLabel();
            }
        }

        private void LateUpdate()
        {
            UpdateWorkerLabel();

            // Pulso del fuego mientras el horno trabaja
            if (firePulseSpeed <= 0f || !_fireActive || fireGlow == null || !fireGlow.activeSelf)
            {
                return;
            }

            _pulseTime += Time.deltaTime * firePulseSpeed;
            float pulse = 1f + Mathf.Sin(_pulseTime) * 0.15f;
            fireGlow.transform.localScale = _fireBaseScale * pulse;
        }

        private void UpdateWorkerLabel()
        {
            if (_workerCanvas == null)
            {
                BuildWorkerLabel();
            }

            if (_workerCanvas == null)
            {
                return;
            }

            bool complete = _siteCached && (_site == null || _site.IsComplete);

            if (_workerCanvas.gameObject.activeSelf != complete)
            {
                _workerCanvas.gameObject.SetActive(complete);
            }

            if (!complete)
            {
                return;
            }

            if (_workerCamera == null)
            {
                _workerCamera = Camera.main;
            }

            if (_workerCamera != null)
            {
                _workerCanvas.transform.rotation = _workerCamera.transform.rotation;
            }
        }

        private void BuildWorkerLabel()
        {
            if (_workerCanvas != null)
            {
                return;
            }

            float top = transform.position.y;
            bool hasBounds = false;
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null ||
                    renderer is ParticleSystemRenderer ||
                    renderer is TrailRenderer ||
                    renderer is LineRenderer)
                {
                    continue;
                }

                float y = renderer.bounds.max.y;

                if (!hasBounds || y > top)
                {
                    top = y;
                    hasBounds = true;
                }
            }

            GameObject canvasObject = new GameObject(
                "Forge Workers Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));

            // Sin padre para no heredar la escala del edificio.
            canvasObject.transform.position = new Vector3(
                transform.position.x,
                top + 0.85f,
                transform.position.z);
            canvasObject.transform.rotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one * 0.01f;

            _workerCanvas = canvasObject.GetComponent<Canvas>();
            _workerCanvas.renderMode = RenderMode.WorldSpace;
            _workerCanvas.sortingOrder = 160;

            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(90f, 44f);

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

            _workerBackground = backgroundObject.GetComponent<Image>();
            _workerBackground.raycastTarget = false;

            GameObject textObject = new GameObject(
                "Text",
                typeof(RectTransform),
                typeof(Text));
            textObject.transform.SetParent(canvasObject.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(2f, 2f);
            textRect.offsetMax = new Vector2(-2f, -2f);

            _workerText = textObject.GetComponent<Text>();
            _workerText.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (_workerText.font == null)
            {
                _workerText.font = UnityEngine.Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            _workerText.fontSize = 28;
            _workerText.fontStyle = FontStyle.Bold;
            _workerText.alignment = TextAnchor.MiddleCenter;
            _workerText.color = Color.white;
            _workerText.raycastTarget = false;

            RefreshWorkerLabel();
        }

        private void RefreshWorkerLabel()
        {
            if (_workerText == null)
            {
                return;
            }

            int count = _assignedWorkers.Count;
            _workerText.text = count + "/" + maxWorkers;

            if (_workerBackground == null)
            {
                return;
            }

            if (count >= maxWorkers)
            {
                _workerBackground.color = new Color(0.18f, 0.5f, 0.22f, 0.95f);
            }
            else if (count > 0)
            {
                _workerBackground.color = new Color(0.62f, 0.42f, 0.12f, 0.95f);
            }
            else
            {
                _workerBackground.color = new Color(0.09f, 0.1f, 0.13f, 0.9f);
            }
        }

        private void SetFire(bool active)
        {
            if (_fireActive == active)
            {
                return;
            }

            _fireActive = active;

            if (fireGlow == null)
            {
                return;
            }

            if (active && !_fireScaleCaptured)
            {
                _fireBaseScale = fireGlow.transform.localScale;
                _fireScaleCaptured = true;
            }

            fireGlow.SetActive(active);
        }
    }
}
