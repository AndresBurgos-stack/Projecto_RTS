using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Metodo 2 - Skinned Mesh para el psiloi: convierte el arco y la armadura en
/// SkinnedMeshRenderers cuyos huesos (bones array) son los mismos del esqueleto
/// del personaje (Maniqui), no un esqueleto propio. Para lograrlo copia cada
/// malla rigida a un asset nuevo con bindposes y boneWeights calculados:
///
/// - Arco: peso total en el hueso en el que ya cuelga (la mano), de modo que
///   conserva la pose ajustada a mano y acompana la mano durante la animacion
///   de tensar y disparar.
/// - Armadura: reparte pesos por proximidad entre Hips, Spine, Chest,
///   UpperChest y los hombros, de modo que la coraza se deforme con el torso
///   al atacar, igual que la piel del personaje.
///
/// Ejecutar desde: Herramientas > Psiloi: Auto-rig skinned (arco y armadura).
/// Si despues se mueve o rota el arco en el editor, volver a ejecutar el menu
/// para rehornear la pose de referencia.
/// </summary>
public static class PsiloiAutoRig
{
    const string PrefabPath = "Assets/_Project/Prefabs/Units/Psiloi.prefab";
    const string MeshesDir = "Assets/_Project/Meshes";

    // Hueso del material del equipo (cuero) por si un renderer queda sin material.
    const string MaterialEquipoGuid = "7c45928d8347f82429010d456494586b";

    // Huesos del torso que reciben pesos de la armadura y el hueso que marca
    // el fin de cada segmento para medir la distancia de cada vertice.
    static readonly (string hueso, string finSegmento)[] Torso =
    {
        ("Hips", "Spine"),
        ("Spine", "Chest"),
        ("Chest", "UpperChest"),
        ("UpperChest", "Neck"),
        ("LeftShoulder", "LeftUpperArm"),
        ("RightShoulder", "RightUpperArm"),
    };

    [MenuItem("Herramientas/Psiloi: Auto-rig skinned (arco y armadura)")]
    public static void RunDesdeMenu()
    {
        Run();
    }

    public static void Run()
    {
        AssetDatabase.Refresh();
        Directory.CreateDirectory(MeshesDir);

        // Se activa Read/Write de los FBX ANTES de abrir el prefab: reimportar
        // con el prefab cargado invalidaria las referencias a sus sub-objetos.
        AsegurarFbxLegible("Assets/AncientGrecce/Bow.fbx");
        AsegurarFbxLegible("Assets/AncientGrecce/Mcitizen_Chest.fbx");

        GameObject raiz = PrefabUtility.LoadPrefabContents(PrefabPath);
        bool cambios = false;
        try
        {
            SkinnedMeshRenderer cuerpo = BuscarCuerpo(raiz);
            if (cuerpo == null)
            {
                Debug.LogError("[PsiloiAutoRig] No encontre el SkinnedMeshRenderer del cuerpo (Maniqui) dentro del prefab; nada que hacer.");
                return;
            }
            Transform[] esqueleto = cuerpo.bones;
            Debug.Log($"[PsiloiAutoRig] Esqueleto del cuerpo: {esqueleto.Length} huesos.");
            Dictionary<string, Transform> mapa = MapaDeHuesos(esqueleto);

            Transform arco = Buscar(raiz, "Bow");
            bool arcoOk = arco != null && RiggearRigido(arco, esqueleto, mapa, "Bow_Psiloi_Skinned");
            if (arco == null) Debug.LogWarning("[PsiloiAutoRig] No encontre el objeto 'Bow' en el prefab.");

            Transform armadura = Buscar(raiz, "Mcitizen_Chest");
            bool armaduraOk = armadura != null && RiggearTorso(armadura, esqueleto, mapa, "Armadura_Psiloi_Skinned");
            if (armadura == null) Debug.LogWarning("[PsiloiAutoRig] No encontre el objeto 'Mcitizen_Chest' en el prefab.");

            cambios = arcoOk || armaduraOk;
            if (cambios)
            {
                PrefabUtility.SaveAsPrefabAsset(raiz, PrefabPath);
                Debug.Log("[PsiloiAutoRig] Prefab guardado con los SkinnedMeshRenderer nuevos.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }

        if (cambios)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    // El cuerpo es el SMR con mas huesos dentro del prefab (el Maniqui).
    static SkinnedMeshRenderer BuscarCuerpo(GameObject raiz)
    {
        SkinnedMeshRenderer mejor = null;
        foreach (SkinnedMeshRenderer smr in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh == null) continue;
            int huesos = smr.bones != null ? smr.bones.Length : 0;
            if (huesos < 10) continue; // los accesorios tienen 0 o 1 hueso
            if (mejor == null || huesos > mejor.bones.Length) mejor = smr;
        }
        return mejor;
    }

    static Transform Buscar(GameObject raiz, string nombre)
    {
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == nombre) return t;
        }
        return null;
    }

    static Dictionary<string, Transform> MapaDeHuesos(Transform[] huesos)
    {
        var mapa = new Dictionary<string, Transform>();
        foreach (Transform h in huesos)
        {
            if (h == null || mapa.ContainsKey(h.name)) continue;
            mapa[h.name] = h;
            string limpio = SinSufijo(h.name, "Model");
            limpio = SinSufijo(limpio, "Geometry");
            if (!mapa.ContainsKey(limpio)) mapa[limpio] = h;
        }
        return mapa;
    }

    static string SinSufijo(string nombre, string sufijo)
    {
        return nombre.EndsWith(sufijo, StringComparison.Ordinal) && nombre.Length > sufijo.Length
            ? nombre.Substring(0, nombre.Length - sufijo.Length)
            : nombre;
    }

    static Transform BuscarHueso(Dictionary<string, Transform> mapa, string patron)
    {
        if (mapa.TryGetValue(patron, out Transform exacto)) return exacto;
        if (mapa.TryGetValue(patron + "Model", out Transform conModelo)) return conModelo;
        foreach (KeyValuePair<string, Transform> kv in mapa)
        {
            if (kv.Key.Contains(patron)) return kv.Value;
        }
        return null;
    }

    // Busca el MeshFilter cuya malla coincida con el nombre esperado; si no lo
    // hay, usa el primero que no sea una pieza conocida de otro accesorio.
    static MeshFilter EncontrarMeshFilter(Transform raiz, string nombreMalla)
    {
        MeshFilter reserva = null;
        foreach (MeshFilter f in raiz.GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh == null) continue;
            if (f.sharedMesh.name.Contains(nombreMalla)) return f;
            if (reserva == null && !f.sharedMesh.name.Contains("Cylinder.014")) reserva = f;
        }
        return reserva;
    }

    // Piezas rigidas (arco): un solo hueso con peso total.
    static bool RiggearRigido(Transform arco, Transform[] esqueleto, Dictionary<string, Transform> mapa, string nombreAsset)
    {
        SkinnedMeshRenderer smr = arco.GetComponent<SkinnedMeshRenderer>();
        Mesh origen = smr != null ? smr.sharedMesh : null;
        if (origen == null)
        {
            MeshFilter mf = arco.GetComponent<MeshFilter>();
            if (mf != null) origen = mf.sharedMesh;
        }
        if (origen == null)
        {
            Debug.LogWarning($"[PsiloiAutoRig] El objeto '{arco.name}' no tiene malla; se omite.");
            return false;
        }

        // El hueso del arco es su padre actual si pertenece al esqueleto; si no,
        // se recurre a la mano derecha y luego a la izquierda.
        Transform hueso = arco.parent != null && Array.IndexOf(esqueleto, arco.parent) >= 0
            ? arco.parent
            : (BuscarHueso(mapa, "RightHand") ?? BuscarHueso(mapa, "LeftHand"));
        if (hueso == null)
        {
            Debug.LogWarning("[PsiloiAutoRig] No encontre el hueso de la mano para el arco; se omite.");
            return false;
        }

        Mesh horneada = CopiarMalla(origen, nombreAsset);
        if (horneada == null) return false;

        var pesos = new BoneWeight[horneada.vertexCount];
        for (int i = 0; i < pesos.Length; i++)
        {
            pesos[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
        }
        horneada.boneWeights = pesos;
        horneada.bindposes = new[] { hueso.worldToLocalMatrix * arco.localToWorldMatrix };

        string ruta = $"{MeshesDir}/{nombreAsset}.asset";
        AssetDatabase.DeleteAsset(ruta);
        AssetDatabase.CreateAsset(horneada, ruta);

        if (smr == null) smr = arco.AddComponent<SkinnedMeshRenderer>();
        Material[] mats = smr.sharedMaterials;
        if (mats == null || mats.Length == 0 || mats.All(m => m == null))
        {
            Material equipo = CargarMaterial(MaterialEquipoGuid);
            if (equipo != null) mats = new[] { equipo };
        }

        smr.sharedMesh = horneada;
        smr.bones = new[] { hueso };
        smr.rootBone = hueso;
        smr.quality = SkinQuality.Bone1;
        smr.updateWhenOffscreen = true;
        if (mats != null && mats.Length > 0) smr.sharedMaterials = mats;

        Debug.Log($"[PsiloiAutoRig] Arco '{arco.name}': {horneada.vertexCount} vertices amarrados al hueso '{hueso.name}' con peso total. Malla: {ruta}");
        return true;
    }

    // Coraza: pesos por proximidad repartidos entre los huesos del torso.
    static bool RiggearTorso(Transform armadura, Transform[] esqueleto, Dictionary<string, Transform> mapa, string nombreAsset)
    {
        MeshFilter mf = EncontrarMeshFilter(armadura, "Circle.043");
        Mesh origen = null;
        Transform renderT = armadura;
        SkinnedMeshRenderer smrPrevio = armadura.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (mf != null)
        {
            origen = mf.sharedMesh;
            renderT = mf.transform;
        }
        else if (smrPrevio != null)
        {
            origen = smrPrevio.sharedMesh;
            renderT = smrPrevio.transform;
        }
        if (origen == null)
        {
            Debug.LogWarning($"[PsiloiAutoRig] '{armadura.name}' no tiene malla; se omite.");
            return false;
        }

        // Hueso del esqueleto del que cuelga la armadura.
        Transform huesoPadre = null;
        for (Transform t = renderT.parent; t != null; t = t.parent)
        {
            if (Array.IndexOf(esqueleto, t) >= 0) { huesoPadre = t; break; }
        }

        var segmentos = new List<(Transform hueso, Vector3 a, Vector3 b)>();
        foreach (var (nombre, fin) in Torso)
        {
            Transform h = BuscarHueso(mapa, nombre);
            if (h == null) continue;
            Transform hFin = BuscarHueso(mapa, fin);
            segmentos.Add((h, h.position, hFin != null ? hFin.position : h.position));
        }
        if (huesoPadre != null && segmentos.All(s => s.hueso != huesoPadre))
        {
            segmentos.Add((huesoPadre, huesoPadre.position, huesoPadre.position));
        }
        if (segmentos.Count == 0)
        {
            Debug.LogWarning("[PsiloiAutoRig] No encontre huesos del torso para la armadura; se omite.");
            return false;
        }

        Mesh horneada = CopiarMalla(origen, nombreAsset);
        if (horneada == null) return false;

        Transform[] huesos = segmentos.Select(s => s.hueso).ToArray();
        var bindposes = new Matrix4x4[huesos.Length];
        for (int i = 0; i < huesos.Length; i++)
        {
            bindposes[i] = huesos[i].worldToLocalMatrix * renderT.localToWorldMatrix;
        }

        Vector3[] vertices = horneada.vertices;
        var pesos = new BoneWeight[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            pesos[i] = PesosPorProximidad(renderT.TransformPoint(vertices[i]), segmentos);
        }

        horneada.boneWeights = pesos;
        horneada.bindposes = bindposes;

        string ruta = $"{MeshesDir}/{nombreAsset}.asset";
        AssetDatabase.DeleteAsset(ruta);
        AssetDatabase.CreateAsset(horneada, ruta);

        // El SMR reemplaza al MeshRenderer en el mismo objeto, conservando el material.
        MeshRenderer mr = renderT.GetComponent<MeshRenderer>();
        SkinnedMeshRenderer smr = renderT.GetComponent<SkinnedMeshRenderer>();
        Material[] mats = null;
        if (mr != null) mats = mr.sharedMaterials;
        else if (smr != null) mats = smr.sharedMaterials;
        if (mats == null || mats.Length == 0 || mats.All(m => m == null))
        {
            Material equipo = CargarMaterial(MaterialEquipoGuid);
            if (equipo != null) mats = new[] { equipo };
        }

        if (smr == null) smr = renderT.AddComponent<SkinnedMeshRenderer>();
        if (mr != null) DestroyImmediate(mr);

        smr.sharedMesh = horneada;
        smr.bones = huesos;
        smr.rootBone = huesoPadre != null ? huesoPadre : huesos[0];
        smr.quality = SkinQuality.Bone4;
        smr.updateWhenOffscreen = true;
        if (mats != null && mats.Length > 0) smr.sharedMaterials = mats;

        string nombres = string.Join(", ", huesos.Select(h => h.name));
        Debug.Log($"[PsiloiAutoRig] Armadura '{armadura.name}': {horneada.vertexCount} vertices repartidos entre {huesos.Length} huesos ({nombres}). Malla: {ruta}");
        return true;
    }

    // Peso del vertice repartido entre los 3 huesos mas cercanos (1/d^2 normalizado).
    static BoneWeight PesosPorProximidad(Vector3 punto, List<(Transform hueso, Vector3 a, Vector3 b)> segmentos)
    {
        int n = segmentos.Count;
        var pesos = new float[n];
        int mejor = 0;
        for (int i = 0; i < n; i++)
        {
            float d = DistanciaSegmento(punto, segmentos[i].a, segmentos[i].b);
            pesos[i] = 1f / (Mathf.Max(d, 0.01f) * Mathf.Max(d, 0.01f));
            if (pesos[i] > pesos[mejor]) mejor = i;
        }

        var orden = new List<int>(n);
        for (int i = 0; i < n; i++) orden.Add(i);
        orden.Sort((x, y) => pesos[y].CompareTo(pesos[x]));

        int tomar = Mathf.Min(3, n);
        float suma = 0f;
        for (int k = 0; k < tomar; k++) suma += pesos[orden[k]];
        if (suma <= 1e-6f)
        {
            return new BoneWeight { boneIndex0 = mejor, weight0 = 1f };
        }

        var bw = new BoneWeight();
        for (int k = 0; k < tomar; k++)
        {
            float w = pesos[orden[k]] / suma;
            switch (k)
            {
                case 0: bw.boneIndex0 = orden[k]; bw.weight0 = w; break;
                case 1: bw.boneIndex1 = orden[k]; bw.weight1 = w; break;
                case 2: bw.boneIndex2 = orden[k]; bw.weight2 = w; break;
            }
        }
        return bw;
    }

    static float DistanciaSegmento(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float largo2 = ab.sqrMagnitude;
        if (largo2 < 1e-8f) return Vector3.Distance(p, a);
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / largo2);
        return Vector3.Distance(p, a + ab * t);
    }

    static Mesh CopiarMalla(Mesh origen, string nombre)
    {
        var m = new Mesh { name = nombre };
        Vector3[] vertices = origen.vertices;
        m.indexFormat = vertices.Length > 65535
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        m.vertices = vertices;
        if (origen.normals is { Length: > 0 }) m.normals = origen.normals; else m.RecalculateNormals();
        if (origen.tangents is { Length: > 0 }) m.tangents = origen.tangents;
        if (origen.uv is { Length: > 0 }) m.uv = origen.uv;
        if (origen.uv2 is { Length: > 0 }) m.uv2 = origen.uv2;
        if (origen.colors is { Length: > 0 }) m.colors = origen.colors;
        m.subMeshCount = origen.subMeshCount;
        for (int i = 0; i < origen.subMeshCount; i++)
        {
            m.SetTriangles(origen.GetTriangles(i), i, false);
        }
        m.bounds = origen.bounds;
        if (origen.blendShapeCount > 0)
        {
            Debug.LogWarning($"[PsiloiAutoRig] '{nombre}': la malla origen tiene {origen.blendShapeCount} blendshapes y no se copian.");
        }
        return m;
    }

    static void AsegurarFbxLegible(string ruta)
    {
        ModelImporter importador = AssetImporter.GetAtPath(ruta) as ModelImporter;
        if (importador == null || importador.isReadable) return;
        importador.isReadable = true;
        importador.SaveAndReimport();
        Debug.Log($"[PsiloiAutoRig] Active Read/Write en {ruta} para poder hornear la malla skinned.");
    }

    static Material CargarMaterial(string guid)
    {
        string ruta = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(ruta) ? null : AssetDatabase.LoadAssetAtPath<Material>(ruta);
    }
}
