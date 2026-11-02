using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectoRTS.Waves
{
    /// <summary>
    /// Runs a fixed calendar of waves. Every day has one wave; the manager
    /// announces the next wave a few seconds early so the player can read
    /// its composition, then hands the wave to the RomanSpawner and waits
    /// for the squad to finish its march.
    /// The manager does not spawn units itself: RomanSpawner owns the
    /// spawn point, the warning marker and the group advance.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DayNightCalendar calendar;
        [SerializeField] private PantheonObjective objective;
        [SerializeField] private RomanSpawner romanSpawner;

        [Header("Calendar")]
        [SerializeField] private List<WaveDefinition> waves = new List<WaveDefinition>();
        [SerializeField, Min(1f)] private float warningLeadTime = 8f;
        [SerializeField] private bool loopCalendar = true;
        [SerializeField] private bool autoStart = true;

        private int _waveIndex;
        private bool _warningRaised;
        private bool _running;
        private bool _gameOver;
        private WaveDefinition _activeWave;

        public event Action<WaveDefinition> WaveWarning;
        public event Action<WaveDefinition> WaveStarted;
        public event Action<WaveDefinition> WaveCleared;
        public event Action GameOver;

        public WaveDefinition NextWave =>
            waves.Count > 0 ? waves[Mathf.Clamp(_waveIndex, 0, waves.Count - 1)] : null;

        public int CurrentDay => calendar != null ? calendar.CurrentDay : 1;
        public bool IsRunning => _running;
        public bool IsGameOver => _gameOver;

        private void Awake()
        {
            if (calendar == null)
            {
                calendar = FindFirstObjectByType<DayNightCalendar>();
            }

            if (objective == null)
            {
                objective = FindFirstObjectByType<PantheonObjective>();
            }

            if (romanSpawner == null)
            {
                romanSpawner = FindFirstObjectByType<RomanSpawner>();
            }
        }

        private void OnEnable()
        {
            if (calendar != null)
            {
                calendar.DayChanged += HandleDayChanged;
            }

            if (objective != null)
            {
                objective.Destroyed += HandleObjectiveDestroyed;
            }

            if (romanSpawner != null)
            {
                romanSpawner.SquadArrived += HandleSquadArrived;
            }
        }

        private void OnDisable()
        {
            if (calendar != null)
            {
                calendar.DayChanged -= HandleDayChanged;
            }

            if (objective != null)
            {
                objective.Destroyed -= HandleObjectiveDestroyed;
            }

            if (romanSpawner != null)
            {
                romanSpawner.SquadArrived -= HandleSquadArrived;
            }
        }

        private void Start()
        {
            if (autoStart)
            {
                StartCalendar();
            }
        }

        public void StartCalendar()
        {
            if (_running || _gameOver)
            {
                return;
            }

            _running = true;
            _waveIndex = 0;
            _warningRaised = false;
        }

        private void HandleDayChanged(int day, DayNightCalendar.Phase phase)
        {
            if (!_running || _gameOver || waves.Count == 0)
            {
                return;
            }

            // Only start a wave when the calendar reaches the day the next
            // wave was authored for. Days without a matching wave stay free
            // so the player can build.
            WaveDefinition next = NextWave;
            if (next == null || next.Day != day)
            {
                return;
            }

            BeginWave();
        }

        private void Update()
        {
            if (!_running || _gameOver)
            {
                return;
            }

            WaveDefinition next = NextWave;
            if (_warningRaised || next == null || calendar == null)
            {
                return;
            }

            // Only warn when the next wave is authored for tomorrow, so
            // days without a wave stay quiet.
            bool nextIsTomorrow = next.Day == calendar.CurrentDay + 1;
            float timeLeft = (1f - calendar.TimeOfDay) * calendar.DayLength;
            if (nextIsTomorrow && timeLeft <= warningLeadTime)
            {
                _warningRaised = true;
                WaveWarning?.Invoke(next);
            }
        }

        private void BeginWave()
        {
            if (waves.Count == 0 || romanSpawner == null)
            {
                return;
            }

            WaveDefinition wave = waves[Mathf.Clamp(_waveIndex, 0, waves.Count - 1)];
            _waveIndex++;
            if (_waveIndex >= waves.Count && loopCalendar)
            {
                _waveIndex = 0;
            }

            _warningRaised = false;
            _activeWave = wave;
            romanSpawner.BeginWave(wave);
            WaveStarted?.Invoke(wave);
        }

        private void HandleSquadArrived()
        {
            if (_gameOver)
            {
                return;
            }

            WaveDefinition cleared = _activeWave;
            _activeWave = null;
            WaveCleared?.Invoke(cleared);
        }

        private void HandleObjectiveDestroyed(PantheonObjective target)
        {
            if (_gameOver)
            {
                return;
            }

            _gameOver = true;
            _running = false;
            if (romanSpawner != null)
            {
                romanSpawner.StopSpawning();
            }

            if (calendar != null)
            {
                calendar.SetPaused(true);
            }

            GameOver?.Invoke();
        }
    }
}