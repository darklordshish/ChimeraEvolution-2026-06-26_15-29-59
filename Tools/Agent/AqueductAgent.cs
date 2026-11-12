using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Agent-side import / assembly / measurement for the aqueduct handoff.
/// No world placement, no terrain cut, no scene save, no commits.
/// Builds four prefabs (AW-R / AW-L / AW-RS / AW-F) from the module kit.
/// </summary>
public static class AqueductAgent
{
    public const string AuthorPath = "Assets/_Chimera/Models/Aqueduct/aqueduct_author.fbx";
    public const string WaterPath = "Assets/_Chimera/Models/Aqueduct/aqueduct_water.fbx";
    public const string MaterialDir = "Assets/_Chimera/Models/Aqueduct/Materials";
    public const string PrefabDir = "Assets/_Chimera/Models/Aqueduct";

    const float PierBaseH = 0.60f;
    const float PierCapH = 0.35f;
    const float TroughBottomToRim = 1.80f;
    const float WaterBelowRim = 0.30f;

    static string F(Vector3 v) =>
        string.Format(CultureInfo.InvariantCulture, "({0:F3};{1:F3};{2:F3})", v.x, v.y, v.z);

    static string Node(Transform t)
    {
        var list = new List<string>();
        while (t != null) { list.Insert(0, t.name); t = t.parent; }
        return string.Join("/", list);
    }

    static string Mats(Renderer r)
    {
        if (r == null) return "no-renderer";
        return string.Join(",", r.sharedMaterials.Select(m => m != null ? m.name : "null"));
    }

    static string FullPath(string assetPath)
    {
        if (assetPath.StartsWith("Assets/") || assetPath.StartsWith("Assets\\"))
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        return Path.GetFullPath(Path.Combine(Application.dataPath, assetPath));
    }

    // ------------------------------------------------------------------
    // 1. Import
    // ------------------------------------------------------------------

    public static string Import()
    {
        var sb = new StringBuilder();
        AssetDatabase.Refresh();
        foreach (var path in new[] { AuthorPath, WaterPath })
        {
            if (!File.Exists(FullPath(path)))
            {
                sb.AppendLine(path + " MISSING ON DISK");
                continue;
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) { sb.AppendLine(path + " NO ModelImporter"); continue; }

            mi.globalScale = 1f;
            mi.useFileUnits = false;          // Convert Units off
            mi.useFileScale = false;
            mi.bakeAxisConversion = false;
            mi.isReadable = false;            // Read/Write off
            mi.importNormals = ModelImporterNormals.Calculate;
            mi.normalSmoothingAngle = 0f;     // Normals Calculate, 0
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.importVisibility = false;
            mi.importConstraints = false;
            mi.weldVertices = true;
            mi.meshOptimizationFlags = MeshOptimizationFlags.Everything;
            mi.preserveHierarchy = false;
            SetSerializedBool(mi, "addColliders", false);   // Generate Colliders off
            mi.SaveAndReimport();

            sb.AppendLine(path);
            sb.AppendLine("  globalScale=" + mi.globalScale +
                " useFileUnits=" + mi.useFileUnits +
                " useFileScale=" + mi.useFileScale +
                " bakeAxisConversion=" + mi.bakeAxisConversion +
                " isReadable=" + mi.isReadable +
                " normals=" + mi.importNormals +
                " angle=" + mi.normalSmoothingAngle +
                " materialMode=" + mi.materialImportMode +
                " addColliders=" + SerializedBool(mi, "addColliders") +
                " anim=" + mi.animationType);
        }
        return sb.ToString();
    }

    static bool SerializedBool(Object obj, string field)
    {
        var so = new SerializedObject(obj);
        var p = so.FindProperty(field);
        return p != null ? p.boolValue : false;
    }

    static void SetSerializedBool(Object obj, string field, bool value)
    {
        var so = new SerializedObject(obj);
        var p = so.FindProperty(field);
        if (p == null) return;
        p.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------
    // 2. Inspect imported FBX
    // ------------------------------------------------------------------

    public static string Inspect()
    {
        var sb = new StringBuilder();
        foreach (var path in new[] { AuthorPath, WaterPath })
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            sb.AppendLine("== " + path + " ==");
            sb.AppendLine("root=" + (go != null ? go.name : "NULL"));
            if (go == null) continue;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh;
                var r = mf.GetComponent<MeshRenderer>();
                var b = m != null ? m.bounds : new Bounds();
                sb.AppendLine("  mesh " + mf.gameObject.name +
                    " t=" + (m != null ? m.triangles.Length / 3 : 0) +
                    " bMin=" + F(b.min) + " bMax=" + F(b.max) +
                    " mats=" + Mats(r));
            }
        }
        return sb.ToString();
    }

    public static string Scenes()
    {
        var list = new List<string>();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            list.Add(s.name + " | " + s.path + " | loaded=" + s.isLoaded);
        }
        return string.Join("\n", list);
    }

    // ------------------------------------------------------------------
    // 3. Prefab assembly
    // ------------------------------------------------------------------

    class Spec
    {
        public string Key;        // River, Lake, Reservoir, Waterfall
        public string Code;       // AW-R, AW-L, AW-RS, AW-F
        public int Sections;
        public float TroughStart; // z of first trough section origin
        public float ShaftHeight; // world height of the pier shaft
        public bool Intake;
        public bool Spill;
        public bool SideSpout;
        public bool Abutments;
        public bool Dam;
        public bool Fall;
        public int Steps;
    }

    static Spec SpecOf(string key)
    {
        switch (key)
        {
            case "River":
                return new Spec { Key = key, Code = "AW-R", Sections = 3, TroughStart = 0f, ShaftHeight = 1.6f,
                    Intake = true, Spill = true, SideSpout = true, Abutments = false, Dam = false, Fall = false, Steps = 0 };
            case "Lake":
                return new Spec { Key = key, Code = "AW-L", Sections = 8, TroughStart = 0f, ShaftHeight = 1.9f,
                    Intake = true, Spill = true, SideSpout = false, Abutments = true, Dam = false, Fall = false, Steps = 0 };
            case "Reservoir":
                return new Spec { Key = key, Code = "AW-RS", Sections = 6, TroughStart = 3.0f, ShaftHeight = 4.0f,
                    Intake = false, Spill = true, SideSpout = false, Abutments = false, Dam = true, Fall = true, Steps = 3 };
            case "Waterfall":
                return new Spec { Key = key, Code = "AW-F", Sections = 4, TroughStart = 0f, ShaftHeight = 2.0f,
                    Intake = true, Spill = true, SideSpout = false, Abutments = false, Dam = false, Fall = true, Steps = 0 };
            default:
                throw new System.Exception("Unknown aqueduct type: " + key);
        }
    }

    static void Unpack(GameObject go)
    {
        if (go != null && PrefabUtility.IsPartOfPrefabInstance(go))
            PrefabUtility.UnpackPrefabInstance(go,
                PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
    }

    static void CloseTemp(UnityEngine.SceneManagement.Scene temp,
        UnityEngine.SceneManagement.Scene previous)
    {
        if (previous.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
        if (temp.IsValid() && temp.isLoaded)
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(temp, true);
    }

    static Transform Group(Transform parent, string name)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        return g.transform;
    }

    static GameObject Place(GameObject lib, Transform parent, string module,
        Vector3 pos, float rotY = 0f, float scaleY = 1f)
    {
        var src = lib.transform.Find(module);
        if (src == null) throw new System.Exception("module missing in kit: " + module);
        var go = Object.Instantiate(src.gameObject, parent);
        go.name = module;
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
        go.transform.localScale = new Vector3(1f, scaleY, 1f);
        return go;
    }

    static readonly string[] Lod1Suffix = { "_LOD1" };

    static bool IsLod1(string n) => n.EndsWith("_LOD1");

    static string BaseName(string n)
    {
        foreach (var s in Lod1Suffix)
            if (n.EndsWith(s)) return n.Substring(0, n.Length - s.Length);
        return n;
    }

    static string MatFor(string baseName)
    {
        switch (baseName)
        {
            case "AW_Trough_12m":
            case "AW_Seam":
            case "AW_PierBase":
            case "AW_PierShaft":
            case "AW_PierCap":
            case "AW_PierNose":
            case "AW_AbutmentShaft":
            case "AW_Intake":
            case "AW_SpillLip":
            case "AW_SpillStep":
            case "AW_Dam":
            case "AW_GateTower":
            case "AW_Collider":
                return "AqueductConcrete";
            case "AW_TroughWetBand":
                return "AqueductConcreteDark";
            case "AW_AbutmentPlinth":
            case "AW_Apron":
                return "AqueductRock";
            case "AW_MossPatches":
                return "AqueductMoss";
            case "AW_WaterTrough_12m":
            case "AW_WaterSpill":
            case "AW_WaterFall":
            case "AW_WaterPlunge":
            case "AW_WaterSideSpout":
                return "AqueductWater";
            case "AW_Foam":
                return "AqueductFoam";
            case "AW_IntakeGrate":
            case "AW_Gate":
            case "AW_Ladder":
            case "AW_Railing":
                return "AqueductMetal";
            default:
                return null;
        }
    }

    public static string BuildAll()
    {
        var sb = new StringBuilder();
        foreach (var key in new[] { "River", "Lake", "Reservoir", "Waterfall" })
        {
            sb.AppendLine(BuildOne(key));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public static string BuildOne(string key)
    {
        var spec = SpecOf(key);
        var materials = LoadMaterials();
        if (materials == null) return key + ": materials missing in " + MaterialDir;

        var authorAsset = AssetDatabase.LoadAssetAtPath<GameObject>(AuthorPath);
        var waterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(WaterPath);
        if (authorAsset == null || waterAsset == null) return key + ": cannot load FBX assets";

        var temp = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
            UnityEditor.SceneManagement.NewSceneMode.Additive);
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(temp);

        var lib = Object.Instantiate(authorAsset);
        lib.name = "LIB";
        Unpack(lib);
        var wlib = Object.Instantiate(waterAsset);
        Unpack(wlib);
        foreach (Transform child in wlib.transform.Cast<Transform>().ToArray())
            child.SetParent(lib.transform, false);
        Object.DestroyImmediate(wlib);

        var root = new GameObject("Aqueduct" + spec.Key);
        var rootT = root.transform;

        float rim = PierBaseH + spec.ShaftHeight + PierCapH + TroughBottomToRim;
        float troughEnd = spec.TroughStart + spec.Sections * 12f;

        var body = Group(rootT, "Body");
        var troughs = Group(body, "Troughs");
        var piers = Group(body, "Piers");
        var seams = Group(body, "Seams");
        var ends = Group(body, "Ends");
        var water = Group(rootT, "Water");

        // --- trough sections + one collider shell per section ---
        for (int i = 0; i < spec.Sections; i++)
        {
            float z = spec.TroughStart + i * 12f;
            var sec = Group(troughs, "Section_" + i.ToString("D2"));
            Place(lib, sec, "AW_Trough_12m", new Vector3(0f, rim, z));
            Place(lib, sec, "AW_TroughWetBand", new Vector3(0f, rim, z));
            Place(lib, sec, "AW_MossPatches", new Vector3(0f, rim, z));
            Place(lib, sec, "AW_Trough_12m_LOD1", new Vector3(0f, rim, z));

            var col = Place(lib, sec, "AW_Collider", new Vector3(0f, rim, z));
            var mf = col.GetComponent<MeshFilter>();
            var mr = col.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
            var mc = col.GetComponent<MeshCollider>();
            if (mc == null) mc = col.AddComponent<MeshCollider>();
            mc.sharedMesh = mf != null ? mf.sharedMesh : null;
            mc.convex = false;

            Place(lib, water, "AW_WaterTrough_12m", new Vector3(0f, rim, z));
        }

        // --- piers every 12 m at section starts ---
        for (int i = 0; i <= spec.Sections; i++)
        {
            float z = spec.TroughStart + i * 12f;
            var pg = Group(piers, "Pier_" + i.ToString("D2"));
            Place(lib, pg, "AW_PierBase", new Vector3(0f, 0f, z));
            Place(lib, pg, "AW_PierShaft", new Vector3(0f, PierBaseH, z), 0f, spec.ShaftHeight / 4f);
            Place(lib, pg, "AW_PierCap", new Vector3(0f, PierBaseH + spec.ShaftHeight, z));
            Place(lib, pg, "AW_PierBase_LOD1", new Vector3(0f, 0f, z));
            Place(lib, pg, "AW_PierShaft_LOD1", new Vector3(0f, PierBaseH, z), 0f, spec.ShaftHeight / 4f);
            Place(lib, pg, "AW_PierCap_LOD1", new Vector3(0f, PierBaseH + spec.ShaftHeight, z));
            if (spec.Key == "River")
                Place(lib, pg, "AW_PierNose", new Vector3(0f, 0f, z - 0.9f));
        }

        // --- seams every 24 m, interior only ---
        for (float z = spec.TroughStart + 24f; z < troughEnd - 0.01f; z += 24f)
        {
            var sg = Group(seams, "Seam_" + Mathf.RoundToInt(z));
            Place(lib, sg, "AW_Seam", new Vector3(0f, rim, z));
            Place(lib, sg, "AW_Seam_LOD1", new Vector3(0f, rim, z));
        }

        // --- intake / dam / spill ---
        if (spec.Intake)
        {
            Place(lib, ends, "AW_Intake", new Vector3(0f, rim, spec.TroughStart));
            Place(lib, ends, "AW_IntakeGrate", new Vector3(0f, rim, spec.TroughStart));
            Place(lib, ends, "AW_Intake_LOD1", new Vector3(0f, rim, spec.TroughStart));
        }

        if (spec.Dam)
        {
            Place(lib, ends, "AW_Dam", new Vector3(0f, 0f, 0f));
            Place(lib, ends, "AW_Dam_LOD1", new Vector3(0f, 0f, 0f));
            Place(lib, ends, "AW_GateTower", new Vector3(0f, 6.2f, 0f));
            Place(lib, ends, "AW_GateTower_LOD1", new Vector3(0f, 6.2f, 0f));
            Place(lib, ends, "AW_Gate", new Vector3(0f, 6.2f, -1.65f));
        }

        if (spec.Spill)
        {
            Place(lib, ends, "AW_SpillLip", new Vector3(0f, rim, troughEnd));
            Place(lib, ends, "AW_SpillLip_LOD1", new Vector3(0f, rim, troughEnd));
            Place(lib, water, "AW_WaterSpill", new Vector3(0f, rim, troughEnd));
        }

        if (spec.SideSpout)
            Place(lib, water, "AW_WaterSideSpout", new Vector3(2.35f, rim - WaterBelowRim, troughEnd + 0.3f));

        if (spec.Abutments)
        {
            foreach (float z in new[] { spec.TroughStart, troughEnd })
            {
                Place(lib, ends, "AW_AbutmentPlinth", new Vector3(0f, 0f, z));
                Place(lib, ends, "AW_AbutmentShaft", new Vector3(0f, 0.5f, z));
                Place(lib, ends, "AW_AbutmentPlinth_LOD1", new Vector3(0f, 0f, z));
                Place(lib, ends, "AW_AbutmentShaft_LOD1", new Vector3(0f, 0.5f, z));
            }
        }

        // --- cascade / waterfall / pool ---
        if (spec.Steps > 0)
        {
            for (int i = 0; i < spec.Steps; i++)
            {
                float top = rim - 1.25f - i * 0.8f;
                float z = troughEnd + 1.6f + i * 0.5f;
                Place(lib, ends, "AW_SpillStep", new Vector3(0f, top - 0.8f, z));
                Place(lib, ends, "AW_SpillStep_LOD1", new Vector3(0f, top - 0.8f, z));
            }
        }

        if (spec.Fall)
        {
            // Receiving basin sits on the ground: apron base y=0, top 0.40,
            // plunge top 0.48, waterfall bottom 0.41 (0.07 overlap into the pool).
            float plungeTop = 0.48f;
            float apronBase = 0f;
            float fallTop = rim - WaterBelowRim;
            float fallBottom = plungeTop - 0.07f;
            float fallH = fallTop - fallBottom;
            const float maxFallH = 4.4f * 1.36f;
            if (fallH > maxFallH)
            {
                float lift = fallH - maxFallH;
                apronBase += lift;
                plungeTop += lift;
                fallBottom += lift;
                fallH = maxFallH;
            }
            float fallScaleY = fallH / 4.4f;

            float zPool = troughEnd + 1.5f;
            Place(lib, ends, "AW_Apron", new Vector3(0f, apronBase, zPool));
            Place(lib, ends, "AW_Apron_LOD1", new Vector3(0f, apronBase, zPool));
            Place(lib, water, "AW_WaterFall", new Vector3(0f, fallTop, troughEnd + 0.9f), 0f, fallScaleY);
            Place(lib, water, "AW_WaterPlunge", new Vector3(0f, plungeTop, zPool));
            Place(lib, water, "AW_Foam", new Vector3(0f, plungeTop, zPool));
        }

        // --- human-scale anchors: one ladder and one railing per prefab ---
        Place(lib, ends, "AW_Ladder", new Vector3(2.16f, rim - 1.5f, spec.TroughStart + 8.5f), 90f);
        Place(lib, ends, "AW_Railing", new Vector3(-1.8f, rim, spec.TroughStart + 6f), 90f);

        Object.DestroyImmediate(lib);

        // --- materials ---
        var unknown = new List<string>();
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            string matName = MatFor(BaseName(r.gameObject.name));
            if (matName == null) { unknown.Add(r.gameObject.name); continue; }
            r.sharedMaterial = materials[matName];
        }

        // --- LODGroup: LOD0 all body without _LOD1 + water/foam; LOD1 rebuilt body ---
        var l0 = new List<Renderer>();
        var l1 = new List<Renderer>();
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!r.enabled) continue; // collider shell renderer
            if (IsLod1(r.gameObject.name)) l1.Add(r);
            else l0.Add(r);
        }
        var lod = root.AddComponent<LODGroup>();
        lod.SetLODs(new[]
        {
            new LOD(0.55f, l0.ToArray()),
            new LOD(0.10f, l1.ToArray()),
        });
        lod.RecalculateBounds();

        string path = PrefabDir + "/Aqueduct" + spec.Key + ".prefab";
        var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
        CloseTemp(temp, previous);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var sb = new StringBuilder();
        sb.AppendLine("== " + spec.Key + " (" + spec.Code + ") ==");
        sb.AppendLine("  prefab " + (ok ? "saved: " + path : "SAVE FAILED") +
            " root=" + (saved != null ? saved.name : "null"));
        sb.AppendLine("  sections=" + spec.Sections + " troughEnd=" + troughEnd.ToString("F1", CultureInfo.InvariantCulture) +
            " rim=" + rim.ToString("F3", CultureInfo.InvariantCulture) +
            " shaft=" + spec.ShaftHeight.ToString("F2", CultureInfo.InvariantCulture));
        sb.AppendLine("  LOD0 renderers=" + l0.Count + " LOD1 renderers=" + l1.Count);
        if (unknown.Count > 0) sb.AppendLine("  UNMAPPED MATERIALS: " + string.Join(",", unknown.Distinct()));
        sb.AppendLine(Measure(path));
        return sb.ToString();
    }

    static Dictionary<string, Material> LoadMaterials()
    {
        var names = new[]
        {
            "AqueductConcrete", "AqueductConcreteDark", "AqueductRock", "AqueductMoss",
            "AqueductWater", "AqueductFoam", "AqueductMetal",
        };
        var d = new Dictionary<string, Material>();
        foreach (var n in names)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/" + n + ".mat");
            if (m == null) return null;
            d[n] = m;
        }
        return d;
    }

    // ------------------------------------------------------------------
    // 4. Measurement
    // ------------------------------------------------------------------

    static Bounds BoundsOfRenderers(GameObject root, System.Func<MeshRenderer, bool> filter,
        out int renderers, out int triangles)
    {
        bool first = true;
        var b = new Bounds();
        renderers = 0;
        triangles = 0;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || !filter(mr)) continue;
            var m = mf.sharedMesh;
            if (m == null) continue;
            renderers++;
            triangles += m.triangles.Length / 3;
            var c = m.bounds.center;
            var e = m.bounds.extents;
            for (int i = 0; i < 8; i++)
            {
                var p = mf.transform.TransformPoint(c + new Vector3(
                    (i & 1) == 0 ? -e.x : e.x,
                    (i & 2) == 0 ? -e.y : e.y,
                    (i & 4) == 0 ? -e.z : e.z));
                if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                else b.Encapsulate(p);
            }
        }
        return b;
    }

    static float MeshTopY(GameObject go)
    {
        var mf = go.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return float.NaN;
        var b = mf.sharedMesh.bounds;
        return mf.transform.TransformPoint(b.center + Vector3.up * b.extents.y).y;
    }

    static float MeshMinY(GameObject go)
    {
        var mf = go.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return float.NaN;
        var b = mf.sharedMesh.bounds;
        return mf.transform.TransformPoint(b.center - Vector3.up * b.extents.y).y;
    }

    static float MeshOriginZ(GameObject go) => go.transform.localPosition.z;

    public static string Measure(string path)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null) return "  prefab missing: " + path;
        var sb = new StringBuilder();

        int r0, t0;
        var b0 = BoundsOfRenderers(go, r => r.enabled && !IsLod1(r.gameObject.name), out r0, out t0);
        int r1, t1;
        var b1 = BoundsOfRenderers(go, r => r.enabled && IsLod1(r.gameObject.name), out r1, out t1);

        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "  bbox LOD0 min={0} max={1} size={2}", F(b0.min), F(b0.max), F(b0.size)));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "  bbox LOD1 min={0} max={1} size={2}", F(b1.min), F(b1.max), F(b1.size)));
        sb.AppendLine("  tris LOD0=" + t0 + " (renderers " + r0 + ")" +
            "  tris LOD1=" + t1 + " (renderers " + r1 + ")" +
            "  asset total=" + (t0 + t1));

        var lod = go.GetComponent<LODGroup>();
        if (lod != null)
        {
            var lods = lod.GetLODs();
            sb.AppendLine("  LODGroup=" + string.Join(" ", lods.Select((l, i) =>
                "LOD" + i + "@" + l.screenRelativeTransitionHeight.ToString("F2", CultureInfo.InvariantCulture) +
                "(" + l.renderers.Count(x => x != null) + ")")));
        }
        else sb.AppendLine("  LODGroup=MISSING");

        // colliders
        var colliders = go.GetComponentsInChildren<Collider>(true);
        int onCollider = 0;
        var strays = new List<string>();
        foreach (var c in colliders)
        {
            if (c.gameObject.name == "AW_Collider") onCollider++;
            else strays.Add(c.gameObject.name + ":" + c.GetType().Name);
            var mc = c as MeshCollider;
            if (mc != null && mc.gameObject.name == "AW_Collider")
                sb.AppendLine("  collider on " + mc.gameObject.name + " type=MeshCollider convex=" + mc.convex +
                    " mesh=" + (mc.sharedMesh != null ? mc.sharedMesh.name : "null") +
                    " renderer=" + (mc.GetComponent<MeshRenderer>() != null && mc.GetComponent<MeshRenderer>().enabled ? "ON" : "off"));
        }
        sb.AppendLine("  colliders=" + colliders.Length + " (AW_Collider shells=" + onCollider + ")" +
            (strays.Count > 0 ? "  STRAY: " + string.Join(",", strays) : ""));
        int sections = go.GetComponentsInChildren<MeshFilter>(true).Count(m => m.gameObject.name == "AW_Trough_12m");
        sb.AppendLine("  trough sections=" + sections);

        // water in the trough vs rim
        var troughs = go.GetComponentsInChildren<MeshFilter>(true)
            .Where(m => m.gameObject.name == "AW_Trough_12m")
            .OrderBy(m => MeshOriginZ(m.gameObject)).ToList();
        var waters = go.GetComponentsInChildren<MeshFilter>(true)
            .Where(m => m.gameObject.name == "AW_WaterTrough_12m")
            .OrderBy(m => MeshOriginZ(m.gameObject)).ToList();
        sb.AppendLine("  water troughs=" + waters.Count + " (expected " + sections + ")");
        float minD = float.PositiveInfinity, maxD = float.NegativeInfinity;
        bool okD = troughs.Count == waters.Count && waters.Count > 0;
        for (int i = 0; i < Mathf.Min(troughs.Count, waters.Count); i++)
        {
            float rimTop = MeshTopY(troughs[i].gameObject);
            float wTop = MeshTopY(waters[i].gameObject);
            float floor = MeshMinY(troughs[i].gameObject);
            float d = rimTop - wTop;
            minD = Mathf.Min(minD, d);
            maxD = Mathf.Max(maxD, d);
            if (d < 0.15f || d > 0.35f) okD = false;
            if (float.IsNaN(wTop) || Mathf.Abs(wTop - floor) < 0.0001f) okD = false;
        }
        if (waters.Count > 0)
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  water below rim: min={0:F3} max={1:F3} m  (target 0.150..0.350)  {2}",
                minD, maxD, okD ? "PASS" : "FAIL"));
        else
            sb.AppendLine("  water below rim: NO WATER");

        int foamTris = go.GetComponentsInChildren<MeshFilter>(true)
            .Where(m => m.gameObject.name == "AW_Foam")
            .Sum(m => m.sharedMesh != null ? m.sharedMesh.triangles.Length / 3 : 0);
        if (foamTris > 0) sb.AppendLine("  foam present (tris " + foamTris + ")");

        return sb.ToString();
    }

    public static string MeasureAll()
    {
        var sb = new StringBuilder();
        foreach (var key in new[] { "River", "Lake", "Reservoir", "Waterfall" })
        {
            sb.AppendLine("== " + key + " ==");
            sb.AppendLine(Measure(PrefabDir + "/Aqueduct" + key + ".prefab"));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // 5. Acceptance checks (materials, colliders, LOD, water geometry)
    // ------------------------------------------------------------------

    static readonly string[] WaterModules =
    {
        "AW_WaterTrough_12m", "AW_WaterSpill", "AW_WaterFall", "AW_WaterPlunge", "AW_WaterSideSpout", "AW_Foam",
    };

    static readonly string[] NoColliderModules =
    {
        "AW_WaterTrough_12m", "AW_WaterSpill", "AW_WaterFall", "AW_WaterPlunge", "AW_WaterSideSpout", "AW_Foam",
        "AW_PierBase", "AW_PierShaft", "AW_PierCap", "AW_PierNose", "AW_MossPatches", "AW_TroughWetBand",
        "AW_IntakeGrate", "AW_Ladder", "AW_Railing", "AW_Gate",
    };

    public static string VerifyAll()
    {
        var sb = new StringBuilder();
        foreach (var key in new[] { "River", "Lake", "Reservoir", "Waterfall" })
        {
            sb.AppendLine("== " + key + " ==");
            sb.AppendLine(Verify(PrefabDir + "/Aqueduct" + key + ".prefab"));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public static string Verify(string path)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null) return "  MISSING " + path;
        var sb = new StringBuilder();
        var fails = new List<string>();

        // root identity
        if (go.transform.position != Vector3.zero || go.transform.rotation != Quaternion.identity ||
            go.transform.localScale != Vector3.one)
            fails.Add("root transform is not identity");

        // materials
        var byMat = new Dictionary<string, int>();
        var noMat = new List<string>();
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            var m = r.sharedMaterial;
            if (m == null) { noMat.Add(r.gameObject.name); continue; }
            if (!byMat.ContainsKey(m.name)) byMat[m.name] = 0;
            byMat[m.name]++;
        }
        sb.AppendLine("  materials: " + string.Join(", ", byMat.OrderBy(k => k.Key)
            .Select(k => k.Key + "=" + k.Value)));
        if (noMat.Count > 0) fails.Add("null material on: " + string.Join(",", noMat.Distinct()));
        foreach (var wrong in new[] { "Lit", "Default-Material" })
            if (byMat.ContainsKey(wrong)) fails.Add("default material still assigned: " + wrong);

        // colliders only on AW_Collider, water/piers/etc have none
        var strayColliders = go.GetComponentsInChildren<Collider>(true)
            .Where(c => c.gameObject.name != "AW_Collider")
            .Select(c => c.gameObject.name).Distinct().ToList();
        if (strayColliders.Count > 0) fails.Add("stray colliders: " + string.Join(",", strayColliders));
        var colRenderers = go.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => r.gameObject.name == "AW_Collider" && r.enabled).ToList();
        if (colRenderers.Count > 0) fails.Add("AW_Collider renderer is ON");
        int colliderShells = go.GetComponentsInChildren<MeshCollider>(true).Length;
        int sections = go.GetComponentsInChildren<MeshFilter>(true).Count(m => m.gameObject.name == "AW_Trough_12m");
        if (colliderShells != sections) fails.Add("collider shells " + colliderShells + " != sections " + sections);

        // LODGroup
        var lod = go.GetComponent<LODGroup>();
        if (lod == null) fails.Add("no LODGroup");
        else
        {
            var lods = lod.GetLODs();
            int l1Water = lods.Length > 1
                ? lods[1].renderers.Count(r => r != null && WaterModules.Contains(r.gameObject.name))
                : -1;
            sb.AppendLine("  LODs=" + string.Join(" ", lods.Select((l, i) =>
                "L" + i + "@" + l.screenRelativeTransitionHeight.ToString("F2", CultureInfo.InvariantCulture) +
                "(" + l.renderers.Count(x => x != null) + ")")));
            if (l1Water != 0) fails.Add("water/foam present in LOD1 (" + l1Water + ")");
            if (lods.Any(l => l.renderers.Any(r => r == null))) fails.Add("null renderer in LODGroup");
        }

        // water surface vs rim + lateral containment
        var troughs = go.GetComponentsInChildren<MeshFilter>(true)
            .Where(m => m.gameObject.name == "AW_Trough_12m")
            .OrderBy(m => MeshOriginZ(m.gameObject)).ToList();
        var waters = go.GetComponentsInChildren<MeshFilter>(true)
            .Where(m => m.gameObject.name == "AW_WaterTrough_12m")
            .OrderBy(m => MeshOriginZ(m.gameObject)).ToList();
        if (troughs.Count != waters.Count) fails.Add("trough sections " + troughs.Count + " != water " + waters.Count);
        float dMin = float.PositiveInfinity, dMax = float.NegativeInfinity, wMinX = float.PositiveInfinity,
            wMaxX = float.NegativeInfinity, innerMinX = float.PositiveInfinity, innerMaxX = float.NegativeInfinity;
        for (int i = 0; i < Mathf.Min(troughs.Count, waters.Count); i++)
        {
            float rimTop = MeshTopY(troughs[i].gameObject);
            float wTop = MeshTopY(waters[i].gameObject);
            float d = rimTop - wTop;
            dMin = Mathf.Min(dMin, d);
            dMax = Mathf.Max(dMax, d);
            var wb = waters[i].sharedMesh.bounds;
            wMinX = Mathf.Min(wMinX, wb.min.x);
            wMaxX = Mathf.Max(wMaxX, wb.max.x);
            innerMinX = Mathf.Min(innerMinX, -1.5f);
            innerMaxX = Mathf.Max(innerMaxX, 1.5f);
            if (d < 0.15f || d > 0.35f) fails.Add("water below rim out of 0.15..0.35 at section " + i + ": " + d);
        }
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "  water below rim={0:F3}..{1:F3} m; water X={2:F2}..{3:F2} vs inner walls {4:F2}..{5:F2}",
            dMin, dMax, wMinX, wMaxX, innerMinX, innerMaxX));
        if (wMinX < innerMinX + 0.001f || wMaxX > innerMaxX - 0.001f)
            fails.Add("water spills over inner wall (X)");

        // waterfall reach: fall bottom vs plunge top (overlap 0.07 expected)
        var fall = go.GetComponentsInChildren<MeshFilter>(true)
            .FirstOrDefault(m => m.gameObject.name == "AW_WaterFall");
        var plunge = go.GetComponentsInChildren<MeshFilter>(true)
            .FirstOrDefault(m => m.gameObject.name == "AW_WaterPlunge");
        if (fall != null && plunge != null)
        {
            float fallBottom = MeshMinY(fall.gameObject);
            float plungeTop = MeshTopY(plunge.gameObject);
            float apronTop = MeshTopY(go.GetComponentsInChildren<MeshFilter>(true)
                .First(m => m.gameObject.name == "AW_Apron").gameObject);
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  fall bottom={0:F3} plunge top={1:F3} apron top={2:F3} fall overlap={3:F3}",
                fallBottom, plungeTop, apronTop, plungeTop - fallBottom));
            if (Mathf.Abs((plungeTop - fallBottom) - 0.07f) > 0.005f)
                fails.Add("waterfall/plunge overlap != 0.07");
            if (fallBottom < apronTop - 0.001f) fails.Add("waterfall pierces the apron");
        }

        // water never above rim
        foreach (var w in waters)
        {
            var t = troughs[Mathf.Min(waters.IndexOf(w), troughs.Count - 1)];
            if (MeshTopY(w.gameObject) > MeshTopY(t.gameObject) + 0.0001f)
                fails.Add("water above rim on " + w.gameObject.name);
        }

        sb.AppendLine(fails.Count == 0 ? "  VERDICT: PASS" : "  VERDICT: FAIL - " + string.Join("; ", fails.Distinct()));
        return sb.ToString();
    }
}
