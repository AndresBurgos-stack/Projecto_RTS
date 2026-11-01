using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectoRTS.Waves
{
    /// <summary>
    /// Simple HUD: day counter, advance warning with the next wave
    /// composition and the Pantheon health bar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveHud : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WaveManager waves;
        [SerializeField] private DayNightCalendar calendar;
        [SerializeField] private PantheonObjective objective;
        [SerializeField] private Text dayLabel;
        [SerializeField] private Text warningLabel;
        [SerializeField] private Image objectiveFill;

        private float _fullWidth = 1f;
        private StringBuilder _builder = new StringBuilder(128);

        private void Awake()
        {
            if (waves == null)
            {
                waves = FindFirstObjectByType<WaveManager>();
            }

            if (calendar == null)
            {
                calendar = FindFirstObjectByType<DayNightCalendar>();
            }

            if (objective == null)
            {
                objective = FindFirstObjectByType<PantheonObjective>();
            }

            if (objectiveFill != null)
            {
                _fullWidth = Mathf.Max(0.01f, objectiveFill.rectTransform.sizeDelta.x);
            }
        }

        private void OnEnable()
        {
            if (waves != null)
            {
                waves.WaveWarning += HandleWaveWarning;
                waves.WaveStarted += HandleWaveStarted;
                waves.GameOver += HandleGameOver;
            }

            if (objective != null)
            {
                objective.HealthChanged += HandleObjectiveHealth;
            }
        }

        private void OnDisable()
        {
            if (waves != null)
            {
                waves.WaveWarning -= HandleWaveWarning;
                waves.WaveStarted -= HandleWaveStarted;
                waves.GameOver -= HandleGameOver;
            }

            if (objective != null)
            {
                objective.HealthChanged -= HandleObjectiveHealth;
            }
        }

        private void Start()
        {
            RefreshDay();
            RefreshObjective();

            if (warningLabel != null)
            {
                warningLabel.text = string.Empty;
            }
        }

        private void RefreshDay()
        {
            if (dayLabel == null || calendar == null)
            {
                return;
            }

            dayLabel.text = $"Día {calendar.CurrentDay}";
        }

        private void HandleWaveWarning(WaveDefinition wave)
        {
            if (warningLabel == null || wave == null)
            {
                return;
            }

            _builder.Clear();
            _builder.Append("Aviso: ");
            _builder.Append(wave.DisplayName);
            _builder.Append(" en camino (");

            for (int i = 0; i < wave.Groups.Count; i++)
            {
                WaveGroup group = wave.Groups[i];
                if (group == null || group.unit == null)
                {
                    continue;
                }

                if (i > 0)
                {
                    _builder.Append(", ");
                }

                _builder.Append(group.count);
                _builder.Append(' ');
                _builder.Append(group.unit.DisplayName);
            }

            _builder.Append(')');
            warningLabel.text = _builder.ToString();
        }

        private void HandleWaveStarted(WaveDefinition wave)
        {
            if (warningLabel == null)
            {
                return;
            }

            warningLabel.text = wave != null
                ? $"Oleada {wave.DisplayName} iniciada"
                : string.Empty;
        }

        private void HandleObjectiveHealth(PantheonObjective target)
        {
            RefreshObjective();
        }

        private void RefreshObjective()
        {
            if (objectiveFill == null || objective == null)
            {
                return;
            }

            float normalized = Mathf.Clamp01(objective.HealthNormalized);
            Vector2 size = objectiveFill.rectTransform.sizeDelta;
            size.x = _fullWidth * normalized;
            objectiveFill.rectTransform.sizeDelta = size;
        }

        private void HandleGameOver()
        {
            if (warningLabel != null)
            {
                warningLabel.text = "El Panteón ha caído. Derrota.";
            }
        }
    }
}
