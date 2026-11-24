using System.Collections.Generic;
using ProjectoRTS.Economy;
using ProjectoRTS.Resources;
using UnityEngine;

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
        [SerializeField, Min(1)] private int bronzePerSmelt = 1;

        [Header("Ritmo")]
        [SerializeField, Min(0.5f)] private float smeltInterval = 7f;

        [Header("Feedback")]
        [SerializeField] private GameObject fireGlow;
        [SerializeField, Min(0f)] private float firePulseSpeed = 0f;

        private int _copperStock;
        private int _tinStock;
        private float _cooldown;
        private ConstructionSite _site;
        private bool _siteCached;
        private bool _fireActive;
        private bool _fireScaleCaptured;
        private float _pulseTime;
        private Vector3 _fireBaseScale = Vector3.one;

        /// <summary>Todas las fundiciones activas de la escena.</summary>
        public static IReadOnlyList<SmeltingForge> All => Forges;

        public int CopperStock => _copperStock;
        public int TinStock => _tinStock;

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

        private void Update()
        {
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
                _cooldown = smeltInterval;
                SetFire(true);
            }
            else
            {
                SetFire(false);
            }
        }

        private void LateUpdate()
        {
            // Pulso del fuego mientras el horno trabaja
            if (firePulseSpeed <= 0f || !_fireActive || fireGlow == null || !fireGlow.activeSelf)
            {
                return;
            }

            _pulseTime += Time.deltaTime * firePulseSpeed;
            float pulse = 1f + Mathf.Sin(_pulseTime) * 0.15f;
            fireGlow.transform.localScale = _fireBaseScale * pulse;
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
