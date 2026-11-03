using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// S5, ворота G7 (шаг 6): пары A/B без пересборки мира и тур по павильону.
/// <para>
/// Протокол — `Docs/superpowers/specs/2026-11-01-landshaft-s5-verifikaciya.md` §4:
/// три высоты камеры (глаз 1.6 м, 50, 100) на одну ось и цель; A — без POI/дорог,
/// B — с ними; скрытие только `SetActive(false)`, БЕЗ `Build()`, иначе это другой мир.
/// </para>
/// <para>
/// Сцена — `ForestPavilion`. v2-профиль ворот (`ForestProfiles.PavilionV2`,
/// hydraulic=false, thermal=true) применяется К ПОЛЯМ существующего компонента:
/// `ForestProfiles` делает `AddComponent`, а второй ForestBuilder в сцене недопустим.
/// </para>
/// Пайплайн: `run_script --file Tools/Agent/G7Shot.cs --entry G7Shot.BuildV2`
/// (сборка ~70 с — поднимать `--timeout`), затем `G7Shot.HidePoi`, кадры A,
/// `G7Shot.ShowPoi`, кадры B, `G7Shot.Tour` по режимам.
/// </summary>
public static class G7Shot
{
    static ForestBuilder B;
    static Transform Root;
    static Camera Cam;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ------------------------------------------------------------------
    // Шаг 2: v2-профиль и сборка
    // ------------------------------------------------------------------

    public static string ConfigureV2()
    {
        var problem = Attach();
        if (problem != null) return problem;
        ApplyV2Profile();
        return "v2 configured (no build)\n" + Facts();
    }

    public static string BuildV2()
    {
        var problem = Attach();
        if (problem != null) return problem;
        ApplyV2Profile();
        if (B.BuiltRoot != null)
        {
            B.Clear();
            Root = B.transform;
        }
        var sw = new System.Diagnostics.Stopwatch();
        sw.Start();
        B.Build();
        sw.Stop();
        if (B.BuiltRoot == null) return "BUILD FAILED: see console (BuiltRoot == null)";
        Root = B.BuiltRoot.transform;
        var sb = new StringBuilder();
        sb.AppendLine($"built in {sw.Elapsed.TotalSeconds.ToString("0.0", Inv)} s");
        sb.AppendLine(Facts());
        return sb.ToString();
    }

    static void ApplyV2Profile()
    {
        // Ровно профиль ворот ForestProfiles.PavilionV2(erosion=true) за вычетом
        // гидроэрозии: S5 меряет её отдельно (PlayMode Hydro_Gate), кадру она не нужна.
        B.buildOnAwake = false;
        B.randomSeed = false;
        B.seed = 1337L;
        B.mapDiameter = 2000f;
        B.chunkSize = 250f;
        B.terrainQuads = 48;
        B.treeCount = 2000;
        B.patchyFlora = true;
        B.undergrowthScale = 0.10f;
        B.forestThreshold = 0.20f;
        B.treeDensityTarget = 3120f;
        B.treeMinCount = 1500;
        B.buildAsh = true;
        B.spawnAtEntry = true;
        B.buildRavines = false;
        B.buildTerrace = true;
        B.buildCanyon = true;
        B.buildStationLoop = true;
        B.buildStationHill = true;
        B.buildLabComplex = true;
        B.buildLabPlan = true;
        B.labPlanVersion = 1;
        B.buildClimate = true;
        B.climateWeather = WeatherKind.Clear;
        B.climateTimeMinutes = 10.5f;
        B.climateTimeScale = 0f;
        B.climateAutoWeather = false;
        B.labStyle = LabStyle.Bastion;
        B.labEntrance = LabEntrance.Rock;
        B.minLabDistance = 150f;
        B.worldVersion = 2;
        B.buildRelief = true;
        B.buildThermalErosion = true;
        B.buildHydraulicErosion = false;
        B.erosionVersion = 3;
        B.erosionResolution = 128;
        B.erosionDrops = 96000;
        B.erosionSteps = 40;
        B.speciesPool = new List<SpeciesSO>();
    }

    public static string Facts()
    {
        var problem = Attach();
        if (problem != null) return problem;
        string ash = B.HasAsh ? B.Ash.pos.ToString() : "none";
        string canyon = B.Canyon.valid ? "valid" : "none";
        string lake = "none";
        if (B.Hydro != null)
            lake = B.Hydro.lakeCenter + " r=" + B.Hydro.lakeRadius.ToString("0.0", Inv);
        string erosion = "off";
        if (B.Erosion != null)
            erosion = "res=" + B.Erosion.resolution + " hydro=" + B.Erosion.hydraulicStatus;
        string roads = "null";
        if (B.Roads != null)
            roads = B.Roads.roads.Count + " bands " + B.Roads.length.ToString("0", Inv) +
                    " m loops=" + B.Roads.loops;
        int forms = B.CompositionForms != null ? B.CompositionForms.Count : 0;

        var sb = new StringBuilder();
        sb.AppendLine($"seed={B.seed} randomSeed={B.randomSeed} worldVersion={B.WorldVersion} " +
                      $"buildRelief={B.buildRelief} thermal={B.buildThermalErosion} " +
                      $"hydraulic={B.buildHydraulicErosion} erosionVersion={B.erosionVersion} " +
                      $"quads={B.terrainQuads} buildOnAwake={B.buildOnAwake}");
        sb.AppendLine($"built={B.BuiltRoot != null} chunks={B.Chunks} trees={B.Trees} " +
                      $"colliders={B.TreeColliders} deadLogs={B.DeadLogs} " +
                      $"crown={B.CrownCover * 100f:F1}%");
        sb.AppendLine($"entry={V(B.Entry)} labDoor={V(B.LabDoor)} labCenter={B.LabCenter} " +
                      $"northDoor={V(B.LabNorthDoor)}");
        sb.AppendLine($"zones={B.Zones.Count} links={B.Links.Count} commons={B.Commons.Count} " +
                      $"waterholes={B.Waterholes.Count} pods={B.CoverPodsBuilt} " +
                      $"edges={B.ArenaEdges} shells={B.Shells.Count} gates={B.Gates.Count} " +
                      $"reliefHills={B.ReliefHills} forms={forms}");
        sb.AppendLine("ash=" + ash + " canyon=" + canyon + " lake=" + lake + " erosion=" + erosion);
        sb.AppendLine("roads=" + roads);
        if (Root != null)
        {
            var t = Root;
            sb.AppendLine($"root={t.name} worldPos={t.position} worldRot={t.eulerAngles} " +
                          "parent=" + (t.parent != null ? t.parent.name : "none"));
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Шаг 3: POI-слой A/B
    // ------------------------------------------------------------------

    /// <summary>Скрыть POI-разметку: SpacePaths, ArenaEdge, поды, ForestRoads, LevelGate, LabShell.</summary>
    public static string HidePoi() => TogglePoi(false);

    /// <summary>Вернуть POI-разметку (B).</summary>
    public static string ShowPoi() => TogglePoi(true);

    static string TogglePoi(bool on)
    {
        var problem = Attach();
        if (problem != null) return problem;
        var podPositions = new List<Vector2>();
        if (B.CoverPods != null)
            foreach (var p in B.CoverPods) podPositions.Add(p.center);

        int paths = 0, edges = 0, pods = 0, roads = 0, gates = 0, shells = 0;
        foreach (var go in SceneObjects())
        {
            string kind = PoiKind(go, podPositions);
            if (kind == null) continue;
            go.SetActive(on);
            switch (kind)
            {
                case "SpacePaths": paths++; break;
                case "ArenaEdge": edges++; break;
                case "CoverPod": pods++; break;
                case "ForestRoads": roads++; break;
                case "LevelGate": gates++; break;
                case "LabShell": shells++; break;
            }
        }
        return (on ? "shown" : "hidden") + $": SpacePaths={paths} ArenaEdge={edges} " +
               $"CoverPods={pods} ForestRoads={roads} LevelGate={gates} LabShell={shells}";
    }

    static string PoiKind(GameObject go, List<Vector2> podPositions)
    {
        string n = go.name;
        if (n == "SpacePaths") return "SpacePaths";
        if (n == "ArenaEdge") return "ArenaEdge";
        if (n == "ForestRoads") return "ForestRoads";
        if (n.StartsWith("LevelGate", StringComparison.Ordinal)) return "LevelGate";
        if (n.StartsWith("LabShell", StringComparison.Ordinal)) return "LabShell";
        // Поды — объекты DomeFlora с ОБЩИМ именем и общим мешем ("Trunk"/"Slab"):
        // ни имя объекта, ни имя меша их не различают. Узнаём по адресу из
        // CoverPods (сборка кладёт объект ровно в центр пода).
        if (n == "DomeFlora" && podPositions.Count > 0)
        {
            Vector3 wp = go.transform.position;
            var xz = new Vector2(wp.x, wp.z);
            foreach (var pp in podPositions)
                if ((xz - pp).sqrMagnitude < 0.25f) return "CoverPod";
        }
        return null;
    }

    static IEnumerable<GameObject> SceneObjects()
    {
        var scene = SceneManager.GetActiveScene();
        foreach (var r in scene.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
                yield return t.gameObject;
    }

    // ------------------------------------------------------------------
    // Шаг 3: кадр A/B — три высоты на одну ось и цель
    // ------------------------------------------------------------------

    /// <summary>height: "00" (глаз 1.6 м), "50", "100". Ось — зона у маршрута к лаборатории.</summary>
    public static string View00() => ViewAB("00");
    public static string View50() => ViewAB("50");
    public static string View100() => ViewAB("100");

    public static string ViewAB(string height)
    {
        var problem = Attach();
        if (problem != null) return problem;
        if (B.BuiltRoot == null) return "world not built";

        float h = height == "00" ? 1.6f : height == "50" ? 50f : height == "100" ? 100f : -1f;
        if (h < 0f) return "height must be 00|50|100";

        Vector2 zone;
        float radius;
        Vector2 dir;
        PickRouteZone(out zone, out radius, out dir);

        // Камера — в коридоре у поляны, цель — центр поляны: на глазу видно
        // дорожку/поды/кромку, на 50–100 м — квартал и карту.
        Vector2 camField = zone + dir * (radius + 90f);
        float groundCam = B.SampleGround(camField.x, camField.y);
        float groundTarget = B.SampleGround(zone.x, zone.y);

        Vector3 target = Field(zone.x, groundTarget + 0.5f, zone.y);
        Vector3 pos = Field(camField.x, groundCam + h, camField.y);
        Place(pos, target, 60f);
        Climate(pos);
        return $"AB height={h.ToString("0.0", Inv)} zone=({zone.x:F1};{zone.y:F1}) r={radius:F1} " +
               $"camXZ=({camField.x:F1};{camField.y:F1}) camY={pos.y:F1} ground={groundCam:F1} " +
               $"target={target} fog={FogLine()}";
    }

    static void PickRouteZone(out Vector2 center, out float radius, out Vector2 dir)
    {
        center = (new Vector2(B.Entry.x, B.Entry.z) + new Vector2(B.LabDoor.x, B.LabDoor.z)) * 0.5f;
        radius = 30f;
        dir = Vector2.right;
        if (B.Zones == null || B.Zones.Count == 0) return;

        Vector2 a = new Vector2(B.Entry.x, B.Entry.z);
        Vector2 b = new Vector2(B.LabDoor.x, B.LabDoor.z);
        var zone = B.Zones[0];
        float best = float.MaxValue;
        foreach (var z in B.Zones)
        {
            float d = DistanceToSegment(z.center, a, b);
            if (d < best) { best = d; zone = z; }
        }
        center = zone.center;
        radius = zone.radius;

        // Направление — вдоль коридора, ведущего в поляну: там дорожка, поды и кромка.
        Vector2 from = Vector2.zero;
        bool hasLink = false;
        if (B.Links != null)
            foreach (var l in B.Links)
            {
                if ((l.a - zone.center).sqrMagnitude < 25f) { from = l.b; hasLink = true; break; }
                if ((l.b - zone.center).sqrMagnitude < 25f) { from = l.a; hasLink = true; break; }
            }
        dir = hasLink ? SafeDir(from - zone.center) : SafeDir(a - zone.center);
    }

    // ------------------------------------------------------------------
    // Шаг 4: тур
    // ------------------------------------------------------------------

    /// <summary>Режимы: canyon | waterfall | lake | station | lab | ash | ring | zone | entry.</summary>
    public static string TourCanyon() => Tour("canyon");
    public static string TourWaterfall() => Tour("waterfall");
    public static string TourLake() => Tour("lake");
    public static string TourStation() => Tour("station");
    public static string TourLab() => Tour("lab");
    public static string TourAsh() => Tour("ash");
    public static string TourRing() => Tour("ring");
    public static string TourZone() => Tour("zone");
    public static string TourEntry() => Tour("entry");
    public static string TourRelief() => Tour("relief");
    public static string TourWaterhole() => Tour("waterhole");

    public static string Tour(string mode)
    {
        var problem = Attach();
        if (problem != null) return problem;
        if (B.BuiltRoot == null) return "world not built";
        switch (mode)
        {
            case "canyon": return Canyon34();
            case "waterfall": return CascadeLip();
            case "lake": return Lake();
            case "station": return Station();
            case "lab": return LabDoorView();
            case "ash": return AshView();
            case "ring": return RingTop();
            case "zone": return Zone34();
            case "entry": return EntryView();
            case "relief": return ReliefView();
            case "waterhole": return WaterholeView();
            default: return "unknown mode " + mode +
                " (canyon|waterfall|lake|station|lab|ash|ring|zone|entry|relief|waterhole)";
        }
    }

    static string Canyon34()
    {
        var c = B.Canyon;
        if (!c.valid) return "canyon invalid";
        Vector2 mid = Mean(c.axis);
        Vector2 d = SafeDir(c.lip - c.mouth);
        Vector2 perp = new Vector2(-d.y, d.x);
        float len = AxisLength(c);
        Vector2 camField = c.mouth - d * (len * 0.38f) + perp * (len * 0.18f);
        Vector3 target = Field(mid.x, B.SampleGround(mid.x, mid.y) + 8f, mid.y);
        Vector3 desired = Field(camField.x,
            B.SampleGround(camField.x, camField.y) + Mathf.Max(22f, len * 0.16f), camField.y);
        return PlaceClear("canyon", target, desired, 2f);
    }

    static string CascadeLip()
    {
        var c = B.Canyon;
        if (!c.valid) return "canyon invalid";
        Vector2 d = SafeDir(c.lip - c.mouth);
        Vector2 perp = new Vector2(-d.y, d.x);
        float len = AxisLength(c);
        Vector2 p = c.lip;
        Vector3 target = Field(p.x, B.SampleGround(p.x, p.y) + 3f, p.y);
        Vector2 camField = p + d * (len * 0.22f) + perp * (len * 0.08f);
        Vector3 desired = Field(camField.x,
            B.SampleGround(camField.x, camField.y) + Mathf.Max(20f, len * 0.12f), camField.y);
        return PlaceClear("waterfall", target, desired, 1.5f);
    }

    static string Lake()
    {
        var h = B.Hydro;
        if (h == null || h.lakeRadius <= 0f) return "no lake";
        var c = B.Canyon;
        Vector2 center = h.lakeCenter;
        Vector2 away = c.valid ? SafeDir(center - c.mouth)
            : SafeDir(center - new Vector2(B.Entry.x, B.Entry.z));
        if (away.sqrMagnitude < 1e-4f) away = Vector2.right;
        float dist = h.lakeRadius * 1.25f + 90f;
        Vector2 camField = center + away * dist;
        Vector3 target = Field(center.x, h.lakeLevel + 4f, center.y);
        Vector3 desired = Field(camField.x,
            B.SampleGround(camField.x, camField.y) +
            Mathf.Max(45f, h.lakeRadius * 0.35f), camField.y);
        return PlaceClear("lake", target, desired, 3f);
    }

    static string Station()
    {
        Vector3 n = B.LabNorthDoor;
        if (n == Vector3.zero) return "no north door";
        Vector3 target = Field(n.x, n.y + 2f, n.z);
        Vector3 desired = Field(n.x, n.y + 12f, n.z + 28f);
        return PlaceClear("station", target, desired, 1.5f);
    }

    static string LabDoorView()
    {
        Vector3 d = B.LabDoor;
        if (d == Vector3.zero) return "no lab door";
        // Снаружи, от леса к двери: остов станции в кадре как POI.
        Vector3 target = Field(d.x, d.y + 2f, d.z);
        Vector3 desired = Field(d.x + 26f, d.y + 14f, d.z - 34f);
        return PlaceClear("lab", target, desired, 1.5f);
    }

    static string AshView()
    {
        if (!B.HasAsh) return "no ash";
        var a = B.Ash;
        // Ясень 85 м: близкий кадр читается «конусом ствола». Целимся в середину
        // кроны и отходим так, чтобы дерево влезло целиком.
        Vector3 target = Field(a.pos.x, a.groundY + 40f, a.pos.y);
        Vector3 desired = Field(a.pos.x + 85f, a.groundY + 48f, a.pos.y - 95f);
        return PlaceClear("ash", target, desired, 4f);
    }

    static string RingTop()
    {
        EnsureCamera();
        float maxGround = B.SampleGround(0f, 0f);
        Vector3 target = Field(0f, maxGround, 0f);
        // ВЫШЕ купола нельзя: свод (радиус 1.4×Ø/2, апекс ~700 м) закрывает вид
        // сверху чёрным диском. Камера — ПОД сводом.
        Vector3 pos = target + Vector3.up * 240f;
        Cam.orthographic = true;
        Cam.orthographicSize = 1050f;
        Cam.nearClipPlane = 0.3f;
        Cam.farClipPlane = 6000f;
        Cam.transform.SetPositionAndRotation(pos, Quaternion.Euler(90f, 0f, 0f));
        Climate(pos);
        return $"ring top pos={pos} size=1050 fog={FogLine()}";
    }

    static string Zone34()
    {
        if (B.Zones == null || B.Zones.Count == 0) return "no zones";
        var z = B.Zones[0];
        float h = B.SampleGround(z.center.x, z.center.y);
        Vector3 center = Field(z.center.x, h + 2f, z.center.y);
        Vector3 desired = Field(z.center.x - 55f, h + 34f, z.center.y - 55f);
        return PlaceClear("zone", center, desired, 2f);
    }

    static string EntryView()
    {
        // Вход у подножия кольца: сама точка Entry лежит В толще портала — камера
        // отступает в чашу и смотрит через павильон, иначе кадр = тёмный камень.
        Vector2 entry = new Vector2(B.Entry.x, B.Entry.z);
        Vector2 dir = SafeDir(-entry);
        Vector2 camField = entry + dir * 12f;
        float gCam = B.SampleGround(camField.x, camField.y);
        Vector3 pos = Field(camField.x, gCam + 1.6f, camField.y);
        Vector3 target = Field(0f, B.SampleGround(0f, 0f) + 6f, 0f);
        Place(pos, target, 66f);
        Climate(pos);
        return $"entry eye pos={pos} target={target} fog={FogLine()}";
    }

    /// <summary>Самая высокая форма композиции S2 в 3/4: читается ли рельеф без разметки.</summary>
    static string ReliefView()
    {
        var forms = B.CompositionForms;
        if (forms == null || forms.Count == 0) return "no composition forms";
        DomeForm best = forms[0];
        foreach (var f in forms)
            if (f.height > best.height) best = f;
        float g = B.SampleGround(best.center.x, best.center.y);
        Vector3 target = Field(best.center.x, g + best.height * 0.5f, best.center.y);
        float dist = Mathf.Max(140f, best.height * 5f);
        Vector3 desired = Field(best.center.x + dist * 0.7f,
            g + Mathf.Max(30f, best.height * 1.2f), best.center.y - dist * 0.7f);
        return PlaceClear("relief " + best.name, target, desired, 3f);
    }

    /// <summary>Водопой: вода и подходы в 3/4.</summary>
    static string WaterholeView()
    {
        if (B.Waterholes == null || B.Waterholes.Count == 0) return "no waterholes";
        var z = B.Waterholes[0];
        float g = B.SampleGround(z.center.x, z.center.y);
        Vector3 target = Field(z.center.x, g + 1f, z.center.y);
        Vector3 desired = Field(z.center.x + 70f, g + 42f, z.center.y - 70f);
        return PlaceClear("waterhole", target, desired, 2f);
    }

    // ------------------------------------------------------------------
    // Проверка «висящих объектов»
    // ------------------------------------------------------------------

    /// <summary>Деревья и поды против НАРИСОВАННОГО пола (луч в MeshCollider чанка).</summary>
    public static string Audit()
    {
        var problem = Attach();
        if (problem != null) return problem;
        var pods = new List<Vector2>();
        if (B.CoverPods != null)
            foreach (var p in B.CoverPods) pods.Add(p.center);

        int podChecked = 0, podFloat = 0;
        float podWorst = float.MinValue;
        Vector3 podAt = Vector3.zero;
        int treeChecked = 0, treeFloat = 0;
        float treeWorst = float.MinValue;
        Vector3 treeAt = Vector3.zero;

        foreach (var go in SceneObjects())
        {
            if (go.name != "DomeFlora") continue;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) continue;
            var p = go.transform.position;
            var xz = new Vector2(p.x, p.z);
            bool isPod = false;
            foreach (var pp in pods)
                if ((xz - pp).sqrMagnitude < 0.25f) { isPod = true; break; }
            bool isTree = false;
            var cap = go.GetComponent<CapsuleCollider>();
            if (cap != null && Mathf.Approximately(cap.height, 14f)) isTree = true;
            if (!isPod && !isTree) continue;

            if (!Physics.Raycast(new Vector3(p.x, 800f, p.z), Vector3.down,
                    out var hit, 1600f, ~0, QueryTriggerInteraction.Ignore))
                continue;
            float rise = mr.bounds.min.y - hit.point.y;
            if (isPod)
            {
                podChecked++;
                if (rise > 0.35f) { podFloat++; if (rise > podWorst) { podWorst = rise; podAt = p; } }
            }
            else
            {
                treeChecked++;
                if (rise > 0.35f) { treeFloat++; if (rise > treeWorst) { treeWorst = rise; treeAt = p; } }
            }
        }
        return $"pods checked={podChecked} floating={podFloat} worst={podWorst:F2}m at {podAt} | " +
               $"trees checked={treeChecked} floating={treeFloat} worst={treeWorst:F2}m at {treeAt}";
    }

    /// <summary>Ленты POI против НАРИСОВАННОГО пола: провал (минус) и парение (плюс).</summary>
    public static string AuditPaths()
    {
        var problem = Attach();
        if (problem != null) return problem;
        var sb = new StringBuilder();
        foreach (var go in SceneObjects())
        {
            if (go.name != "SpacePaths" && go.name != "ForestRoads") continue;
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var mesh = mf.sharedMesh;
            var verts = mesh.vertices;
            int step = Mathf.Max(1, verts.Length / 400);
            int n = 0, buried = 0, floating = 0;
            float min = float.MaxValue, max = float.MinValue;
            Vector3 minAt = Vector3.zero;
            for (int i = 0; i < verts.Length; i += step)
            {
                Vector3 w = go.transform.TransformPoint(verts[i]);
                if (!Physics.Raycast(new Vector3(w.x, 800f, w.z), Vector3.down, out var hit, 1600f, ~0,
                        QueryTriggerInteraction.Ignore)) continue;
                float d = w.y - hit.point.y;
                n++;
                if (d < -0.15f) buried++;
                if (d > 0.25f) floating++;
                if (d < min) { min = d; minAt = w; }
                if (d > max) max = d;
            }
            sb.AppendLine($"{go.name}: samples={n} buried(<-0.15)={buried} floating(>0.25)={floating} " +
                          $"min={min:F2} at ({minAt.x:F0};{minAt.z:F0}) max={max:F2}");
        }
        return sb.Length > 0 ? sb.ToString() : "no path meshes";
    }

    // ------------------------------------------------------------------
    // Камера
    // ------------------------------------------------------------------

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

    static void Place(Vector3 pos, Vector3 target, float fov)
    {
        var cam = EnsureCamera();
        cam.orthographic = false;
        cam.fieldOfView = fov;
        cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(target - pos, Vector3.up));
    }

    static string PlaceClear(string label, Vector3 target, Vector3 desired, float radius)
    {
        var cam = EnsureCamera();
        float dist = Vector3.Distance(target, desired);
        float step = Mathf.Max(4f, dist * 0.10f);
        Vector3 pos = desired + Vector3.up * (10f * step);
        int raised = 10;
        for (int i = 0; i < 10; i++)
        {
            var candidate = desired + Vector3.up * (i * step);
            if (!Blocked(target, candidate, radius))
            {
                pos = candidate;
                raised = i;
                break;
            }
        }
        if (raised == 10) Debug.LogWarning($"[G7Shot] {label}: стена не отпускает — камера поднята на {raised * step:F0} м");
        cam.orthographic = false;
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 6000f;
        cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(target - pos, Vector3.up));
        Climate(pos);
        return $"{label} pos={pos} target={target} raised={raised * step:F0}m fog={FogLine()}";
    }

    static bool Blocked(Vector3 from, Vector3 to, float radius)
    {
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist < 1e-3f) return false;
        return Physics.SphereCast(from, radius, delta / dist, out _, dist, ~0,
            QueryTriggerInteraction.Ignore);
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

    // ------------------------------------------------------------------
    // Мелочи
    // ------------------------------------------------------------------

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

    static string V(Vector3 v) => $"({v.x:F1};{v.y:F1};{v.z:F1})";

    static Vector2 SafeDir(Vector2 v) =>
        v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.right;

    static Vector2 Mean(List<Vector2> pts)
    {
        Vector2 sum = Vector2.zero;
        for (int i = 0; i < pts.Count; i++) sum += pts[i];
        return sum / Mathf.Max(1, pts.Count);
    }

    static float AxisLength(DomeCanyon.State c)
    {
        float len = 0f;
        for (int i = 1; i < c.axis.Count; i++) len += Vector2.Distance(c.axis[i - 1], c.axis[i]);
        return Mathf.Max(1f, len);
    }

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 1e-6f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return Vector2.Distance(p, a + ab * t);
    }
}
