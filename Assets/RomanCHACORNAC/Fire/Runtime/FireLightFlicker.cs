using UnityEngine;

namespace RomanCHACORNAC.Fire
{
    [RequireComponent(typeof(Light))]
    [DisallowMultipleComponent]
    [AddComponentMenu("RomanCHACORNAC/Fire Light Flicker")]
    public sealed class FireLightFlicker : MonoBehaviour
    {
        [Tooltip("How much the intensity varies, as a fraction of the Light intensity.")]
        [Range(0f, 1f)] public float intensityAmount = 0.25f;

        [Tooltip("How much the range varies, as a fraction of the Light range.")]
        [Range(0f, 0.5f)] public float rangeAmount = 0.05f;

        [Tooltip("Flicker speed.")]
        [Min(0f)] public float speed = 3f;

        Light _light;
        float _baseIntensity;
        float _baseRange;
        float _seed;

        void OnEnable()
        {
            _light         = GetComponent<Light>();
            _baseIntensity = _light.intensity;
            _baseRange     = _light.range;
            _seed          = Random.value * 100f;
        }

        void OnDisable()
        {
            if (_light == null)
                return;
            _light.intensity = _baseIntensity;
            _light.range     = _baseRange;
        }

        void Update()
        {
            float t     = Time.time * speed;
            float noise = Mathf.PerlinNoise(t, _seed) * 0.7f + Mathf.PerlinNoise(t * 2.7f, _seed + 3.1f) * 0.3f;
            float wave  = (noise - 0.5f) * 2f;

            _light.intensity = _baseIntensity * (1f + wave * intensityAmount);
            _light.range     = _baseRange * (1f + wave * rangeAmount);
        }
    }
}
