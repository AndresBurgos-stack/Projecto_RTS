#if UNITY_EDITOR
using System.IO;
using System.Linq;
using ProjectoRTS.Buildings;
using ProjectoRTS.Units;
using ProjectoRTS.Waves;
using ProjectoRTS.CameraControl;
using Unity.AI.Navigation;
using Unity.Cinemachine;
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
        private const int MapSize = 300;
        private const float MapHalfSize = MapSize * 0.5f;
        private const float GroundScale = MapSize * 0.1f + 2f;

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
            Material storageMaterial = GetOrCreateMaterial(
                "Storage",
                new Color(0.42f, 0.3f, 0.16f, 1f));
            Material hopliteMaterial = GetOrCreateMaterial(
                "Hoplite",
                new Color(0.2f, 0.35f, 0.75f, 1f));
            Material psiloiMaterial = GetOrCreateMaterial(
                "Psiloi",
                new Color(0.2f, 0.65f, 0.55f, 1f));
            Material romanMaterial = GetOrCreateMaterial(
                "Roman",
                new Color(0.42f, 0.43f, 0.45f, 1f));
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
                    WoodCost = 20,
                    BronzeCost = 30,
                    TrainTime = 4f,
                    PopulationCost = 1
                },
                selectionMaterial,
                healthBarBackgroundMaterial,
                healthBarFillMaterial,
                palette);

            UnitDefinition trabajador = CreateUnitDefinition(
                new UnitConfig
                {
                    AssetName = "Trabajador",
                    DisplayName = "Trabajador",
                    Faction = Faction.Greek,
                    Role = UnitRole.Infantry,
                    Size = new Vector3(0.75f, 1.7f, 0.75f),
                    Material = psiloiMaterial,
                    MaxHealth = 60f,
                    AttackDamage = 0f,
                    AttackRange = 0f,
                    AttackInterval = 1f,
                    DamageReduction = 0f,
                    MoveSpeed = 4.5f,
                    WoodCost = 25,
                    BronzeCost = 0,
                    TrainTime = 4f,
                    PopulationCost = 1
                },
                selectionMaterial,
                healthBarBackgroundMaterial,
                healthBarFillMaterial,
                palette);

            RebuildWorkerVisual(trabajador);

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
                    AttackRange = 9f,
                    AttackInterval = 1.8f,
                    DamageReduction = 0f,
                    MoveSpeed = 3.8f,
                    WoodCost = 20,
                    BronzeCost = 15,
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
                    DisplayName = "Soldado auxiliar",
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
                    WoodCost = 0,
                    BronzeCost = 0,
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
                new Vector3(0.96f, 1.25f, 0.96f),
                wallMaterial,
                woodCost: 10,
                bronzeCost: 5,
                maxHealth: 200f,
                configurePrefab: root => ConfigureWallMountPrefab(root),
                palette: palette);

            BuildingDefinition gate = CreateBuildingDefinition(
                "GreekGate",
                "Greek Gate",
                BuildingCategory.Defense,
                new Vector2Int(2, 1),
                new Vector3(1.8f, 1.5f, 0.9f),
                gateMaterial,
                woodCost: 30,
                bronzeCost: 20,
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
                bronzeCost: 40,
                maxHealth: 350f,
                attackDamage: 10f,
                attackRange: 9f,
                configurePrefab: root => ConfigureTowerPrefab(root),
                palette: palette);

            BuildingDefinition house = CreateBuildingDefinition(
                "GreekHouse",
                "Greek House",
                BuildingCategory.Economy,
                new Vector2Int(1, 1),
                new Vector3(0.9f, 1f, 0.9f),
                buildingMaterial,
                woodCost: 50,
                bronzeCost: 20,
                maxHealth: 150f,
                palette: palette);

            BuildingDefinition storage = CreateBuildingDefinition(
                "GreekStorage",
                "Almacen",
                BuildingCategory.Economy,
                new Vector2Int(2, 2),
                new Vector3(1.8f, 1.1f, 1.8f),
                storageMaterial,
                woodCost: 75,
                bronzeCost: 10,
                maxHealth: 250f,
                configurePrefab: root => ConfigureStoragePrefab(root),
                palette: palette);

            BuildingDefinition barracks = CreateBuildingDefinition(
                "GreekBarracks",
                "Greek Barracks",
                BuildingCategory.Production,
                new Vector2Int(2, 2),
                new Vector3(1.8f, 1.5f, 1.8f),
                barracksMaterial,
                woodCost: 100,
                bronzeCost: 50,
                maxHealth: 300f,
                configurePrefab: root => ConfigureBarracksPrefab(root, hoplite, psiloi),
                palette: palette);

            BuildingDefinition townCenter = CreateBuildingDefinition(
                "GreekTownCenter",
                "Ayuntamiento",
                BuildingCategory.Production,
                new Vector2Int(3, 3),
                new Vector3(2.6f, 2.0f, 2.6f),
                barracksMaterial,
                woodCost: 125,
                bronzeCost: 75,
                maxHealth: 400f,
                configurePrefab: root => ConfigureBarracksPrefab(root, trabajador),
                palette: palette);

            if (wall == null || gate == null || tower == null || house == null || storage == null || barracks == null || townCenter == null)
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
            ground.transform.localScale = new Vector3(GroundScale, 1f, GroundScale);
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

            if (cameraObject.GetComponent<CinemachineBrain>() == null)
            {
                cameraObject.AddComponent<CinemachineBrain>();
            }

            RtsCameraController cameraController = CreateRtsCamera(MapHalfSize);

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject calendarObject = new GameObject("DayNight Calendar");
            DayNightCalendar calendar = calendarObject.AddComponent<DayNightCalendar>();
            SetObjectReference(calendar, "sun", light);
            SerializedObject calendarSerialized = new SerializedObject(calendar);
            calendarSerialized.FindProperty("dayLength").floatValue = 60f;
            calendarSerialized.FindProperty("startTimeOfDay").floatValue = 0.05f;
            calendarSerialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject buildingsRoot = new GameObject("Buildings");

            GameObject gridObject = new GameObject("BuildGrid");
            BuildGrid grid = gridObject.AddComponent<BuildGrid>();
            ConfigureGrid(grid, MapSize, MapSize, 1f, new Vector3(-MapHalfSize, 0f, -MapHalfSize));

            // Starter town centre, placed away from the Pantheon so the player
            // has room to expand around it without covering the temple.
            if (townCenter != null && townCenter.Prefab != null)
            {
                Vector2Int townHallCell = new Vector2Int(MapSize / 2 - 16, MapSize / 2 - 13);
                Vector3 townHallPosition = grid.FootprintCenterWorld(townHallCell, townCenter.Footprint);
                GameObject townHall = Object.Instantiate(
                    townCenter.Prefab,
                    townHallPosition,
                    Quaternion.identity,
                    buildingsRoot.transform);
                townHall.name = townCenter.DisplayName;

                BuildingRuntime townHallRuntime = townHall.AddComponent<BuildingRuntime>();
                townHallRuntime.Configure(townCenter.MaxHealth);

                if (townHall.GetComponent<StructureHealthBar>() == null)
                {
                    townHall.AddComponent<StructureHealthBar>();
                }
                grid.TryPlace(townCenter, townHallCell);
            }

            CreateResourceNodes(palette);

            GameObject placementObject = new GameObject("Placement");
            BuildingPlacementController placement = placementObject.AddComponent<BuildingPlacementController>();
            SetObjectReference(placement, "grid", grid);
            SetObjectReference(placement, "raycastCamera", camera);
            SetObjectReference(placement, "buildingParent", buildingsRoot.transform);
            SetObjectReference(placement, "validPreviewMaterial", validPreviewMaterial);
            SetObjectReference(placement, "invalidPreviewMaterial", invalidPreviewMaterial);

            GameObject unitsRoot = new GameObject("Units");

            RomanSpawner romanSpawner = CreateRomanSpawner(legionary, unitsRoot.transform);

            Material marbleMaterial = GetOrCreateMaterial(
                "PantheonMarble",
                new Color(0.9f, 0.88f, 0.82f, 1f));
            Material terracottaMaterial = GetOrCreateMaterial(
                "PantheonTerracotta",
                new Color(0.72f, 0.36f, 0.22f, 1f));
            PantheonObjective objective = CreatePantheon(
                Vector3.zero,
                marbleMaterial,
                terracottaMaterial);

            SetObjectReference(cameraController, "lookTarget", objective.transform);

            WaveDefinition waveDay1 = CreateWaveDefinition(
                "WaveDay1",
                "Dia 2 - Auxiliares",
                2,
                2f,
                20f,
                (legionary, 4, 0f));
            WaveDefinition waveDay2 = CreateWaveDefinition(
                "WaveDay2",
                "Dia 3 - Refuerzos",
                3,
                2f,
                20f,
                (legionary, 7, 0f));
            WaveDefinition waveDay3 = CreateWaveDefinition(
                "WaveDay3",
                "Dia 4 - Asalto",
                4,
                2f,
                20f,
                (legionary, 11, 0f));

            GameObject waveManagerObject = new GameObject("Wave Manager");
            WaveManager waveManager = waveManagerObject.AddComponent<WaveManager>();
            SetObjectReference(waveManager, "calendar", calendar);
            SetObjectReference(waveManager, "objective", objective);
            SetObjectReference(waveManager, "romanSpawner", romanSpawner);

            SerializedObject waveManagerSerialized = new SerializedObject(waveManager);
            SerializedProperty wavesProperty = waveManagerSerialized.FindProperty("waves");
            wavesProperty.arraySize = 3;
            wavesProperty.GetArrayElementAtIndex(0).objectReferenceValue = waveDay1;
            wavesProperty.GetArrayElementAtIndex(1).objectReferenceValue = waveDay2;
            wavesProperty.GetArrayElementAtIndex(2).objectReferenceValue = waveDay3;
            waveManagerSerialized.ApplyModifiedPropertiesWithoutUndo();

            CreateWaveHud(waveManager, calendar);

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
            definitions.arraySize = 7;
            definitions.GetArrayElementAtIndex(0).objectReferenceValue = wall;
            definitions.GetArrayElementAtIndex(1).objectReferenceValue = gate;
            definitions.GetArrayElementAtIndex(2).objectReferenceValue = tower;
            definitions.GetArrayElementAtIndex(3).objectReferenceValue = house;
            definitions.GetArrayElementAtIndex(4).objectReferenceValue = barracks;
            definitions.GetArrayElementAtIndex(5).objectReferenceValue = townCenter;
            definitions.GetArrayElementAtIndex(6).objectReferenceValue = storage;
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

        private static RtsCameraController CreateRtsCamera(float halfSize)
        {
            GameObject rigObject = new GameObject("Camera Rig");

            GameObject targetObject = new GameObject("Camera Target");
            targetObject.transform.SetParent(rigObject.transform, false);
            targetObject.transform.position = Vector3.zero;

            GameObject virtualObject = new GameObject("Cinemachine Camera");
            virtualObject.transform.SetParent(rigObject.transform, false);

            CinemachineCamera virtualCamera = virtualObject.AddComponent<CinemachineCamera>();
            virtualCamera.Follow = null;
            virtualCamera.LookAt = null;
            virtualCamera.Priority = 100;
            virtualObject.transform.position = new Vector3(0f, 39.94f, -36f);

            CinemachinePanTilt panTilt = virtualObject.AddComponent<CinemachinePanTilt>();
            panTilt.PanAxis.Value = 0f;
            panTilt.PanAxis.Range = new Vector2(0f, 360f);
            panTilt.PanAxis.Wrap = true;
            panTilt.TiltAxis.Value = 46.528f;
            panTilt.TiltAxis.Range = new Vector2(10f, 85f);

            RtsCameraController controller = rigObject.AddComponent<RtsCameraController>();
            SetObjectReference(controller, "virtualCamera", virtualCamera);
            SetObjectReference(controller, "panTilt", panTilt);
            SetObjectReference(controller, "lookTarget", targetObject.transform);
            controller.SetLimits(
                new Vector2(-halfSize, -halfSize),
                new Vector2(halfSize, halfSize));

            SerializedObject cameraSerialized = new SerializedObject(controller);
            cameraSerialized.FindProperty("panSpeed").floatValue = 52f;
            cameraSerialized.FindProperty("maxHeight").floatValue = 120f;
            cameraSerialized.FindProperty("startPosition").vector3Value = new Vector3(0f, 55f, -50f);
            cameraSerialized.ApplyModifiedPropertiesWithoutUndo();

            return controller;
        }

        private static void ConfigureWorkerPrefab(UnitDefinition worker)
        {
            if (worker == null || worker.Prefab == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(worker.Prefab);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform[] all = contents.GetComponentsInChildren<Transform>(true);
                for (int i = all.Length - 1; i >= 0; i--)
                {
                    string partName = all[i].name;
                    if (partName == "Spear" || partName == "Spear Tip")
                    {
                        Object.DestroyImmediate(all[i].gameObject);
                    }
                }

                Material shared = FindFirstMaterial(contents);
                Transform visuals = contents.transform.Find("Visuals");
                Transform parent = visuals != null ? visuals : contents.transform;

                CreateHammerPart(
                    PrimitiveType.Cylinder,
                    "Hammer Handle",
                    parent,
                    new Vector3(0.34f, 1.05f, 0.2f),
                    new Vector3(0.05f, 0.28f, 0.05f),
                    shared);
                CreateHammerPart(
                    PrimitiveType.Cube,
                    "Hammer Head",
                    parent,
                    new Vector3(0.34f, 1.3f, 0.2f),
                    new Vector3(0.18f, 0.12f, 0.12f),
                    shared);

                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static Material FindFirstMaterial(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].sharedMaterial != null)
                {
                    return renderers[i].sharedMaterial;
                }
            }

            return null;
        }

        private static void CreateHammerPart(
            PrimitiveType type,
            string partName,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = scale;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static void RebuildWorkerVisual(UnitDefinition worker)
        {
            if (worker == null || worker.Prefab == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(worker.Prefab);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform visuals = contents.transform.Find("Visuals");
                if (visuals == null)
                {
                    Debug.LogWarning("RebuildWorkerVisual: the worker prefab has no Visuals child.");
                    return;
                }

                Material body = FindFirstMaterial(contents);

                for (int i = visuals.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(visuals.GetChild(i).gameObject);
                }

                Material cap = CreateOrLoadCapMaterial();

                CreateHammerPart(PrimitiveType.Cylinder, "Leg L", visuals, new Vector3(-0.13f, 0.35f, 0f), new Vector3(0.13f, 0.35f, 0.13f), body);
                CreateHammerPart(PrimitiveType.Cylinder, "Leg R", visuals, new Vector3(0.13f, 0.35f, 0f), new Vector3(0.13f, 0.35f, 0.13f), body);
                CreateHammerPart(PrimitiveType.Cube, "Sandal L", visuals, new Vector3(-0.13f, 0.04f, 0.04f), new Vector3(0.18f, 0.08f, 0.28f), body);
                CreateHammerPart(PrimitiveType.Cube, "Sandal R", visuals, new Vector3(0.13f, 0.04f, 0.04f), new Vector3(0.18f, 0.08f, 0.28f), body);
                CreateHammerPart(PrimitiveType.Cube, "Tunic", visuals, new Vector3(0f, 1.05f, 0f), new Vector3(0.46f, 0.7f, 0.3f), cap);
                CreateHammerPart(PrimitiveType.Sphere, "Head", visuals, new Vector3(0f, 1.58f, 0f), new Vector3(0.28f, 0.3f, 0.28f), body);
                CreateHammerPart(PrimitiveType.Cylinder, "Arm L", visuals, new Vector3(-0.3f, 1.12f, 0f), new Vector3(0.1f, 0.26f, 0.1f), body);
                CreateHammerPart(PrimitiveType.Cylinder, "Arm R", visuals, new Vector3(0.3f, 1.12f, 0f), new Vector3(0.1f, 0.26f, 0.1f), body);

                CreateHammerPart(PrimitiveType.Sphere, "Cap", visuals, new Vector3(0f, 1.72f, 0f), new Vector3(0.3f, 0.14f, 0.3f), cap);
                CreateHammerPart(PrimitiveType.Cylinder, "Cap Brim", visuals, new Vector3(0f, 1.66f, 0.08f), new Vector3(0.38f, 0.02f, 0.38f), cap);

                CreateHammerPart(PrimitiveType.Cylinder, "Hammer Handle", visuals, new Vector3(0.36f, 1.05f, 0.18f), new Vector3(0.05f, 0.3f, 0.05f), body);
                CreateHammerPart(PrimitiveType.Cube, "Hammer Head", visuals, new Vector3(0.36f, 1.3f, 0.18f), new Vector3(0.18f, 0.12f, 0.12f), body);

                if (contents.GetComponent<WorkerBuilder>() == null)
                {
                    contents.AddComponent<WorkerBuilder>();
                }

                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static Material CreateOrLoadCapMaterial()
        {
            const string folder = "Assets/_Project/Materials";
            const string assetPath = folder + "/WorkerCap.mat";

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Materials");
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            material.name = "WorkerCap";
            Color white = new Color(0.95f, 0.94f, 0.9f, 1f);

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", white);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", white);
            }

            AssetDatabase.CreateAsset(material, assetPath);
            return material;
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
