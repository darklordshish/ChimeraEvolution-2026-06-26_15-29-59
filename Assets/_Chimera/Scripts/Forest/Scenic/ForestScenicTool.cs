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

    public static void SetupCameras(string sizeText)
    {
        float size = 35f;
        float.TryParse(sizeText, out size);
        if (size <= 0f) size = 35f;
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
            top.AddComponent<Camera>();
            top.transform.position = new Vector3(0f, 60f, 0f);
            top.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
        var topCam = top.GetComponent<Camera>();
        topCam.orthographic = true;
        topCam.orthographicSize = size;
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

    /// <summary>
    /// Превью рельефа павильона чанками (s10a): диаметр + quads на чанк.
    /// Снос — WipeDomePreview. В сцену ничего не сохраняется.
    /// </summary>
    public static void BuildDomePreview(string diameterText, string quadsText)
    {
        DomePreview.BuildPreview(diameterText, quadsText);
    }

    public static void WipeDomePreview()
    {
        DomePreview.WipePreview();
        Debug.Log("[Forest] превью купола снесено");
    }

    /// <summary>
    /// Покраска витрины (Showcase.Build не красит — аппликатор без материала;
    /// без этого террейн маджента). s10d.
    /// </summary>
    public static void PaintPreview()
    {
        var go = GameObject.Find("~ForestTerrain");
        if (go == null) throw new System.InvalidOperationException("сначала Showcase.Build (нет ~ForestTerrain)");
        var applier = go.GetComponent<TerrainApplier>();
        if (applier == null) throw new System.InvalidOperationException("на ~ForestTerrain нет TerrainApplier");
        PaintRelief(applier);
        Debug.Log("[Forest] витрина покрашена");
    }

    /// <summary>
    /// Крупный кадр реки (s10b-3): камера у середины русла, смотрит вниз по течению.
    /// Требует построенного превью (DomePreview.Hydro). Туман гасится, как в FrameDome.
    /// </summary>
    public static void FrameRiver()
    {
        if (!DomePreview.Reattach())
            throw new System.InvalidOperationException("сначала BuildDomePreview (нет Hydro)");
        var s = DomePreview.Hydro;
        int mid = s.riverPts.Count / 2;
        Vector2 p = s.riverPts[mid];
        Vector2 q = s.riverPts[Mathf.Min(mid + 1, s.riverPts.Count - 1)];
        Vector2 d = q - p;
        d = d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.right;
        Vector3 target = new Vector3(p.x, s.riverBed[mid], p.y);
        RenderSettings.fog = false;
        SetupCameras("35");
        var sandbox = GameObject.Find("SandboxCam");
        sandbox.transform.position = target + new Vector3(-d.x * 90f, 55f, -d.y * 90f);
        sandbox.transform.LookAt(target);
        sandbox.GetComponent<Camera>().farClipPlane = 6000f;
        Debug.Log($"[Forest] кадр реки настроен (точка {mid}/{s.riverPts.Count}, туман выкл)");
    }

    /// <summary>
    /// Крупный кадр устья (s10e-2): камера перед первым порталом, смотрит в нишу.
    /// Требует построенного превью (устья — в нём). Туман гасится.
    /// </summary>
    public static void FrameMouth()
    {
        if (!DomePreview.Reattach() || DomePreview.Mouths == null || DomePreview.Mouths.Count == 0)
            throw new System.InvalidOperationException("сначала BuildDomePreview с устьями");
        var m = DomePreview.Mouths[0];
        Vector2 fwd = new Vector2(Mathf.Cos(m.facing), Mathf.Sin(m.facing));
        var mouthObj = GameObject.Find("Mouth");
        Vector3 target = mouthObj != null
            ? mouthObj.transform.position + new Vector3(0f, 2f, 0f)
            : new Vector3(m.pos.x, 2f, m.pos.y);
        RenderSettings.fog = false;
        SetupCameras("35");
        var sandbox = GameObject.Find("SandboxCam");
        sandbox.transform.position = new Vector3(m.pos.x - fwd.x * 16f, 5.5f, m.pos.y - fwd.y * 16f);
        sandbox.transform.LookAt(target + new Vector3(0f, 1.5f, 0f));
        sandbox.GetComponent<Camera>().farClipPlane = 6000f;
        Debug.Log($"[Forest] кадр устья настроен (портал 0/{DomePreview.Mouths.Count}, туман выкл)");
    }

    /// <summary>
    /// Кадр лаборатории (s10f-2): камера над двором, смотрит на боксы.
    /// </summary>
    public static void FrameLab()
    {
        if (!DomePreview.Reattach() || DomePreview.Facility == null)
            throw new System.InvalidOperationException("сначала BuildDomePreview с корпусами");
        RenderSettings.fog = false;
        SetupCameras("35");
        var sandbox = GameObject.Find("SandboxCam");
        sandbox.transform.position = new Vector3(0f, 28f, -38f);
        sandbox.transform.LookAt(new Vector3(0f, 2f, 5f));
        sandbox.GetComponent<Camera>().farClipPlane = 6000f;
        Debug.Log("[Forest] кадр лаборатории настроен (туман выкл)");
    }

    /// <summary>
    /// Кадр ангара (s10f-2): камера на площадке, смотрит в портал.
    /// </summary>
    public static void FrameHangar()
    {
        if (!DomePreview.Reattach() || DomePreview.Facility == null)
            throw new System.InvalidOperationException("сначала BuildDomePreview с корпусами");
        var f = DomePreview.Facility;
        Vector2 fwd = new Vector2(
            Mathf.Sin(f.hangarYaw * Mathf.Deg2Rad), Mathf.Cos(f.hangarYaw * Mathf.Deg2Rad));
        RenderSettings.fog = false;
        SetupCameras("35");
        // Высота ворот — по построенным деталям (ангар стоит на склоне кольца,
        // земля там +40…+100, а не 0 — поймано кадром s10f).
        float topY = 5f;
        var facRoot = GameObject.Find("Facility");
        if (facRoot != null)
        {
            foreach (Transform c in facRoot.transform)
            {
                Vector3 p = c.position;
                float dx = p.x - f.hangarPos.x, dz = p.z - f.hangarPos.y;
                if (dx * dx + dz * dz < 30f * 30f && p.y > topY) topY = p.y;
            }
        }
        var sandbox = GameObject.Find("SandboxCam");
        sandbox.transform.position = new Vector3(
            f.hangarPos.x - fwd.x * 30f, topY + 8f, f.hangarPos.y - fwd.y * 30f);
        sandbox.transform.LookAt(new Vector3(f.hangarPos.x, topY - 2f, f.hangarPos.y));
        sandbox.GetComponent<Camera>().farClipPlane = 6000f;
        Debug.Log("[Forest] кадр ангара настроен (туман выкл)");
    }

    /// <summary>
    /// Кадр дождя (s10h): погода в превью + камера лаборатории.
    /// </summary>
    public static void FrameRain()
    {
        if (!DomePreview.Reattach())
            throw new System.InvalidOperationException("сначала BuildDomePreview");
        DomePreview.BuildWeather();
        FrameLab();
        Debug.Log("[Forest] кадр дождя настроен");
    }

    /// <summary>
    /// Кадр ясеня (s10g-2): камера перед исполином, в кадре ствол + крона + гнездо.
    /// </summary>
    public static void FrameAsh()
    {
        if (!DomePreview.Reattach() || !DomePreview.HasAsh)
            throw new System.InvalidOperationException("сначала BuildDomePreview с ясенем");
        var a = DomePreview.Ash;
        RenderSettings.fog = false;
        SetupCameras("35");
        var sandbox = GameObject.Find("SandboxCam");
        sandbox.transform.position = new Vector3(a.pos.x - 45f, 18f, a.pos.y - 55f);
        sandbox.transform.LookAt(new Vector3(a.pos.x, a.groundY + 22f, a.pos.y));
        sandbox.GetComponent<Camera>().farClipPlane = 6000f;
        Debug.Log("[Forest] кадр ясеня настроен (туман выкл)");
    }

    /// <summary>
    /// Фаза неба превью (s10c): day → 10.5 мин, sunset → 20.5, night → 25.5 (или минуты числом).
    /// Позиции камер — как в FrameDome, затем риг ставит свет/туман/шейдер по фазе.
    /// </summary>
    public static void FrameSky(string phaseText)
    {
        if (!DomePreview.Reattach())
            throw new System.InvalidOperationException("сначала BuildDomePreview (нет превью)");
        float minutes = 10.5f;
        if (phaseText == "sunset") minutes = 20.5f;
        else if (phaseText == "night") minutes = 25.5f;
        else if (phaseText != "day") float.TryParse(phaseText, out minutes);
        FrameDome("2000");
        // Камера висты ВНУТРИ свода (апекс +700): снаружи видно только изнанку.
        var sandbox = GameObject.Find("SandboxCam");
        sandbox.transform.position = new Vector3(0f, 300f, -700f);
        sandbox.transform.LookAt(new Vector3(0f, 0f, 200f));
        DomeSkyRig.ApplyPhase(DomePreview.PreviewRoot, minutes);
    }

    /// <summary>
    /// Кадр под купол (s10a): туман гасится (иначе Exp2 0.012 на 2000м — белое молоко),
    /// far plane камер — 6000, TopCam — топдаун-орто на весь диаметр, SandboxCam — 3/4.
    /// Возврат тумана/света — ForestLightSetup.SetupSandbox() (как в s7/s9).
    /// </summary>
    public static void FrameDome(string diameterText)
    {
        float diameter = 2000f;
        float.TryParse(diameterText, out diameter);
        if (diameter <= 0f) diameter = 2000f;
        RenderSettings.fog = false;
        SetupCameras("35");
        var top = GameObject.Find("TopCam");
        var topCam = top.GetComponent<Camera>();
        top.transform.position = new Vector3(0f, diameter * 1.3f, 0f);
        topCam.orthographicSize = diameter * 0.5f + 100f;
        topCam.farClipPlane = 6000f;
        var sandbox = GameObject.Find("SandboxCam");
        var sandboxCam = sandbox.GetComponent<Camera>();
        sandbox.transform.position = new Vector3(0f, diameter * 0.45f, -diameter * 0.55f);
        sandbox.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
        sandboxCam.farClipPlane = 6000f;
        Debug.Log($"[Forest] кадр купола настроен (Ø{diameter} м, туман выкл, far 6000)");
    }
}
