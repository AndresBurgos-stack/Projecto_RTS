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
        private const string ConstructionPath = "SFX/SonidoDeConstruccion";

        private const int MaxSources = 32;
        private const float UnitMinDistance = 4f;
        private const float UnitMaxDistance = 35f;
        private const float ConstructionMinDistance = 5f;
        private const float ConstructionMaxDistance = 40f;

        private static readonly List<AudioSource> Sources = new List<AudioSource>(MaxSources);
        private static readonly HashSet<string> MissingClips = new HashSet<string>();

        private static Transform _root;
        private static AudioClip _arrowRelease;
        private static AudioClip _arrowImpact;
        private static AudioClip _pilumImpact;
        private static AudioClip _swordStrike;
        private static AudioClip _construction;

        /// <summary>Loopable clip played by a building while it is under construction.</summary>
        public static AudioClip ConstructionClip =>
            LoadClip(ConstructionPath, ref _construction);

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

        /// <summary>Plays the sword hit sound at the point of impact.</summary>
        public static void PlaySwordStrike(Vector3 position)
        {
            PlayOneShot(
                LoadClip(SwordStrikePath, ref _swordStrike),
                position,
                0.6f,
                Random.Range(0.92f, 1.08f));
        }

        /// <summary>
        /// Adds a looping, spatialized construction AudioSource to the supplied
        /// building. Returns null when the clip is missing.
        /// </summary>
        public static AudioSource CreateConstructionSource(GameObject host)
        {
            AudioClip clip = ConstructionClip;

            if (host == null || clip == null)
            {
                return null;
            }

            AudioSource source = host.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.volume = 0.45f;
            source.minDistance = ConstructionMinDistance;
            source.maxDistance = ConstructionMaxDistance;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            return source;
        }

        private static void PlayOneShot(
            AudioClip clip,
            Vector3 position,
            float volume,
            float pitch)
        {
            if (clip == null)
            {
                return;
            }

            AudioSource source = GetSource();
            source.transform.position = position;
            source.clip = clip;
            source.loop = false;
            source.volume = volume;
            source.pitch = pitch;
            source.spatialBlend = 1f;
            source.minDistance = UnitMinDistance;
            source.maxDistance = UnitMaxDistance;
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
