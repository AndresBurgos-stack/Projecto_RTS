#if UNITY_EDITOR
using ProjectoRTS.Units;
using ProjectoRTS.Waves;
using UnityEditor;
using UnityEngine;

namespace ProjectoRTS.EditorTools
{
    public static partial class PlacementDemoBuilder
    {
        private static RomanSpawner CreateRomanSpawner(UnitDefinition unit, Transform parent)
        {
            GameObject spawnerRoot = new GameObject("Roman Spawner");
            RomanSpawner spawner = spawnerRoot.AddComponent<RomanSpawner>();

            GameObject point = new GameObject("Spawn Point");
            point.transform.SetParent(spawnerRoot.transform, false);
            point.transform.position = new Vector3(0f, 0f, 46f);

            SetObjectReference(spawner, "spawnPoint", point.transform);
            SetObjectReference(spawner, "unitParent", parent);

            SerializedObject serialized = new SerializedObject(spawner);

            serialized.FindProperty("warningTime").floatValue = 4f;
            serialized.FindProperty("spawnStagger").floatValue = 0.15f;
            serialized.FindProperty("formationSpacing").floatValue = 1.5f;
            serialized.FindProperty("repathInterval").floatValue = 0.5f;
            serialized.FindProperty("markerRadius").floatValue = 2.5f;
            serialized.FindProperty("autoStart").boolValue = false;

            SerializedProperty squad = serialized.FindProperty("squad");
            squad.arraySize = 1;
            SerializedProperty entry = squad.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("unit").objectReferenceValue = unit;
            entry.FindPropertyRelative("count").intValue = 3;
            entry.FindPropertyRelative("delay").floatValue = 0f;

            serialized.ApplyModifiedPropertiesWithoutUndo();

            return spawner;
        }
    }
}
#endif