using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Плейсер корпусов (s10f-2): детали раскладки → GO (Slab-меш на уникальный размер,
/// Bars — 5 стоек детьми) на 5 shared-материалах палитры s9 + BoxCollider'ы.
/// Материалы живут списком — снос в ClearMats (паттерн плейсеров линии).
/// </summary>
public static class DomeFacilityPlacer
{
    static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
    static readonly List<Material> mats = new List<Material>();

    static Material Flat(Color color)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        mat.enableInstancing = true;
        mat.SetFloat("_Smoothness", 0f);
        mats.Add(mat);
        return mat;
    }

    static Mesh SlabCached(Vector3 size)
    {
        string key = $"slab|{size.x:F2}|{size.y:F2}|{size.z:F2}";
        if (!meshCache.TryGetValue(key, out var mesh))
        {
            mesh = FloraMeshKit.Slab(size.x / 2, size.y, size.z / 2);
            meshCache[key] = mesh;
        }
        return mesh;
    }

    public static GameObject Build(GameObject parent, DomeFacilityLayout.Facility f,
        System.Func<float, float, float> ground)
    {
        if (parent == null) throw new System.ArgumentNullException(nameof(parent));
        if (f == null) throw new System.ArgumentNullException(nameof(f));
        if (ground == null) throw new System.ArgumentNullException(nameof(ground));
        var concrete = Flat(new Color(0.43f, 0.45f, 0.47f));
        var dark = Flat(new Color(0.33f, 0.35f, 0.37f));
        var glass = Flat(new Color(0.62f, 0.71f, 0.77f));
        var rust = Flat(new Color(0.54f, 0.29f, 0.17f));
        var void_ = Flat(new Color(0.14f, 0.15f, 0.16f));
        var root = new GameObject("Facility");
        root.transform.SetParent(parent.transform, false);
        Vector3 lab = new Vector3(f.labCenter.x, 0f, f.labCenter.y);
        foreach (var p in f.parts)
        {
            Material mat = p.mat switch
            {
                DomeFacilityLayout.FacMat.Dark => dark,
                DomeFacilityLayout.FacMat.Glass => glass,
                DomeFacilityLayout.FacMat.Rust => rust,
                DomeFacilityLayout.FacMat.Void => void_,
                _ => concrete,
            };
            if (p.mesh == DomeFacilityLayout.FacMesh.Bars)
                BuildBars(root.transform, p, mat, lab, ground);
            else
            {
                Vector3 wp = lab + p.pos;
                wp.y += ground(wp.x, wp.z);
                BuildBox(root.transform, SlabCached(p.size), mat, wp, p.eulerY, p.collider, p.size);
            }
        }
        return root;
    }

    static void BuildBox(Transform parent, Mesh mesh, Material mat, Vector3 pos, float eulerY,
        bool collider, Vector3 size)
    {
        var go = new GameObject("FacPart");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, eulerY, 0f);
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (collider)
        {
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
        }
    }

    static void BuildBars(Transform parent, DomeFacilityLayout.FacPart p, Material mat,
        Vector3 lab, System.Func<float, float, float> ground)
    {
        var root = new GameObject("FacBars");
        root.transform.SetParent(parent, false);
        Vector3 wp = lab + p.pos;
        wp.y += ground(wp.x, wp.z);
        root.transform.position = wp;
        root.transform.rotation = Quaternion.Euler(0f, p.eulerY, 0f);
        for (int i = 0; i < 5; i++)
        {
            float x = -p.size.x / 2 + p.size.x * i / 4f;
            var go = new GameObject("FacBar");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(x, 0f, 0f);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = SlabCached(new Vector3(0.12f, p.size.y, 0.12f));
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (p.collider)
            {
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(0.12f, p.size.y, 0.12f);
            }
        }
    }

    public static void ClearMats()
    {
        foreach (var mat in mats)
            if (mat != null) Object.DestroyImmediate(mat);
        mats.Clear();
        foreach (var mesh in meshCache.Values)
            if (mesh != null) Object.DestroyImmediate(mesh);
        meshCache.Clear();
    }

    public static List<Material> LiveMats()
    {
        mats.RemoveAll(m => m == null);
        return mats;
    }
}
