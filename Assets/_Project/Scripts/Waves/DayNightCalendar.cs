using System;
using UnityEngine;

namespace ProjectoRTS.Waves
{
    /// <summary>
    /// Fixed day/night cycle. Each day is split into dawn, day, dusk and
    /// night; the light and ambient color follow the phase. The wave manager
    /// uses <see cref="PhaseChanged"/> to time waves.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DayNightCalendar : MonoBehaviour
    {
        public enum Phase
        {
            Dawn = 0,
            Day = 1,
            Dusk = 2,
            Night = 3
        }

        [Header("References")]
        [SerializeField] private Light sun;

        [Header("Cycle")]
        [SerializeField, Min(5f)] private float dayLength = 60f;
        [SerializeField, Min(1)] private int startDay = 1;
        [SerializeField, Range(0f, 1f)] private float startTimeOfDay = 0.05f;
        [SerializeField] private bool paused;

        [Header("Colors")]
        [SerializeField] private Color dawnColor = new Color(0.95f, 0.7f, 0.55f);
        [SerializeField] private Color dayColor = new Color(1f, 0.97f, 0.9f);
        [SerializeField] private Color duskColor = new Color(0.85f, 0.5f, 0.35f);
        [SerializeField] private Color nightColor = new Color(0.28f, 0.34f, 0.55f);
        [SerializeField] private Color nightAmbient = new Color(0.12f, 0.14f, 0.22f);
        [SerializeField] private Color dayAmbient = new Color(0.35f, 0.36f, 0.4f);

        public event Action<int, Phase> DayChanged;
        public event Action<Phase> PhaseChanged;

        public int CurrentDay { get; private set; }
        public Phase CurrentPhase { get; private set; }
        public float TimeOfDay { get; private set; }
        public float DayLength => dayLength;

        private Phase _lastPhase;

        private void Awake()
        {
            CurrentDay = Mathf.Max(1, startDay);
            TimeOfDay = Mathf.Clamp01(startTimeOfDay);
            CurrentPhase = PhaseFromTime(TimeOfDay);
            _lastPhase = CurrentPhase;
        }

        private void Start()
        {
            ApplyPhase(CurrentPhase);
            DayChanged?.Invoke(CurrentDay, CurrentPhase);
            PhaseChanged?.Invoke(CurrentPhase);
        }

        private void Update()
        {
            if (paused)
            {
                return;
            }

            TimeOfDay += Time.deltaTime / dayLength;
            while (TimeOfDay >= 1f)
            {
                TimeOfDay -= 1f;
                CurrentDay++;
                DayChanged?.Invoke(CurrentDay, CurrentPhase);
            }

            Phase phase = PhaseFromTime(TimeOfDay);
            if (phase != _lastPhase)
            {
                _lastPhase = phase;
                CurrentPhase = phase;
                ApplyPhase(phase);
                PhaseChanged?.Invoke(phase);
            }

            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(
                    Mathf.Lerp(-10f, 190f, TimeOfDay),
                    -30f,
                    0f);
            }
        }

        public void SetPaused(bool value)
        {
            paused = value;
        }

        public static Phase PhaseFromTime(float timeOfDay)
        {
            if (timeOfDay < 0.2f) return Phase.Dawn;
            if (timeOfDay < 0.65f) return Phase.Day;
            if (timeOfDay < 0.8f) return Phase.Dusk;
            return Phase.Night;
        }

        private void ApplyPhase(Phase phase)
        {
            if (sun == null)
            {
                return;
            }

            switch (phase)
            {
                case Phase.Dawn:
                    sun.color = dawnColor;
                    sun.intensity = 0.8f;
                    RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, 0.5f);
                    break;
                case Phase.Day:
                    sun.color = dayColor;
                    sun.intensity = 1.2f;
                    RenderSettings.ambientLight = dayAmbient;
                    break;
                case Phase.Dusk:
                    sun.color = duskColor;
                    sun.intensity = 0.7f;
                    RenderSettings.ambientLight = Color.Lerp(dayAmbient, nightAmbient, 0.6f);
                    break;
                default:
                    sun.color = nightColor;
                    sun.intensity = 0.25f;
                    RenderSettings.ambientLight = nightAmbient;
                    break;
            }
        }
    }
}
