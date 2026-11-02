using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectoRTS.Waves
{
    /// <summary>
    /// Simple HUD: day counter and advance warning with the next wave
    /// composition.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveHud : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WaveManager waves;
        [SerializeField] private DayNightCalendar calendar;
        [SerializeField] private Text dayLabel;
        [SerializeField] private Text warningLabel;

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

        }

        private void OnEnable()
        {
            if (waves != null)
            {
                waves.WaveWarning += HandleWaveWarning;
                waves.WaveStarted += HandleWaveStarted;
                waves.GameOver += HandleGameOver;
            }

            if (calendar != null)
            {
                calendar.DayChanged += HandleDayChanged;
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

            if (calendar != null)
            {
                calendar.DayChanged -= HandleDayChanged;
            }
        }

        private void Start()
        {
            RefreshDay();

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

            dayLabel.text = $"Dia {calendar.CurrentDay}";
        }

        private void HandleDayChanged(int day, DayNightCalendar.Phase phase)
        {
            RefreshDay();
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

        private void HandleGameOver()
        {
            if (warningLabel != null)
            {
                warningLabel.text = "El Panteón ha caído. Derrota.";
            }
        }
    }
}
