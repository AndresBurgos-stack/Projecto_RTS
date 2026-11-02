using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectoRTS.Buildings
{
    public enum BuildingCategory
    {
        Wall = 0,
        Tower = 1,
        Production = 2,
        Economy = 3,
        Defense = 4
    }

    /// <summary>
    /// Designer-authored data for a placeable building.
    /// Create assets from the menu: ProjectoRTS > Buildings > Building Definition.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BuildingDefinition",
        menuName = "ProjectoRTS/Buildings/Building Definition")]
    public sealed class BuildingDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "New Building";
        [SerializeField] private BuildingCategory category = BuildingCategory.Wall;
        [SerializeField] private GameObject prefab;

        [Header("Grid Footprint (cells)")]
        [SerializeField, Min(1)] private int width = 1;
        [SerializeField, Min(1)] private int depth = 1;

        [Header("Economy")]
        [SerializeField, Min(0)] private int woodCost;
        [FormerlySerializedAs("stoneCost")]
        [SerializeField, Min(0)] private int bronzeCost;
        [SerializeField, Min(0)] private int goldCost;

        [Header("Combat")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Min(0f)] private float attackDamage;
        [SerializeField, Min(0f)] private float attackRange;

        public string DisplayName => displayName;
        public BuildingCategory Category => category;
        public GameObject Prefab => prefab;

        public int Width => width;
        public int Depth => depth;
        public Vector2Int Footprint => new Vector2Int(width, depth);

        public int WoodCost => woodCost;
        public int BronzeCost => bronzeCost;
        public int GoldCost => goldCost;

        public float MaxHealth => maxHealth;
        public float AttackDamage => attackDamage;
        public float AttackRange => attackRange;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = name;
            }
        }
    }
}
