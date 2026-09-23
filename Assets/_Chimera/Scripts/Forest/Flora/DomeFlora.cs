using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Флора купола (s10i): детерминированный rejection sampling с гейтами
/// (крутизна/вода/пояса) + посадка китом на shared-материалах. Без коллайдеров
/// (навигация — отдельным бейком). Пояса: ели выше/суше, лиственные в низине,
/// папоротники во влаге, скалы на крутизне.
/// </summary>
public static class DomeFlora
{
    public struct Pt
    {
        public Vector3 pos;
        public float scale;
        public float rotY;
    }

    /// <summary>
    /// Скаттер: sample(x,z) отдаёт (высота, годен). Чистая логика — тесты на синтетике.
    /// </summary>
    public static List<Pt> Scatter(long seed, int salt, int count, float diameter,
        float minDist, Func<float, float, (float h, bool ok)> sample)
    {
        var pts = new List<Pt>();
        if (count <= 0 || diameter <= 0f || minDist <= 0f) return pts;
        var grid = new Dictionary<(int, int), List<Vector2>>();
        int attempts = 0;
        int maxAttempts = count * 25 + 200;
        int i = 0;
        while (pts.Count < count && attempts < maxAttempts)
        {
            attempts++;
            float x = (SeededHash.ToFloat01(SeededHash.Hash(seed + salt, i, 1)) * 2f - 1f) * diameter / 2;
            float z = (SeededHash.ToFloat01(SeededHash.Hash(seed + salt, i, 2)) * 2f - 1f) * diameter / 2;
            i++;
            if (new Vector2(x, z).magnitude > diameter / 2) continue;
            var (h, ok) = sample(x, z);
            if (!ok) continue;
            int cx = Mathf.FloorToInt(x / minDist);
            int cz = Mathf.FloorToInt(z / minDist);
            bool clash = false;
            for (int ax = cx - 1; ax <= cx + 1 && !clash; ax++)
                for (int az = cz - 1; az <= cz + 1 && !clash; az++)
                {
                    if (!grid.TryGetValue((ax, az), out var cellPts)) continue;
                    foreach (var q in cellPts)
                    {
                        float dx = q.x - x, dz = q.y - z;
                        if (dx * dx + dz * dz < minDist * minDist) { clash = true; break; }
                    }
                }
            if (clash) continue;
            if (!grid.TryGetValue((cx, cz), out var list)) grid[(cx, cz)] = list = new List<Vector2>();
            list.Add(new Vector2(x, z));
            pts.Add(new Pt
            {
                pos = new Vector3(x, h, z),
                scale = 0.8f + SeededHash.ToFloat01(SeededHash.Hash(seed + salt, i, 3)) * 0.5f,
                rotY = SeededHash.ToFloat01(SeededHash.Hash(seed + salt, i, 4)) * 360f,
            });
        }
        return pts;
    }

    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
    static readonly List<Material> mats = new List<Material>();

    static Mesh Kit(string key, Func<Mesh> build)
    {
        if (!meshes.TryGetValue(key, out var mesh)) meshes[key] = mesh = build();
        return mesh;
    }

    static Material Flat(Color color, bool doubleSided)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        mat.enableInstancing = true;
        if (doubleSided) mat.doubleSidedGI = true;
        mats.Add(mat);
        return mat;
    }

    static GameObject Part(Transform root, Mesh mesh, Material mat, Pt p)
    {
        var go = new GameObject("DomeFlora");
        go.transform.SetParent(root, false);
        go.transform.position = p.pos;
        go.transform.rotation = Quaternion.Euler(0f, p.rotY, 0f);
        go.transform.localScale = Vector3.one * p.scale;
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    static void Tree(Transform root, Pt p, Material trunkMat, Material leafMat,
        float r0, float r1, float h, Func<float, Mesh> crown, float crownR, float crownLift)
    {
        Part(root, Kit($"dtrunk{r0}/{r1}/{h}", () => FloraMeshKit.Trunk(r0, r1, h)), trunkMat, p);
        var c = Part(root, Kit($"dcrown{crownR}", () => crown(crownR)), leafMat, p);
        c.transform.localPosition += new Vector3(0f, crownLift, 0f);
    }

    /// <summary>
    /// Сборка флоры превью: counts для кадра (не лимиты s10d — те для Indirect-рантайма).
    /// </summary>
    public static void Build(GameObject root, DomeGenConfigSO cfg, DomeHydro.State hydro, long seed)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        if (hydro == null) throw new ArgumentNullException(nameof(hydro));
        float d = cfg.mapDiameter;
        float lakeY = hydro.lakeLevel;
        Func<float, float, (float, bool)> ground = (x, z) =>
        {
            float h = DomeHydro.SampleHydro(cfg, hydro, x, z);
            if (h < lakeY) return (h, false);
            if (DomeSlope.SlopeAt(cfg, x, z) > cfg.maxSlopeDegrees) return (h, false);
            float dx = x - hydro.lakeCenter.x, dz = z - hydro.lakeCenter.y;
            if (dx * dx + dz * dz < hydro.lakeRadius * hydro.lakeRadius) return (h, false);
            return (h, true);
        };
        var trunkMat = Flat(new Color(0.35f, 0.25f, 0.18f), false);
        var leafMat = Flat(new Color(0.30f, 0.45f, 0.25f), false);
        var grassMat = Flat(new Color(0.55f, 0.60f, 0.30f), true);
        var rockMatF = Flat(new Color(0.5f, 0.5f, 0.5f), false);
        var trees = Scatter(seed, 111, 700, d, 10f, ground);
        int k = 0;
        foreach (var p in trees)
        {
            var kind = FloraPlacer.TreeKindFor(seed, k, p.pos.y - lakeY);
            k++;
            if (kind == FloraKind.PikeTree) Tree(root.transform, p, trunkMat, leafMat, 0.14f, 0.09f, 2.6f, r => FloraMeshKit.Crown(r), 1.1f, 2.6f);
            else if (kind == FloraKind.UmbrellaTree) Tree(root.transform, p, trunkMat, leafMat, 0.16f, 0.10f, 1.8f, r => FloraMeshKit.Crown(r), 1.5f, 1.8f);
            else if (kind == FloraKind.SpruceTree) Tree(root.transform, p, trunkMat, leafMat, 0.11f, 0.05f, 4.5f, (r) => FloraMeshKit.SpruceCrown(1f, 3f), 1f, 3.5f);
            else Part(root.transform, Kit("dhook", () => FloraMeshKit.Trunk(0.10f, 0.03f, 2.2f)), trunkMat, p);
        }
        var bushMesh = Kit("dbush", () => FloraMeshKit.Bush(0.7f));
        foreach (var p in Scatter(seed, 133, 400, d, 4f, ground))
            Part(root.transform, bushMesh, leafMat, p);
        var blade = Kit("dblade", () => FloraMeshKit.GrassBlade(0.12f, 0.7f));
        foreach (var p in Scatter(seed, 144, 2000, d, 1.5f, ground))
            Part(root.transform, blade, grassMat, p);
        var fern = Kit("dfern", () => FloraMeshKit.Fern(0.8f, 0.5f, 0.08f));
        foreach (var p in Scatter(seed, 155, 250, d, 2f, (x, z) =>
        {
            var (h, ok) = ground(x, z);
            if (!ok || h > lakeY + 1.5f) return (h, false);
            return (h, true);
        }))
            Part(root.transform, fern, grassMat, p);
        foreach (var p in Scatter(seed, 166, 120, d, 6f, (x, z) =>
        {
            float h = DomeHydro.SampleHydro(cfg, hydro, x, z);
            if (h < lakeY || DomeSlope.SlopeAt(cfg, x, z) < 20f) return (h, false);
            return (h, true);
        }))
            Part(root.transform, Kit("dslab", () => FloraMeshKit.Slab(0.9f, 0.5f, 0.7f)), rockMatF, p);
        Debug.Log($"[Forest] флора купола: деревья {trees.Count} + кусты/трава/папоротники/скалы");
    }

    public static void ClearMats()
    {
        foreach (var mat in mats)
            if (mat != null) UnityEngine.Object.DestroyImmediate(mat);
        mats.Clear();
        foreach (var mesh in meshes.Values)
            if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
        meshes.Clear();
    }
}
