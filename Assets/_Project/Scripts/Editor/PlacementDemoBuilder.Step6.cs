#if UNITY_EDITOR
using System.Collections.Generic;
using ProjectoRTS.Buildings;
using ProjectoRTS.Units;
using ProjectoRTS.Waves;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace ProjectoRTS.EditorTools
{
    public static partial class PlacementDemoBuilder
    {
        private const string WaveDataFolder = "Assets/_Project/Data/Waves";

        private static WaveDefinition CreateWaveDefinition(
            string assetName,
            string displayName,
            int day,
            float spawnInterval,
            float restAfterWave,
            params (UnitDefinition unit, int count, float delay)[] groups)
        {
            EnsureFolder(WaveDataFolder);
            string path = $"{WaveDataFolder}/{assetName}.asset";
            WaveDefinition wave = AssetDatabase.LoadAssetAtPath<WaveDefinition>(path);
            if (wave == null)
            {
                wave = ScriptableObject.CreateInstance<WaveDefinition>();
                AssetDatabase.CreateAsset(wave, path);
            }

            SerializedObject serialized = new SerializedObject(wave);
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("day").intValue = day;
            serialized.FindProperty("spawnInterval").floatValue = spawnInterval;
            serialized.FindProperty("restAfterWave").floatValue = restAfterWave;

            SerializedProperty list = serialized.FindProperty("groups");
            list.arraySize = groups.Length;
            for (int i = 0; i < groups.Length; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("unit").objectReferenceValue = groups[i].unit;
                element.FindPropertyRelative("count").intValue = groups[i].count;
                element.FindPropertyRelative("delay").floatValue = groups[i].delay;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(wave);
            return wave;
        }

        private static PantheonObjective CreatePantheon(Vector3 position, Material marbleMaterial, Material terracottaMaterial)
        {
            GameObject root = new GameObject("Pantheon");
            root.transform.position = position;

            GameObject baseMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseMesh.name = "Base";
            baseMesh.transform.SetParent(root.transform, false);
            baseMesh.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            baseMesh.transform.localScale = new Vector3(8f, 0.6f, 14f);
            ApplyMaterial(baseMesh, marbleMaterial);

            GameObject steps = GameObject.CreatePrimitive(PrimitiveType.Cube);
            steps.name = "Steps";
            steps.transform.SetParent(root.transform, false);
            steps.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            steps.transform.localScale = new Vector3(9f, 0.3f, 15f);
            ApplyMaterial(steps, marbleMaterial);

            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 8; i++)
                {
                    float z = -6f + i * 1.7f;
                    float x = side * 3.4f;
                    GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    column.name = $"Column {side} {i}";
                    column.transform.SetParent(root.transform, false);
                    column.transform.localPosition = new Vector3(x, 3.6f, z);
                    column.transform.localScale = new Vector3(0.7f, 2.6f, 0.7f);
                    ApplyMaterial(column, marbleMaterial);
                }
            }

            GameObject entablature = GameObject.CreatePrimitive(PrimitiveType.Cube);
            entablature.name = "Entablature";
            entablature.transform.SetParent(root.transform, false);
            entablature.transform.localPosition = new Vector3(0f, 6.5f, 0f);
            entablature.transform.localScale = new Vector3(8.5f, 1.2f, 14f);
            ApplyMaterial(entablature, marbleMaterial);

            GameObject pediment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pediment.name = "Pediment";
            pediment.transform.SetParent(root.transform, false);
            pediment.transform.localPosition = new Vector3(0f, 7.6f, 0f);
            pediment.transform.localScale = new Vector3(8f, 1f, 13f);
            ApplyMaterial(pediment, terracottaMaterial);

            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(root.transform, false);
            roof.transform.localPosition = new Vector3(0f, 8.4f, 0f);
            roof.transform.localScale = new Vector3(7f, 0.6f, 12f);
            ApplyMaterial(roof, terracottaMaterial);

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 4f, 0f);
            collider.size = new Vector3(8f, 8f, 14f);

            NavMeshObstacle obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0f, 4f, 0f);
            obstacle.size = new Vector3(9f, 8f, 15f);
            obstacle.carving = true;

            PantheonObjective objective = root.AddComponent<PantheonObjective>();
            SerializedObject serialized = new SerializedObject(objective);
            serialized.FindProperty("maxHealth").floatValue = 2000f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return objective;
        }

        private static void ApplyMaterial(GameObject target, Material material)
        {
            if (material == null || target == null)
            {
                return;
            }

            Collider collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static void ConfigureMapSize(GameObject ground, NavMeshSurface surface)
        {
            if (ground != null)
            {
                ground.transform.localScale = new Vector3(6.5f, 1f, 6.5f);
            }

            BuildGrid grid = Object.FindFirstObjectByType<BuildGrid>();
            if (grid != null)
            {
                SerializedObject serialized = new SerializedObject(grid);
                serialized.FindProperty("width").intValue = 52;
                serialized.FindProperty("height").intValue = 52;
                serialized.FindProperty("origin").vector3Value = new Vector3(-26f, 0f, -26f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            if (surface != null)
            {
                surface.BuildNavMesh();
            }
        }
    }
}
#endif
