using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectoRTS.Units
{
    public enum Faction
    {
        None = 0,
        Greek = 1,
        Roman = 2
    }

    public enum UnitRole
    {
        Infantry = 0,
        Ranged = 1,
        Cavalry = 2,
        Siege = 3
    }

    /// <summary>
    /// Designer-authored data for a single unit type.
    /// Create assets from the menu: ProjectoRTS > Units > Unit Definition.
    /// </summary>
    [CreateAssetMenu(
        fileName = "UnitDefinition",
        menuName = "ProjectoRTS/Units/Unit Definition")]
    public sealed class UnitDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "New Unit";
        [SerializeField] private Faction faction = Faction.Greek;
        [SerializeField] private UnitRole role = UnitRole.Infantry;
        [SerializeField] private GameObject prefab;

        [Header("Combat")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Min(0f)] private float attackDamage = 10f;
        [SerializeField, Min(0f)] private float attackRange = 1.5f;
        [SerializeField, Min(0.05f)] private float attackInterval = 1f;
        [SerializeField, Range(0f, 0.95f)] private float damageReduction;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 3.5f;
        [SerializeField, Min(0f)] private float turnSpeed = 720f;
        [SerializeField, Min(0f)] private float stoppingDistance = 0.5f;

        [Header("Economy")]
        [FormerlySerializedAs("cost")]
        [SerializeField, Min(0)] private int woodCost = 50;
        [SerializeField, Min(0)] private int bronzeCost;
        [SerializeField, Min(0f)] private float trainTime = 5f;
        [SerializeField, Min(1)] private int populationCost = 1;

        public string DisplayName => displayName;
        public Faction Faction => faction;
        public UnitRole Role => role;
        public GameObject Prefab => prefab;

        public float MaxHealth => maxHealth;
        public float AttackDamage => attackDamage;
        public float AttackRange => attackRange;
        public float AttackInterval => attackInterval;
        public float DamageReduction => damageReduction;

        public float MoveSpeed => moveSpeed;
        public float TurnSpeed => turnSpeed;
        public float StoppingDistance => stoppingDistance;

        public int WoodCost => woodCost;
        public int BronzeCost => bronzeCost;
        public float TrainTime => trainTime;
        public int PopulationCost => populationCost;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = name;
            }
        }
    }
}
