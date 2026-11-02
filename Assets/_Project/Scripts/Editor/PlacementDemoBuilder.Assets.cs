#if UNITY_EDITOR
using ProjectoRTS.Buildings;
using ProjectoRTS.Combat;
using ProjectoRTS.Resources;
using ProjectoRTS.Units;
using ProjectoRTS.Waves;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace ProjectoRTS.EditorTools
{
    public static partial class PlacementDemoBuilder
    {
        private sealed class UnitConfig
        {
            public string AssetName;
            public string DisplayName;
            public Faction Faction;
            public UnitRole Role;
            public Vector3 Size;
            public Material Material;
            public float MaxHealth;
            public float AttackDamage;
            public float AttackRange;
            public float AttackInterval;
            public float DamageReduction;
            public float MoveSpeed;
            public float TurnSpeed = 720f;
            public float StoppingDistance = 0.5f;
            public int WoodCost;
            public int BronzeCost;
            public float TrainTime;
            public int PopulationCost;
        }

        private sealed class VisualPalette
        {
            public Material Stone;
            public Material Marble;
            public Material Terracotta;
            public Material Wood;
            public Material Bronze;
            public Material Leather;
            public Material Iron;
            public Material Skin;
            public Material Crest;
            public Material GreekAccent;
        }

        private static GameObject CreatePart(
            PrimitiveType type,
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool keepCollider = false)
        {
            return CreatePart(
                type,
                name,
                parent,
                localPosition,
                localScale,
                Quaternion.identity,
                material,
                keepCollider);
        }

        private static GameObject CreatePart(
            PrimitiveType type,
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Material material,
            bool keepCollider = false)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = localScale;

            if (!keepCollider)
            {
                Collider collider = part.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.DestroyImmediate(collider);
                }
            }

            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (material != null)
                {
                    renderer.sharedMaterial = material;
                }

                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            return part;
        }

        private static VisualPalette CreateVisualPalette()
        {
            Material stone = GetOrCreateMaterial(
                "DetailStone",
                new Color(0.58f, 0.56f, 0.52f, 1f));
            Material marble = GetOrCreateMaterial(
                "Marble",
                new Color(0.86f, 0.84f, 0.78f, 1f));
            Material terracotta = GetOrCreateMaterial(
                "Terracotta",
                new Color(0.66f, 0.3f, 0.18f, 1f));
            Material wood = GetOrCreateMaterial(
                "DetailWood",
                new Color(0.35f, 0.22f, 0.12f, 1f));
            Material bronze = GetOrCreateMaterial(
                "Bronze",
                new Color(0.72f, 0.52f, 0.2f, 1f));
            Material leather = GetOrCreateMaterial(
                "Leather",
                new Color(0.32f, 0.22f, 0.15f, 1f));
            Material iron = GetOrCreateMaterial(
                "Iron",
                new Color(0.62f, 0.64f, 0.68f, 1f));
            Material skin = GetOrCreateMaterial(
                "Skin",
                new Color(0.83f, 0.64f, 0.5f, 1f));
            Material crest = GetOrCreateMaterial(
                "Crest",
                new Color(0.78f, 0.2f, 0.18f, 1f));
            Material greekAccent = GetOrCreateMaterial(
                "GreekAccent",
                new Color(0.24f, 0.42f, 0.68f, 1f));

            SetMaterialFinish(bronze, 0.65f, 0.85f);
            SetMaterialFinish(iron, 0.55f, 0.8f);
            SetMaterialFinish(marble, 0.35f, 0f);
            SetMaterialFinish(stone, 0.15f, 0f);

            return new VisualPalette
            {
                Stone = stone,
                Marble = marble,
                Terracotta = terracotta,
                Wood = wood,
                Bronze = bronze,
                Leather = leather,
                Iron = iron,
                Skin = skin,
                Crest = crest,
                GreekAccent = greekAccent
            };
        }

        private static void SetMaterialFinish(
            Material material,
            float smoothness,
            float metallic)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            EditorUtility.SetDirty(material);
        }

        private static BuildingDefinition CreateBuildingDefinition(
            string assetName,
            string displayName,
            BuildingCategory category,
            Vector2Int footprint,
            Vector3 size,
            Material material,
            int woodCost,
            int bronzeCost,
            float maxHealth,
            float attackDamage = 0f,
            float attackRange = 0f,
            System.Action<GameObject> configurePrefab = null,
            VisualPalette palette = null)
        {
            string prefabPath = $"{PrefabFolder}/{assetName}.prefab";
            GameObject root = new GameObject(assetName);
            GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.name = "Mesh";
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            mesh.transform.localScale = size;

            MeshRenderer renderer = mesh.GetComponent<MeshRenderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            NavMeshObstacle obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0f, size.y * 0.5f, 0f);
            obstacle.size = new Vector3(footprint.x, size.y, footprint.y);
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;

            CreateBuildingDetails(root, assetName, footprint, size, material, palette);

            configurePrefab?.Invoke(root);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            if (prefab == null)
            {
                Debug.LogError($"Could not create the prefab at {prefabPath}.");
                return null;
            }

            string assetPath = $"{DataFolder}/{assetName}.asset";
            BuildingDefinition definition = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<BuildingDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("category").enumValueIndex = (int)category;
            serialized.FindProperty("prefab").objectReferenceValue = prefab;
            serialized.FindProperty("width").intValue = footprint.x;
            serialized.FindProperty("depth").intValue = footprint.y;
            serialized.FindProperty("woodCost").intValue = woodCost;
            serialized.FindProperty("bronzeCost").intValue = bronzeCost;
            serialized.FindProperty("goldCost").intValue = 0;
            serialized.FindProperty("maxHealth").floatValue = maxHealth;
            serialized.FindProperty("attackDamage").floatValue = attackDamage;
            serialized.FindProperty("attackRange").floatValue = attackRange;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            return definition;
        }

        private static GameObject CreateUnitVisual(
            Transform parent,
            UnitConfig config,
            VisualPalette palette)
        {
            GameObject visuals = new GameObject("Visuals");
            visuals.transform.SetParent(parent, false);
            float scale = config.Size.y / 1.8f;
            visuals.transform.localScale = new Vector3(scale, scale, scale);

            Material tunic = config.Material;
            Material bronze = palette != null ? palette.Bronze : tunic;
            Material leather = palette != null ? palette.Leather : tunic;
            Material wood = palette != null ? palette.Wood : tunic;
            Material iron = palette != null ? palette.Iron : tunic;
            Material skin = palette != null ? palette.Skin : tunic;
            Material crest = palette != null ? palette.Crest : tunic;

            bool roman = config.Faction == Faction.Roman;
            bool ranged = config.Role == UnitRole.Ranged;

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.13f;
                CreatePart(
                    PrimitiveType.Cylinder,
                    "Leg",
                    visuals.transform,
                    new Vector3(x, 0.35f, 0f),
                    new Vector3(0.13f, 0.35f, 0.13f),
                    skin);
                CreatePart(
                    PrimitiveType.Cube,
                    "Sandal",
                    visuals.transform,
                    new Vector3(x, 0.04f, 0.04f),
                    new Vector3(0.18f, 0.08f, 0.28f),
                    leather);

                if (!ranged)
                {
                    CreatePart(
                        PrimitiveType.Cylinder,
                        "Greave",
                        visuals.transform,
                        new Vector3(x, 0.3f, 0.02f),
                        new Vector3(0.15f, 0.22f, 0.15f),
                        bronze);
                }
            }

            CreatePart(
                PrimitiveType.Cylinder,
                "Torso",
                visuals.transform,
                new Vector3(0f, 1.0f, 0f),
                new Vector3(0.3f, 0.3f, 0.22f),
                tunic);
            CreatePart(
                PrimitiveType.Cube,
                "Belt",
                visuals.transform,
                new Vector3(0f, 0.76f, 0f),
                new Vector3(0.62f, 0.08f, 0.46f),
                leather);

            if (!ranged)
            {
                CreatePart(
                    PrimitiveType.Cube,
                    "Cape",
                    visuals.transform,
                    new Vector3(0f, 0.98f, -0.23f),
                    new Vector3(0.5f, 0.6f, 0.04f),
                    tunic);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.34f;
                CreatePart(
                    PrimitiveType.Capsule,
                    "Arm",
                    visuals.transform,
                    new Vector3(x, 1.05f, 0.03f),
                    new Vector3(0.12f, 0.22f, 0.12f),
                    skin);
                CreatePart(
                    PrimitiveType.Sphere,
                    "Hand",
                    visuals.transform,
                    new Vector3(x + side * 0.03f, 0.85f, 0.12f),
                    new Vector3(0.1f, 0.1f, 0.1f),
                    skin);
            }

            CreatePart(
                PrimitiveType.Sphere,
                "Head",
                visuals.transform,
                new Vector3(0f, 1.5f, 0f),
                new Vector3(0.28f, 0.3f, 0.28f),
                skin);

            if (ranged)
            {
                CreatePart(
                    PrimitiveType.Sphere,
                    "Cap",
                    visuals.transform,
                    new Vector3(0f, 1.58f, 0f),
                    new Vector3(0.32f, 0.2f, 0.32f),
                    leather);
                CreatePart(
                    PrimitiveType.Cylinder,
                    "Cap Brim",
                    visuals.transform,
                    new Vector3(0f, 1.48f, 0f),
                    new Vector3(0.38f, 0.03f, 0.38f),
                    leather);
            }
            else
            {
                CreatePart(
                    PrimitiveType.Sphere,
                    "Helmet",
                    visuals.transform,
                    new Vector3(0f, 1.58f, 0f),
                    new Vector3(0.34f, 0.26f, 0.34f),
                    bronze);
                CreatePart(
                    PrimitiveType.Cylinder,
                    "Helmet Brim",
                    visuals.transform,
                    new Vector3(0f, 1.47f, 0f),
                    new Vector3(0.4f, 0.03f, 0.4f),
                    bronze);
                CreatePart(
                    PrimitiveType.Cube,
                    "Nose Guard",
                    visuals.transform,
                    new Vector3(0f, 1.47f, 0.16f),
                    new Vector3(0.06f, 0.18f, 0.04f),
                    bronze);
                CreatePart(
                    PrimitiveType.Cube,
                    "Crest",
                    visuals.transform,
                    new Vector3(0f, 1.78f, 0f),
                    new Vector3(
                        roman ? 0.06f : 0.08f,
                        roman ? 0.12f : 0.2f,
                        roman ? 0.3f : 0.46f),
                    crest);

                if (roman)
                {
                    CreatePart(
                        PrimitiveType.Cube,
                        "Cheek Guard Left",
                        visuals.transform,
                        new Vector3(-0.18f, 1.48f, 0.06f),
                        new Vector3(0.05f, 0.18f, 0.12f),
                        bronze);
                    CreatePart(
                        PrimitiveType.Cube,
                        "Cheek Guard Right",
                        visuals.transform,
                        new Vector3(0.18f, 1.48f, 0.06f),
                        new Vector3(0.05f, 0.18f, 0.12f),
                        bronze);
                }
            }

            if (roman)
            {
                CreatePart(
                    PrimitiveType.Cube,
                    "Scutum Back",
                    visuals.transform,
                    new Vector3(-0.42f, 1.0f, 0.16f),
                    new Vector3(0.6f, 0.8f, 0.04f),
                    bronze);
                CreatePart(
                    PrimitiveType.Cube,
                    "Scutum",
                    visuals.transform,
                    new Vector3(-0.42f, 1.0f, 0.2f),
                    new Vector3(0.52f, 0.72f, 0.06f),
                    tunic);
                CreatePart(
                    PrimitiveType.Cube,
                    "Scutum Strip",
                    visuals.transform,
                    new Vector3(-0.42f, 1.0f, 0.24f),
                    new Vector3(0.06f, 0.68f, 0.02f),
                    bronze);
                CreatePart(
                    PrimitiveType.Sphere,
                    "Scutum Boss",
                    visuals.transform,
                    new Vector3(-0.42f, 1.0f, 0.26f),
                    new Vector3(0.12f, 0.12f, 0.06f),
                    bronze);
                CreatePart(
                    PrimitiveType.Cube,
                    "Gladius",
                    visuals.transform,
                    new Vector3(0.3f, 0.78f, 0.18f),
                    new Vector3(0.06f, 0.3f, 0.06f),
                    iron);
                CreatePart(
                    PrimitiveType.Sphere,
                    "Gladius Hilt",
                    visuals.transform,
                    new Vector3(0.3f, 0.96f, 0.18f),
                    new Vector3(0.08f, 0.08f, 0.08f),
                    bronze);
                CreatePart(
                    PrimitiveType.Cylinder,
                    "Pilum",
                    visuals.transform,
                    new Vector3(0.38f, 1.0f, 0.12f),
                    new Vector3(0.035f, 0.95f, 0.035f),
                    wood);
                CreatePart(
                    PrimitiveType.Cube,
                    "Pilum Tip",
                    visuals.transform,
                    new Vector3(0.38f, 1.98f, 0.12f),
                    new Vector3(0.06f, 0.18f, 0.03f),
                    iron);
            }
            else if (ranged)
            {
                // Arco: cuerpo curvo hecho con dos mitades inclinadas y cuerda.
                CreatePart(
                    PrimitiveType.Cube,
                    "Bow Upper",
                    visuals.transform,
                    new Vector3(0.4f, 1.22f, 0.2f),
                    new Vector3(0.04f, 0.5f, 0.04f),
                    Quaternion.Euler(0f, 0f, -18f),
                    wood);
                CreatePart(
                    PrimitiveType.Cube,
                    "Bow Lower",
                    visuals.transform,
                    new Vector3(0.4f, 0.78f, 0.2f),
                    new Vector3(0.04f, 0.5f, 0.04f),
                    Quaternion.Euler(0f, 0f, 18f),
                    wood);
                CreatePart(
                    PrimitiveType.Cube,
                    "Bow String",
                    visuals.transform,
                    new Vector3(0.49f, 1.0f, 0.2f),
                    new Vector3(0.012f, 1.06f, 0.012f),
                    iron);
                CreatePart(
                    PrimitiveType.Sphere,
                    "Bow Grip",
                    visuals.transform,
                    new Vector3(0.38f, 1.0f, 0.2f),
                    new Vector3(0.06f, 0.08f, 0.06f),
                    leather);

                // Carcaj a la espalda con flechas asomando.
                CreatePart(
                    PrimitiveType.Cylinder,
                    "Quiver",
                    visuals.transform,
                    new Vector3(-0.16f, 1.08f, -0.26f),
                    new Vector3(0.1f, 0.28f, 0.1f),
                    Quaternion.Euler(18f, 0f, 12f),
                    leather);
                for (int arrow = -1; arrow <= 1; arrow++)
                {
                    CreatePart(
                        PrimitiveType.Cylinder,
                        "Arrow Shaft",
                        visuals.transform,
                        new Vector3(-0.16f + arrow * 0.035f, 1.38f, -0.3f),
                        new Vector3(0.012f, 0.12f, 0.012f),
                        wood);
                    CreatePart(
                        PrimitiveType.Cube,
                        "Arrow Fletching",
                        visuals.transform,
                        new Vector3(-0.16f + arrow * 0.035f, 1.5f, -0.3f),
                        new Vector3(0.05f, 0.06f, 0.012f),
                        crest);
                }

                CreatePart(
                    PrimitiveType.Cylinder,
                    "Buckler",
                    visuals.transform,
                    new Vector3(-0.42f, 1.0f, 0.18f),
                    new Vector3(0.26f, 0.04f, 0.26f),
                    Quaternion.Euler(90f, 0f, 0f),
                    leather);
                CreatePart(
                    PrimitiveType.Sphere,
                    "Buckler Boss",
                    visuals.transform,
                    new Vector3(-0.42f, 1.0f, 0.23f),
                    new Vector3(0.06f, 0.06f, 0.04f),
                    bronze);
            }
            else
            {
                CreatePart(
                    PrimitiveType.Cylinder,
                    "Shield",
                    visuals.transform,
                    new Vector3(-0.42f, 1.0f, 0.18f),
                    new Vector3(0.55f, 0.05f, 0.55f),
                    Quaternion.Euler(90f, 0f, 0f),
                    bronze);
                CreatePart(
                    PrimitiveType.Cylinder,
                    "Shield Face",
                    visuals.transform,
                    new Vector3(-0.42f, 1.0f, 0.24f),
                    new Vector3(0.44f, 0.02f, 0.44f),
                    Quaternion.Euler(90f, 0f, 0f),
                    tunic);
                CreatePart(
                    PrimitiveType.Sphere,
                    "Shield Boss",
                    visuals.transform,
                    new Vector3(-0.42f, 1.0f, 0.28f),
                    new Vector3(0.1f, 0.1f, 0.05f),
                    bronze);
                CreatePart(
                    PrimitiveType.Cylinder,
                    "Spear",
                    visuals.transform,
                    new Vector3(0.38f, 1.0f, 0.12f),
                    new Vector3(0.035f, 0.95f, 0.035f),
                    wood);
                CreatePart(
                    PrimitiveType.Cube,
                    "Spear Tip",
                    visuals.transform,
                    new Vector3(0.38f, 1.98f, 0.12f),
                    new Vector3(0.08f, 0.18f, 0.03f),
                    bronze);
            }

            return visuals;
        }

        private static UnitDefinition CreateUnitDefinition(
            UnitConfig config,
            Material selectionMaterial,
            Material healthBarBackgroundMaterial,
            Material healthBarFillMaterial,
            VisualPalette palette = null)
        {
            string assetPath = $"{UnitDataFolder}/{config.AssetName}.asset";
            string prefabPath = $"{UnitPrefabFolder}/{config.AssetName}.prefab";

            UnitDefinition definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<UnitDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            GameObject root = new GameObject(config.AssetName);
            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.radius = Mathf.Max(0.2f, Mathf.Min(config.Size.x, config.Size.z) * 0.5f);
            collider.height = Mathf.Max(config.Size.y, collider.radius * 2f);
            collider.center = new Vector3(0f, collider.height * 0.5f, 0f);

            GameObject visuals = CreateUnitVisual(root.transform, config, palette);
            Renderer[] visualRenderers = visuals.GetComponentsInChildren<Renderer>(true);

            UnitRuntime runtime = root.AddComponent<UnitRuntime>();
            SetObjectReference(runtime, "definition", definition);

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = Mathf.Max(0.2f, Mathf.Min(config.Size.x, config.Size.z) * 0.5f);
            agent.height = config.Size.y;
            agent.speed = config.MoveSpeed;
            agent.angularSpeed = config.TurnSpeed;
            agent.acceleration = Mathf.Max(8f, config.MoveSpeed * 4f);
            agent.stoppingDistance = config.StoppingDistance;
            agent.autoBraking = true;
            agent.avoidancePriority = 50;

            GameObject selectionIndicator = CreateSelectionIndicator(
                root.transform,
                selectionMaterial);

            UnitMovement movement = root.AddComponent<UnitMovement>();
            SetObjectReference(movement, "agent", agent);
            SetObjectReference(movement, "selectionIndicator", selectionIndicator);

            UnitCombat combat = root.AddComponent<UnitCombat>();
            SetObjectReference(combat, "runtime", runtime);
            SetObjectReference(combat, "movement", movement);

            if (config.Faction == Faction.Roman)
            {
                EnemyAiController ai = root.AddComponent<EnemyAiController>();
                SetObjectReference(ai, "runtime", runtime);
                SetObjectReference(ai, "movement", movement);
                SetObjectReference(ai, "combat", combat);
            }

            CreateHealthBar(
                root.transform,
                runtime,
                config.Size.y,
                healthBarBackgroundMaterial,
                healthBarFillMaterial);

            UnitDamageFlash flash = root.AddComponent<UnitDamageFlash>();
            SetObjectReference(flash, "runtime", runtime);
            SerializedObject flashSerialized = new SerializedObject(flash);
            SerializedProperty flashRenderers = flashSerialized.FindProperty("renderers");
            flashRenderers.arraySize = visualRenderers.Length;
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                flashRenderers.GetArrayElementAtIndex(i).objectReferenceValue = visualRenderers[i];
            }

            flashSerialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            if (prefab == null)
            {
                Debug.LogError($"Could not create the unit prefab at {prefabPath}.");
                return null;
            }

            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("displayName").stringValue = config.DisplayName;
            serialized.FindProperty("faction").enumValueIndex = (int)config.Faction;
            serialized.FindProperty("role").enumValueIndex = (int)config.Role;
            serialized.FindProperty("prefab").objectReferenceValue = prefab;
            serialized.FindProperty("maxHealth").floatValue = config.MaxHealth;
            serialized.FindProperty("attackDamage").floatValue = config.AttackDamage;
            serialized.FindProperty("attackRange").floatValue = config.AttackRange;
            serialized.FindProperty("attackInterval").floatValue = config.AttackInterval;
            serialized.FindProperty("damageReduction").floatValue = config.DamageReduction;
            serialized.FindProperty("moveSpeed").floatValue = config.MoveSpeed;
            serialized.FindProperty("turnSpeed").floatValue = config.TurnSpeed;
            serialized.FindProperty("stoppingDistance").floatValue = config.StoppingDistance;
            serialized.FindProperty("woodCost").intValue = config.WoodCost;
            serialized.FindProperty("bronzeCost").intValue = config.BronzeCost;
            serialized.FindProperty("trainTime").floatValue = config.TrainTime;
            serialized.FindProperty("populationCost").intValue = config.PopulationCost;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            return definition;
        }

        private static void CreateTowerDetails(
            Transform parent,
            Vector3 size,
            Material stone,
            Material marble,
            Material terracotta,
            Material wood,
            Material bronze,
            Material crest,
            Material accent)
        {
            CreatePart(
                PrimitiveType.Cube,
                "Stone Band Low",
                parent,
                new Vector3(0f, 0.9f, 0f),
                new Vector3(size.x + 0.04f, 0.08f, size.z + 0.04f),
                stone);
            CreatePart(
                PrimitiveType.Cube,
                "Stone Band High",
                parent,
                new Vector3(0f, 1.7f, 0f),
                new Vector3(size.x + 0.04f, 0.08f, size.z + 0.04f),
                stone);
            CreatePart(
                PrimitiveType.Cube,
                "Cornice",
                parent,
                new Vector3(0f, 2.45f, 0f),
                new Vector3(size.x + 0.14f, 0.12f, size.z + 0.14f),
                marble);
            CreatePart(
                PrimitiveType.Sphere,
                "Dome",
                parent,
                new Vector3(0f, 2.62f, 0f),
                new Vector3(size.x + 0.1f, 0.5f, size.z + 0.1f),
                terracotta);
            CreatePart(
                PrimitiveType.Sphere,
                "Finial",
                parent,
                new Vector3(0f, 2.9f, 0f),
                new Vector3(0.1f, 0.1f, 0.1f),
                bronze);
            CreatePart(
                PrimitiveType.Cube,
                "Door",
                parent,
                new Vector3(0f, 0.45f, size.z * 0.5f + 0.02f),
                new Vector3(0.32f, 0.8f, 0.04f),
                wood);
            CreatePart(
                PrimitiveType.Sphere,
                "Handle",
                parent,
                new Vector3(0.08f, 0.5f, size.z * 0.5f + 0.05f),
                new Vector3(0.05f, 0.05f, 0.05f),
                bronze);
            CreatePart(
                PrimitiveType.Cube,
                "Window Frame",
                parent,
                new Vector3(0f, 1.9f, size.z * 0.5f),
                new Vector3(0.24f, 0.36f, 0.01f),
                bronze);
            CreatePart(
                PrimitiveType.Cube,
                "Window",
                parent,
                new Vector3(0f, 1.9f, size.z * 0.5f + 0.01f),
                new Vector3(0.18f, 0.3f, 0.02f),
                crest);

            for (int side = -1; side <= 1; side += 2)
            {
                CreatePart(
                    PrimitiveType.Cube,
                    "Arrow Slit",
                    parent,
                    new Vector3(side * 0.22f, 1.35f, size.z * 0.5f + 0.01f),
                    new Vector3(0.05f, 0.2f, 0.02f),
                    crest);
            }

            CreatePart(
                PrimitiveType.Cube,
                "Banner",
                parent,
                new Vector3(0.3f, 2.0f, size.z * 0.5f + 0.03f),
                new Vector3(0.15f, 0.3f, 0.02f),
                accent);
        }

        private static void CreateHouseDetails(
            Transform parent,
            Vector3 size,
            Material stone,
            Material marble,
            Material terracotta,
            Material wood,
            Material bronze,
            Material crest)
        {
            CreatePart(
                PrimitiveType.Cube,
                "Door",
                parent,
                new Vector3(0f, 0.38f, size.z * 0.5f + 0.02f),
                new Vector3(0.28f, 0.66f, 0.04f),
                wood);
            CreatePart(
                PrimitiveType.Sphere,
                "Handle",
                parent,
                new Vector3(0.08f, 0.42f, size.z * 0.5f + 0.05f),
                new Vector3(0.04f, 0.04f, 0.04f),
                bronze);
            CreatePart(
                PrimitiveType.Cube,
                "Window Frame",
                parent,
                new Vector3(0.28f, 0.65f, size.z * 0.5f),
                new Vector3(0.18f, 0.22f, 0.01f),
                marble);
            CreatePart(
                PrimitiveType.Cube,
                "Window",
                parent,
                new Vector3(0.28f, 0.65f, size.z * 0.5f + 0.01f),
                new Vector3(0.14f, 0.18f, 0.02f),
                crest);

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.32f;
                CreatePart(
                    PrimitiveType.Cylinder,
                    "Column",
                    parent,
                    new Vector3(x, 0.5f, size.z * 0.5f + 0.04f),
                    new Vector3(0.07f, 0.4f, 0.07f),
                    marble);
                CreatePart(
                    PrimitiveType.Cube,
                    "Column Base",
                    parent,
                    new Vector3(x, 0.1f, size.z * 0.5f + 0.04f),
                    new Vector3(0.14f, 0.05f, 0.14f),
                    marble);
                CreatePart(
                    PrimitiveType.Cube,
                    "Column Capital",
                    parent,
                    new Vector3(x, 0.9f, size.z * 0.5f + 0.04f),
                    new Vector3(0.16f, 0.05f, 0.16f),
                    marble);
            }

            CreatePart(
                PrimitiveType.Cube,
                "Porch Roof",
                parent,
                new Vector3(0f, 0.95f, size.z * 0.5f + 0.12f),
                new Vector3(0.8f, 0.08f, 0.24f),
                terracotta);
            CreatePart(
                PrimitiveType.Cube,
                "Roof Left",
                parent,
                new Vector3(-0.22f, 1.1f, 0f),
                new Vector3(0.6f, 0.08f, size.z + 0.1f),
                Quaternion.Euler(0f, 0f, 30f),
                terracotta);
            CreatePart(
                PrimitiveType.Cube,
                "Roof Right",
                parent,
                new Vector3(0.22f, 1.1f, 0f),
                new Vector3(0.6f, 0.08f, size.z + 0.1f),
                Quaternion.Euler(0f, 0f, -30f),
                terracotta);
            CreatePart(
                PrimitiveType.Cube,
                "Ridge",
                parent,
                new Vector3(0f, 1.25f, 0f),
                new Vector3(0.08f, 0.08f, size.z + 0.12f),
                terracotta);
            CreatePart(
                PrimitiveType.Cube,
                "Chimney",
                parent,
                new Vector3(0.25f, 1.3f, 0.15f),
                new Vector3(0.12f, 0.3f, 0.12f),
                stone);
        }

        private static void CreateBarracksDetails(
            Transform parent,
            Vector3 size,
            Material stone,
            Material marble,
            Material terracotta,
            Material wood,
            Material bronze,
            Material accent)
        {
            CreatePart(
                PrimitiveType.Cube,
                "Door",
                parent,
                new Vector3(0f, 0.55f, size.z * 0.5f + 0.03f),
                new Vector3(0.7f, 1.1f, 0.06f),
                wood);
            CreatePart(
                PrimitiveType.Cube,
                "Door Band Low",
                parent,
                new Vector3(0f, 0.35f, size.z * 0.5f + 0.07f),
                new Vector3(0.74f, 0.05f, 0.02f),
                bronze);
            CreatePart(
                PrimitiveType.Cube,
                "Door Band High",
                parent,
                new Vector3(0f, 0.8f, size.z * 0.5f + 0.07f),
                new Vector3(0.74f, 0.05f, 0.02f),
                bronze);

            for (int side = -1; side <= 1; side += 2)
            {
                CreatePart(
                    PrimitiveType.Sphere,
                    "Handle",
                    parent,
                    new Vector3(side * 0.12f, 0.58f, size.z * 0.5f + 0.1f),
                    new Vector3(0.06f, 0.06f, 0.06f),
                    bronze);
            }

            float[] columns = { -0.75f, -0.45f, 0.45f, 0.75f };
            for (int i = 0; i < columns.Length; i++)
            {
                float x = columns[i];
                CreatePart(
                    PrimitiveType.Cylinder,
                    "Column",
                    parent,
                    new Vector3(x, 0.6f, size.z * 0.5f + 0.05f),
                    new Vector3(0.08f, 0.5f, 0.08f),
                    marble);
                CreatePart(
                    PrimitiveType.Cube,
                    "Column Base",
                    parent,
                    new Vector3(x, 0.1f, size.z * 0.5f + 0.05f),
                    new Vector3(0.16f, 0.05f, 0.16f),
                    marble);
                CreatePart(
                    PrimitiveType.Cube,
                    "Column Capital",
                    parent,
                    new Vector3(x, 1.1f, size.z * 0.5f + 0.05f),
                    new Vector3(0.18f, 0.05f, 0.18f),
                    marble);
            }

            CreatePart(
                PrimitiveType.Cube,
                "Porch Roof",
                parent,
                new Vector3(0f, 1.15f, size.z * 0.5f + 0.14f),
                new Vector3(1.8f, 0.1f, 0.3f),
                terracotta);
            CreatePart(
                PrimitiveType.Cube,
                "Roof Left",
                parent,
                new Vector3(-0.4f, 1.65f, 0f),
                new Vector3(0.95f, 0.1f, size.z + 0.2f),
                Quaternion.Euler(0f, 0f, 30f),
                terracotta);
            CreatePart(
                PrimitiveType.Cube,
                "Roof Right",
                parent,
                new Vector3(0.4f, 1.65f, 0f),
                new Vector3(0.95f, 0.1f, size.z + 0.2f),
                Quaternion.Euler(0f, 0f, -30f),
                terracotta);
            CreatePart(
                PrimitiveType.Cube,
                "Ridge",
                parent,
                new Vector3(0f, 1.9f, 0f),
                new Vector3(0.1f, 0.1f, size.z + 0.24f),
                terracotta);
            CreatePart(
                PrimitiveType.Cylinder,
                "Banner Pole",
                parent,
                new Vector3(0f, 1.35f, size.z * 0.5f + 0.06f),
                new Vector3(0.02f, 0.35f, 0.02f),
                bronze);
            CreatePart(
                PrimitiveType.Cube,
                "Banner",
                parent,
                new Vector3(0f, 1.45f, size.z * 0.5f + 0.08f),
                new Vector3(0.35f, 0.3f, 0.02f),
                accent);
        }

        private static void CreateBuildingDetails(
            GameObject root,
            string assetName,
            Vector2Int footprint,
            Vector3 size,
            Material mainMaterial,
            VisualPalette palette)
        {
            if (palette == null)
            {
                return;
            }

            GameObject details = new GameObject("Details");
            details.transform.SetParent(root.transform, false);
            Transform parent = details.transform;

            Material stone = palette.Stone != null ? palette.Stone : mainMaterial;
            Material marble = palette.Marble != null ? palette.Marble : mainMaterial;
            Material terracotta = palette.Terracotta != null ? palette.Terracotta : mainMaterial;
            Material wood = palette.Wood != null ? palette.Wood : mainMaterial;
            Material bronze = palette.Bronze != null ? palette.Bronze : mainMaterial;
            Material crest = palette.Crest != null ? palette.Crest : mainMaterial;
            Material accent = palette.GreekAccent != null ? palette.GreekAccent : mainMaterial;

            CreatePart(
                PrimitiveType.Cube,
                "Plinth",
                parent,
                new Vector3(0f, 0.06f, 0f),
                new Vector3(size.x + 0.08f, 0.12f, size.z + 0.08f),
                stone);

            switch (assetName)
            {
                case "GreekWall":
                    CreateWallDetails(parent, size, stone, marble, bronze);
                    break;
                case "GreekGate":
                    CreateGateDetails(parent, size, stone, marble, wood, bronze, accent);
                    break;
                case "GreekTower":
                    CreateTowerDetails(parent, size, stone, marble, terracotta, wood, bronze, crest, accent);
                    break;
                case "GreekHouse":
                    CreateHouseDetails(parent, size, stone, marble, terracotta, wood, bronze, crest);
                    break;
                case "GreekBarracks":
                    CreateBarracksDetails(parent, size, stone, marble, terracotta, wood, bronze, accent);
                    break;
                case "GreekStorage":
                    CreateStorageDetails(parent, size, stone, terracotta, wood, bronze);
                    break;
            }
        }

        private static void CreateStorageDetails(
            Transform parent,
            Vector3 size,
            Material stone,
            Material terracotta,
            Material wood,
            Material bronze)
        {
            CreatePart(
                PrimitiveType.Cube,
                "Roof",
                parent,
                new Vector3(0f, size.y + 0.06f, 0f),
                new Vector3(size.x + 0.24f, 0.12f, size.z + 0.24f),
                terracotta);

            CreatePart(
                PrimitiveType.Cube,
                "Door",
                parent,
                new Vector3(0f, 0.42f, size.z * 0.5f + 0.01f),
                new Vector3(0.52f, 0.84f, 0.06f),
                wood);

            CreatePart(
                PrimitiveType.Cube,
                "Door Band",
                parent,
                new Vector3(0f, 0.62f, size.z * 0.5f + 0.02f),
                new Vector3(0.54f, 0.08f, 0.05f),
                bronze);

            CreatePart(
                PrimitiveType.Cube,
                "Side Beam",
                parent,
                new Vector3(size.x * 0.5f - 0.1f, 0.62f, 0f),
                new Vector3(0.1f, 0.08f, size.z + 0.04f),
                wood);
        }

        private static void CreateWallDetails(
            Transform parent,
            Vector3 size,
            Material stone,
            Material marble,
            Material bronze)
        {
            CreatePart(
                PrimitiveType.Cube,
                "Cap",
                parent,
                new Vector3(0f, size.y - 0.03f, 0f),
                new Vector3(size.x + 0.04f, 0.08f, size.z + 0.04f),
                marble);

            CreatePart(
                PrimitiveType.Cube,
                "Groove",
                parent,
                new Vector3(0f, 0.5f, 0f),
                new Vector3(size.x + 0.005f, 0.04f, size.z + 0.005f),
                stone);

            for (int side = -1; side <= 1; side += 2)
            {
                CreatePart(
                    PrimitiveType.Cube,
                    "Merlon",
                    parent,
                    new Vector3(0f, size.y + 0.07f, side * (size.z * 0.5f - 0.08f)),
                    new Vector3(size.x - 0.1f, 0.16f, 0.14f),
                    stone);
            }

            CreatePart(
                PrimitiveType.Cube,
                "Stairs",
                parent,
                new Vector3(-0.34f, 0.55f, size.z * 0.5f + 0.32f),
                new Vector3(0.55f, 0.07f, 1.15f),
                Quaternion.Euler(42f, 0f, 0f),
                stone);
        }

        private static void CreateGateDetails(
            Transform parent,
            Vector3 size,
            Material stone,
            Material marble,
            Material wood,
            Material bronze,
            Material accent)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.7f;
                CreatePart(
                    PrimitiveType.Cube,
                    "Gate Tower",
                    parent,
                    new Vector3(x, 0.9f, 0f),
                    new Vector3(0.38f, 1.8f, size.z + 0.08f),
                    stone);
                CreatePart(
                    PrimitiveType.Cube,
                    "Tower Cornice",
                    parent,
                    new Vector3(x, 1.82f, 0f),
                    new Vector3(0.46f, 0.1f, size.z + 0.14f),
                    marble);

                for (int merlon = -1; merlon <= 1; merlon += 2)
                {
                    CreatePart(
                        PrimitiveType.Cube,
                        "Tower Merlon",
                        parent,
                        new Vector3(x + merlon * 0.12f, 1.95f, 0f),
                        new Vector3(0.12f, 0.2f, size.z + 0.1f),
                        stone);
                }
            }

            CreatePart(
                PrimitiveType.Cube,
                "Lintel",
                parent,
                new Vector3(0f, 1.32f, size.z * 0.5f + 0.02f),
                new Vector3(1.1f, 0.18f, 0.12f),
                marble);

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.23f;
                CreatePart(
                    PrimitiveType.Cube,
                    "Door",
                    parent,
                    new Vector3(x, 0.55f, size.z * 0.5f + 0.04f),
                    new Vector3(0.42f, 1.1f, 0.06f),
                    wood);
            }

            CreatePart(
                PrimitiveType.Cube,
                "Door Band Low",
                parent,
                new Vector3(0f, 0.35f, size.z * 0.5f + 0.08f),
                new Vector3(0.9f, 0.05f, 0.02f),
                bronze);
            CreatePart(
                PrimitiveType.Cube,
                "Door Band High",
                parent,
                new Vector3(0f, 0.8f, size.z * 0.5f + 0.08f),
                new Vector3(0.9f, 0.05f, 0.02f),
                bronze);

            for (int side = -1; side <= 1; side += 2)
            {
                CreatePart(
                    PrimitiveType.Sphere,
                    "Handle",
                    parent,
                    new Vector3(side * 0.08f, 0.58f, size.z * 0.5f + 0.11f),
                    new Vector3(0.06f, 0.06f, 0.06f),
                    bronze);
            }

            CreatePart(
                PrimitiveType.Cylinder,
                "Banner Pole",
                parent,
                new Vector3(0f, 1.4f, size.z * 0.5f + 0.05f),
                new Vector3(0.02f, 0.4f, 0.02f),
                bronze);
            CreatePart(
                PrimitiveType.Cube,
                "Banner",
                parent,
                new Vector3(0f, 1.48f, size.z * 0.5f + 0.08f),
                new Vector3(0.3f, 0.3f, 0.02f),
                accent);
        }

        private static GameObject CreateSelectionIndicator(Transform parent, Material material)
        {
            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            indicator.name = "Selection Indicator";
            indicator.transform.SetParent(parent, false);
            indicator.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            indicator.transform.localScale = new Vector3(1.15f, 0.01f, 1.15f);

            Collider collider = indicator.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            MeshRenderer renderer = indicator.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            indicator.SetActive(false);
            return indicator;
        }

        private static void CreateHealthBar(
            Transform parent,
            UnitRuntime runtime,
            float unitHeight,
            Material backgroundMaterial,
            Material fillMaterial)
        {
            GameObject bar = new GameObject("Health Bar");
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = new Vector3(0f, unitHeight + 0.35f, 0f);

            CreateBarQuad(
                "Background",
                bar.transform,
                new Vector3(1.12f, 0.15f, 1f),
                Vector3.zero,
                backgroundMaterial);

            GameObject fill = CreateBarQuad(
                "Fill",
                bar.transform,
                new Vector3(1f, 0.09f, 1f),
                new Vector3(0f, 0f, -0.01f),
                fillMaterial);

            UnitHealthBar healthBar = bar.AddComponent<UnitHealthBar>();
            SetObjectReference(healthBar, "runtime", runtime);
            SetObjectReference(healthBar, "fill", fill.transform);
        }

        private static GameObject CreateBarQuad(
            string name,
            Transform parent,
            Vector3 scale,
            Vector3 localPosition,
            Material material)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = localPosition;
            quad.transform.localScale = scale;

            Collider collider = quad.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return quad;
        }

        private static void ConfigureWallMountPrefab(GameObject root)
        {
            WallMountPoint mount = root.GetComponent<WallMountPoint>();
            if (mount == null)
            {
                mount = root.AddComponent<WallMountPoint>();
            }

            SerializedObject serialized = new SerializedObject(mount);
            serialized.FindProperty("capacity").intValue = 2;
            serialized.FindProperty("topHeight").floatValue = 1.35f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureTowerPrefab(GameObject root)
        {
            BuildingRangedAttack attack = root.GetComponent<BuildingRangedAttack>();
            if (attack == null)
            {
                attack = root.AddComponent<BuildingRangedAttack>();
            }

            SerializedObject serialized = new SerializedObject(attack);
            serialized.FindProperty("attackDamage").floatValue = 10f;
            serialized.FindProperty("attackRange").floatValue = 9f;
            serialized.FindProperty("attackInterval").floatValue = 1.2f;
            serialized.FindProperty("muzzleHeight").floatValue = 2.7f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureStoragePrefab(GameObject root)
        {
            if (root.GetComponent<ResourceDropOff>() == null)
            {
                root.AddComponent<ResourceDropOff>();
            }
        }

        private static void ConfigureBarracksPrefab(GameObject root, params UnitDefinition[] units)
        {
            BarracksBuilding barracks = root.AddComponent<BarracksBuilding>();

            if (root.GetComponent<ResourceDropOff>() == null)
            {
                root.AddComponent<ResourceDropOff>();
            }

            GameObject spawnPoint = new GameObject("SpawnPoint");
            spawnPoint.transform.SetParent(root.transform, false);
            spawnPoint.transform.localPosition = new Vector3(0f, 0f, 2.5f);

            SerializedObject serialized = new SerializedObject(barracks);
            SerializedProperty recruitableUnits = serialized.FindProperty("recruitableUnits");
            recruitableUnits.arraySize = units.Length;
            for (int i = 0; i < units.Length; i++)
            {
                recruitableUnits.GetArrayElementAtIndex(i).objectReferenceValue = units[i];
            }

            serialized.FindProperty("spawnPoint").objectReferenceValue = spawnPoint.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SpawnRomanTestSquad(UnitDefinition roman, Transform parent)
        {
            if (roman == null || roman.Prefab == null)
            {
                return;
            }

            GameObject squad = new GameObject("Roman Test Squad");
            squad.transform.SetParent(parent, false);

            Vector3[] positions =
            {
                new Vector3(10f, 0f, 10f),
                new Vector3(11.5f, 0f, 10f),
                new Vector3(10f, 0f, 11.5f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(
                    roman.Prefab,
                    squad.transform) as GameObject;

                if (instance == null)
                {
                    continue;
                }

                instance.name = $"{roman.DisplayName} {i + 1}";
                instance.transform.position = positions[i];

                UnitRuntime runtime = instance.GetComponent<UnitRuntime>();
                if (runtime != null)
                {
                    runtime.Initialize(roman);
                }

                UnitCombat combat = instance.GetComponent<UnitCombat>();
                if (combat != null)
                {
                    combat.Initialize(roman);
                }

                EnemyAiController ai = instance.GetComponent<EnemyAiController>();
                if (ai != null)
                {
                    ai.SetHome(positions[i], 4f);
                }
            }
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            return GetOrCreateMaterial(
                name,
                color,
                "Universal Render Pipeline/Lit",
                "Standard",
                false);
        }

        private static Material GetOrCreateUnlitMaterial(string name, Color color, bool transparent)
        {
            return GetOrCreateMaterial(
                name,
                color,
                "Universal Render Pipeline/Unlit",
                "Unlit/Color",
                transparent);
        }

        private static Material GetOrCreatePreviewMaterial(string name, Color color)
        {
            return GetOrCreateUnlitMaterial(name, color, true);
        }

        private static Material GetOrCreateMaterial(
            string name,
            Color color,
            string shaderName,
            string fallbackShaderName,
            bool transparent)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                shader = Shader.Find(fallbackShaderName);
            }

            if (shader == null)
            {
                Debug.LogError($"Could not find a shader for the {name} material.");
                return null;
            }

            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            ApplyColor(material, color);

            if (transparent)
            {
                SetTransparent(material);
            }
            else
            {
                SetOpaque(material);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ApplyColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static void SetOpaque(Material material)
        {
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 0f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 1f);
            }

            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = -1;
        }

        private static void SetTransparent(Material material)
        {
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
#endif
