using System;
using System.Collections.Generic;
using ProjectoRTS.Units;
using UnityEngine;

namespace ProjectoRTS.Waves
{
    /// <summary>One spawn group inside a wave.</summary>
    [Serializable]
    public sealed class WaveGroup
    {
        public UnitDefinition unit;
        [Min(1)] public int count = 3;
        [Min(0f)] public float delay = 0f;
    }

    /// <summary>
    /// Designer-authored wave. A calendar is a fixed sequence of these, so
    /// the player can read the composition in advance.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WaveDefinition",
        menuName = "ProjectoRTS/Waves/Wave Definition")]
    public sealed class WaveDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "Oleada";
        [SerializeField, Min(1)] private int day = 1;

        [Header("Composition")]
        [SerializeField] private List<WaveGroup> groups = new List<WaveGroup>();

        [Header("Timing")]
        [SerializeField, Min(0f)] private float spawnInterval = 1.5f;
        [SerializeField, Min(0f)] private float restAfterWave = 20f;

        public string DisplayName => displayName;
        public int Day => day;
        public IReadOnlyList<WaveGroup> Groups => groups;
        public float SpawnInterval => spawnInterval;
        public float RestAfterWave => restAfterWave;

        /// <summary>Total units in the wave, used for the advance warning.</summary>
        public int TotalUnits
        {
            get
            {
                int total = 0;
                for (int i = 0; i < groups.Count; i++)
                {
                    if (groups[i] != null && groups[i].unit != null)
                    {
                        total += groups[i].count;
                    }
                }

                return total;
            }
        }
    }
}
