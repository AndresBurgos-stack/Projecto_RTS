using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Imports the AssetLoom mannequin as a Unity Humanoid and places it in a
/// dedicated preview scene so it can be seen immediately when opening the project.
/// </summary>
[InitializeOnLoad]
public static class MannequinSceneSetup
{
    private const string FbxPath = "Assets/Mannequin/Mannequin.fbx";
    private const string ScenePath = "Assets/Scenes/MannequinPreview.unity";
    private const string SkinMatPath = "Assets/_Project/Materials/Skin.mat";
    private const string AutoOpenedKey = "ProjectoRTS_MannequinPreview_AutoOpened";

    static MannequinSceneSetup()
    {
        EditorApplication.update += PollForModel;
    }

    private static void PollForModel()
    {
        // Configure importer as soon as it becomes available (may trigger a reimport).
        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer != null)
        {
            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                changed = true;
            }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }
            if (Mathf.Abs(importer.globalScale - 1f) > 0.0001f)
            {
                importer.globalScale = 1f;
                changed = true;
            }
            if (!importer.useFileUnits)
            {
                importer.useFileUnits = true;
                changed = true;
            }
            if (changed)
            {
                importer.SaveAndReimport();
                return; // wait for reimport to finish
            }
        }

        GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (modelPrefab == null)
        {
            return; // keep polling
        }

        EditorApplication.update -= PollForModel;
        EnsureMannequinScene(autoOpen: true);
    }

    [MenuItem("Tools/Mannequin/Place Mannequin In Scene")]
    public static void PlaceMannequinMenu()
    {
        EnsureMannequinScene(autoOpen: true);
    }

    [MenuItem("Tools/Mannequin/Rebuild Preview Scene")]
    public static void RebuildSceneMenu()
    {
        if (File.Exists(ScenePath))
        {
            File.Delete(ScenePath);
            string meta = ScenePath + ".meta";
            if (File.Exists(meta)) File.Delete(meta);
            AssetDatabase.Refresh();
        }
        EnsureMannequinScene(autoOpen: true);
    }

    // Entry point for batch-mode setup (Unity -executeMethod MannequinSceneSetup.RunBatchSetup).
    public static void RunBatchSetup()
    {
        EnsureMannequinScene(autoOpen: false);
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }

    public static void EnsureMannequinScene(bool autoOpen)
    {
        // Ensure the asset is imported before configuring.
        AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);

        // Configure importer (Humanoid, correct scale/units) before loading the model.
        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer != null)
        {
            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                changed = true;
            }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }
            if (Mathf.Abs(importer.globalScale - 1f) > 0.0001f)
            {
                importer.globalScale = 1f;
                changed = true;
            }
            if (!importer.useFileUnits)
            {
                importer.useFileUnits = true;
                changed = true;
            }
            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (modelPrefab == null)
        {
            Debug.LogWarning("[MannequinSceneSetup] Mannequin model not found at " + FbxPath);
            return;
        }

        // Ensure a skin-colored material exists.
        Material skinMat = AssetDatabase.LoadAssetAtPath<Material>(SkinMatPath);
        if (skinMat == null)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");
            skinMat = new Material(urpLit);
            skinMat.name = "Skin";
            Color skin = new Color(0.85f, 0.68f, 0.55f, 1f);
            if (skinMat.HasProperty("_BaseColor")) skinMat.SetColor("_BaseColor", skin);
            if (skinMat.HasProperty("_Color")) skinMat.SetColor("_Color", skin);
            skinMat.SetFloat("_Smoothness", 0.35f);
            skinMat.SetFloat("_Metallic", 0f);
            string matDir = Path.GetDirectoryName(SkinMatPath);
            if (!Directory.Exists(matDir)) Directory.CreateDirectory(matDir);
            AssetDatabase.CreateAsset(skinMat, SkinMatPath);
            AssetDatabase.SaveAssets();
        }

        if (!File.Exists(ScenePath))
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            string[] names = { "Hoplite", "Toxote", "Trabajador" };
            float[] xs = { -1.4f, 0f, 1.4f };
            for (int i = 0; i < names.Length; i++)
            {
                GameObject mannequin = PrefabUtility.InstantiatePrefab(modelPrefab) as GameObject;
                if (mannequin != null)
                {
                    mannequin.name = names[i];
                    mannequin.transform.position = new Vector3(xs[i], 0f, 0f);
                    mannequin.transform.rotation = Quaternion.identity;

                    // Apply skin material to all renderers.
                    foreach (Renderer r in mannequin.GetComponentsInChildren<Renderer>(true))
                    {
                        r.sharedMaterial = skinMat;
                    }
                }
            }

            GameObject camGO = new GameObject("Main Camera");
            Camera cam = camGO.AddComponent<Camera>();
            camGO.tag = "MainCamera";
            camGO.transform.position = new Vector3(0f, 1.6f, -4.2f);
            camGO.transform.LookAt(new Vector3(0f, 1f, 0f));

            GameObject lightGO = new GameObject("Directional Light");
            Light light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }
        else
        {
            // Scene already exists: still ensure skin material assignment? skip; user can rebuild.
        }

        if (autoOpen && !EditorPrefs.GetBool(AutoOpenedKey, false))
        {
            EditorPrefs.SetBool(AutoOpenedKey, true);
            EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
