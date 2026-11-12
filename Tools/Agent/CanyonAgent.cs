using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Agent-side import/prep for the canyon handoff. No world placement here.
/// The prefab is assembled in the active scene and the scene is never saved.
/// </summary>
public static class CanyonAgent
{
    public const string AuthorPath = "Assets/_Chimera/Models/Canyon/canyon_author.fbx";
    public const string WaterPath = "Assets/_Chimera/Models/Canyon/canyon_water.fbx";
    public const string MaterialDir = "Assets/_Chimera/Models/Canyon/Materials";
    public const string PrefabPath = "Assets/_Chimera/Models/Canyon/CanyonAuthor.prefab";

    static string Node(Transform t)
    {
        var list = new List<string>();
        while (t != null)
        {
            list.Insert(0, t.name);
            t = t.parent;
        }
        return string.Join("/", list);
    }

    static string Mats(Renderer r)
    {
        if (r == null) return "no-renderer";
        return string.Join(",", r.sharedMaterials.Select(m => m != null ? m.name : "null"));
    }

    static Bounds RootBounds(GameObject root)
    {
        bool first = true;
        var b = new Bounds();
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var m = mf.sharedMesh;
            if (m == null) continue;
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

    public static string Import()
    {
        var sb = new StringBuilder();
        foreach (var path in new[] { AuthorPath, WaterPath })
        {
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) { sb.AppendLine(path + " NO ModelImporter"); continue; }
            mi.globalScale = 1f;
            mi.useFileUnits = false;          // Convert Units off
            mi.useFileScale = false;
            mi.bakeAxisConversion = false;
            mi.isReadable = false;
            mi.importNormals = ModelImporterNormals.Calculate;
            mi.normalSmoothingAngle = 0f;
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
            mi.SaveAndReimport();
            sb.AppendLine(path + " globalScale=" + mi.globalScale +
                " useFileUnits=" + mi.useFileUnits +
                " useFileScale=" + mi.useFileScale +
                " bakeAxisConversion=" + mi.bakeAxisConversion +
                " isReadable=" + mi.isReadable +
                " normals=" + mi.importNormals +
                " angle=" + mi.normalSmoothingAngle +
                " materialMode=" + mi.materialImportMode +
                " anim=" + mi.animationType);
        }
        sb.AppendLine(Inspect());
        return sb.ToString();
    }

    public static string Inspect()
    {
        var sb = new StringBuilder();
        foreach (var path in new[] { AuthorPath, WaterPath })
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            sb.AppendLine("== " + path + " ==");
            sb.AppendLine("root=" + (go != null ? go.name : "NULL"));
            if (go == null) continue;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                sb.AppendLine("  node " + Node(t) +
                    " pos=" + F(t.localPosition) + " rot=" + F(t.localEulerAngles) +
                    " scale=" + F(t.localScale));
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh;
                var r = mf.GetComponent<MeshRenderer>();
                sb.AppendLine("  mesh " + Node(mf.transform) +
                    " v=" + (m != null ? m.vertexCount : 0) +
                    " t=" + (m != null ? m.triangles.Length / 3 : 0) +
                    " bounds=" + (m != null ? F(m.bounds.size) + "@" + F(m.bounds.center) : "none") +
                    " mats=" + Mats(r));
            }
            var b = RootBounds(go);
            sb.AppendLine("  total bounds min=" + F(b.min) + " max=" + F(b.max) +
                " size=" + F(b.size) + " center=" + F(b.center));
        }
        return sb.ToString();
    }

    static string F(Vector3 v) =>
        string.Format("({0:F3};{1:F3};{2:F3})", v.x, v.y, v.z);

    // ------------------------------------------------------------------
    // Materials + prefab assembly
    // ------------------------------------------------------------------

    static Material EnsureMat(string name, Color color, float smoothness)
    {
        string path = MaterialDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Lit");
            if (sh == null) throw new System.Exception("URP Lit shader not found");
            m = new Material(sh) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        var shader = m.shader;
        if (shader == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Lit");
            m.shader = sh;
        }
        m.SetColor("_BaseColor", color);
        m.SetColor("_Color", color);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", smoothness);
        // Opaque, no alpha tricks; palette only.
        m.SetFloat("_Surface", 0f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        m.SetFloat("_ZWrite", 1f);
        m.renderQueue = -1;
        EditorUtility.SetDirty(m);
        return m;
    }

    public static string Materials()
    {
        var rock = EnsureMat("CanyonRock", new Color(0.50f, 0.50f, 0.50f, 1f), 0.10f);
        var water = EnsureMat("CanyonWater", new Color(0.144f, 0.324f, 0.432f, 1f), 0.55f);
        var foam = EnsureMat("CanyonFoam", new Color(0.78f, 0.82f, 0.84f, 1f), 0.35f);
        AssetDatabase.SaveAssets();
        return "materials: " + rock.name + " / " + water.name + " / " + foam.name +
            " in " + MaterialDir;
    }

    static Renderer FindRenderer(Transform root, string name)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            if (r.gameObject.name == name) return r;
        return null;
    }

    static string CloseUntitled()
    {
        var closed = new List<string>();
        var forest = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByPath(
            "Assets/Scenes/ForestPavilion.unity");
        for (int i = UnityEngine.SceneManagement.SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!s.isLoaded || !string.IsNullOrEmpty(s.path)) continue;
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene() == s && forest.IsValid())
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(forest);
            closed.Add(string.IsNullOrEmpty(s.name) ? "<untitled>" : s.name);
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(s, true);
        }
        return string.Join(",", closed);
    }

    public static string ResetScenes()
    {
        string closed = CloseUntitled();
        var forest = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByPath(
            "Assets/Scenes/ForestPavilion.unity");
        if (forest.IsValid())
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(forest);
        var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        return "closed untitled: " + (closed.Length > 0 ? closed : "none") +
            "; active=" + active.path;
    }

    public static string BuildPrefab()
    {
        CloseUntitled();
        var rock = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/CanyonRock.mat");
        var water = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/CanyonWater.mat");
        var foam = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/CanyonFoam.mat");
        if (rock == null || water == null || foam == null)
            return "materials missing - run CanyonAgent.Materials first";

        var authorAsset = AssetDatabase.LoadAssetAtPath<GameObject>(AuthorPath);
        var waterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(WaterPath);
        if (authorAsset == null || waterAsset == null)
            return "cannot load FBX assets";

        // A temp additive scene keeps the user's ForestPavilion clean and unsaved.
        var temp = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
            UnityEditor.SceneManagement.NewSceneMode.Additive);
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(temp);

        var root = Object.Instantiate(authorAsset);
        root.name = "CanyonAuthor";
        Unpack(root);

        var wroot = Object.Instantiate(waterAsset);
        Unpack(wroot);
        foreach (Transform child in wroot.transform.Cast<Transform>().ToArray())
            child.SetParent(root.transform, false);
        Object.DestroyImmediate(wroot);

        // Materials by object name.
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            string n = r.gameObject.name;
            if (n == "CanyonFoam") r.sharedMaterial = foam;
            else if (n == "CanyonWater" || n.StartsWith("CanyonFallStep")) r.sharedMaterial = water;
            else r.sharedMaterial = rock;
        }

        // One MeshCollider (CanyonCollider, convex=false). No colliders anywhere else.
        var colliderGo = root.transform.Find("CanyonCollider");
        if (colliderGo == null)
        {
            CloseTemp(temp, previous);
            return "CanyonCollider object missing";
        }
        foreach (var c in root.GetComponentsInChildren<Collider>(true))
            if (c.gameObject != colliderGo.gameObject) Object.DestroyImmediate(c);
        var mf = colliderGo.GetComponent<MeshFilter>();
        var mc = colliderGo.GetComponent<MeshCollider>();
        if (mc == null) mc = colliderGo.gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = mf != null ? mf.sharedMesh : null;
        mc.convex = false;
        var cr = colliderGo.GetComponent<MeshRenderer>();
        if (cr != null) cr.enabled = false;

        // LODGroup on the single origin.
        var lod = root.GetComponent<LODGroup>();
        if (lod == null) lod = root.AddComponent<LODGroup>();
        var l0 = new[]
        {
            FindRenderer(root.transform, "CanyonAuthor"),
            FindRenderer(root.transform, "CanyonSkirtW"),
            FindRenderer(root.transform, "CanyonSkirtE"),
        };
        var l1 = new[]
        {
            FindRenderer(root.transform, "CanyonAuthor_LOD1"),
            FindRenderer(root.transform, "CanyonSkirtW_LOD1"),
            FindRenderer(root.transform, "CanyonSkirtE_LOD1"),
        };
        foreach (var r in l0) if (r == null) { CloseTemp(temp, previous); return "LOD0 renderer missing"; }
        foreach (var r in l1) if (r == null) { CloseTemp(temp, previous); return "LOD1 renderer missing"; }
        lod.SetLODs(new[]
        {
            new LOD(0.55f, l0),
            new LOD(0.10f, l1),
        });
        lod.RecalculateBounds();

        var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
        CloseTemp(temp, previous);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "prefab " + (ok ? saved != null ? "saved: " + PrefabPath : "save returned null" : "FAILED") +
            "\n" + InspectPrefab();
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

    public static string InspectPrefab()
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (go == null) return "prefab missing: " + PrefabPath;
        var sb = new StringBuilder();
        sb.AppendLine("prefab=" + PrefabPath);
        var lod = go.GetComponent<LODGroup>();
        sb.AppendLine("LODGroup=" + (lod != null));
        if (lod != null)
        {
            var lods = lod.GetLODs();
            for (int i = 0; i < lods.Length; i++)
                sb.AppendLine("  LOD" + i + " h=" + lods[i].screenRelativeTransitionHeight.ToString("F3") +
                    " renderers=" + string.Join(",", lods[i].renderers.Select(r => r != null ? r.name : "null")));
        }
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            sb.AppendLine("  node " + Node(t) + " pos=" + F(t.localPosition) +
                " rot=" + F(t.localEulerAngles) + " scale=" + F(t.localScale));
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            sb.AppendLine("  renderer " + Node(r.transform) + " enabled=" + r.enabled +
                " mats=" + Mats(r));
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
        {
            var m = c as MeshCollider;
            sb.AppendLine("  collider " + Node(c.transform) + " type=" + c.GetType().Name +
                (m != null ? " convex=" + m.convex + " mesh=" + (m.sharedMesh != null ? m.sharedMesh.name : "null") : ""));
        }
        var b = RootBounds(go);
        sb.AppendLine("  prefab bounds min=" + F(b.min) + " max=" + F(b.max) +
            " size=" + F(b.size) + " center=" + F(b.center));
        // Triangle budget of visible LOD0 stone.
        int tris0 = 0;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            string n = mf.gameObject.name;
            if (n == "CanyonAuthor" || n == "CanyonSkirtW" || n == "CanyonSkirtE")
                tris0 += mf.sharedMesh != null ? mf.sharedMesh.triangles.Length / 3 : 0;
        }
        sb.AppendLine("  LOD0 stone triangles=" + tris0);
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Vertical gap against the live seed-1337 field (no scene save)
    // ------------------------------------------------------------------

    static readonly Vector2[] LocalAxis =
    {
        new Vector2(0.000f, 0.000f),
        new Vector2(-3.085f, 13.279f),
        new Vector2(-5.914f, 26.615f),
        new Vector2(-6.404f, 40.239f),
        new Vector2(-4.233f, 53.698f),
        new Vector2(-1.120f, 66.970f),
        new Vector2(0.540f, 80.502f),
        new Vector2(-0.590f, 94.088f),
        new Vector2(-3.694f, 107.362f),
        new Vector2(-6.494f, 120.704f),
        new Vector2(-6.930f, 134.330f),
        new Vector2(-4.721f, 147.782f),
        new Vector2(-1.614f, 161.056f),
        new Vector2(0.000f, 174.593f),
    };
    const float Wout = 11.758f;

    static Vector2 RotYaw(Vector2 local, float yaw)
    {
        float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
        return new Vector2(local.x * c + local.y * s, -local.x * s + local.y * c);
    }

    static float AxisLength( Vector2[] pts)
    {
        float len = 0f;
        for (int i = 1; i < pts.Length; i++) len += Vector2.Distance(pts[i - 1], pts[i]);
        return len;
    }

    public static string Measure()
    {
        CloseUntitled();
        var src = Object.FindAnyObjectByType<ForestBuilder>();
        if (src == null) return "no ForestBuilder in the open scene";

        var sb = new StringBuilder();
        sb.AppendLine("source builder: seed=" + src.seed + " mapDiameter=" + src.mapDiameter +
            " chunk=" + src.chunkSize + " quads=" + src.terrainQuads +
            " worldVersion=" + src.worldVersion + " treeVersion=" + src.treeVersion +
            " relief=" + src.buildRelief + " thermal=" + src.buildThermalErosion +
            " hydraulic=" + src.buildHydraulicErosion);

        var temp = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
            UnityEditor.SceneManagement.NewSceneMode.Additive);
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(temp);
        var go = new GameObject("CanyonMeasureBuilder");
        var b = go.AddComponent<ForestBuilder>();

        // Copy only what affects SampleGround; keep the rest cheap.
        b.seed = src.seed;
        b.randomSeed = false;
        b.mapDiameter = src.mapDiameter;
        b.chunkSize = src.chunkSize;
        b.terrainQuads = src.terrainQuads;
        b.worldVersion = src.worldVersion;
        b.treeVersion = src.treeVersion;
        b.fingerprintVersion = src.fingerprintVersion;
        b.buildRelief = src.buildRelief;
        b.buildThermalErosion = src.buildThermalErosion;
        b.buildHydraulicErosion = src.buildHydraulicErosion;
        b.erosionVersion = src.erosionVersion;
        b.erosionCacheDir = src.erosionCacheDir;
        b.erosionResolution = src.erosionResolution;
        b.erosionDrops = src.erosionDrops;
        b.erosionSteps = src.erosionSteps;
        b.buildTerrace = src.buildTerrace;
        b.buildCanyon = src.buildCanyon;
        b.buildStationLoop = src.buildStationLoop;
        b.buildStationHill = src.buildStationHill;
        b.buildLabComplex = src.buildLabComplex;
        b.buildLabPlan = src.buildLabPlan;
        b.labPlanVersion = src.labPlanVersion;
        b.labStyle = src.labStyle;
        b.labEntrance = src.labEntrance;
        b.minLabDistance = src.minLabDistance;
        b.buildRavines = src.buildRavines;
        // Speed only: ground does not depend on these.
        b.buildAsh = false;
        b.buildClimate = false;
        b.spawnAtEntry = false;
        b.treeCount = 1;
        b.treeDensityTarget = 0f;
        b.treeMinCount = 0;
        b.treeMaxCount = 0;
        b.undergrowthScale = 0.05f;
        // Must match the source profile: zones/corridor pads feed back into SampleGround.
        b.patchyFlora = src.patchyFlora;
        b.forestThreshold = src.forestThreshold;
        b.forestFraction = src.forestFraction;
        b.speciesPool = new List<SpeciesSO>();

        try
        {
            b.Build();
            if (b.BuiltRoot == null) return sb.AppendLine("world built=false").ToString();
            sb.AppendLine("world built: chunks=" + b.Chunks + " trees=" + b.Trees +
                " zones=" + (b.Zones != null ? b.Zones.Count : 0) +
                " links=" + (b.Links != null ? b.Links.Count : 0) +
                " lab=" + b.HasStation + " scheme=" + b.Scheme + " near=" + b.Near);
            var c = b.Canyon;
            sb.AppendLine("canyon valid=" + c.valid + " policy=" + c.policy +
                " axis=" + (c.axis != null ? c.axis.Count : 0) +
                " halfWidth=" + c.halfWidth.ToString("F3") +
                " rimParam=" + c.rim.ToString("F3") +
                " depth=" + c.depthMouth.ToString("F2") + ".." + c.depthHead.ToString("F2"));
            if (!c.valid || c.axis == null || c.axis.Count < 2)
                return sb.AppendLine("canyon invalid - no rim gap").ToString();

            sb.AppendLine("world mouth=" + F2(c.mouth) + " lip=" + F2(c.lip) +
                " chord=" + Vector2.Distance(c.mouth, c.lip).ToString("F3") +
                " axisLen=" + AxisLength(c.axis.ToArray()).ToString("F3"));
            sb.AppendLine("local chord=174.593 axisLen=177.225 (passport)");

            // Axis alignment: local +Z -> world chord.
            Vector2 d = c.lip - c.mouth;
            float yaw = Mathf.Atan2(d.x, d.y);
            sb.AppendLine("yaw=" + (yaw * Mathf.Rad2Deg).ToString("F3") + " deg");
            if (c.axis.Count == LocalAxis.Length)
            {
                float sumSq = 0f, max = 0f;
                for (int i = 0; i < LocalAxis.Length; i++)
                {
                    Vector2 w = c.mouth + RotYaw(LocalAxis[i], yaw);
                    float e = Vector2.Distance(w, c.axis[i]);
                    sumSq += e * e;
                    if (e > max) max = e;
                }
                sb.AppendLine("axis match vs current Canyon.State: rms=" +
                    Mathf.Sqrt(sumSq / LocalAxis.Length).ToString("F3") +
                    " max=" + max.ToString("F3") + " m");
            }

            // Ground at the rim: local axis +/- Wout, transformed into the world.
            var stations = new[] { 0, 6, 13 };
            var names = new[] { "mouth", "mid", "lip" };
            var rims = new float[stations.Length, 2];
            var gaps = new float[stations.Length, 2];
            var centers = new float[stations.Length];
            for (int k = 0; k < stations.Length; k++)
            {
                int i = stations[k];
                Vector2 p = LocalAxis[i];
                Vector2 fwd = i < LocalAxis.Length - 1
                    ? (LocalAxis[i + 1] - LocalAxis[i]).normalized
                    : (LocalAxis[i] - LocalAxis[i - 1]).normalized;
                Vector2 perp = new Vector2(-fwd.y, fwd.x);
                Vector2 wc = c.mouth + RotYaw(p, yaw);
                centers[k] = b.SampleGround(wc.x, wc.y);
                for (int side = 0; side < 2; side++)
                {
                    float sgn = side == 0 ? -1f : 1f;
                    Vector2 wp = c.mouth + RotYaw(p + perp * (sgn * Wout), yaw);
                    rims[k, side] = b.SampleGround(wp.x, wp.y);
                }
            }
            float originRef = (rims[0, 0] + rims[0, 1]) * 0.5f;
            sb.AppendLine("reference originY = mean mouth rim = " + originRef.ToString("F3"));
            sb.AppendLine("station   axisGround   rimL      rimR      gapL      gapR");
            for (int k = 0; k < stations.Length; k++)
            {
                gaps[k, 0] = rims[k, 0] - originRef;
                gaps[k, 1] = rims[k, 1] - originRef;
                sb.AppendLine(string.Format("{0,-7} {1,10:F3} {2,9:F3} {3,9:F3} {4,9:F3} {5,9:F3}",
                    names[k] + "(" + stations[k] + ")", centers[k], rims[k, 0], rims[k, 1],
                    gaps[k, 0], gaps[k, 1]));
            }

            // Full rim scan, same two edge lines as above.
            float minGap = float.PositiveInfinity, maxGap = float.NegativeInfinity;
            string minAt = "", maxAt = "";
            float s = 0f;
            for (int i = 0; i < LocalAxis.Length; i++)
            {
                if (i > 0) s += Vector2.Distance(LocalAxis[i - 1], LocalAxis[i]);
                Vector2 fwd = i < LocalAxis.Length - 1
                    ? (LocalAxis[i + 1] - LocalAxis[i]).normalized
                    : (LocalAxis[i] - LocalAxis[i - 1]).normalized;
                Vector2 perp = new Vector2(-fwd.y, fwd.x);
                for (int side = 0; side < 2; side++)
                {
                    float sgn = side == 0 ? -1f : 1f;
                    Vector2 wp = c.mouth + RotYaw(LocalAxis[i] + perp * (sgn * Wout), yaw);
                    float g = b.SampleGround(wp.x, wp.y) - originRef;
                    if (g < minGap) { minGap = g; minAt = "s=" + s.ToString("F1") + " " + (side == 0 ? "L" : "R"); }
                    if (g > maxGap) { maxGap = g; maxAt = "s=" + s.ToString("F1") + " " + (side == 0 ? "L" : "R"); }
                }
            }
            sb.AppendLine("rim gap range = " + minGap.ToString("F3") + " .. " + maxGap.ToString("F3") +
                " m (min at " + minAt + ", max at " + maxAt + ")");
            sb.AppendLine("skirt covers 2.7 m down; unresolved if gap > 2.7 m");
            sb.AppendLine("NOTE: current SampleGround still includes procedural DomeCanyon.Apply; " +
                "the FBX terrain cut is forest-warden's step.");
        }
        finally
        {
            if (b != null && b.BuiltRoot != null) b.Clear();
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(temp, true);
        }
        return sb.ToString();
    }

    static string F2(Vector2 v) => string.Format("({0:F3};{1:F3})", v.x, v.y);
}
