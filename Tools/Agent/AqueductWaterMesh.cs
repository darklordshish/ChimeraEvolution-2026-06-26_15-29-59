using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A3 (07.11.2026): водопад AW-F — одно изогнутое полотно + 3–5 тонких лент,
/// пена/всплеск у плёса, каменные полки — фон. Только геометрия воды и материалы
/// водопада; каньон/станция/ясень не трогаются, посадка и профиль не меняются.
/// </summary>
public static class AqueductWaterMesh
{
    const string ModelPrefab = "Assets/_Chimera/Models/Aqueduct/AqueductWaterfall.prefab";
    const string ResourcePrefab = "Assets/_Chimera/Resources/Aqueduct/AqueductWaterfall.prefab";
    const string MeshDir = "Assets/_Chimera/Models/Aqueduct";
    const string MaterialDir = "Assets/_Chimera/Models/Aqueduct/Materials";

    // Местная система AW_WaterFall: верх = 0, низ = -FallH. Размеры — из задания A3.
    const float FallH = 4.04f;
    const float LipZ = -0.78f;      // губа сброса у конца лотка (z=48.0 при объекте z=48.9)
    const float BaseZ = 0.72f;      // удар в плёс (z=49.62)
    const float WidthTop = 3.00f;
    const float WidthBottom = 3.34f;
    const float SheetThickness = 0.18f;
    const int SheetRows = 10;
    const int RibbonRows = 8;

    static float YShift => 0.5f * SheetThickness * BottomNormal().y;

    static float CurveZ(float t) => Mathf.Lerp(LipZ, BaseZ, t * t);
    static float CurveY(float t) => -FallH * t + YShift;

    static Vector3 Tangent(float t) =>
        new Vector3(0f, -FallH, 2f * (BaseZ - LipZ) * t).normalized;

    static Vector3 NormalAt(float t)
    {
        var tg = Tangent(t);
        return new Vector3(0f, tg.z, -tg.y).normalized;
    }

    static Vector3 BottomNormal() => NormalAt(1f);

    static float WidthAt(float t) => Mathf.Lerp(WidthTop, WidthBottom, Mathf.SmoothStep(0f, 1f, t));

    static Vector3 FrontPoint(float t, float x) =>
        SheetPoint(t, x) + NormalAt(t) * (0.5f * SheetThickness);

    static Vector3 BackPoint(float t, float x) =>
        SheetPoint(t, x) - NormalAt(t) * (0.5f * SheetThickness);

    static Vector3 SheetPoint(float t, float x) =>
        new Vector3(x, CurveY(t), CurveZ(t));

    // ------------------------------------------------------------------
    public static string Run()
    {
        AssetDatabase.Refresh();

        var sheet = BuildSheet();
        var ribbons = BuildRibbons();
        var splash = BuildSplash();
        var foamPatch = BuildFoamPatch();

        SaveMesh(sheet, MeshDir + "/AqueductFallSheet.asset");
        SaveMesh(ribbons, MeshDir + "/AqueductFallRibbons.asset");
        SaveMesh(splash, MeshDir + "/AqueductFallSplash.asset");
        SaveMesh(foamPatch, MeshDir + "/AqueductFallFoam.asset");

        var sheetMat = MakeWaterMaterial("AqueductFallWater",
            new Color(0.035f, 0.115f, 0.160f, 0.955f), 0.30f);
        var ribbonMat = MakeWaterMaterial("AqueductFallRibbon",
            new Color(0.100f, 0.255f, 0.335f, 0.80f), 0.45f);
        var foam = MakeOpaqueMaterial("AqueductFallFoam",
            new Color(0.720f, 0.770f, 0.790f, 1f), 0.15f);

        var sb = new StringBuilder();
        sb.AppendLine("mesh sheet: " + Describe(sheet));
        sb.AppendLine("mesh ribbons: " + Describe(ribbons));
        sb.AppendLine("mesh splash: " + Describe(splash));
        sb.AppendLine("mesh foam patch: " + Describe(foamPatch));
        sb.AppendLine("mat sheet: " + AssetDatabase.GetAssetPath(sheetMat));
        sb.AppendLine("mat ribbons: " + AssetDatabase.GetAssetPath(ribbonMat));
        sb.AppendLine("mat foam: " + AssetDatabase.GetAssetPath(foam));
        sb.AppendLine(PatchPrefab(ModelPrefab, sheet, ribbons, splash, foamPatch, sheetMat, ribbonMat, foam));
        sb.AppendLine(PatchPrefab(ResourcePrefab, sheet, ribbons, splash, foamPatch, sheetMat, ribbonMat, foam));

        // Старый монолитный набор пяти «балок» больше не нужен.
        if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/AqueductFallStreams.asset") != null)
        {
            AssetDatabase.DeleteAsset(MeshDir + "/AqueductFallStreams.asset");
            sb.AppendLine("removed orphan AqueductFallStreams.asset");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        // После DeleteAsset+CreateAsset ссылки в prefab на диске новые, но импортёр
        // может держать старую (нулевую) версию — принудительно перечитываем.
        foreach (var p in new[]
                 {
                     MeshDir + "/AqueductFallSheet.asset", MeshDir + "/AqueductFallRibbons.asset",
                     MeshDir + "/AqueductFallSplash.asset", MeshDir + "/AqueductFallFoam.asset",
                     ModelPrefab, ResourcePrefab,
                 })
            AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        sb.AppendLine(Verify());
        return sb.ToString();
    }

    public static string Verify()
    {
        var sb = new StringBuilder();
        foreach (var p in new[]
                 {
                     MeshDir + "/AqueductFallSheet.asset", MeshDir + "/AqueductFallRibbons.asset",
                     MeshDir + "/AqueductFallSplash.asset", MeshDir + "/AqueductFallFoam.asset",
                 })
        {
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(p);
            sb.AppendLine(p + " => " + Describe(m));
        }
        foreach (var p in new[] { ModelPrefab, ResourcePrefab })
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go == null) { sb.AppendLine(p + " PREFAB MISSING"); continue; }
            var fall = Find(go.transform, "AW_WaterFall");
            var mf = fall != null ? fall.GetComponent<MeshFilter>() : null;
            sb.AppendLine(p + " sheetMesh=" +
                (mf != null && mf.sharedMesh != null ? mf.sharedMesh.name + " v=" + mf.sharedMesh.vertexCount : "NULL"));
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // 1. Одно изогнутое полотно
    // ------------------------------------------------------------------
    static Mesh BuildSheet()
    {
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var tris = new List<int>();

        for (int i = 0; i < SheetRows; i++)
        {
            float t0 = i / (float)SheetRows;
            float t1 = (i + 1) / (float)SheetRows;
            float w0 = WidthAt(t0) * 0.5f;
            float w1 = WidthAt(t1) * 0.5f;

            Vector3 fl0 = FrontPoint(t0, -w0), fr0 = FrontPoint(t0, w0);
            Vector3 fl1 = FrontPoint(t1, -w1), fr1 = FrontPoint(t1, w1);
            Vector3 bl0 = BackPoint(t0, -w0), br0 = BackPoint(t0, w0);
            Vector3 bl1 = BackPoint(t1, -w1), br1 = BackPoint(t1, w1);
            Vector3 n0 = NormalAt(t0), n1 = NormalAt(t1);

            // Лицо к воде (вниз по потоку): нормали по кривой, иначе полотно
            // читается лестницей горизонтальных полос, а не потоком.
            AddQuadN(verts, normals, tris, fl1, fr1, fr0, fl0, n1, n1, n0, n0);
            // Изнанка.
            AddQuadN(verts, normals, tris, br1, bl1, bl0, br0, -n1, -n1, -n0, -n0);
            // Боковые кромки.
            AddQuad(verts, normals, tris, bl1, fl1, fl0, bl0);
            AddQuad(verts, normals, tris, fr1, br1, br0, fr0);

            if (i == 0) AddQuad(verts, normals, tris, fl0, fr0, br0, bl0);             // верх
            if (i == SheetRows - 1) AddQuad(verts, normals, tris, bl1, br1, fr1, fl1); // низ
        }

        return MakeMesh("AqueductFallSheet", verts, normals, tris);
    }

    // ------------------------------------------------------------------
    // 2. 4 тонкие ленты поверх полотна (не колонны)
    // ------------------------------------------------------------------
    static Mesh BuildRibbons()
    {
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var tris = new List<int>();

        float[] xs = { -1.12f, -0.44f, 0.32f, 1.02f };
        float[] widths = { 0.15f, 0.11f, 0.19f, 0.13f };
        float[] lengths = { 0.98f, 0.84f, 1.00f, 0.91f };
        float[] offsets = { 0.09f, 0.16f, 0.12f, 0.20f };
        float[] phases = { 0.4f, 2.1f, 4.0f, 5.2f };

        for (int r = 0; r < xs.Length; r++)
        {
            for (int i = 0; i < RibbonRows; i++)
            {
                float t0 = (i / (float)RibbonRows) * lengths[r];
                float t1 = ((i + 1) / (float)RibbonRows) * lengths[r];
                float w = widths[r] * 0.5f;

                Vector3 c0 = RibbonCenter(t0, xs[r], offsets[r], phases[r]);
                Vector3 c1 = RibbonCenter(t1, xs[r], offsets[r], phases[r]);
                Vector3 n0 = NormalAt(t0);
                Vector3 n1 = NormalAt(t1);

                Vector3 a = c1 - Vector3.right * w;
                Vector3 b = c1 + Vector3.right * w;
                Vector3 c = c0 + Vector3.right * w;
                Vector3 d = c0 - Vector3.right * w;
                AddQuadN(verts, normals, tris, a, b, c, d, n1, n1, n0, n0);
                // Изнанка ленты.
                AddQuadN(verts, normals, tris, b, a, d, c, -n1, -n1, -n0, -n0);
            }
        }

        return MakeMesh("AqueductFallRibbons", verts, normals, tris);
    }

    static Vector3 RibbonCenter(float t, float x, float offset, float phase)
    {
        float drift = 0.065f * Mathf.Sin(t * 7f + phase);
        return FrontPoint(t, x + drift) + NormalAt(t) * offset;
    }

    // ------------------------------------------------------------------
    // 3. Всплеск у плёса (один низкий зубец, повторы ставятся объектами)
    // ------------------------------------------------------------------
    static Mesh BuildSplash()
    {
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var tris = new List<int>();

        Vector3[] ring =
        {
            new Vector3(0.55f, 0f, 0.24f),
            new Vector3(-0.42f, 0f, 0.48f),
            new Vector3(-0.62f, 0f, -0.18f),
            new Vector3(0.38f, 0f, -0.44f),
        };
        Vector3 apex = new Vector3(0.02f, 0.62f, 0.03f);

        for (int i = 0; i < ring.Length; i++)
        {
            Vector3 a = ring[i];
            Vector3 b = ring[(i + 1) % ring.Length];
            AddTriangle(verts, normals, tris, a, b, apex);
            AddTriangle(verts, normals, tris, b, a, Vector3.zero);
        }
        // Пена читается светлой, поэтому нормали смотрят вверх, а не в тень.
        for (int i = 0; i < normals.Count; i++) normals[i] = Vector3.up;
        return MakeMesh("AqueductFallSplash", verts, normals, tris);
    }

    /// <summary>Рваное кольцо пены вокруг удара: центр — тёмная вода, края неровные.</summary>
    static Mesh BuildFoamPatch()
    {
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var tris = new List<int>();

        const int n = 20;
        float[] inner =
        {
            0.62f, 0.75f, 0.58f, 0.81f, 0.66f, 0.72f, 0.55f, 0.78f, 0.64f, 0.83f,
            0.61f, 0.74f, 0.57f, 0.80f, 0.68f, 0.71f, 0.59f, 0.77f, 0.63f, 0.79f,
        };
        float[] outer =
        {
            1.22f, 1.46f, 1.12f, 1.54f, 1.28f, 1.60f, 1.08f, 1.40f, 1.52f, 1.16f,
            1.34f, 1.57f, 1.13f, 1.45f, 1.24f, 1.58f, 1.07f, 1.36f, 1.19f, 1.50f,
        };

        for (int i = 0; i < n; i++)
        {
            float a0 = i / (float)n * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)n * Mathf.PI * 2f;
            int j = (i + 1) % n;
            Vector3 i0 = new Vector3(Mathf.Cos(a0) * inner[i], 0.01f, Mathf.Sin(a0) * inner[i]);
            Vector3 i1 = new Vector3(Mathf.Cos(a1) * inner[j], 0.01f, Mathf.Sin(a1) * inner[j]);
            Vector3 o0 = new Vector3(Mathf.Cos(a0) * outer[i], 0f, Mathf.Sin(a0) * outer[i]);
            Vector3 o1 = new Vector3(Mathf.Cos(a1) * outer[j], 0f, Mathf.Sin(a1) * outer[j]);
            AddQuadN(verts, normals, tris, i0, i1, o1, o0,
                Vector3.up, Vector3.up, Vector3.up, Vector3.up);
        }
        return MakeMesh("AqueductFallFoam", verts, normals, tris);
    }

    // ------------------------------------------------------------------
    // 4. Материал: тёмная полупрозрачная вода (URP Lit, Cull Off)
    // ------------------------------------------------------------------
    static Material MakeWaterMaterial(string name, Color color, float smoothness)
    {
        string path = MaterialDir + "/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/AqueductWater.mat").shader;
        var m = existing != null ? existing : new Material(shader);
        m.shader = shader;
        m.SetColor("_BaseColor", color);
        m.SetColor("_Color", color);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", (float)CullMode.Off);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_ReceiveShadows", 1f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)RenderQueue.Transparent;
        if (existing == null) AssetDatabase.CreateAsset(m, path);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material MakeOpaqueMaterial(string name, Color color, float smoothness)
    {
        string path = MaterialDir + "/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/AqueductFoam.mat").shader;
        var m = existing != null ? existing : new Material(shader);
        m.shader = shader;
        m.SetColor("_BaseColor", color);
        m.SetColor("_Color", color);
        m.SetFloat("_Surface", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.One);
        m.SetFloat("_DstBlend", (float)BlendMode.Zero);
        m.SetFloat("_ZWrite", 1f);
        m.SetFloat("_Cull", (float)CullMode.Back);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", smoothness);
        m.SetOverrideTag("RenderType", "Opaque");
        m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = -1;
        if (existing == null) AssetDatabase.CreateAsset(m, path);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------------
    // 5. Правка prefab: полотно + ленты + всплеск + полки-фон
    // ------------------------------------------------------------------
    static string PatchPrefab(string path, Mesh sheet, Mesh ribbons, Mesh splash, Mesh foamPatch,
        Material sheetMat, Material ribbonMat, Material foamMat)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return "MISSING " + path;
        var root = PrefabUtility.LoadPrefabContents(path);
        var sb = new StringBuilder();
        try
        {
            var water = Find(root.transform, "Water");
            var fall = Find(root.transform, "AW_WaterFall");
            var trough = Find(root.transform, "AW_Trough_12m_LOD1");
            if (water == null || fall == null) return "FAIL " + path + ": Water/AW_WaterFall missing";

            // Основное полотно.
            fall.localPosition = new Vector3(0f, 4.45f, 48.9f);
            fall.localRotation = Quaternion.identity;
            fall.localScale = Vector3.one;
            fall.GetComponent<MeshFilter>().sharedMesh = sheet;
            var sheetRenderer = fall.GetComponent<MeshRenderer>();
            sheetRenderer.sharedMaterial = sheetMat;
            sheetRenderer.shadowCastingMode = ShadowCastingMode.Off;
            // Вода не ловит тени полок/быков: иначе полотно читается лестницей полос.
            sheetRenderer.receiveShadows = false;

            // Ленты — отдельный рендерер, чтобы у них был свой тон и они тонули в Water.
            var rib = Find(root.transform, "AW_WaterFallRibbons");
            if (rib == null)
            {
                var go = new GameObject("AW_WaterFallRibbons");
                go.transform.SetParent(water, false);
                go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>();
                rib = go.transform;
            }
            rib.SetParent(water, false);
            rib.localPosition = fall.localPosition;
            rib.localRotation = Quaternion.identity;
            rib.localScale = Vector3.one;
            rib.GetComponent<MeshFilter>().sharedMesh = ribbons;
            var ribRenderer = rib.GetComponent<MeshRenderer>();
            ribRenderer.sharedMaterial = ribbonMat;
            ribRenderer.shadowCastingMode = ShadowCastingMode.Off;
            ribRenderer.receiveShadows = false;

            // Всплеск: пять низких зубцов вокруг точки удара (не выше 0.35 м).
            var splashDefs = new[]
            {
                new SplashDef(-1.55f, -0.30f, 1.10f, 0.42f, 0.85f, 14f),
                new SplashDef(-0.78f, -0.08f, 0.82f, 0.30f, 0.72f, -24f),
                new SplashDef(0.00f, 0.00f, 1.30f, 0.50f, 1.02f, 6f),
                new SplashDef(0.80f, 0.10f, 0.78f, 0.28f, 0.72f, 28f),
                new SplashDef(1.58f, 0.30f, 1.08f, 0.38f, 0.86f, -12f),
            };
            for (int i = 0; i < splashDefs.Length; i++)
            {
                var s = Find(root.transform, "AW_FallSplash_" + i);
                if (s == null) continue;
                var d = splashDefs[i];
                s.localPosition = new Vector3(d.X, 0.48f, 49.55f + d.Z);
                s.localRotation = Quaternion.Euler(0f, d.RotY, 0f);
                s.localScale = new Vector3(d.Sx, d.Sy, d.Sz);
                s.GetComponent<MeshFilter>().sharedMesh = splash;
                var mr = s.GetComponent<MeshRenderer>();
                mr.sharedMaterial = foamMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            // Пена в плёсе — неровный диск вокруг удара, а не белый прямоугольник.
            var poolFoam = Find(root.transform, "AW_Foam");
            if (poolFoam != null)
            {
                poolFoam.localPosition = new Vector3(0f, 0.50f, 49.55f);
                poolFoam.localRotation = Quaternion.identity;
                poolFoam.localScale = Vector3.one;
                poolFoam.GetComponent<MeshFilter>().sharedMesh = foamPatch;
                var mr = poolFoam.GetComponent<MeshRenderer>();
                mr.sharedMaterial = foamMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            // Каменные полки — фон за полотном; пенные полосы на полках гасим,
            // иначе они читаются белыми полосами сквозь воду.
            MoveShelf(root.transform, "AW_FallShelf_1", 3.15f, 4.00f);
            MoveShelf(root.transform, "AW_FallShelf_2", 2.07f, 4.00f);
            MoveShelf(root.transform, "AW_FallShelf_3", 0.99f, 3.70f);
            foreach (var n in new[] { "AW_FallShelfFoam_1", "AW_FallShelfFoam_2", "AW_FallShelfFoam_3" })
            {
                var t = Find(root.transform, n);
                if (t != null) t.gameObject.SetActive(false);
            }

            // LOD0 должен знать про новый рендерер.
            var lod = root.GetComponent<LODGroup>();
            if (lod != null)
            {
                var lods = lod.GetLODs();
                if (lods.Length > 0)
                {
                    var list = lods[0].renderers.Where(r => r != null && r != ribRenderer).ToList();
                    list.Add(ribRenderer);
                    lods[0] = new LOD(lods[0].screenRelativeTransitionHeight, list.ToArray());
                    lod.SetLODs(lods);
                    lod.RecalculateBounds();
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            sb.AppendLine("patched " + path +
                " sheetTris=" + (sheet.triangles.Length / 3) +
                " ribbonTris=" + (ribbons.triangles.Length / 3) +
                " splashTris=" + (splash.triangles.Length / 3) +
                " foamTris=" + (foamPatch.triangles.Length / 3) +
                " lods=" + (lod != null ? lod.GetLODs().Length : 0));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return sb.ToString();
    }

    readonly struct SplashDef
    {
        public readonly float X, Z, Sx, Sy, Sz, RotY;
        public SplashDef(float x, float z, float sx, float sy, float sz, float rotY)
        { X = x; Z = z; Sx = sx; Sy = sy; Sz = sz; RotY = rotY; }
    }

    static void MoveShelf(Transform root, string name, float y, float width)
    {
        var t = Find(root, name);
        if (t == null) return;
        t.localPosition = new Vector3(0f, y, 47.60f);
        var s = t.localScale;
        t.localScale = new Vector3(width, s.y, s.z);
    }

    // ------------------------------------------------------------------
    // helpers
    // ------------------------------------------------------------------
    static Mesh MakeMesh(string name, List<Vector3> verts, List<Vector3> normals, List<int> tris)
    {
        var m = new Mesh { name = name };
        m.SetVertices(verts);
        m.SetNormals(normals);
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    static void SaveMesh(Mesh mesh, string path)
    {
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
    }

    static void AddQuad(List<Vector3> v, List<Vector3> n, List<int> idx,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        var normal = Vector3.Cross(b - a, c - a);
        if (normal.sqrMagnitude < 1e-8f) normal = Vector3.up;
        normal.Normalize();
        int i0 = v.Count;
        v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        n.Add(normal); n.Add(normal); n.Add(normal); n.Add(normal);
        idx.Add(i0); idx.Add(i0 + 1); idx.Add(i0 + 2);
        idx.Add(i0); idx.Add(i0 + 2); idx.Add(i0 + 3);
    }

    static void AddQuadN(List<Vector3> v, List<Vector3> n, List<int> idx,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d,
        Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd)
    {
        int i0 = v.Count;
        v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        n.Add(na); n.Add(nb); n.Add(nc); n.Add(nd);
        idx.Add(i0); idx.Add(i0 + 1); idx.Add(i0 + 2);
        idx.Add(i0); idx.Add(i0 + 2); idx.Add(i0 + 3);
    }

    static void AddTriangle(List<Vector3> v, List<Vector3> n, List<int> idx,
        Vector3 a, Vector3 b, Vector3 c)
    {
        var normal = Vector3.Cross(b - a, c - a);
        if (normal.sqrMagnitude < 1e-8f) normal = Vector3.up;
        normal.Normalize();
        int i0 = v.Count;
        v.Add(a); v.Add(b); v.Add(c);
        n.Add(normal); n.Add(normal); n.Add(normal);
        idx.Add(i0); idx.Add(i0 + 1); idx.Add(i0 + 2);
    }

    static Transform Find(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = Find(root.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }

    static string Describe(Mesh m)
    {
        if (m == null) return "null";
        var b = m.bounds;
        return string.Format(CultureInfo.InvariantCulture,
            "tris={0} verts={1} min=({2:F2};{3:F2};{4:F2}) max=({5:F2};{6:F2};{7:F2})",
            m.triangles.Length / 3, m.vertexCount,
            b.min.x, b.min.y, b.min.z, b.max.x, b.max.y, b.max.z);
    }
}
