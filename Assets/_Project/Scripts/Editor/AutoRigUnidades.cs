using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Metodo 2 - Skinned Mesh para las unidades: convierte los accesorios (armas,
/// escudos, cascos y armaduras) en SkinnedMeshRenderers cuyo array `bones` usa
/// los mismos huesos del esqueleto del cuerpo (Maniqui), no un esqueleto propio.
/// Para lograrlo copia cada malla rigida a un asset nuevo con bindposes y
/// boneWeights calculados:
///
/// - Piezas rigidas (arco, escudo, gladius, casco, martillo): peso total en el
///   primer ancestro de la pieza que sea hueso del esqueleto, de modo que
///   conserva la pose ajustada a mano y sigue ese hueso durante la animacion.
/// - Piezas que se deforman (corazas, tunica, falda): reparten pesos por
///   proximidad entre los tramos del cuerpo segun su preseteo: Torso (tronco y
///   claviculas), TorsoConBrazos (agrega los brazos, para hombreras) y Piernas
///   (cadera y piernas, para faldas), de modo que sigan al cuerpo igual que la
///   piel del personaje.
///
/// Cada unidad declara su prefab y sus piezas en las configs de abajo. Se
/// ejecuta desde: Herramientas > Psiloi: Auto-rig skinned (arco y armadura) /
/// Herramientas > Auxiliar: Auto-rig skinned (armadura y armas) /
/// Herramientas > Trabajador: Auto-rig skinned (tunica y martillo) /
/// Herramientas > Legionario: Auto-rig skinned (armadura y armas) /
/// Herramientas > Unidades: Auto-rig todas.
/// Si despues se mueve o rota una pieza en el editor, volver a ejecutar el menu
/// para rehornear la pose de referencia.
/// </summary>
public static class AutoRigUnidades
{
    const string MeshesDir = "Assets/_Project/Meshes";

    // Material del equipo (cuero) por si un renderer queda sin material.
    const string MaterialEquipoGuid = "7c45928d8347f82429010d456494586b";

    enum TipoPieza { Rigida, Torso }

    // Que tramos del cuerpo reciben pesos por proximidad en una pieza que se
    // deforma. Cada segmento va de un hueso al hueso que le sigue: la distancia
    // de cada vertice se mide a esos tramos.
    enum Preseteo
    {
        Torso,          // tronco y claviculas (corazas cortas, tunica)
        TorsoConBrazos, // agrega los brazos: hombreras que bajan por el brazo
        Piernas,        // cadera y piernas: faldas y pantalones que siguen las piernas
    }

    class Pieza
    {
        public string objeto;      // nombre del GO raiz de la pieza dentro del prefab
        public string malla;       // nombre (parcial) de la malla, para ubicar el render correcto
        public string asset;       // nombre del asset horneado en Assets/_Project/Meshes
        public TipoPieza tipo;
        public Preseteo preseteo;  // tramos de proximidad para piezas de tipo Torso
        public Vector3 desplazamiento; // se suman a los vertices de la malla horneada, en metros de mundo
        public bool recurrirAMano; // si no cuelga de un hueso, usar la mano derecha y luego la izquierda
    }

    class Unidad
    {
        public string nombre;
        public string prefab;
        public Pieza[] piezas;
    }

    // Segmentos por preseteo: el hueso de partida y el hueso que marca el fin
    // de cada tramo para medir la distancia de cada vertice.
    static readonly (string hueso, string finSegmento)[] SegmentosTorso =
    {
        ("Hips", "Spine"),
        ("Spine", "Chest"),
        ("Chest", "UpperChest"),
        ("UpperChest", "Neck"),
        ("LeftShoulder", "LeftUpperArm"),
        ("RightShoulder", "RightUpperArm"),
    };

    static readonly (string hueso, string finSegmento)[] SegmentosTorsoConBrazos =
    {
        ("Hips", "Spine"),
        ("Spine", "Chest"),
        ("Chest", "UpperChest"),
        ("UpperChest", "Neck"),
        ("LeftShoulder", "LeftUpperArm"),
        ("RightShoulder", "RightUpperArm"),
        ("LeftUpperArm", "LeftLowerArm"),
        ("RightUpperArm", "RightLowerArm"),
    };

    static readonly (string hueso, string finSegmento)[] SegmentosPiernas =
    {
        ("Hips", "Spine"),
        ("Hips", "LeftUpperLeg"),
        ("Hips", "RightUpperLeg"),
        ("LeftUpperLeg", "LeftLowerLeg"),
        ("LeftLowerLeg", "LeftFoot"),
        ("RightUpperLeg", "RightLowerLeg"),
        ("RightLowerLeg", "RightFoot"),
    };

    static (string hueso, string finSegmento)[] SegmentosDe(Preseteo preseteo)
    {
        switch (preseteo)
        {
            case Preseteo.TorsoConBrazos: return SegmentosTorsoConBrazos;
            case Preseteo.Piernas: return SegmentosPiernas;
            default: return SegmentosTorso;
        }
    }

    static readonly Unidad Psiloi = new Unidad
    {
        nombre = "Psiloi",
        prefab = "Assets/_Project/Prefabs/Units/Psiloi.prefab",
        piezas = new Pieza[]
        {
            new Pieza { objeto = "Bow", malla = "Bow", asset = "Bow_Psiloi_Skinned", tipo = TipoPieza.Rigida, recurrirAMano = true },
            new Pieza { objeto = "Mcitizen_Chest", malla = "Circle.043", asset = "Armadura_Psiloi_Skinned", tipo = TipoPieza.Torso, preseteo = Preseteo.TorsoConBrazos },
        },
    };

    static readonly Unidad Auxiliar = new Unidad
    {
        nombre = "Auxiliar",
        prefab = "Assets/_Project/Prefabs/Units/Auxiliar.prefab",
        piezas = new Pieza[]
        {
            new Pieza { objeto = "Armadura", malla = "Cube.001", asset = "Armadura_Auxiliar_Skinned", tipo = TipoPieza.Torso, preseteo = Preseteo.TorsoConBrazos },
            new Pieza { objeto = "Roman Shield", malla = "Plane.013", asset = "Escudo_Auxiliar_Skinned", tipo = TipoPieza.Rigida },
            new Pieza { objeto = "Gladius Variant", malla = "Cylinder.001", asset = "Gladius_Auxiliar_Skinned", tipo = TipoPieza.Rigida },
            new Pieza { objeto = "Casco romano", malla = "Cube.004", asset = "Casco_Auxiliar_Skinned", tipo = TipoPieza.Rigida },
        },
    };

    static readonly Unidad Trabajador = new Unidad
    {
        nombre = "Trabajador",
        prefab = "Assets/_Project/Prefabs/Units/Trabajador.prefab",
        piezas = new Pieza[]
        {
            new Pieza { objeto = "M Citizen Side Robe", malla = "Circle.043", asset = "Tunica_Trabajador_Skinned", tipo = TipoPieza.Torso },
            new Pieza { objeto = "Hammer Handle", malla = "Cylinder", asset = "Mango_Martillo_Skinned", tipo = TipoPieza.Rigida },
            new Pieza { objeto = "Hammer Head", malla = "Cube", asset = "Cabeza_Martillo_Skinned", tipo = TipoPieza.Rigida },
        },
    };

    static readonly Unidad Legionario = new Unidad
    {
        nombre = "Legionario",
        prefab = "Assets/_Project/Prefabs/Units/Legionario.prefab",
        piezas = new Pieza[]
        {
            new Pieza { objeto = "ArmaduraLeginario", malla = "Cube.008", asset = "Armadura_Leginario_Skinned", tipo = TipoPieza.Torso, preseteo = Preseteo.TorsoConBrazos },
            new Pieza { objeto = "PantalonesLegionario", malla = "Cube.002", asset = "Pantalones_Leginario_Skinned", tipo = TipoPieza.Torso, preseteo = Preseteo.Piernas },
            new Pieza { objeto = "Casco romano", malla = "Cube.004", asset = "Casco_Leginario_Skinned", tipo = TipoPieza.Rigida },
            new Pieza { objeto = "Gladius Variant", malla = "Cylinder.001", asset = "Gladius_Leginario_Skinned", tipo = TipoPieza.Rigida },
        },
    };

    [MenuItem("Herramientas/Psiloi: Auto-rig skinned (arco y armadura)")]
    public static void PsiloiDesdeMenu()
    {
        Ejecutar(Psiloi);
    }

    [MenuItem("Herramientas/Auxiliar: Auto-rig skinned (armadura y armas)")]
    public static void AuxiliarDesdeMenu()
    {
        Ejecutar(Auxiliar);
    }

    [MenuItem("Herramientas/Trabajador: Auto-rig skinned (tunica y martillo)")]
    public static void TrabajadorDesdeMenu()
    {
        Ejecutar(Trabajador);
    }

    [MenuItem("Herramientas/Legionario: Auto-rig skinned (armadura y armas)")]
    public static void LegionarioDesdeMenu()
    {
        Ejecutar(Legionario);
    }

    [MenuItem("Herramientas/Unidades: Auto-rig todas")]
    public static void TodasDesdeMenu()
    {
        Ejecutar(Psiloi);
        Ejecutar(Auxiliar);
        Ejecutar(Trabajador);
        Ejecutar(Legionario);
    }

    static void Ejecutar(Unidad unidad)
    {
        AssetDatabase.Refresh();
        Directory.CreateDirectory(MeshesDir);

        // Se activa Read/Write de los FBX ANTES de abrir el prefab: reimportar
        // con el prefab cargado invalidaria las referencias a sus sub-objetos.
        AsegurarFbxLegibles(unidad);

        GameObject raiz = PrefabUtility.LoadPrefabContents(unidad.prefab);
        bool cambios = false;
        try
        {
            SkinnedMeshRenderer cuerpo = BuscarCuerpo(raiz);
            if (cuerpo == null)
            {
                Debug.LogError($"[{Etiqueta(unidad)}] No encontre el SkinnedMeshRenderer del cuerpo (Maniqui) dentro del prefab; nada que hacer.");
                return;
            }
            Transform[] esqueleto = cuerpo.bones;
            Debug.Log($"[{Etiqueta(unidad)}] Esqueleto del cuerpo: {esqueleto.Length} huesos.");
            Dictionary<string, Transform> mapa = MapaDeHuesos(esqueleto);

            foreach (Pieza pieza in unidad.piezas)
            {
                Transform t = Buscar(raiz, pieza.objeto);
                if (t == null)
                {
                    Debug.LogWarning($"[{Etiqueta(unidad)}] No encontre el objeto '{pieza.objeto}' en el prefab.");
                    continue;
                }
                bool ok;
                try
                {
                    ok = pieza.tipo == TipoPieza.Torso
                        ? RiggearTorso(unidad, t, pieza, esqueleto, mapa)
                        : RiggearRigida(unidad, t, pieza, esqueleto, mapa);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[{Etiqueta(unidad)}] Error riggeando '{pieza.objeto}': {e.GetType().Name}: {e.Message}");
                    ok = false;
                }
                cambios |= ok;
            }

            if (cambios)
            {
                PrefabUtility.SaveAsPrefabAsset(raiz, unidad.prefab);
                Debug.Log($"[{Etiqueta(unidad)}] Prefab guardado con los SkinnedMeshRenderer nuevos.");
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

    static string Etiqueta(Unidad unidad)
    {
        return $"AutoRig {unidad.nombre}";
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

    // Resuelve, ANTES de abrir el prefab en modo edicion, el FBX de cada pieza
    // (cargando el prefab como asset de solo lectura) y activa su Read/Write.
    static void AsegurarFbxLegibles(Unidad unidad)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(unidad.prefab);
        if (asset == null)
        {
            Debug.LogWarning($"[{Etiqueta(unidad)}] No pude cargar el prefab como asset para detectar los FBX; verifica que las piezas ya tengan Read/Write.");
            return;
        }
        var rutas = new HashSet<string>();
        foreach (Pieza pieza in unidad.piezas)
        {
            Transform t = Buscar(asset, pieza.objeto);
            if (t == null) continue;
            RenderPieza render = LocalizarRender(t, pieza.malla);
            if (render == null || render.malla == null) continue;
            string ruta = AssetDatabase.GetAssetPath(render.malla);
            if (!string.IsNullOrEmpty(ruta) && ruta.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                rutas.Add(ruta);
            }
        }
        foreach (string ruta in rutas)
        {
            AsegurarFbxLegible(ruta);
        }
    }

    class RenderPieza
    {
        public Mesh malla;
        public Transform transform;
        public SkinnedMeshRenderer smr;
        public MeshRenderer mr;
    }

    // Encuentra el render de la pieza: prefiere un render cuya malla coincida
    // con el nombre esperado; si no hay coincidencia, toma el primero que
    // encuentre (primero los SkinnedMeshRenderer, que en re-ejecuciones ya
    // contienen la malla horneada).
    static RenderPieza LocalizarRender(Transform pieza, string pista)
    {
        RenderPieza reserva = null;
        foreach (SkinnedMeshRenderer smr in pieza.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh == null) continue;
            if (Coincide(smr.sharedMesh.name, pista))
            {
                return new RenderPieza { malla = smr.sharedMesh, transform = smr.transform, smr = smr };
            }
            if (reserva == null) reserva = new RenderPieza { malla = smr.sharedMesh, transform = smr.transform, smr = smr };
        }
        foreach (MeshFilter f in pieza.GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh == null) continue;
            if (Coincide(f.sharedMesh.name, pista))
            {
                return new RenderPieza { malla = f.sharedMesh, transform = f.transform, mr = f.GetComponent<MeshRenderer>() };
            }
            if (reserva == null) reserva = new RenderPieza { malla = f.sharedMesh, transform = f.transform, mr = f.GetComponent<MeshRenderer>() };
        }
        return reserva;
    }

    static bool Coincide(string nombreMalla, string pista)
    {
        return !string.IsNullOrEmpty(pista) && nombreMalla.Contains(pista);
    }

    // Piezas rigidas (armas, escudo, casco): un solo hueso con peso total.
    static bool RiggearRigida(Unidad unidad, Transform pieza, Pieza cfg, Transform[] esqueleto, Dictionary<string, Transform> mapa)
    {
        RenderPieza render = LocalizarRender(pieza, cfg.malla);
        if (render == null || render.malla == null)
        {
            Debug.LogWarning($"[{Etiqueta(unidad)}] El objeto '{pieza.name}' no tiene malla; se omite.");
            return false;
        }

        // El hueso es el primer ancestro de la pieza que pertenece al esqueleto;
        // si no hay ninguno y la pieza lo permite, se recurre a la mano derecha
        // y luego a la izquierda.
        Transform hueso = BuscarHuesoAncestro(pieza, esqueleto)
            ?? (cfg.recurrirAMano ? (BuscarHueso(mapa, "RightHand") ?? BuscarHueso(mapa, "LeftHand")) : null);
        if (hueso == null)
        {
            Debug.LogWarning($"[{Etiqueta(unidad)}] No encontre un hueso del esqueleto del que cuelgue '{pieza.name}'; se omite.");
            return false;
        }

        Mesh horneada = CopiarMalla(render.malla, cfg.asset);
        if (horneada == null) return false;
        DesplazarMalla(horneada, render.transform, cfg.desplazamiento);

        var pesos = new BoneWeight[horneada.vertexCount];
        for (int i = 0; i < pesos.Length; i++)
        {
            pesos[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
        }
        horneada.boneWeights = pesos;
        horneada.bindposes = new[] { hueso.worldToLocalMatrix * render.transform.localToWorldMatrix };

        string ruta = GuardarAsset(horneada, cfg.asset);
        if (!AplicarSkinned(unidad, render, horneada, new[] { hueso }, hueso, SkinQuality.Bone1)) return false;

        Debug.Log($"[{Etiqueta(unidad)}] Pieza rigida '{pieza.name}': {horneada.vertexCount} vertices amarrados al hueso '{hueso.name}' con peso total. Malla: {ruta}");
        return true;
    }

    // Coraza: pesos por proximidad repartidos entre los huesos del torso.
    static bool RiggearTorso(Unidad unidad, Transform pieza, Pieza cfg, Transform[] esqueleto, Dictionary<string, Transform> mapa)
    {
        RenderPieza render = LocalizarRender(pieza, cfg.malla);
        if (render == null || render.malla == null)
        {
            Debug.LogWarning($"[{Etiqueta(unidad)}] '{pieza.name}' no tiene malla; se omite.");
            return false;
        }

        // Hueso del esqueleto del que cuelga la pieza.
        Transform huesoPadre = BuscarHuesoAncestro(pieza, esqueleto);

        var segmentos = new List<(Transform hueso, Vector3 a, Vector3 b)>();
        foreach (var (nombre, fin) in SegmentosDe(cfg.preseteo))
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
            Debug.LogWarning($"[{Etiqueta(unidad)}] No encontre huesos del torso para '{pieza.name}'; se omite.");
            return false;
        }

        Mesh horneada = CopiarMalla(render.malla, cfg.asset);
        if (horneada == null) return false;
        DesplazarMalla(horneada, render.transform, cfg.desplazamiento);

        Transform[] huesos = segmentos.Select(s => s.hueso).ToArray();
        var bindposes = new Matrix4x4[huesos.Length];
        for (int i = 0; i < huesos.Length; i++)
        {
            bindposes[i] = huesos[i].worldToLocalMatrix * render.transform.localToWorldMatrix;
        }

        Vector3[] vertices = horneada.vertices;
        var pesos = new BoneWeight[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            pesos[i] = PesosPorProximidad(render.transform.TransformPoint(vertices[i]), segmentos);
        }

        horneada.boneWeights = pesos;
        horneada.bindposes = bindposes;

        string ruta = GuardarAsset(horneada, cfg.asset);
        if (!AplicarSkinned(unidad, render, horneada, huesos, huesoPadre != null ? huesoPadre : huesos[0], SkinQuality.Bone4)) return false;

        string nombres = string.Join(", ", huesos.Select(h => h.name));
        Debug.Log($"[{Etiqueta(unidad)}] Armadura '{pieza.name}': {horneada.vertexCount} vertices repartidos entre {huesos.Length} huesos ({nombres}). Malla: {ruta}");
        return true;
    }

    // Primer ancestro de la pieza (empezando por su padre) que sea un hueso del
    // esqueleto del cuerpo.
    static Transform BuscarHuesoAncestro(Transform pieza, Transform[] esqueleto)
    {
        for (Transform t = pieza.parent; t != null; t = t.parent)
        {
            if (Array.IndexOf(esqueleto, t) >= 0) return t;
        }
        return null;
    }

    // Suma a los vertices de la malla horneada un desplazamiento expresado en
    // el espacio del mundo (se convierte al espacio local de la malla). Se
    // aplica sobre la copia, nunca sobre la malla original; como el fuente de
    // cada horneado es siempre la malla cruda, el efecto es idempotente.
    static void DesplazarMalla(Mesh horneada, Transform pieza, Vector3 mundo)
    {
        if (mundo == Vector3.zero) return;
        Vector3 local = pieza.worldToLocalMatrix.MultiplyVector(mundo);
        Vector3[] vertices = horneada.vertices;
        for (int i = 0; i < vertices.Length; i++) vertices[i] += local;
        horneada.vertices = vertices;
    }

    // Reemplaza (o reutiliza) el renderer de la pieza con un SkinnedMeshRenderer,
    // conservando el material.
    static bool AplicarSkinned(Unidad unidad, RenderPieza render, Mesh horneada, Transform[] huesos, Transform rootBone, SkinQuality calidad)
    {
        Material[] mats = null;
        if (render.mr != null) mats = render.mr.sharedMaterials;
        else if (render.smr != null) mats = render.smr.sharedMaterials;
        if (mats == null || mats.Length == 0 || mats.All(m => m == null))
        {
            Material equipo = CargarMaterial(MaterialEquipoGuid);
            if (equipo != null) mats = new[] { equipo };
        }

        SkinnedMeshRenderer smr = render.smr;
        // En re-ejecuciones ya existe un SkinnedMeshRenderer sobre el mismo
        // objeto aunque el descubrimiento haya venido por el MeshFilter
        // original; preferirlo en vez de intentar agregar otro.
        if (smr == null) smr = render.transform.GetComponent<SkinnedMeshRenderer>();
        if (smr == null) smr = render.transform.gameObject.AddComponent<SkinnedMeshRenderer>();
        if (smr == null)
        {
            Debug.LogError($"[{Etiqueta(unidad)}] No pude agregar SkinnedMeshRenderer a '{render.transform.name}' (la instancia del prefab esta rota o es inmutable). Revisa el prefab y vuelve a correr el menu.");
            return false;
        }
        if (render.mr != null)
        {
            UnityEngine.Object.DestroyImmediate(render.mr);
            Debug.Log($"[{Etiqueta(unidad)}] '{render.transform.name}': reemplace el MeshRenderer por SkinnedMeshRenderer.");
        }

        smr.sharedMesh = horneada;
        smr.bones = huesos;
        smr.rootBone = rootBone;
        smr.quality = calidad;
        smr.updateWhenOffscreen = true;
        if (mats != null && mats.Length > 0) smr.sharedMaterials = mats;
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
            Debug.LogWarning($"[AutoRigUnidades] '{nombre}': la malla origen tiene {origen.blendShapeCount} blendshapes y no se copian.");
        }
        return m;
    }

    static string GuardarAsset(Mesh horneada, string nombre)
    {
        string ruta = $"{MeshesDir}/{nombre}.asset";
        AssetDatabase.DeleteAsset(ruta);
        AssetDatabase.CreateAsset(horneada, ruta);
        return ruta;
    }

    static void AsegurarFbxLegible(string ruta)
    {
        ModelImporter importador = AssetImporter.GetAtPath(ruta) as ModelImporter;
        if (importador == null || importador.isReadable) return;
        importador.isReadable = true;
        importador.SaveAndReimport();
        Debug.Log($"[AutoRigUnidades] Active Read/Write en {ruta} para poder hornear la malla skinned.");
    }

    static Material CargarMaterial(string guid)
    {
        string ruta = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(ruta) ? null : AssetDatabase.LoadAssetAtPath<Material>(ruta);
    }
}
