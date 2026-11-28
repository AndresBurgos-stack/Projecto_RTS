using System.Collections.Generic;
using UnityEngine;

namespace ProjectoRTS.Audio
{
    /// <summary>
    /// Centralized one-shot sound effects for units and construction. The clips
    /// live under Resources/SFX so no prefab needs an AudioSource wired by hand.
    /// </summary>
    public static class GameAudio
    {
        private const string ArrowReleasePath = "SFX/ArrowRelease";
        private const string ArrowImpactPath = "SFX/ArrowImpact";
        private const string PilumImpactPath = "SFX/ImpactoLanzamp3";
        private const string SwordStrikePath = "SFX/SwordStrike";
        private const string SwordStrike2Path = "SFX/sword-hit2";
        private const string SwordStrike3Path = "SFX/sword-hit3";
        private const string ConstructionPath = "SFX/SonidoDeConstruccion";
        private const string DeathPath1 = "SFX/Death-Sound-1";
        private const string DeathPath2 = "SFX/Death-Sound-2";
        private const string DeathPath3 = "SFX/Death-Sound-3";
        private const string DeathPath4 = "SFX/Death-Sound-4";
        private const string WaveAlertPath = "AlertaOleada";

        private const int MaxSources = 32;
        private const float UnitMinDistance = 4f;
        private const float UnitMaxDistance = 35f;
        private const float ConstructionMinDistance = 5f;
        private const float ConstructionMaxDistance = 40f;
        // The clip opens with ~95 ms of silence before the hammer transient.
        private const float ConstructionHitOffset = 0.095f;

        private static readonly List<AudioSource> Sources = new List<AudioSource>(MaxSources);
        private static readonly HashSet<string> MissingClips = new HashSet<string>();

        private static Transform _root;
        private static AudioClip _arrowRelease;
        private static AudioClip _arrowImpact;
        private static AudioClip _pilumImpact;
        private static AudioClip _swordStrike;
        private static AudioClip _swordStrike2;
        private static AudioClip _swordStrike3;
        private static AudioClip _construction;
        private static AudioClip _death1;
        private static AudioClip _death2;
        private static AudioClip _death3;
        private static AudioClip _death4;
        private static AudioClip _waveAlert;

        /// <summary>Plays one hammer strike at the given hit position.</summary>
        public static void PlayConstructionHit(Vector3 position)
        {
            PlayOneShot(
                LoadClip(ConstructionPath, ref _construction),
                position,
                0.55f,
                Random.Range(0.94f, 1.06f),
                ConstructionMinDistance,
                ConstructionMaxDistance,
                ConstructionHitOffset);
        }

        /// <summary>Plays the bow/javelin release sound at the attacker.</summary>
        public static void PlayArrowRelease(Vector3 position)
        {
            PlayOneShot(
                LoadClip(ArrowReleasePath, ref _arrowRelease),
                position,
                0.5f,
                Random.Range(0.95f, 1.05f));
        }

        /// <summary>Plays the light projectile impact sound.</summary>
        public static void PlayArrowImpact(Vector3 position)
        {
            PlayOneShot(
                LoadClip(ArrowImpactPath, ref _arrowImpact),
                position,
                0.65f,
                Random.Range(0.95f, 1.05f));
        }

        /// <summary>Plays the heavy pilum impact sound.</summary>
        public static void PlayPilumImpact(Vector3 position)
        {
            PlayOneShot(
                LoadClip(PilumImpactPath, ref _pilumImpact),
                position,
                0.7f,
                Random.Range(0.97f, 1.03f));
        }

        /// <summary>Plays a random sword hit sound at the point of impact.</summary>
        public static void PlaySwordStrike(Vector3 position)
        {
            AudioClip clip;
            switch (Random.Range(0, 3))
            {
                case 1:
                    clip = LoadClip(SwordStrike2Path, ref _swordStrike2);
                    break;
                case 2:
                    clip = LoadClip(SwordStrike3Path, ref _swordStrike3);
                    break;
                default:
                    clip = LoadClip(SwordStrikePath, ref _swordStrike);
                    break;
            }

            PlayOneShot(
                clip,
                position,
                0.6f,
                Random.Range(0.92f, 1.08f));
        }

        /// <summary>Plays a heavy two-handed weapon hit (mace/axe): hammer thud or pilum-like thunk.</summary>
        public static void PlayHeavyStrike(Vector3 position)
        {
            bool hammer = Random.value < 0.5f;
            AudioClip clip = hammer
                ? LoadClip(ConstructionPath, ref _construction)
                : LoadClip(PilumImpactPath, ref _pilumImpact);
            float startTime = hammer ? ConstructionHitOffset : 0f;

            PlayOneShot(
                clip,
                position,
                0.7f,
                Random.Range(0.9f, 1.1f),
                UnitMinDistance,
                UnitMaxDistance,
                startTime);
        }

        /// <summary>Plays a random death groan at the fallen unit.</summary>
        public static void PlayDeath(Vector3 position)
        {
            AudioClip clip;
            float volume = 0.65f;

            switch (Random.Range(0, 4))
            {
                case 1:
                    clip = LoadClip(DeathPath2, ref _death2);
                    break;
                case 2:
                    clip = LoadClip(DeathPath3, ref _death3);
                    break;
                case 3:
                    clip = LoadClip(DeathPath4, ref _death4);
                    // Este gemido viene mas bajo en la mezcla: se sube un poco.
                    volume = 0.8f;
                    break;
                default:
                    clip = LoadClip(DeathPath1, ref _death1);
                    break;
            }

            PlayOneShot(
                clip,
                position,
                volume,
                Random.Range(0.9f, 1.1f),
                UnitMinDistance,
                UnitMaxDistance);
        }

        /// <summary>
        /// Plays the wave warning horn as a non-positional alert and returns
        /// its length in seconds so callers can duck the music meanwhile.
        /// </summary>
        public static float PlayWaveAlert()
        {
            AudioClip clip = LoadClip(WaveAlertPath, ref _waveAlert);
            PlayOneShot(
                clip,
                Vector3.zero,
                0.7f,
                1f,
                spatialBlend: 0f);

            return clip != null ? clip.length : 0f;
        }

        private static void PlayOneShot(
            AudioClip clip,
            Vector3 position,
            float volume,
            float pitch,
            float minDistance = UnitMinDistance,
            float maxDistance = UnitMaxDistance,
            float startTime = 0f,
            float spatialBlend = 1f)
        {
            if (clip == null)
            {
                return;
            }

            AudioSource source = GetSource();
            source.transform.position = position;
            source.clip = clip;
            source.loop = false;
            source.time = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, clip.length - 0.01f));
            source.volume = volume * GameSettings.SfxVolume;
            source.pitch = pitch;
            source.spatialBlend = spatialBlend;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.Play();
        }

        private static AudioSource GetSource()
        {
            EnsureRoot();

            for (int i = Sources.Count - 1; i >= 0; i--)
            {
                if (Sources[i] == null)
                {
                    Sources.RemoveAt(i);
                }
            }

            for (int i = 0; i < Sources.Count; i++)
            {
                if (!Sources[i].isPlaying)
                {
                    return Sources[i];
                }
            }

            if (Sources.Count < MaxSources)
            {
                GameObject sourceObject = new GameObject("SFX Source");
                sourceObject.transform.SetParent(_root, false);

                AudioSource source = sourceObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                Sources.Add(source);
                return source;
            }

            // The pool is saturated: reuse the oldest source instead of
            // dropping the sound entirely.
            return Sources[0];
        }

        private static void EnsureRoot()
        {
            if (_root != null)
            {
                return;
            }

            GameObject root = new GameObject("Game Audio");
            UnityEngine.Object.DontDestroyOnLoad(root);
            _root = root.transform;
            Sources.Clear();
        }

        private static AudioClip LoadClip(string path, ref AudioClip cache)
        {
            if (cache != null)
            {
                return cache;
            }

            if (MissingClips.Contains(path))
            {
                return null;
            }

            cache = UnityEngine.Resources.Load<AudioClip>(path);

            if (cache == null)
            {
                MissingClips.Add(path);
                Debug.LogWarning($"[GameAudio] No se encontro el clip '{path}' en Resources.");
            }

            return cache;
        }
    }
}
