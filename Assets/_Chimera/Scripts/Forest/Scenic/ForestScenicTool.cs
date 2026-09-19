using System.IO;
using UnityEngine;

/// <summary>
/// Энтрипойнты для пайплайна (run_script): карта, камеры, постройка/с нос террейна.
/// Террейн строится командой и сносится — сцена хранит только свет/камеры (как Полигон).
/// Лес, слайс s5.
/// </summary>
public static class ForestScenicTool
{
    const string MapDir = "../Docs/Диаграммы/Лес"; // dataPath=Assets → один уровень вверх

    public static void GenerateMap(string seedText, string resText)
    {
        long seed = 1337;
        int res = 64;
        long.TryParse(seedText, out seed);
        int.TryParse(resText, out res);
        if (res <= 0) res = 64;
        var cfg = ScriptableObject.CreateInstance<WorldGenConfigSO>();
        cfg.seed = seed;
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, MapDir));
        ForestMapWriter.WriteMap(cfg, res, 2f, dir, out _, out _, out _);
        Object.DestroyImmediate(cfg);
        Debug.Log($"[Forest] карта записана в {dir}");
    }

    public static void SetupCameras()
    {
        var sandbox = GameObject.Find("SandboxCam");
        if (sandbox == null)
        {
            sandbox = new GameObject("SandboxCam");
            sandbox.AddComponent<Camera>();
            sandbox.transform.position = new Vector3(0f, 30f, -30f);
            sandbox.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
        }
        var top = GameObject.Find("TopCam");
        if (top == null)
        {
            top = new GameObject("TopCam");
            var cam = top.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 35f;
            top.transform.position = new Vector3(0f, 60f, 0f);
            top.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
        Debug.Log("[Forest] камеры SandboxCam (3/4) + TopCam (топдаун-орто) готовы");
    }

    public static void BuildTerrain(string seedText, string resText, string sizeText)
    {
        long seed = 1337;
        int res = 32;
        float size = 60f;
        long.TryParse(seedText, out seed);
        int.TryParse(resText, out res);
        float.TryParse(sizeText, out size);
        var cfg = ScriptableObject.CreateInstance<WorldGenConfigSO>();
        cfg.seed = seed;
        var go = GameObject.Find("~ForestTerrain");
        if (go == null) go = new GameObject("~ForestTerrain");
        var applier = go.GetComponent<TerrainApplier>();
        if (applier == null) applier = go.AddComponent<TerrainApplier>();
        applier.Configure(cfg, res, size);
        applier.Build();
        PaintRelief(applier);
        Object.DestroyImmediate(cfg);
        Debug.Log($"[Forest] террейн построен (сид {seed}, {res}×{res}, {size} м)");
    }

    /// <summary>
    /// Раскраска террейна: планарный UV + текстура из тех же цветов, что карта
    /// (URP/Lit вертекс-цвета меша не ест — проверено кадром: всё ровно-синее).
    /// Нейтральный URP/Lit сверху, фасетка читается. Материал и текстура живут
    /// с объектом — сносятся WipeTerrain.
    /// </summary>
    static void PaintRelief(TerrainApplier applier)
    {
        var mesh = applier.BuiltMesh;
        float size = Mathf.Max(applier.size, 1e-6f);
        var verts = mesh.vertices;
        var colors = new Color[verts.Length];
        var uvs = new Vector2[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            float t = verts[i].y / Mathf.Max(applier.config.amplitude, 1e-6f) * 0.5f + 0.5f;
            colors[i] = ForestMapBuilder.ReliefColor(t);
            uvs[i] = new Vector2(verts[i].x / size + 0.5f, verts[i].z / size + 0.5f);
        }
        mesh.colors = colors;
        mesh.uv = uvs;

        int texRes = Mathf.Clamp(applier.resolution, 4, 128);
        float step = size / texRes;
        Color[] cells = ForestMapBuilder.BuildColors(applier.config, texRes, step);
        cells = FloraWaterTint.TintCells(cells, applier.config, texRes, step); // s8: мокрое дно
        var tex = new Texture2D(texRes, texRes, TextureFormat.RGB24, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int iz = 0; iz < texRes; iz++)
            for (int ix = 0; ix < texRes; ix++)
                tex.SetPixel(ix, iz, cells[iz * texRes + ix]);
        tex.Apply();

        var renderer = applier.TerrainRoot.GetComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.mainTexture = tex;
        renderer.sharedMaterial = mat;
    }

    public static void WipeTerrain()
    {
        var go = GameObject.Find("~ForestTerrain");
        if (go == null) return;
        var applier = go.GetComponent<TerrainApplier>();
        if (applier != null) applier.Clear();
        var renderer = go.GetComponentInChildren<MeshRenderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            if (renderer.sharedMaterial.mainTexture != null)
                Object.DestroyImmediate(renderer.sharedMaterial.mainTexture);
            Object.DestroyImmediate(renderer.sharedMaterial);
        }
        Object.DestroyImmediate(go);
        Debug.Log("[Forest] террейн снесён");
    }
}
