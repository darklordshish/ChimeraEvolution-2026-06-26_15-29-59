using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// G7 шаг 6 — приёмка. Собирает ровно профиль `ForestProfiles.PavilionV2`
/// (worldVersion 2, treeVersion 2, эрозия thermal+hydraulic) на существующем
/// ForestBuilder сцены `ForestPavilion`, ставит камеры протокола S5 §4
/// (Entry 00/50/100, три станции чтения, тур 10, сверху) и пишет подписи
/// в `Docs/Диаграммы/Лес/Кадры/16-G7-место-и-форма/G7-метаданные.txt`.
/// <para>
/// Кадр снимает пайплайн (`capture_game_view`): здесь только позиция камеры
/// и подпись. Схему не форсируем — её выбирает дерево по сиду; чтобы получить
/// все три, `ProbeSchemes` называет сиды, а `Rebuild` собирает мир на нужном.
/// </para>
/// </summary>
public static class G7Accept
{
    static ForestBuilder B;
    static Transform Root;
    static Camera Cam;
    static Vector3 LastTarget;
    static string LastHeight = "ground";
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    const string Dir = "Docs/Диаграммы/Лес/Кадры/16-G7-место-и-форма/";
    const string MetaFile = Dir + "G7-метаданные.txt";

    // ------------------------------------------------------------------
    // Профиль и сборка
    // ------------------------------------------------------------------

    public static string Apply()
    {
        var problem = Attach();
        if (problem != null) return problem;
        ApplyProfile();
        return "profile applied (no build)\n" + Facts();
    }

    public static string Build()
    {
        var problem = Attach();
        if (problem != null) return problem;
        ApplyProfile();
        RebuildWorld();
        Directory.CreateDirectory(Resolve(Dir));
        if (File.Exists(Resolve(MetaFile))) File.Delete(Resolve(MetaFile));
        var sb = new StringBuilder();
        sb.AppendLine(Head("canonical seed " + B.seed));
        sb.AppendLine(Facts());
        return sb.ToString();
    }

    /// <summary>Собрать мир на другом сиде: дерево само выберет схему.</summary>
    public static string Rebuild(string seedText)
    {
        var problem = Attach();
        if (problem != null) return problem;
        if (!long.TryParse(seedText, NumberStyles.Integer, Inv, out long seed))
            return "seed must be integer";
        ApplyProfile();
        B.seed = seed;
        RebuildWorld();
        var sb = new StringBuilder();
        sb.AppendLine(Head("seed " + seed));
        sb.AppendLine(Facts());
        return sb.ToString();
    }

    static string Head(string what)
    {
        return $"=== {what} ===\nseed={B.seed} worldVersion={B.WorldVersion} " +
               $"treeVersion={B.TreeVersion} scheme={B.Scheme} near={B.Near}\n" +
               $"fingerprint={B.FingerprintHex}\n";
    }

    /// <summary>Короткая опись мира — без ссылки на G7Shot: run_script компилирует один файл.</summary>
    static string Facts()
    {
        string ash = B.HasAsh ? B.Ash.pos.ToString() : "none";
        string canyon = B.Canyon.valid ? B.Canyon.policy.ToString() : "none";
        string lake = B.Hydro != null
            ? B.Hydro.lakeCenter + " r=" + B.Hydro.lakeRadius.ToString("0.0", Inv)
            : "none";
        string erosion = B.Erosion != null
            ? "res=" + B.Erosion.resolution + " hydro=" + B.Erosion.hydraulicStatus
            : "off";
        string roads = B.Roads != null
            ? B.Roads.roads.Count + " bands " + B.Roads.length.ToString("0", Inv) +
              " m loops=" + B.Roads.loops
            : "null";
        int forms = B.CompositionForms != null ? B.CompositionForms.Count : 0;
        var sb = new StringBuilder();
        sb.AppendLine($"seed={B.seed} randomSeed={B.randomSeed} worldVersion={B.WorldVersion} " +
                      $"treeVersion={B.TreeVersion} buildRelief={B.buildRelief} " +
                      $"thermal={B.buildThermalErosion} hydraulic={B.buildHydraulicErosion} " +
                      $"erosionVersion={B.erosionVersion} quads={B.terrainQuads} " +
                      $"buildOnAwake={B.buildOnAwake}");
        sb.AppendLine($"built={B.BuiltRoot != null} chunks={B.Chunks} trees={B.Trees} " +
                      $"colliders={B.TreeColliders} deadLogs={B.DeadLogs} " +
                      $"crown={B.CrownCover * 100f:F1}%");
        sb.AppendLine($"entry={V(B.Entry)} labDoor={V(B.LabDoor)} " +
                      $"labCenter={B.LabCenter} northDoor={V(B.LabNorthDoor)}");
        float doorChord = Vector2.Distance(
            new Vector2(B.Entry.x, B.Entry.z), new Vector2(B.LabDoor.x, B.LabDoor.z));
        sb.AppendLine($"station={B.HasStation} style={B.ResolvedLabStyle} " +
                      $"entrance={B.ResolvedLabEntrance} doorChord={doorChord:F1} " +
                      $"centerChord={Vector2.Distance(new Vector2(B.Entry.x, B.Entry.z), B.LabCenter):F1}");
        sb.AppendLine($"scheme={B.Scheme} near={B.Near} " +
                      $"path={(B.Plan != null ? B.Plan.path : "none")}");
        sb.AppendLine($"zones={B.Zones.Count} links={B.Links.Count} commons={B.Commons.Count} " +
                      $"waterholes={B.Waterholes.Count} pods={B.CoverPodsBuilt} " +
                      $"edges={B.ArenaEdges} shells={B.Shells.Count} gates={B.Gates.Count} " +
                      $"reliefHills={B.ReliefHills} forms={forms}");
        sb.AppendLine("ash=" + ash + " canyon=" + canyon + " lake=" + lake +
                      " erosion=" + erosion);
        sb.AppendLine("roads=" + roads);
        sb.AppendLine("fingerprint=" + B.FingerprintHex);
        return sb.ToString();
    }

    static string V(Vector3 v) => $"({v.x:F1};{v.y:F1};{v.z:F1})";

    static void RebuildWorld()
    {
        if (B.BuiltRoot != null) B.Clear();
        var sw = new System.Diagnostics.Stopwatch();
        sw.Start();
        B.Build();
        sw.Stop();
        Root = B.BuiltRoot != null ? B.BuiltRoot.transform : B.transform;
        Debug.Log($"[G7Accept] built in {sw.Elapsed.TotalSeconds:0.0} s");
    }

    static void ApplyProfile()
    {
        // Ровно ForestProfiles.PavilionV2(host, 1337, erosion: true).
        B.buildOnAwake = false;
        B.randomSeed = false;
        B.seed = 1337L;
        B.mapDiameter = 2000f;
        B.chunkSize = 250f;
        B.terrainQuads = 48;
        B.treeCount = 2000;
        B.patchyFlora = true;
        B.undergrowthScale = 0.01f;
        B.forestThreshold = 0.20f;
        B.treeDensityTarget = 3120f;
        B.treeMinCount = 1500;
        B.buildAsh = true;
        B.spawnAtEntry = true;
        B.buildRavines = false;
        B.buildTerrace = true;
        B.buildCanyon = true;
        B.buildStationLoop = true;
        B.buildStationHill = false;
        B.buildLabComplex = true;
        B.buildLabPlan = true;
        B.labPlanVersion = 1;
        B.buildClimate = true;
        B.climateWeather = WeatherKind.Clear;
        B.climateTimeMinutes = 10.5f;
        B.climateTimeScale = 0f;
        B.climateAutoWeather = false;
        B.labStyle = LabStyle.Auto;
        B.labEntrance = LabEntrance.Auto;
        B.minLabDistance = 150f;
        B.worldVersion = 2;
        B.treeVersion = 2;
        B.buildRelief = true;
        B.buildThermalErosion = true;
        B.buildHydraulicErosion = true;
        B.erosionVersion = 3;
        B.erosionResolution = 128;
        B.erosionDrops = 96000;
        B.erosionSteps = 40;
        B.speciesPool = new List<SpeciesSO>();
    }

    // ------------------------------------------------------------------
    // Схемы: какие сиды дают какую схему
    // ------------------------------------------------------------------

    public static string ProbeSchemes()
    {
        var problem = Attach();
        if (problem != null) return problem;
        var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
        cfg.seed = 1337L;
        cfg.mapDiameter = 2000f;
        cfg.worldVersion = 2;
        var sb = new StringBuilder();
        Vector2 entry = new Vector2(B.Entry.x, B.Entry.z);
        try
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var s = DomeSchemes.Select(cfg, seed, entry, B.Hydro,
                    (x, z) => B.SampleGround(x, z));
                sb.AppendLine($"{seed}\t{s.id}\t{DomeSchemes.SelectNear(s, seed)}" +
                              (s.fallback ? "\tfallback" : ""));
            }
            sb.AppendLine($"current: seed={B.seed} scheme={B.Scheme} near={B.Near}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cfg);
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Кадры: A/B 0/50/100 на Entry (станция ①), станции чтения, сверху
    // ------------------------------------------------------------------

    public static string Gate00() => Gate("00");
    public static string Gate50() => Gate("50");
    public static string Gate100() => Gate("100");

    /// <summary>
    /// Стрелка 0/50/100 по S5 §4: одна ось (хорда Entry → LabDoor), одна цель,
    /// три высоты. Камера стоит в 8 м ВНУТРИ чаши от точки Entry, чтобы кадр
    /// был взглядом из ворот, а не тёмным камнем портала.
    /// </summary>
    public static string Gate(string height)
    {
        var problem = Attach();
        if (problem != null) return problem;
        if (B.BuiltRoot == null) return "world not built";
        float h = height == "00" ? 1.6f : height == "50" ? 50f : height == "100" ? 100f : -1f;
        if (h < 0f) return "height must be 00|50|100";
        LastHeight = height;

        Vector2 e = new Vector2(B.Entry.x, B.Entry.z);
        Vector2 d = new Vector2(B.LabDoor.x, B.LabDoor.z);
        Vector2 dir = SafeDir(d - e);
        Vector2 camF = e + dir * 8f;
        float groundCam = B.SampleGround(camF.x, camF.y);
        Vector2 targetF = e + dir * 140f;
        float groundTarget = B.SampleGround(targetF.x, targetF.y);

        Vector3 pos = Field(camF.x, groundCam + h, camF.y);
        Vector3 target = Field(targetF.x, groundTarget + 2f, targetF.y);
        Place(pos, target, 60f);
        Climate(pos);
        return $"gate {height} camXZ=({camF.x:F1};{camF.y:F1}) camY={pos.y:F1} " +
               $"ground={groundCam:F1} target={target} axis={AxisLine(e, d)} fog={FogLine()}";
    }

    public static string Station1() => StationRead(1);
    public static string Station2() => StationRead(2);
    public static string Station3() => StationRead(3);

    /// <summary>Три станции чтения: ① Entry, ② середина ~450 м, ③ за 150 м до станции.</summary>
    public static string StationRead(int index)
    {
        var problem = Attach();
        if (problem != null) return problem;
        if (B.BuiltRoot == null) return "world not built";
        LastHeight = "00";
        Vector2 e = new Vector2(B.Entry.x, B.Entry.z);
        Vector2 d = new Vector2(B.LabDoor.x, B.LabDoor.z);
        Vector2 dir = SafeDir(d - e);
        float chord = Vector2.Distance(e, d);
        Vector2 p = index == 1 ? e : index == 2 ? Vector2.Lerp(e, d, 0.5f) : d - dir * 150f;
        float g = B.SampleGround(p.x, p.y);
        Vector3 pos = Field(p.x, g + 1.6f, p.y);
        Vector2 targetF = new Vector2(p.x, p.y) + dir * 90f;
        Vector3 target = Field(targetF.x, B.SampleGround(targetF.x, targetF.y) + 2f, targetF.y);
        Place(pos, target, 60f);
        Climate(pos);
        return $"station{index} p=({p.x:F1};{p.y:F1}) along={(chord - Vector2.Distance(p, d)):F1} " +
               $"camY={pos.y:F1} ground={g:F1} axis={AxisLine(e, d)} fog={FogLine()}";
    }

    public static string Reverse1() => Reverse(1);
    public static string Reverse2() => Reverse(2);

    /// <summary>Обратный проход: от лаборатории к входу.</summary>
    public static string Reverse(int index)
    {
        var problem = Attach();
        if (problem != null) return problem;
        if (B.BuiltRoot == null) return "world not built";
        LastHeight = "00";
        Vector2 e = new Vector2(B.Entry.x, B.Entry.z);
        Vector2 d = new Vector2(B.LabDoor.x, B.LabDoor.z);
        Vector2 dir = SafeDir(d - e);
        Vector2 p = index == 1 ? d - dir * 30f : Vector2.Lerp(e, d, 0.5f);
        Vector2 back = -dir;
        float g = B.SampleGround(p.x, p.y);
        Vector3 pos = Field(p.x, g + 1.6f, p.y);
        Vector2 targetF = p + back * 120f;
        Vector3 target = Field(targetF.x, B.SampleGround(targetF.x, targetF.y) + 2f, targetF.y);
        Place(pos, target, 60f);
        Climate(pos);
        return $"reverse{index} p=({p.x:F1};{p.y:F1}) camY={pos.y:F1} ground={g:F1} " +
               $"axis={AxisLine(e, d)} fog={FogLine()}";
    }

    /// <summary>Дорога: первая primary-лента, взгляд вдоль неё (тур 10).</summary>
    public static string RoadView()
    {
        var problem = Attach();
        if (problem != null) return problem;
        if (B.BuiltRoot == null) return "world not built";
        if (B.Roads == null || B.Roads.roads.Count == 0) return "no roads";
        LastHeight = "00";
        ForestRoad road = B.Roads.roads[0];
        foreach (var r in B.Roads.roads)
            if (r.primary && r.points != null && r.points.Count > 2) { road = r; break; }
        int mid = Mathf.Clamp(road.points.Count / 2, 1, road.points.Count - 2);
        Vector2 p = road.points[mid];
        Vector2 dir = SafeDir(road.points[mid + 1] - road.points[mid - 1]);
        float g = B.SampleGround(p.x, p.y);
        Vector3 pos = Field(p.x, g + 1.6f, p.y);
        Vector2 tf = p + dir * 80f;
        Vector3 target = Field(tf.x, B.SampleGround(tf.x, tf.y) + 2f, tf.y);
        Place(pos, target, 60f);
        Climate(pos);
        return $"road p=({p.x:F1};{p.y:F1}) width={road.width:F1} " +
               $"primary={road.primary} camY={pos.y:F1} ground={g:F1} fog={FogLine()}";
    }

    /// <summary>Сверху: силуэт схемы/героя и связность (под сводом, ortho).</summary>
    public static string Top()
    {
        var problem = Attach();
        if (problem != null) return problem;
        if (B.BuiltRoot == null) return "world not built";
        LastHeight = "top";
        EnsureCamera();
        float ground = B.SampleGround(0f, 0f);
        Vector3 target = Field(0f, ground, 0f);
        LastTarget = target;
        Mark(target, "top");
        Vector3 pos = target + Vector3.up * 240f;
        Cam.orthographic = true;
        Cam.orthographicSize = 1050f;
        Cam.nearClipPlane = 0.3f;
        Cam.farClipPlane = 6000f;
        Cam.transform.SetPositionAndRotation(pos, Quaternion.Euler(90f, 0f, 0f));
        Climate(pos);
        return $"top pos={pos} target={target} ortho=1050 fog={FogLine()}";
    }

    // ------------------------------------------------------------------
    // Станция снаружи: 3/4, чтобы читалось плечо, а не плоская крышка
    // ------------------------------------------------------------------

    /// <summary>Станция в 3/4 с юго-востока: плечо, посадка и вход.</summary>
    public static string Station34()
    {
        var problem = Attach();
        if (problem != null) return problem;
        if (!B.HasStation) return "station=none";
        Vector2 c = B.LabCenter;
        Vector2 camF = c + new Vector2(120f, -150f);
        float g = B.SampleGround(c.x, c.y);
        float gc = B.SampleGround(camF.x, camF.y);
        Vector3 target = Field(c.x, g + 5f, c.y);
        Vector3 pos = Field(camF.x, gc + 44f, camF.y);
        Place(pos, target, 45f);
        Climate(pos);
        return $"station34 c=({c.x:F1};{c.y:F1}) cam=({camF.x:F1};{camF.y:F1}) " +
               $"camY={pos.y:F1} g={gc:F1} fog={FogLine()}";
    }

    /// <summary>Дверь станции снаружи, от леса: остов и вход как POI.</summary>
    public static string Lab34()
    {
        var problem = Attach();
        if (problem != null) return problem;
        Vector3 d = B.LabDoor;
        if (d == Vector3.zero) return "no lab door";
        Vector2 camF = new Vector2(d.x + 70f, d.z - 95f);
        float gc = B.SampleGround(camF.x, camF.y);
        Vector3 target = Field(d.x, d.y + 4f, d.z);
        Vector3 pos = Field(camF.x, gc + 28f, camF.y);
        Place(pos, target, 50f);
        Climate(pos);
        return $"lab34 door={V(d)} cam=({camF.x:F1};{camF.y:F1}) camY={pos.y:F1} " +
               $"g={gc:F1} fog={FogLine()}";
    }

    // ------------------------------------------------------------------
    // Подписи кадра
    // ------------------------------------------------------------------

    /// <summary>Начать подпись заново (один прогон — один файл).</summary>
    public static string ResetMeta()
    {
        Directory.CreateDirectory(Resolve(Dir));
        if (File.Exists(Resolve(MetaFile))) File.Delete(Resolve(MetaFile));
        File.AppendAllText(Resolve(MetaFile),
            "# G7 шаг 6 — подписи кадров приёмки\n" +
            "# один мир на прогон; A/B только SetActive по PoiWhitelist\n");
        return "metadata reset: " + MetaFile;
    }

    public static string Meta(string label, string image, string proves,
        string hidden = null)
    {
        var problem = Attach();
        if (problem != null) return problem;
        var cam = FindCamera();
        if (cam == null) return "no PavilionCam — сначала поставь кадр";
        Vector3 target = LastTarget;
        string height = LastHeight;
        var state = FindState();
        if (state != null)
        {
            target = state.transform.position;
            int bar = state.name.IndexOf('|');
            if (bar >= 0) height = state.name.Substring(bar + 1);
        }
        return AppendMeta(label, image, proves, hidden, cam, target, height);
    }

    /// <summary>
    /// Подпись кадра, снятого чужим инструментом (`G7Shot.Tour`): статики между
    /// run_script не живут, метка `G7State` не ставится — цель берём по оси камеры.
    /// </summary>
    public static string MetaCamera(string label, string image, string proves,
        string hidden, string height)
    {
        var problem = Attach();
        if (problem != null) return problem;
        var cam = FindCamera();
        if (cam == null) return "no PavilionCam — сначала поставь кадр";
        Vector3 target = cam.transform.position + cam.transform.forward * 100f;
        return AppendMeta(label, image, proves, hidden, cam, target,
            string.IsNullOrEmpty(height) ? "ground" : height);
    }

    static string AppendMeta(string label, string image, string proves, string hidden,
        Camera cam, Vector3 target, string height)
    {
        Directory.CreateDirectory(Resolve(Dir));
        var sb = new StringBuilder();
        Vector2 e = new Vector2(B.Entry.x, B.Entry.z);
        Vector2 d = new Vector2(B.LabDoor.x, B.LabDoor.z);
        var canyon = B.Canyon;
        sb.AppendLine("---");
        sb.AppendLine($"frame={label}");
        sb.AppendLine($"image={image}");
        sb.AppendLine($"seed={B.seed}");
        sb.AppendLine($"worldVersion={B.WorldVersion}");
        sb.AppendLine($"treeVersion={B.TreeVersion}");
        sb.AppendLine("path=" + (B.Plan != null ? B.Plan.path : "absent"));
        sb.AppendLine("scheme=" + (B.Scheme ?? "absent"));
        sb.AppendLine("near=" + (B.Near ?? "absent"));
        sb.AppendLine("signature=" + (B.Plan != null ? B.Plan.signature : "absent"));
        sb.AppendLine("fingerprint=" + B.FingerprintHex);
        sb.AppendLine($"profile=terrainQuads={B.terrainQuads},treeCount={B.treeCount}," +
                      $"patchyFlora={B.patchyFlora},buildRelief={B.buildRelief}," +
                      $"buildTerrace={B.buildTerrace},buildCanyon={B.buildCanyon}," +
                      $"buildAsh={B.buildAsh},buildLabComplex={B.buildLabComplex}," +
                      $"labStyle={B.labStyle},labEntrance={B.labEntrance}," +
                      $"buildStationLoop={B.buildStationLoop},buildStationHill={B.buildStationHill};" +
                      $"hydraulic={B.buildHydraulicErosion},thermal={B.buildThermalErosion};" +
                      $"erosionRes={B.erosionResolution},erosionVersion={B.erosionVersion}");
        sb.AppendLine($"station={B.HasStation} style={B.ResolvedLabStyle} " +
                      $"entrance={B.ResolvedLabEntrance} " +
                      $"doorChord={Vector2.Distance(e, d):F1} " +
                      $"centerChord={Vector2.Distance(e, B.LabCenter):F1}");
        sb.AppendLine($"canyon={canyon.policy} valid={canyon.valid} " +
                      $"length={AxisLength(canyon):F0} " +
                      $"lip={(canyon.valid ? canyon.lip.ToString() : "none")}");
        sb.AppendLine($"ash={(B.HasAsh ? B.Ash.pos.ToString() : "none")} " +
                      $"chunks={B.Chunks} trees={B.Trees} crown={B.CrownCover * 100f:F1}%");
        sb.AppendLine($"camera=pos=({cam.transform.position.x:F2};{cam.transform.position.y:F2};" +
                      $"{cam.transform.position.z:F2}),target=({target.x:F2};" +
                      $"{target.y:F2};{target.z:F2}),yaw={cam.transform.eulerAngles.y:F1}," +
                      $"pitch={cam.transform.eulerAngles.x:F1},fov={cam.fieldOfView:F1}," +
                      $"ortho={(cam.orthographic ? cam.orthographicSize.ToString("0", Inv) : "off")}");
        sb.AppendLine($"height={height}");
        sb.AppendLine($"axis={AxisLine(e, d)}");
        sb.AppendLine("time=10.5,weather=Clear,timeScale=0");
        sb.AppendLine($"fog={FogLine()}");
        sb.AppendLine("hidden=" + (string.IsNullOrEmpty(hidden)
            ? string.Join(",", ForestBuilder.PoiWhitelist)
            : hidden));
        sb.AppendLine($"proves={proves.Replace('_', ' ')}");
        File.AppendAllText(Resolve(MetaFile), sb.ToString());
        return $"metadata appended: {label}";
    }

    static string AxisLine(Vector2 entry, Vector2 door)
    {
        Vector2 dir = SafeDir(door - entry);
        float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
        return $"chord {Vector2.Distance(entry, door):F1} m, yaw {yaw:F1} deg, " +
               $"dir=({dir.x:F3};{dir.y:F3})";
    }

    // ------------------------------------------------------------------
    // Камера и мелочи
    // ------------------------------------------------------------------

    /// <summary>Найти камеру, НЕ трогая её настройки (после кадра нельзя сбрасывать ortho).</summary>
    static Camera FindCamera()
    {
        if (Cam != null) return Cam;
        foreach (var t in SceneObjects())
            if (t.name == "PavilionCam") { Cam = t.GetComponent<Camera>(); return Cam; }
        return null;
    }

    static Camera EnsureCamera()
    {
        foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            if (c != null && c.name != "PavilionCam") c.enabled = false;

        GameObject go = null;
        foreach (var t in SceneObjects())
            if (t.name == "PavilionCam") { go = t; break; }
        if (go == null) go = new GameObject("PavilionCam");
        go.SetActive(true);
        go.tag = "Untagged";
        Cam = go.GetComponent<Camera>();
        if (Cam == null) Cam = go.AddComponent<Camera>();
        Cam.enabled = true;
        Cam.orthographic = false;
        Cam.fieldOfView = 60f;
        Cam.nearClipPlane = 0.3f;
        Cam.farClipPlane = 6000f;
        Cam.clearFlags = CameraClearFlags.Skybox;
        return Cam;
    }

    /// <summary>
    /// Метка последнего кадра: run_script компилируется отдельной сборкой на вызов,
    /// статики между вызовами не живут. Цель и высоту храним объектом сцены.
    /// </summary>
    static void Mark(Vector3 target, string height)
    {
        var go = FindState();
        if (go == null) go = new GameObject("G7State");
        go.transform.position = target;
        go.name = "G7State|" + height;
    }

    static GameObject FindState()
    {
        foreach (var t in SceneObjects())
            if (t.name.StartsWith("G7State", StringComparison.Ordinal)) return t;
        return null;
    }

    static IEnumerable<GameObject> SceneObjects()
    {
        var scene = SceneManager.GetActiveScene();
        foreach (var r in scene.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
                yield return t.gameObject;
    }

    static void Place(Vector3 pos, Vector3 target, float fov)
    {
        LastTarget = target;
        Mark(target, LastHeight);
        var cam = EnsureCamera();
        cam.orthographic = false;
        cam.fieldOfView = fov;
        cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(target - pos, Vector3.up));
    }

    static void Climate(Vector3 viewPoint)
    {
        var c = B.GetComponent<ForestClimate>();
        if (c == null) return;
        c.timeMinutes = 10.5f;
        c.timeScale = 0f;
        c.autoWeather = false;
        c.currentKind = WeatherKind.Clear;
        c.SetWeatherImmediate(WeatherKind.Clear);
        c.ApplyVisuals(viewPoint);
    }

    static string FogLine() =>
        $"fog={RenderSettings.fog} density={RenderSettings.fogDensity.ToString("0.000000", Inv)} " +
        $"mode={RenderSettings.fogMode} color={RenderSettings.fogColor}";

    static string Attach()
    {
        if (B == null) B = UnityEngine.Object.FindAnyObjectByType<ForestBuilder>();
        if (B == null) return "no ForestBuilder in open scene";
        Root = B.BuiltRoot != null ? B.BuiltRoot.transform : B.transform;
        return null;
    }

    static Vector3 Field(float x, float y, float z)
    {
        var p = new Vector3(x, y, z);
        return Root != null ? Root.TransformPoint(p) : p;
    }

    static Vector2 SafeDir(Vector2 v) =>
        v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.right;

    static float AxisLength(DomeCanyon.State c)
    {
        float len = 0f;
        if (c.axis != null)
            for (int i = 1; i < c.axis.Count; i++)
                len += Vector2.Distance(c.axis[i - 1], c.axis[i]);
        return len;
    }

    static string Resolve(string path)
    {
        if (Path.IsPathRooted(path)) return path;
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
    }
}

