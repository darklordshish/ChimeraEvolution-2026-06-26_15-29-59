using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Превью рельефа павильона (s10a): строит чанковые меши командой, сносит Wipe.
/// Паттерн — как BuildPreview флоры/бункера: корень ~DomePreview, shared-материал,
/// в сцену ничего не сохраняется. Полный диаметр по формуле чанков
/// (D=2000 → 8×8); quads — параметр (превью 16–24, кадры рельефа).
/// </summary>
public static class DomePreview
{
    public static GameObject PreviewRoot { get; private set; }
    static Material previewMat;
    static Material rockMat;
    static Material waterMat;

    static Material Flat(Color color)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        mat.SetFloat("_Smoothness", 0f); // лоу-поли: шершаво, без блика-засвета (кадр s10a)
        return mat;
    }

    public static void BuildPreview(string diameterText, string quadsText)
    {
        float diameter = 2000f;
        float.TryParse(diameterText, out diameter);
        int quads = 16;
        int.TryParse(quadsText, out quads);
        WipePreview();

        var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
        cfg.seed = 1337;
        cfg.randomSeed = false;
        cfg.mapDiameter = Mathf.Clamp(diameter, 100f, 4000f);
        diameter = cfg.mapDiameter;
        int n = DomeGenRules.ChunkCount(cfg);

        previewMat = Flat(new Color(0.32f, 0.44f, 0.26f));
        rockMat = Flat(new Color(0.5f, 0.5f, 0.5f));
        waterMat = Flat(new Color(0.2f, 0.45f, 0.6f));
        PreviewRoot = new GameObject("~DomePreview");
        var hydro = DomeHydro.Build(cfg, 128);
        System.Func<float, float, float> sampler = (x, z) => DomeHydro.SampleHydro(cfg, hydro, x, z);
        for (int cz = 0; cz < n; cz++)
            for (int cx = 0; cx < n; cx++)
            {
                var mesh = DomeChunkMesh.BuildChunkMesh(cfg, cx, cz, quads, sampler);
                Part($"Chunk_{cx}_{cz}", mesh, previewMat);
            }
        Part("RockRing", DomeRockRing.BuildRingMesh(cfg, 256, 6), rockMat);
        Part("Lake", LakeDisc(hydro), waterMat);
        Part("River", RiverRibbon(hydro), waterMat);
        Object.DestroyImmediate(cfg);
        Debug.Log($"[Forest] превью купола построено (сид 1337, Ø{diameter} м, чанков {n}×{n} + кольцо + озеро r={hydro.lakeRadius:F0}м + река {hydro.riverPts.Count} т.)");
    }

    static Mesh LakeDisc(DomeHydro.State s)
    {
        const int segs = 48;
        var verts = new Vector3[segs * 3];
        var center = new Vector3(s.lakeCenter.x, s.lakeLevel, s.lakeCenter.y);
        float r = s.lakeRadius * 1.02f;
        for (int i = 0; i < segs; i++)
        {
            float a0 = i / (float)segs * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)segs * Mathf.PI * 2f;
            verts[i * 3] = center;
            verts[i * 3 + 1] = center + new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r);
            verts[i * 3 + 2] = center + new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r);
        }
        return FacetMesh("LakeDisc", verts);
    }

    static Mesh RiverRibbon(DomeHydro.State s)
    {
        var pts = s.riverPts;
        var verts = new Vector3[(pts.Count - 1) * 6];
        float w = s.riverHalfWidth;
        int v = 0;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector2 d = (pts[i + 1] - pts[i]).normalized;
            Vector2 p = new Vector2(-d.y, d.x) * w;
            Vector3 l0 = new Vector3(pts[i].x - p.x, s.riverBed[i] + 0.35f, pts[i].y - p.y);
            Vector3 r0 = new Vector3(pts[i].x + p.x, s.riverBed[i] + 0.35f, pts[i].y + p.y);
            Vector3 l1 = new Vector3(pts[i + 1].x - p.x, s.riverBed[i + 1] + 0.35f, pts[i + 1].y - p.y);
            Vector3 r1 = new Vector3(pts[i + 1].x + p.x, s.riverBed[i + 1] + 0.35f, pts[i + 1].y + p.y);
            verts[v++] = l0; verts[v++] = r0; verts[v++] = r1;
            verts[v++] = l0; verts[v++] = r1; verts[v++] = l1;
        }
        return FacetMesh("RiverRibbon", verts);
    }

    static Mesh FacetMesh(string name, Vector3[] verts)
    {
        var tris = new int[verts.Length];
        for (int i = 0; i < tris.Length; i++) tris[i] = i;
        var mesh = new Mesh { name = name };
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void Part(string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(PreviewRoot.transform, false);
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
    }

    public static void WipePreview()
    {
        if (PreviewRoot != null)
        {
            Object.DestroyImmediate(PreviewRoot);
            PreviewRoot = null;
        }
        if (previewMat != null)
        {
            Object.DestroyImmediate(previewMat);
            previewMat = null;
        }
        if (rockMat != null)
        {
            Object.DestroyImmediate(rockMat);
            rockMat = null;
        }
        if (waterMat != null)
        {
            Object.DestroyImmediate(waterMat);
            waterMat = null;
        }
    }

    public static GameObject NavRoot { get; private set; }
    public static NavMeshSurface NavSurface { get; private set; }
    static Material navMat;
    static float navAmplitude;

    /// <summary>
    /// NavMesh-доказательство тайлования (s10a): чанки с MeshCollider + поверхность
    /// мелкими тайлами (tileSize=32 вокселя) + синхронный бейк. Моно-бейк на Ø2000
    /// запрещён спекой; здесь малый диаметр проверяет plumbing, полный — витрина s10i.
    /// </summary>
    public static void BuildNavPreview(string diameterText, string quadsText)
    {
        float diameter = 500f;
        float.TryParse(diameterText, out diameter);
        int quads = 8;
        int.TryParse(quadsText, out quads);
        WipeNavPreview();

        var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
        cfg.seed = 1337;
        cfg.randomSeed = false;
        cfg.amplitude = 2f;
        cfg.mapDiameter = Mathf.Clamp(diameter, 100f, 4000f);
        diameter = cfg.mapDiameter;
        int n = DomeGenRules.ChunkCount(cfg);

        navMat = Flat(new Color(0.32f, 0.44f, 0.26f));
        NavRoot = new GameObject("~DomeNav");
        for (int cz = 0; cz < n; cz++)
            for (int cx = 0; cx < n; cx++)
            {
                var mesh = DomeChunkMesh.BuildChunkMesh(cfg, cx, cz, quads);
                var go = new GameObject($"NavChunk_{cx}_{cz}");
                go.transform.SetParent(NavRoot.transform, false);
                var filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = navMat;
                var collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
            }
        NavSurface = NavRoot.AddComponent<NavMeshSurface>();
        NavSurface.collectObjects = CollectObjects.Children;
        NavSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        NavSurface.overrideVoxelSize = true;
        NavSurface.voxelSize = cfg.navVoxelSize;
        NavSurface.overrideTileSize = true;
        NavSurface.tileSize = 32;
        NavSurface.center = Vector3.zero;
        NavSurface.size = new Vector3(diameter + 400f, 200f, diameter + 400f);
        NavSurface.BuildNavMesh();
        navAmplitude = cfg.amplitude;
        Object.DestroyImmediate(cfg);
        Debug.Log($"[Forest] nav-превью запечено (Ø{diameter} м, чанков {n}×{n}, тайл 32)");
    }

    public static bool SampleNav(float x, float z, out Vector3 pos, float maxDistance = 5f)
    {
        var probe = new Vector3(x, navAmplitude + 6f, z);
        NavMeshHit hit;
        bool ok = NavMesh.SamplePosition(probe, out hit, Mathf.Max(maxDistance, navAmplitude + 8f), NavMesh.AllAreas);
        pos = hit.position;
        return ok;
    }

    public static void WipeNavPreview()
    {
        if (NavSurface != null && NavSurface.navMeshData != null)
            NavSurface.RemoveData();
        NavSurface = null;
        if (NavRoot != null)
        {
            Object.DestroyImmediate(NavRoot);
            NavRoot = null;
        }
        if (navMat != null)
        {
            Object.DestroyImmediate(navMat);
            navMat = null;
        }
    }
}
