#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ProjectoRTS.EditorTools
{
    /// <summary>
    /// Provisional Phase 0 blockout for the island map: water plane, cliff rim,
    /// two low-poly mountain rings, fog and a procedural skybox. Everything in
    /// this partial is decoration: no colliders and layer 2, outside the NavMesh.
    /// </summary>
    public static partial class PlacementDemoBuilder
    {
        private const string MeshFolder = "Assets/_Project/Meshes";
        private const int EnvironmentLayer = 2;
        private const float WaterLevel = -2f;
        private const float MapFogStart = 320f;
        private const float MapFogEnd = 1500f;
        private const float MapCameraFarClip = 5000f;

        private static readonly Color MapFogColor = new Color(0.68f, 0.78f, 0.84f, 1f);
        private static readonly Vector3[] CliffOutward =
        {
            Vector3.back,
            Vector3.right,
            Vector3.forward,
            Vector3.left
        };

        [MenuItem("ProjectoRTS/Setup/Add Map Backdrop To Placement Demo")]
        private static void AddMapBackdropToPlacementDemo()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            AddMapBackdropToScene();
        }

        /// <summary>
        /// Batch entry point: opens the existing PlacementDemo scene, replaces the
        /// blockout objects and saves the scene without regenerating gameplay.
        /// </summary>
        public static void AddMapBackdropToScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"Could not open the placement demo scene at {ScenePath}.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                return;
            }

            RemoveMapBackdrop();
            EnsureFolder(MeshFolder);
            EnsureFolder(MaterialFolder);
            CreateMapBackdrop();

            ConfigureMapCamera(Object.FindFirstObjectByType<Camera>());
            ConfigureMapSun(FindDirectionalLight());

            NavMeshSurface surface = Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface != null)
            {
                surface.layerMask = ~(1 << EnvironmentLayer);
                EditorUtility.SetDirty(surface);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError($"Could not save the map backdrop into {ScenePath}.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PlacementDemoBuilder] Map backdrop blockout added to {ScenePath}.");

            if (Application.isBatchMode)
            {
                CaptureMapPreviews();
            }
        }

        private static GameObject CreateMapBackdrop()
        {
            EnsureFolder(MeshFolder);

            GameObject root = new GameObject("Map Backdrop");
            root.transform.position = Vector3.zero;
            root.layer = EnvironmentLayer;

            Material waterMaterial = GetOrCreateMaterial(
                "Water",
                new Color(0.11f, 0.34f, 0.42f, 1f));
            ConfigureMapSurface(waterMaterial, 0.05f, 0.78f);

            Material cliffMaterial = GetOrCreateMaterial(
                "Cliff",
                new Color(0.42f, 0.38f, 0.32f, 1f));
            ConfigureMapSurface(cliffMaterial, 0f, 0.08f);

            Material nearMountainMaterial = GetOrCreateMaterial(
                "MountainNear",
                new Color(0.38f, 0.46f, 0.44f, 1f));
            ConfigureMapSurface(nearMountainMaterial, 0f, 0.04f);

            Material farMountainMaterial = GetOrCreateMaterial(
                "MountainFar",
                new Color(0.5f, 0.6f, 0.68f, 1f));
            ConfigureMapSurface(farMountainMaterial, 0f, 0.02f);

            CreateWaterPlane(root.transform, waterMaterial);
            CreateCliffRim(root.transform, cliffMaterial);
            CreateMountainRing(
                root.transform,
                "Mountains Near",
                "MountainRingNear",
                700f,
                260f,
                35f,
                90f,
                1201,
                nearMountainMaterial);
            CreateMountainRing(
                root.transform,
                "Mountains Far",
                "MountainRingFar",
                1100f,
                380f,
                60f,
                150f,
                7301,
                farMountainMaterial);

            DisableShadows(root);
            SetLayerRecursively(root, EnvironmentLayer);
            ConfigureMapAtmosphere();
            return root;
        }

        private static void CreateWaterPlane(Transform parent, Material material)
        {
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Water";
            water.transform.SetParent(parent, false);
            water.transform.localPosition = new Vector3(0f, WaterLevel, 0f);
            water.transform.localScale = new Vector3(600f, 1f, 600f);
            water.layer = EnvironmentLayer;

            Collider collider = water.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            MeshRenderer renderer = water.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static void CreateCliffRim(Transform parent, Material material)
        {
            Mesh mesh = SaveMeshAsset("IslandCliffRing", BuildCliffRimMesh());
            GameObject cliffs = new GameObject("Island Cliffs");
            cliffs.transform.SetParent(parent, false);
            cliffs.layer = EnvironmentLayer;
            cliffs.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = cliffs.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void CreateMountainRing(
            Transform parent,
            string objectName,
            string meshName,
            float radius,
            float width,
            float minHeight,
            float maxHeight,
            int seed,
            Material material)
        {
            Mesh mesh = SaveMeshAsset(
                meshName,
                BuildMountainRingMesh(radius, width, minHeight, maxHeight, seed));
            GameObject ring = new GameObject(objectName);
            ring.transform.SetParent(parent, false);
            ring.layer = EnvironmentLayer;
            ring.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = ring.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Mesh BuildCliffRimMesh()
        {
            const int samplesPerSide = 48;
            int totalSamples = samplesPerSide * 4;
            float[] halfExtents = { 156f, 161f, 165f, 174f };
            float[] heights = { 0.08f, 0.04f, -1.8f, -5.2f };
            float[] jitters = { 0.8f, 1.2f, 2.2f, 4.5f };
            System.Random random = new System.Random(4711);

            Vector3[][] rings = new Vector3[halfExtents.Length][];
            for (int ringIndex = 0; ringIndex < halfExtents.Length; ringIndex++)
            {
                rings[ringIndex] = new Vector3[totalSamples];
                int sample = 0;
                for (int side = 0; side < 4; side++)
                {
                    for (int i = 0; i < samplesPerSide; i++)
                    {
                        float t = i / (float)samplesPerSide;
                        Vector3 point = SquarePerimeterPoint(side, t, halfExtents[ringIndex]);
                        float jitter = NextSigned(random) * jitters[ringIndex];
                        point += CliffOutward[side] * jitter;
                        point.y = heights[ringIndex] + NextSigned(random) * 0.12f;
                        rings[ringIndex][sample] = point;
                        sample++;
                    }
                }
            }

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            for (int ringIndex = 0; ringIndex < rings.Length - 1; ringIndex++)
            {
                for (int i = 0; i < totalSamples; i++)
                {
                    int next = (i + 1) % totalSamples;
                    AddQuad(
                        vertices,
                        triangles,
                        rings[ringIndex][i],
                        rings[ringIndex][next],
                        rings[ringIndex + 1][next],
                        rings[ringIndex + 1][i]);
                }
            }

            return BuildFlatMesh(vertices, triangles);
        }

        private static Mesh BuildMountainRingMesh(
            float radius,
            float width,
            float minHeight,
            float maxHeight,
            int seed)
        {
            const int samples = 220;
            float offset = seed % 997;
            Vector3[] inner = new Vector3[samples];
            Vector3[] ridge = new Vector3[samples];
            Vector3[] outer = new Vector3[samples];

            for (int i = 0; i < samples; i++)
            {
                float angle = i / (float)samples * Mathf.PI * 2f;
                float cosine = Mathf.Cos(angle);
                float sine = Mathf.Sin(angle);

                float broad = Mathf.PerlinNoise(cosine * 1.7f + offset, sine * 1.7f + offset);
                float medium = Mathf.PerlinNoise(cosine * 5.3f + offset * 0.5f, sine * 5.3f + offset * 0.5f);
                float detail = Mathf.PerlinNoise(cosine * 13f + offset * 0.25f, sine * 13f + offset * 0.25f);
                float ridged = 1f - Mathf.Abs(broad * 2f - 1f);
                float height = Mathf.Lerp(
                    minHeight,
                    maxHeight,
                    Mathf.Clamp01(ridged * 0.62f + medium * 0.28f + detail * 0.1f));
                float ridgeJitter = (medium - 0.5f) * width * 0.1f;
                float baseRadius = radius - width * 0.55f;
                float outerRadius = radius + width * 0.55f;

                inner[i] = new Vector3(cosine * baseRadius, WaterLevel - 6f, sine * baseRadius);
                ridge[i] = new Vector3(
                    cosine * (radius + ridgeJitter),
                    height,
                    sine * (radius + ridgeJitter));
                outer[i] = new Vector3(cosine * outerRadius, WaterLevel - 6f, sine * outerRadius);
            }

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            for (int i = 0; i < samples; i++)
            {
                int next = (i + 1) % samples;
                AddQuad(vertices, triangles, inner[i], inner[next], ridge[next], ridge[i]);
                AddQuad(vertices, triangles, ridge[i], ridge[next], outer[next], outer[i]);
            }

            return BuildFlatMesh(vertices, triangles);
        }

        private static Mesh BuildFlatMesh(List<Vector3> vertices, List<int> triangles)
        {
            Mesh mesh = new Mesh { name = "Map Backdrop Mesh" };
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            Vector3[] normals = mesh.normals;
            float normalY = 0f;
            for (int i = 0; i < normals.Length; i++)
            {
                normalY += normals[i].y;
            }

            if (normalY < 0f)
            {
                int[] indices = mesh.triangles;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    int swap = indices[i];
                    indices[i] = indices[i + 2];
                    indices[i + 2] = swap;
                }

                mesh.triangles = indices;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }

            return mesh;
        }

        private static Mesh SaveMeshAsset(string assetName, Mesh mesh)
        {
            string path = $"{MeshFolder}/{assetName}.asset";
            Mesh asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (asset == null)
            {
                mesh.name = assetName;
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            EditorUtility.CopySerialized(mesh, asset);
            asset.name = assetName;
            EditorUtility.SetDirty(asset);
            Object.DestroyImmediate(mesh);
            return asset;
        }

        private static Vector3 SquarePerimeterPoint(int side, float t, float halfExtent)
        {
            switch (side)
            {
                case 0:
                    return new Vector3(Mathf.Lerp(-halfExtent, halfExtent, t), 0f, -halfExtent);
                case 1:
                    return new Vector3(halfExtent, 0f, Mathf.Lerp(-halfExtent, halfExtent, t));
                case 2:
                    return new Vector3(Mathf.Lerp(halfExtent, -halfExtent, t), 0f, halfExtent);
                default:
                    return new Vector3(-halfExtent, 0f, Mathf.Lerp(halfExtent, -halfExtent, t));
            }
        }

        private static void AddQuad(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static float NextSigned(System.Random random)
        {
            return (float)random.NextDouble() * 2f - 1f;
        }

        private static void ConfigureMapAtmosphere()
        {
            Material skybox = GetOrCreateSkyboxMaterial();
            if (skybox != null)
            {
                RenderSettings.skybox = skybox;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = MapFogColor;
            RenderSettings.fogStartDistance = MapFogStart;
            RenderSettings.fogEndDistance = MapFogEnd;
        }

        private static Material GetOrCreateSkyboxMaterial()
        {
            string path = $"{MaterialFolder}/Skybox.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
            {
                shader = Shader.Find("Skybox/6 Sided");
            }

            if (shader == null)
            {
                Debug.LogWarning("Could not find a skybox shader for the map backdrop.");
                return null;
            }

            if (material == null)
            {
                material = new Material(shader) { name = "Skybox" };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_SkyTint"))
            {
                material.SetColor("_SkyTint", new Color(0.32f, 0.5f, 0.75f, 1f));
            }

            if (material.HasProperty("_GroundColor"))
            {
                material.SetColor("_GroundColor", new Color(0.45f, 0.55f, 0.6f, 1f));
            }

            if (material.HasProperty("_AtmosphereThickness"))
            {
                material.SetFloat("_AtmosphereThickness", 1f);
            }

            if (material.HasProperty("_SunSize"))
            {
                material.SetFloat("_SunSize", 0.045f);
            }

            if (material.HasProperty("_SunSizeConvergence"))
            {
                material.SetFloat("_SunSizeConvergence", 5f);
            }

            if (material.HasProperty("_Exposure"))
            {
                material.SetFloat("_Exposure", 1.15f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureMapSurface(Material material, float metallic, float smoothness)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            EditorUtility.SetDirty(material);
        }

        private static void ConfigureMapCamera(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = MapFogColor;
            camera.farClipPlane = MapCameraFarClip;
        }

        private static void ConfigureMapSun(Light sun)
        {
            if (sun != null)
            {
                RenderSettings.sun = sun;
            }
        }

        private static Light FindDirectionalLight()
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                {
                    return lights[i];
                }
            }

            return null;
        }

        private static void RemoveMapBackdrop()
        {
            string[] names = { "Map Backdrop", "Water", "Island Cliffs", "Mountains Near", "Mountains Far" };
            for (int i = 0; i < names.Length; i++)
            {
                GameObject existing = GameObject.Find(names[i]);
                if (existing != null)
                {
                    Object.DestroyImmediate(existing);
                }
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
            }
        }

        private static void CaptureMapPreviews()
        {
            Camera source = Object.FindFirstObjectByType<Camera>();
            if (source == null)
            {
                Debug.LogWarning("Could not find a camera to render the map previews.");
                return;
            }

            const int width = 1600;
            const int height = 900;
            GameObject previewObject = new GameObject("Map Preview Camera");
            Camera preview = previewObject.AddComponent<Camera>();
            preview.CopyFrom(source);
            preview.enabled = false;
            preview.clearFlags = CameraClearFlags.Skybox;
            preview.backgroundColor = MapFogColor;
            preview.farClipPlane = MapCameraFarClip;
            if (previewObject.GetComponent<UniversalAdditionalCameraData>() == null)
            {
                previewObject.AddComponent<UniversalAdditionalCameraData>();
            }

            RenderTexture target = new RenderTexture(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            target.antiAliasing = 4;
            preview.targetTexture = target;

            string folder = Path.Combine(Path.GetTempPath(), "ProjectoRTS_map_blockout");
            Directory.CreateDirectory(folder);
            bool fogEnabled = RenderSettings.fog;
            RenderSettings.fog = false;
            preview.orthographic = false;
            preview.fieldOfView = 50f;
            preview.transform.position = new Vector3(0f, 1200f, -1900f);
            preview.transform.rotation = Quaternion.LookRotation(
                new Vector3(0f, 0f, 0f) - preview.transform.position,
                Vector3.up);
            preview.Render();
            CaptureView(
                preview,
                target,
                new Vector3(0f, 1200f, -1900f),
                new Vector3(0f, 0f, 0f),
                Path.Combine(folder, "overview.png"),
                width,
                height);

            RenderSettings.fog = fogEnabled;
            preview.fieldOfView = 40f;
            CaptureView(
                preview,
                target,
                new Vector3(0f, 70f, -120f),
                new Vector3(0f, 2f, 160f),
                Path.Combine(folder, "gameplay.png"),
                width,
                height);

            preview.targetTexture = null;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(previewObject);
            Debug.Log($"[PlacementDemoBuilder] Map blockout previews saved to {folder}.");
        }

        private static void CaptureView(
            Camera camera,
            RenderTexture target,
            Vector3 position,
            Vector3 lookAt,
            string path,
            int width,
            int height)
        {
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
    }
}
#endif
