using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Agent-side import/prep for the GRADED canyon handoff.
/// No world placement, no terrain cut, no scene save.
/// Produces a separate prefab; the ungraded CanyonAuthor.prefab is left untouched.
/// </summary>
public static class CanyonGradedAgent
{
    public const string AuthorPath = "Assets/_Chimera/Models/Canyon/canyon_author_graded.fbx";
    public const string WaterPath = "Assets/_Chimera/Models/Canyon/canyon_water_graded.fbx";
    public const string MaterialDir = "Assets/_Chimera/Models/Canyon/Materials";
    public const string PrefabPath = "Assets/_Chimera/Models/Canyon/CanyonAuthorGraded.prefab";
    public const string CsvPath = "../Docs/les/canyon-rim-1337.csv";
    public const float OriginY = -7.5925f;   // world Y of local origin for seed 1337
    public const float Wout = 11.758f;       // halfWidth 8.886 + rim 2.872

    static string F(Vector3 v) =>
        string.Format(CultureInfo.InvariantCulture, "({0:F3};{1:F3};{2:F3})", v.x, v.y, v.z);
    static string F2(Vector2 v) =>
        string.Format(CultureInfo.InvariantCulture, "({0:F3};{1:F3})", v.x, v.y);

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
        sb.AppendLine();
        sb.AppendLine(Inspect());
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

    public static string Probe()
    {
        var sb = new StringBuilder();
        foreach (var p in new[] { AuthorPath, WaterPath,
            "Assets/_Chimera/Models/Canyon/canyon_author.fbx",
            "Assets/_Chimera/Models/Canyon/canyon_water.fbx" })
        {
            var mi = AssetImporter.GetAtPath(p) as ModelImporter;
            if (mi == null) { sb.AppendLine(p + " no importer"); continue; }
            var so = new SerializedObject(mi);
            sb.AppendLine(p);
            sb.AppendLine("  fileScale=" + mi.fileScale +
                " useFileScale=" + mi.useFileScale +
                " useFileUnits=" + mi.useFileUnits +
                " globalScale=" + mi.globalScale +
                " bakeAxis=" + mi.bakeAxisConversion);
            foreach (var f in new[] { "fileScale", "useFileScale", "useFileUnits",
                "globalScale", "bakeAxisConversion", "addColliders",
                "normalSmoothingAngle", "normalCalculationMode" })
            {
                var pr = so.FindProperty(f);
                if (pr == null) continue;
                string val = pr.propertyType == SerializedPropertyType.Float ? pr.floatValue.ToString("F4", CultureInfo.InvariantCulture)
                    : pr.propertyType == SerializedPropertyType.Boolean ? pr.boolValue.ToString()
                    : pr.propertyType == SerializedPropertyType.Integer ? pr.intValue.ToString()
                    : "?";
                sb.AppendLine("  " + f + "=" + val);
            }
        }
        return sb.ToString();
    }

    static string FullPath(string assetPath)
    {
        if (assetPath.StartsWith("Assets/") || assetPath.StartsWith("Assets\\"))
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        return Path.GetFullPath(Path.Combine(Application.dataPath, assetPath));
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
                    " boundsC=" + (m != null ? F(m.bounds.center) : "none") +
                    " boundsS=" + (m != null ? F(m.bounds.size) : "none") +
                    " mats=" + Mats(r));
            }
            var b = RootBounds(go);
            sb.AppendLine("  total min=" + F(b.min) + " max=" + F(b.max) +
                " size=" + F(b.size) + " center=" + F(b.center));
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // 3. Materials
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
        if (m.shader == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Lit");
            m.shader = sh;
        }
        m.SetColor("_BaseColor", color);
        m.SetColor("_Color", color);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", smoothness);
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
        return "materials: " + rock.name + " / " + water.name + " / " + foam.name + " in " + MaterialDir;
    }

    // ------------------------------------------------------------------
    // 4. Prefab
    // ------------------------------------------------------------------

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

    public static string BuildPrefab()
    {
        CloseUntitled();
        var rock = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/CanyonRock.mat");
        var water = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/CanyonWater.mat");
        var foam = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/CanyonFoam.mat");
        if (rock == null || water == null || foam == null)
            return "materials missing - run CanyonGradedAgent.Materials first";

        var authorAsset = AssetDatabase.LoadAssetAtPath<GameObject>(AuthorPath);
        var waterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(WaterPath);
        if (authorAsset == null || waterAsset == null)
            return "cannot load graded FBX assets";

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
        var colliderT = root.transform.Find("CanyonCollider");
        if (colliderT == null)
        {
            CloseTemp(temp, previous);
            return "CanyonCollider object missing";
        }
        foreach (var c in root.GetComponentsInChildren<Collider>(true))
            if (c.gameObject != colliderT.gameObject) Object.DestroyImmediate(c);
        var mf = colliderT.GetComponent<MeshFilter>();
        var mc = colliderT.GetComponent<MeshCollider>();
        if (mc == null) mc = colliderT.gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = mf != null ? mf.sharedMesh : null;
        mc.convex = false;
        var cr = colliderT.GetComponent<MeshRenderer>();
        if (cr != null) cr.enabled = false;

        // LODGroup on the single origin: LOD0 / LOD1 by name.
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
        return "prefab " + (ok ? (saved != null ? "saved: " + PrefabPath : "save returned null") : "FAILED") +
            "\n" + InspectPrefab();
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
                sb.AppendLine("  LOD" + i + " h=" + lods[i].screenRelativeTransitionHeight.ToString("F3",
                    CultureInfo.InvariantCulture) +
                    " renderers=" + string.Join(",", lods[i].renderers.Select(r => r != null ? r.name : "null")));
        }
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            sb.AppendLine("  node " + Node(t) + " pos=" + F(t.localPosition) +
                " rot=" + F(t.localEulerAngles) + " scale=" + F(t.localScale));
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            sb.AppendLine("  renderer " + Node(r.transform) + " enabled=" + r.enabled + " mats=" + Mats(r));
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
        {
            var m = c as MeshCollider;
            sb.AppendLine("  collider " + Node(c.transform) + " type=" + c.GetType().Name +
                (m != null ? " convex=" + m.convex + " mesh=" + (m.sharedMesh != null ? m.sharedMesh.name : "null") : ""));
        }
        var b = RootBounds(go);
        sb.AppendLine("  prefab bounds min=" + F(b.min) + " max=" + F(b.max) +
            " size=" + F(b.size) + " center=" + F(b.center));
        int tris0 = 0, tris1 = 0;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            string n = mf.gameObject.name;
            int t = mf.sharedMesh != null ? mf.sharedMesh.triangles.Length / 3 : 0;
            if (n == "CanyonAuthor" || n == "CanyonSkirtW" || n == "CanyonSkirtE") tris0 += t;
            if (n == "CanyonAuthor_LOD1" || n == "CanyonSkirtW_LOD1" || n == "CanyonSkirtE_LOD1") tris1 += t;
        }
        sb.AppendLine("  LOD0 stone triangles=" + tris0 + " LOD1 stone triangles=" + tris1);
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // 5. Rim / water verification against the passport
    // ------------------------------------------------------------------

    struct Row
    {
        public int i;
        public float X, Z;
        public float rimL, rimR;   // local
        public float ground;       // world, CSV control column
    }

    static List<Row> ReadCsv()
    {
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, CsvPath));
        var rows = new List<Row>();
        if (!File.Exists(path)) return rows;

        string[] lines = File.ReadAllLines(path);
        if (lines.Length < 2) return rows;

        var pts = new List<Vector2>();
        var rimL = new List<float>();
        var rimR = new List<float>();
        var ground = new List<float>();
        int idxX = -1, idxZ = -1, idxRL = -1, idxRR = -1, idxGC = -1;
        var head = lines[0].Split(',');
        for (int c = 0; c < head.Length; c++)
        {
            string h = head[c].Trim();
            if (h == "world_x") idxX = c;
            else if (h == "world_z") idxZ = c;
            else if (h == "rimLeftY") idxRL = c;
            else if (h == "rimRightY") idxRR = c;
            else if (h == "groundCenter") idxGC = c;
        }
        for (int l = 1; l < lines.Length; l++)
        {
            if (string.IsNullOrWhiteSpace(lines[l])) continue;
            var c = lines[l].Split(',');
            pts.Add(new Vector2(
                float.Parse(c[idxX], CultureInfo.InvariantCulture),
                float.Parse(c[idxZ], CultureInfo.InvariantCulture)));
            rimL.Add(float.Parse(c[idxRL], CultureInfo.InvariantCulture));
            rimR.Add(float.Parse(c[idxRR], CultureInfo.InvariantCulture));
            ground.Add(idxGC >= 0 ? float.Parse(c[idxGC], CultureInfo.InvariantCulture) : 0f);
        }

        Vector2 mouth = pts[0];
        Vector2 chord = pts[pts.Count - 1] - mouth;
        Vector2 fwd = chord.normalized;
        Vector2 right = new Vector2(fwd.y, -fwd.x);

        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 rel = pts[i] - mouth;
            rows.Add(new Row
            {
                i = i,
                X = Vector2.Dot(rel, right),
                Z = Vector2.Dot(rel, fwd),
                rimL = rimL[i] + 7.5925f,
                rimR = rimR[i] + 7.5925f,
                ground = ground[i],
            });
        }
        return rows;
    }

    static Mesh FindMesh(GameObject root, string name)
    {
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            if (mf.gameObject.name == name) return mf.sharedMesh;
        return null;
    }

    // Transform mesh vertices into the FBX-root frame with the node scale removed
    // (rotation kept). Used to separate "export transform defect" from "form defect".
    static Vector3[] WorldNoScale(Transform t, Mesh mesh)
    {
        var m = Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
        var src = mesh.vertices;
        var dst = new Vector3[src.Length];
        for (int i = 0; i < src.Length; i++) dst[i] = m.MultiplyPoint3x4(src[i]);
        return dst;
    }

    static Bounds BoundsOf(Vector3[] pts)
    {
        var b = new Bounds(pts[0], Vector3.zero);
        for (int i = 1; i < pts.Length; i++) b.Encapsulate(pts[i]);
        return b;
    }

    public static string Diagnose()
    {
        var sb = new StringBuilder();
        var author = AssetDatabase.LoadAssetAtPath<GameObject>(AuthorPath);
        sb.AppendLine("== graded author: node transforms ==");
        foreach (var t in author.GetComponentsInChildren<Transform>(true))
        {
            if (t == author.transform) continue;
            sb.AppendLine(" " + t.name +
                " scale=" + F(t.localScale) +
                " quat=(" + t.localRotation.x.ToString("F6", CultureInfo.InvariantCulture) +
                ";" + t.localRotation.y.ToString("F6", CultureInfo.InvariantCulture) +
                ";" + t.localRotation.z.ToString("F6", CultureInfo.InvariantCulture) +
                ";" + t.localRotation.w.ToString("F6", CultureInfo.InvariantCulture) + ")" +
                " euler=" + F(t.localEulerAngles));
        }

        sb.AppendLine();
        sb.AppendLine("== graded author: bounds with node SCALE REMOVED (rotation kept) ==");
        foreach (var mf in author.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var pts = WorldNoScale(mf.transform, mf.sharedMesh);
            var b = BoundsOf(pts);
            sb.AppendLine(" " + mf.gameObject.name +
                " min=" + F(b.min) + " max=" + F(b.max) + " size=" + F(b.size));
        }

        sb.AppendLine();
        sb.AppendLine("== graded water: bounds with node SCALE REMOVED ==");
        var water = AssetDatabase.LoadAssetAtPath<GameObject>(WaterPath);
        foreach (var mf in water.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var pts = WorldNoScale(mf.transform, mf.sharedMesh);
            var b = BoundsOf(pts);
            sb.AppendLine(" " + mf.gameObject.name +
                " min=" + F(b.min) + " max=" + F(b.max) + " size=" + F(b.size));
        }

        sb.AppendLine();
        sb.AppendLine(CheckRimWorld(false));
        sb.AppendLine();
        sb.AppendLine(CheckRimWorld(false));
        sb.AppendLine();
        sb.AppendLine(CheckRimWorld(true));
        sb.AppendLine();
        sb.AppendLine(CheckWaterWorld());
        return sb.ToString();
    }

    static string CheckRimWorld(bool mirrorX)
    {
        var rows = ReadCsv();
        if (rows.Count != 20) return "CSV rows=" + rows.Count;
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AuthorPath);
        var mf = asset.GetComponentsInChildren<MeshFilter>(true)
            .FirstOrDefault(x => x.gameObject.name == "CanyonAuthor");
        if (mf == null || mf.sharedMesh == null) return "CanyonAuthor mesh not found";
        var verts = WorldNoScale(mf.transform, mf.sharedMesh);

        var sb = new StringBuilder();
        sb.AppendLine("RIM CHECK in FBX-root frame (node scale removed, rotation kept" +
            (mirrorX ? ", X un-mirrored" : ", raw X") + "), 20 CSV anchors:");
        sb.AppendLine(" i    side  dist    meshY    expY     dev     swappedDev");
        float maxDirect = 0f, maxSwapped = 0f;
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            Vector2 tangent;
            if (i == 0) tangent = new Vector2(rows[1].X - rows[0].X, rows[1].Z - rows[0].Z);
            else if (i == rows.Count - 1) tangent = new Vector2(rows[i].X - rows[i - 1].X, rows[i].Z - rows[i - 1].Z);
            else tangent = new Vector2(rows[i + 1].X - rows[i - 1].X, rows[i + 1].Z - rows[i - 1].Z);
            tangent.Normalize();
            Vector2 leftPerp = new Vector2(-tangent.y, tangent.x);
            Vector2 axis = new Vector2(r.X, r.Z);
            for (int side = 0; side < 2; side++)
            {
                Vector2 target = side == 0 ? axis + leftPerp * Wout : axis - leftPerp * Wout;
                float best = float.PositiveInfinity;
                Vector3 bv = Vector3.zero;
                for (int v = 0; v < verts.Length; v++)
                {
                    float dx = (mirrorX ? -verts[v].x : verts[v].x) - target.x;
                    float dz = verts[v].z - target.y;
                    float d2 = dx * dx + dz * dz;
                    if (d2 < best) { best = d2; bv = verts[v]; }
                }
                float expected = side == 0 ? r.rimL : r.rimR;
                float expectedSwapped = side == 0 ? r.rimR : r.rimL;
                float dev = bv.y - expected;
                float devSwap = bv.y - expectedSwapped;
                if (Mathf.Abs(dev) > maxDirect) maxDirect = Mathf.Abs(dev);
                if (Mathf.Abs(devSwap) > maxSwapped) maxSwapped = Mathf.Abs(devSwap);
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0,2}   {1,-4}  {2,6:F3}  {3,7:F3}  {4,7:F3}  {5,7:F3}  {6,8:F3}",
                    i, side == 0 ? "L" : "R", Mathf.Sqrt(best), bv.y, expected, dev, devSwap));
            }
        }
        sb.AppendLine("max |dev| direct  = " + maxDirect.ToString("F4", CultureInfo.InvariantCulture));
        sb.AppendLine("max |dev| swapped = " + maxSwapped.ToString("F4", CultureInfo.InvariantCulture));
        sb.AppendLine(maxDirect <= 0.001f
            ? "VERDICT: PASS - left=rimLeftY, right=rimRightY, 0.000 m"
            : maxSwapped <= 0.001f
                ? "VERDICT: SIDES SWAPPED - mesh left matches rimRightY; form is mirrored in X relative to CSV"
                : "VERDICT: FAIL - matches neither convention");
        return sb.ToString();
    }

    static string CheckWaterWorld()
    {
        var rows = ReadCsv();
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(WaterPath);
        var mf = asset.GetComponentsInChildren<MeshFilter>(true)
            .FirstOrDefault(x => x.gameObject.name == "CanyonWater");
        if (mf == null || mf.sharedMesh == null) return "CanyonWater mesh not found";
        var pts = WorldNoScale(mf.transform, mf.sharedMesh);
        var b = BoundsOf(pts);
        float groundLipWorld = rows.Count > 0 ? rows[rows.Count - 1].ground : 0f;
        float topWorld = b.max.y + OriginY;
        float bottomWorld = b.min.y + OriginY;
        float floorLipLocal = -1.581f;
        var sb = new StringBuilder();
        sb.AppendLine("WATER CHECK in FBX-root frame (node scale removed):");
        sb.AppendLine(" top local=" + b.max.y.ToString("F4", CultureInfo.InvariantCulture) +
            " world=" + topWorld.ToString("F4", CultureInfo.InvariantCulture) +
            " delta over lip groundCenter(" + groundLipWorld.ToString("F3", CultureInfo.InvariantCulture) + ")=" +
            (topWorld - groundLipWorld).ToString("F4", CultureInfo.InvariantCulture));
        sb.AppendLine(" bottom local=" + b.min.y.ToString("F4", CultureInfo.InvariantCulture) +
            " world=" + bottomWorld.ToString("F4", CultureInfo.InvariantCulture) +
            " delta over mesh floor=" + (b.min.y - floorLipLocal).ToString("F4", CultureInfo.InvariantCulture));
        sb.AppendLine(" height=" + (b.max.y - b.min.y).ToString("F4", CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    public static string CheckRim()
    {
        var rows = ReadCsv();
        if (rows.Count != 20) return "CSV rows=" + rows.Count + " (expected 20): " + CsvPath;

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AuthorPath);
        if (asset == null) return "author FBX missing";
        var mesh = FindMesh(asset, "CanyonAuthor");
        if (mesh == null) return "CanyonAuthor mesh not found in graded FBX";
        var verts = mesh.vertices;

        var sb = new StringBuilder();
        sb.AppendLine("rim check: mesh CanyonAuthor (" + verts.Length + " verts) vs CSV 20 anchors");
        sb.AppendLine("local expected = CSV rim Y + 7.5925 (origin world Y -7.5925)");
        sb.AppendLine(" i    side  targetXZ            dist    meshY    expY     dev    swappedDev");

        float maxDirect = 0f, maxSwapped = 0f;
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            Vector2 tangent;
            if (i == 0) tangent = new Vector2(rows[1].X - rows[0].X, rows[1].Z - rows[0].Z);
            else if (i == rows.Count - 1) tangent = new Vector2(rows[i].X - rows[i - 1].X, rows[i].Z - rows[i - 1].Z);
            else tangent = new Vector2(rows[i + 1].X - rows[i - 1].X, rows[i + 1].Z - rows[i - 1].Z);
            tangent.Normalize();
            Vector2 leftPerp = new Vector2(-tangent.y, tangent.x);
            Vector2 axis = new Vector2(r.X, r.Z);

            for (int side = 0; side < 2; side++)
            {
                Vector2 target = side == 0 ? axis + leftPerp * Wout : axis - leftPerp * Wout;
                float best = float.PositiveInfinity;
                Vector3 bv = Vector3.zero;
                for (int v = 0; v < verts.Length; v++)
                {
                    float dx = verts[v].x - target.x;
                    float dz = verts[v].z - target.y;
                    float d2 = dx * dx + dz * dz;
                    if (d2 < best) { best = d2; bv = verts[v]; }
                }
                float dist = Mathf.Sqrt(best);
                float expected = side == 0 ? r.rimL : r.rimR;
                float expectedSwapped = side == 0 ? r.rimR : r.rimL;
                float dev = bv.y - expected;
                float devSwap = bv.y - expectedSwapped;
                if (Mathf.Abs(dev) > maxDirect) maxDirect = Mathf.Abs(dev);
                if (Mathf.Abs(devSwap) > maxSwapped) maxSwapped = Mathf.Abs(devSwap);
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0,2}   {1,-4}  {2,-18} {3,6:F3}  {4,7:F3}  {5,7:F3}  {6,7:F3}  {7,8:F3}",
                    i, side == 0 ? "L" : "R", F2(target), dist, bv.y, expected, dev, devSwap));
            }
        }
        sb.AppendLine();
        sb.AppendLine("max |dev| direct  = " + maxDirect.ToString("F4", CultureInfo.InvariantCulture) + " m");
        sb.AppendLine("max |dev| swapped = " + maxSwapped.ToString("F4", CultureInfo.InvariantCulture) + " m");
        sb.AppendLine(maxDirect <= 0.001f
            ? "VERDICT: PASS - left=rimLeftY, right=rimRightY, deviation 0.000 m"
            : maxSwapped <= 0.001f
                ? "VERDICT: SIDES SWAPPED - mesh left matches rimRightY / vice versa; report to model line"
                : "VERDICT: FAIL - does not match either CSV convention");
        return sb.ToString();
    }

    public static string CheckWater()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(WaterPath);
        if (asset == null) return "water FBX missing";
        var sb = new StringBuilder();
        sb.AppendLine("water check: canyon_water_graded.fbx");
        sb.AppendLine(" name                Ymin     Ymax     Zmin     Zmax    tris");
        foreach (var mf in asset.GetComponentsInChildren<MeshFilter>(true))
        {
            var m = mf.sharedMesh;
            if (m == null) continue;
            var b = m.bounds;
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                " {0,-18} {1,8:F3} {2,8:F3} {3,8:F3} {4,8:F3} {5,6}",
                mf.gameObject.name, b.min.y, b.max.y, b.min.z, b.max.z, m.triangles.Length / 3));
        }

        var rows = ReadCsv();
        float groundLip = rows.Count > 0 ? (rows[rows.Count - 1].rimL + rows[rows.Count - 1].rimR) * 0.5f : 0f;
        var waterMesh = FindMesh(asset, "CanyonWater");
        float topLocal = waterMesh != null ? waterMesh.bounds.max.y : float.NaN;
        float topWorld = topLocal + OriginY;
        float topDelta = topWorld - groundLip;

        // floor at lip from passport: local -1.581 (world -9.173); water bottom local -1.3805
        float bottomLocal = waterMesh != null ? waterMesh.bounds.min.y : float.NaN;
        float bottomWorld = bottomLocal + OriginY;
        float floorLipLocal = -1.581f;
        float bottomDelta = (bottomLocal - floorLipLocal);

        sb.AppendLine();
        sb.AppendLine("CanyonWater top local = " + topLocal.ToString("F4", CultureInfo.InvariantCulture) +
            "  world = " + topWorld.ToString("F4", CultureInfo.InvariantCulture) +
            "  delta over lip ground (" + groundLip.ToString("F3", CultureInfo.InvariantCulture) + ") = " +
            topDelta.ToString("F4", CultureInfo.InvariantCulture) + " m  (passport +0.20)");
        sb.AppendLine("CanyonWater bottom local = " + bottomLocal.ToString("F4", CultureInfo.InvariantCulture) +
            "  world = " + bottomWorld.ToString("F4", CultureInfo.InvariantCulture) +
            "  delta over mesh floor (" + floorLipLocal.ToString("F3", CultureInfo.InvariantCulture) + ") = " +
            bottomDelta.ToString("F4", CultureInfo.InvariantCulture) + " m  (passport +0.20)");
        sb.AppendLine("water height = " + (topLocal - bottomLocal).ToString("F4", CultureInfo.InvariantCulture) +
            " m  (passport 14.102)");
        sb.AppendLine(Mathf.Abs(topDelta - 0.20f) <= 0.001f &&
                      Mathf.Abs(bottomDelta - 0.20f) <= 0.001f
            ? "VERDICT: PASS - top and bottom both +0.20 m"
            : "VERDICT: CHECK - offsets differ from +0.20 m");
        return sb.ToString();
    }

    public static string CheckAll()
    {
        var sb = new StringBuilder();
        sb.AppendLine(CheckRim());
        sb.AppendLine();
        sb.AppendLine(CheckWater());
        return sb.ToString();
    }
}
