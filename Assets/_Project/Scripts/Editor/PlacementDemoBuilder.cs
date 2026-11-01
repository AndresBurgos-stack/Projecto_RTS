#if UNITY_EDITOR
using System.IO;
using System.Linq;
using ProjectoRTS.Buildings;
using ProjectoRTS.Units;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ProjectoRTS.EditorTools
{
    /// <summary>
    /// Creates a ready-to-play scene for testing placement, recruitment,
    /// movement and combat. Menu: ProjectoRTS > Setup > Create Placement Demo Scene.
    /// </summary>
    public static partial class PlacementDemoBuilder
    {
        private const string DataFolder = "Assets/_Project/Data/Buildings";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Buildings";
        private const string UnitDataFolder = "Assets/_Project/Data/Units";
        private const string UnitPrefabFolder = "Assets/_Project/Prefabs/Units";
        private const string NavMeshFolder = "Assets/_Project/Data/NavMesh";
        private const string MaterialFolder = "Assets/_Project/Materials";
        private const string ScenePath = "Assets/Scenes/PlacementDemo.unity";

        [MenuItem("ProjectoRTS/Setup/Create Placement Demo Scene")]
        public static void CreateDemoScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolder("Assets/_Project");
            EnsureFolder("Assets/_Project/Data");
            EnsureFolder(DataFolder);
            EnsureFolder(UnitDataFolder);
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder(PrefabFolder);
            EnsureFolder(UnitPrefabFolder);
            EnsureFolder(NavMeshFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder("Assets/Scenes");

            Material groundMaterial = GetOrCreateMaterial(
                "Ground",
                new Color(0.22f, 0.28f, 0.2f, 1f));
            Material buildingMaterial = GetOrCreateMaterial(
                "Building",
                new Color(0.7f, 0.7f, 0.7f, 1f));
            Material wallMaterial = GetOrCreateMaterial(
                "Wall",
                new Color(0.62f, 0.62f, 0.58f, 1f));
            Material gateMaterial = GetOrCreateMaterial(
                "Gate",
                new Color(0.45f, 0.28f, 0.12f, 1f));
            Material towerMaterial = GetOrCreateMaterial(
                "Tower",
                new Color(0.45f, 0.48f, 0.55f, 1f));
            Material barracksMaterial = GetOrCreateMaterial(
                "Barracks",
                new Color(0.55f, 0.45f, 0.32f, 1f));
            Material hopliteMaterial = GetOrCreateMaterial(
                "Hoplite",
                new Color(0.2f, 0.35f, 0.75f, 1f));
            Material psiloiMaterial = GetOrCreateMaterial(
                "Psiloi",
                new Color(0.2f, 0.65f, 0.55f, 1f));
            Material romanMaterial = GetOrCreateMaterial(
                "Roman",
                new Color(0.72f, 0.18f, 0.14f, 1f));
            Material selectionMaterial = GetOrCreateMaterial(
                "Selection",
                new Color(0.25f, 0.9f, 1f, 1f));
            Material healthBarBackgroundMaterial = GetOrCreateUnlitMaterial(
                "HealthBarBackground",
                new Color(0.05f, 0.06f, 0.07f, 1f),
                false);
            Material healthBarFillMaterial = GetOrCreateUnlitMaterial(
                "HealthBarFill",
                new Color(0.2f, 0.85f, 0.32f, 1f),
                false);
            Material validPreviewMaterial = GetOrCreatePreviewMaterial(
                "PreviewValid",
                new Color(0.2f, 0.9f, 0.3f, 0.45f));
            Material invalidPreviewMaterial = GetOrCreatePreviewMaterial(
                "PreviewInvalid",
                new Color(0.95f, 0.2f, 0.15f, 0.45f));

            VisualPalette palette = CreateVisualPalette();

            UnitDefinition hoplite = CreateUnitDefinition(
                new UnitConfig
                {
                    AssetName = "HopliteArcadio",
                    DisplayName = "Hoplita arcadio",
                    Faction = Faction.Greek,
                    Role = UnitRole.Infantry,
                    Size = new Vector3(0.8f, 1.8f, 0.8f),
                    Material = hopliteMaterial,
                    MaxHealth = 120f,
                    AttackDamage = 12f,
                    AttackRange = 1.5f,
                    AttackInterval = 1.2f,
                    DamageReduction = 0.2f,
                    MoveSpeed = 3.2f,
                    Cost = 50,
                    TrainTime = 4f,
                    PopulationCost = 1
                },
                selectionMaterial,
                healthBarBackgroundMaterial,
                healthBarFillMaterial,
                palette);

            UnitDefinition psiloi = CreateUnitDefinition(
                new UnitConfig
                {
                    AssetName = "Psiloi",
                    DisplayName = "Toxote psiloi",
                    Faction = Faction.Greek,
                    Role = UnitRole.Ranged,
                    Size = new Vector3(0.7f, 1.6f, 0.7f),
                    Material = psiloiMaterial,
                    MaxHealth = 70f,
                    AttackDamage = 8f,
                    AttackRange = 7f,
                    AttackInterval = 1.8f,
                    DamageReduction = 0f,
                    MoveSpeed = 3.8f,
                    Cost = 35,
                    TrainTime = 3f,
                    PopulationCost = 1
                },
                selectionMaterial,
                healthBarBackgroundMaterial,
                healthBarFillMaterial,
                palette);

            UnitDefinition legionary = CreateUnitDefinition(
                new UnitConfig
                {
                    AssetName = "RomanLegionary",
                    DisplayName = "Legionario romano",
                    Faction = Faction.Roman,
                    Role = UnitRole.Infantry,
                    Size = new Vector3(0.8f, 1.8f, 0.8f),
                    Material = romanMaterial,
                    MaxHealth = 110f,
                    AttackDamage = 10f,
                    AttackRange = 1.5f,
                    AttackInterval = 1.3f,
                    DamageReduction = 0.15f,
                    MoveSpeed = 2.9f,
                    Cost = 0,
                    TrainTime = 0f,
                    PopulationCost = 1
                },
                selectionMaterial,
                healthBarBackgroundMaterial,
                healthBarFillMaterial,
                palette);

            if (hoplite == null || psiloi == null || legionary == null)
            {
                Debug.LogError("Could not create the demo unit definitions.");
                return;
            }

            BuildingDefinition wall = CreateBuildingDefinition(
                "GreekWall",
                "Greek Wall",
                BuildingCategory.Wall,
                new Vector2Int(1, 1),
                new Vector3(0.9f, 1.2f, 0.9f),
                wallMaterial,
                woodCost: 10,
                stoneCost: 5,
                maxHealth: 200f,
                palette: palette);

            BuildingDefinition gate = CreateBuildingDefinition(
                "GreekGate",
                "Greek Gate",
                BuildingCategory.Defense,
                new Vector2Int(2, 1),
                new Vector3(1.8f, 1.5f, 0.9f),
                gateMaterial,
                woodCost: 30,
                stoneCost: 20,
                maxHealth: 300f,
                palette: palette);

            BuildingDefinition tower = CreateBuildingDefinition(
                "GreekTower",
                "Greek Tower",
                BuildingCategory.Tower,
                new Vector2Int(1, 1),
                new Vector3(0.9f, 2.6f, 0.9f),
                towerMaterial,
                woodCost: 30,
                stoneCost: 40,
                maxHealth: 350f,
                attackDamage: 10f,
                attackRange: 8f,
                palette: palette);

            BuildingDefinition house = CreateBuildingDefinition(
                "GreekHouse",
                "Greek House",
                BuildingCategory.Economy,
                new Vector2Int(1, 1),
                new Vector3(0.9f, 1f, 0.9f),
                buildingMaterial,
                woodCost: 50,
                stoneCost: 20,
                maxHealth: 150f,
                palette: palette);

            BuildingDefinition barracks = CreateBuildingDefinition(
                "GreekBarracks",
                "Greek Barracks",
                BuildingCategory.Production,
                new Vector2Int(2, 2),
                new Vector3(1.8f, 1.5f, 1.8f),
                barracksMaterial,
                woodCost: 100,
                stoneCost: 50,
                maxHealth: 300f,
                configurePrefab: root => ConfigureBarracksPrefab(root, hoplite, psiloi),
                palette: palette);

            if (wall == null || gate == null || tower == null || house == null || barracks == null)
            {
                Debug.LogError("Could not create the placement demo building definitions.");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.36f, 0.4f);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(5f, 1f, 5f);
            MeshRenderer groundRenderer = ground.GetComponent<MeshRenderer>();
            if (groundRenderer != null && groundMaterial != null)
            {
                groundRenderer.sharedMaterial = groundMaterial;
            }

            GameObject navMeshObject = new GameObject("NavMesh Surface");
            NavMeshSurface navMeshSurface = navMeshObject.AddComponent<NavMeshSurface>();
            navMeshSurface.collectObjects = CollectObjects.All;
            navMeshSurface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            navMeshSurface.layerMask = ~0;
            navMeshSurface.minRegionArea = 0.5f;
            BakeNavMesh(navMeshSurface);

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 26f, -26f);
            cameraObject.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
            camera.fieldOfView = 60f;
            if (cameraObject.GetComponent<UniversalAdditionalCameraData>() == null)
            {
                cameraObject.AddComponent<UniversalAdditionalCameraData>();
            }

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject buildingsRoot = new GameObject("Buildings");

            GameObject gridObject = new GameObject("BuildGrid");
            BuildGrid grid = gridObject.AddComponent<BuildGrid>();
            ConfigureGrid(grid, 40, 40, 1f, new Vector3(-20f, 0f, -20f));

            GameObject placementObject = new GameObject("Placement");
            BuildingPlacementController placement = placementObject.AddComponent<BuildingPlacementController>();
            SetObjectReference(placement, "grid", grid);
            SetObjectReference(placement, "raycastCamera", camera);
            SetObjectReference(placement, "buildingParent", buildingsRoot.transform);
            SetObjectReference(placement, "validPreviewMaterial", validPreviewMaterial);
            SetObjectReference(placement, "invalidPreviewMaterial", invalidPreviewMaterial);

            GameObject unitsRoot = new GameObject("Units");
            SpawnRomanTestSquad(legionary, unitsRoot.transform);

            RecruitmentPanel recruitmentPanel = CreateRecruitmentUI();
            GameObject recruitmentObject = new GameObject("Recruitment");
            RecruitmentManager recruitment = recruitmentObject.AddComponent<RecruitmentManager>();
            SetObjectReference(recruitment, "panel", recruitmentPanel);
            SetObjectReference(recruitment, "unitParent", unitsRoot.transform);
            SetObjectReference(recruitmentPanel, "manager", recruitment);

            GameObject selectionObject = new GameObject("Building Selection");
            BuildingSelectionController selection = selectionObject.AddComponent<BuildingSelectionController>();
            SetObjectReference(selection, "raycastCamera", camera);
            SetObjectReference(selection, "placement", placement);
            SetObjectReference(selection, "recruitment", recruitment);

            GameObject unitSelectionObject = new GameObject("Unit Selection");
            UnitSelectionController unitSelection = unitSelectionObject.AddComponent<UnitSelectionController>();
            SetObjectReference(unitSelection, "raycastCamera", camera);
            SetObjectReference(unitSelection, "placement", placement);

            GameObject hotkeysObject = new GameObject("Placement Hotkeys");
            BuildingPlacementHotkeys hotkeys = hotkeysObject.AddComponent<BuildingPlacementHotkeys>();
            SetObjectReference(hotkeys, "placement", placement);
            SerializedObject hotkeysSerialized = new SerializedObject(hotkeys);
            SerializedProperty definitions = hotkeysSerialized.FindProperty("definitions");
            definitions.arraySize = 5;
            definitions.GetArrayElementAtIndex(0).objectReferenceValue = wall;
            definitions.GetArrayElementAtIndex(1).objectReferenceValue = gate;
            definitions.GetArrayElementAtIndex(2).objectReferenceValue = tower;
            definitions.GetArrayElementAtIndex(3).objectReferenceValue = house;
            definitions.GetArrayElementAtIndex(4).objectReferenceValue = barracks;
            hotkeysSerialized.ApplyModifiedPropertiesWithoutUndo();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError($"Could not save the placement demo scene at {ScenePath}.");
                return;
            }

            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Placement demo created at {ScenePath}. Press Play, place the Greek Barracks with 5, recruit units, select them and right-click a Roman legionary to attack.");
        }

        private static void BakeNavMesh(NavMeshSurface surface)
        {
            if (surface == null)
            {
                return;
            }

            string path = $"{NavMeshFolder}/PlacementDemoNavMesh.asset";
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            surface.BuildNavMesh();
            NavMeshData data = surface.navMeshData;
            if (data == null)
            {
                Debug.LogError("Could not bake the demo NavMesh.");
                return;
            }

            data.name = "PlacementDemoNavMesh";
            AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();
            surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            EditorUtility.SetDirty(surface);
        }

        private static void ConfigureGrid(BuildGrid grid, int width, int height, float cellSize, Vector3 origin)
        {
            SerializedObject serialized = new SerializedObject(grid);
            serialized.FindProperty("width").intValue = width;
            serialized.FindProperty("height").intValue = height;
            serialized.FindProperty("cellSize").floatValue = cellSize;
            serialized.FindProperty("origin").vector3Value = origin;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectReference(Object target, string fieldName, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"Field '{fieldName}' was not found on {target}.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(scene => scene.path == scenePath))
            {
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
