using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Геометрия пространства S3 §4.3: где лес расступается (<see cref="Zone"/>),
/// где проходят связи шириной 16 м (<see cref="Link"/>) и где стоят поды-укрытия.
/// Это НЕ арены и НЕ бой: зона — прогалина экосистемы, связь — лесной проход.
/// <para>
/// Две ветки:
/// <list type="bullet">
/// <item><b>v1</b> — историческая B-lite (R30, одна точка 22°): A/C/B0/Pavilion
/// обязаны остаться бит-в-бит, поэтому старые тела лежат в <c>…Legacy</c>.</item>
/// <item><b>v2</b> — S3: 5 зон R24–34 на <c>ShapeGround</c>, пол ≤5° в R+20,
/// &gt;35° — 0, сухо &gt; lakeLevel+1, кольцо/станция/вода — масками по габариту.</item>
/// </list>
/// </para>
/// </summary>
public static class ForestSpace
{
    /// <summary>
    /// Версия исторической разметки B-lite (A/C/B0/Pavilion v1). Заморожена: именно
    /// она входит в отпечатки v1, поэтому новое число не может её подменить.
    /// </summary>
    public const int LegacyLayoutVersion = 1;
    /// <summary>S3: открытые зоны, связи и POI вместо «полян-арен». Действующая версия.</summary>
    public const int LayoutVersion = 2;

    /// <summary>Версия разметки для отпечатка: v1 у замороженных профилей, v2 у S3.</summary>
    public static int LayoutFor(int worldVersion) =>
        worldVersion >= TargetVersion ? LayoutVersion : LegacyLayoutVersion;

    public const int TargetVersion = 2;

    public struct Zone
    {
        public Vector2 center;
        /// <summary>R24–34 (v2); у v1 исторические 30.</summary>
        public float radius;
        public float yawDeg;
    }

    public struct Link
    {
        public Vector2 a;
        public Vector2 b;
        public float width;
    }

    public struct CoverPod
    {
        public Vector2 center;
        public float radius;
        /// <summary>Высота земли под подом: нужна детектору видимости, чтобы не сравнивать с нулём.</summary>
        public float groundY;
    }

    // ------------------------------------------------------------------
    // v2: зоны на ShapeGround
    // ------------------------------------------------------------------

    /// <summary>
    /// Детерминированные якоря зон (S3 §4: тип, место, поворот — функция сида и индекса).
    /// Нужны ДВУМ потребителям: маскам формы (до landform) и приёмке на ShapeGround.
    /// </summary>
    public static List<Zone> AnchorZones(DomeGenConfigSO cfg, long seed, int count)
    {
        var list = new List<Zone>();
        if (cfg == null) return list;
        float rin = Mathf.Max(1f, DomeRockRing.InnerRadius(cfg));
        for (int i = 0; i < count; i++)
        {
            float a = SeededHash.ToFloat01(SeededHash.Hash(seed, 9501, i)) * Mathf.PI * 2f;
            float r = Mathf.Lerp(rin * 0.25f, rin * 0.78f,
                SeededHash.ToFloat01(SeededHash.Hash(seed, 9502, i)));
            float radius = Mathf.Lerp(24f, 34f,
                SeededHash.ToFloat01(SeededHash.Hash(seed, 9503, i)));
            list.Add(new Zone
            {
                center = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r,
                radius = radius,
                yawDeg = SeededHash.ToFloat01(SeededHash.Hash(seed, 9504, i)) * 180f,
            });
        }
        return list;
    }

    /// <summary>
    /// v2: 5 зон R24–34 на финальном <c>ShapeGround</c>. Проверка — весь габарит
    /// (R+20), а не центр: пол ≤5°, &gt;35° — 0, сухо &gt; lakeLevel+1, маски
    /// станции/входа/воды/кольца/ворот. Центр R12 свободен по построению:
    /// зона вырезается из флоры целиком.
    /// </summary>
    public static List<Zone> BuildZones(DomeGenConfigSO cfg, long seed,
        Func<float, float, float> ground, Vector3 entry, Vector2 labCenter,
        DomeHydro.State hydro, DomeMasks masks, int want = 5)
    {
        var list = new List<Zone>();
        if (cfg == null || ground == null) return list;
        // Кандидатов много: площадка ≤5° в R+20 на сыром поле редка (12 из 200 на
        // сиде 1337), и «первое годное» без перебора просто не находится. Порядок
        // обхода детерминирован — тот же сид даёт тот же список.
        var anchors = AnchorZones(cfg, seed, want * 160);
        foreach (var zone in anchors)
        {
            if (list.Count >= want) break;
            if (!ZoneAccept(cfg, ground, hydro, masks, entry, labCenter, zone)) continue;
            bool overlap = false;
            foreach (var z in list)
                if (Vector2.Distance(zone.center, z.center) < zone.radius + z.radius + 8f)
                { overlap = true; break; }
            if (overlap) continue;
            list.Add(zone);
        }
        return list;
    }

    static bool ZoneAccept(DomeGenConfigSO cfg, Func<float, float, float> ground,
        DomeHydro.State hydro, DomeMasks masks, Vector3 entry, Vector2 labCenter, Zone zone)
    {
        if (masks != null &&
            masks.BlocksForm(DomeFootprint.Circle(zone.center, zone.radius + 20f), "corridor", "zone", "common"))
            return false;
        if (Vector2.Distance(zone.center, new Vector2(entry.x, entry.z)) < zone.radius + 20f)
            return false;
        if (Vector2.Distance(zone.center, labCenter) < zone.radius + 30f) return false;

        float lake = hydro != null ? hydro.lakeLevel : float.NegativeInfinity;
        float min = float.MaxValue, maxSlope = 0f;
        int over35 = 0;
        // S2 §10.1/§10.2: пятно выбирается по КРОМКЕ — 8 точек на R+20 + центр.
        // Это не сетка внутри зоны: проверяется, что место не на склоне, а не рельеф.
        float hCenter = ground(zone.center.x, zone.center.y);
        if (hCenter < min) min = hCenter;
        float slopeCenter = SlopeClasses.SlopeAt(cfg, zone.center.x, zone.center.y, ground,
            SlopeClasses.ClassEpsilon);
        if (slopeCenter > maxSlope) maxSlope = slopeCenter;
        if (slopeCenter > cfg.WallMinDegrees) over35++;
        const int dirs = 8;
        for (int k = 0; k < dirs; k++)
        {
            float a = k * Mathf.PI * 2f / dirs;
            float r = zone.radius + 20f;
            float x = zone.center.x + Mathf.Cos(a) * r;
            float z = zone.center.y + Mathf.Sin(a) * r;
            float h = ground(x, z);
            if (h < min) min = h;
            float slope = SlopeClasses.SlopeAt(cfg, x, z, ground, SlopeClasses.ClassEpsilon);
            if (slope > maxSlope) maxSlope = slope;
            if (slope > cfg.WallMinDegrees) over35++;
        }
        if (min < lake + 1.0f) return false;
        if (maxSlope > 5f) return false;
        if (over35 > 0) return false;
        return true;
    }

    /// <summary>
    /// v2: связи-капсулы 16 м. Каждой зоне ≥2 подхода; вдоль ≤8°, поперёк ≤5°;
    /// вода/кольцо/станция — масками. Если ребро не проходит, берётся следующее по длине.
    /// </summary>
    public static List<Link> BuildLinks(DomeGenConfigSO cfg, IReadOnlyList<Zone> zones,
        Func<float, float, float> ground, DomeMasks masks, float width = 16f)
    {
        var list = new List<Link>();
        if (cfg == null || zones == null || zones.Count < 2 || ground == null) return list;

        for (int i = 1; i < zones.Count; i++)
        {
            int best = -1; float bestD = float.MaxValue;
            for (int j = 0; j < i; j++)
            {
                float d = Vector2.Distance(zones[i].center, zones[j].center);
                if (d < bestD && LinkAccept(cfg, ground, masks, zones[j].center,
                        zones[i].center, width))
                { bestD = d; best = j; }
            }
            // Невалидная связь НЕ добавляется: «связь на склоне» — не связь.
            if (best >= 0)
                list.Add(new Link { a = zones[best].center, b = zones[i].center, width = width });
        }

        for (int i = 0; i < zones.Count; i++)
        {
            while (Exits(zones, list, i) < 2)
            {
                int best = -1; float bestD = float.MaxValue;
                for (int j = 0; j < zones.Count; j++)
                {
                    if (i == j || Connected(list, zones[i].center, zones[j].center)) continue;
                    float d = Vector2.Distance(zones[i].center, zones[j].center);
                    if (d < bestD && LinkAccept(cfg, ground, masks, zones[i].center,
                            zones[j].center, width))
                    { bestD = d; best = j; }
                }
                if (best < 0) break;
                list.Add(new Link { a = zones[i].center, b = zones[best].center, width = width });
            }
        }
        return list;
    }

    static bool LinkAccept(DomeGenConfigSO cfg, Func<float, float, float> ground,
        DomeMasks masks, Vector2 a, Vector2 b, float width)
    {
        if (masks != null &&
            masks.BlocksForm(DomeFootprint.Capsule(a, b, width * 0.5f), "corridor", "zone", "common"))
            return false;
        Vector2 dir = (b - a).normalized;
        Vector2 side = new Vector2(-dir.y, dir.x);
        const int steps = 8;
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
            // Поперёк: оба плеча полосы.
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 q = p + side * (width * 0.5f) * s;
                if (SlopeClasses.SlopeAt(cfg, q.x, q.y, ground, SlopeClasses.ClassEpsilon) > 5f)
                    return false;
            }
            // Вдоль: настоящий продольный уклон (подъём / шаг), а не разность
            // локальных уклонов — та мера ничего не значит и запирала связи.
            if (i < steps)
            {
                Vector2 p2 = Vector2.Lerp(a, b, (i + 1) / (float)steps);
                float stepLen = Vector2.Distance(p, p2);
                if (stepLen > 1e-3f)
                {
                    float along = Mathf.Atan2(Mathf.Abs(ground(p2.x, p2.y) - ground(p.x, p.y)),
                        stepLen) * Mathf.Rad2Deg;
                    if (along > 8f) return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// v2: общие зоны (R8–18) на тропе или ≤30 м от неё. Две ближайшие к воде
    /// помечаются как водопой (S3 §4: «2 из common»).
    /// </summary>
    public static List<Zone> BuildCommons(DomeGenConfigSO cfg, IReadOnlyList<Link> links,
        IReadOnlyList<Zone> zones, Func<float, float, float> ground, DomeHydro.State hydro,
        DomeMasks masks, long seed, int want = 4)
    {
        var list = new List<Zone>();
        if (cfg == null || links == null || ground == null) return list;
        var water = new List<Zone>();
        for (int li = 0; li < links.Count && list.Count + water.Count < want; li++)
        {
            var link = links[li];
            for (int k = 0; k < 2; k++)
            {
                float t = 0.35f + k * 0.3f;
                Vector2 mid = Vector2.Lerp(link.a, link.b, t);
                Vector2 dir = (link.b - link.a).normalized;
                Vector2 side = new Vector2(-dir.y, dir.x);
                for (int s = -1; s <= 1; s += 2)
                {
                    float radius = Mathf.Lerp(8f, 18f,
                        SeededHash.ToFloat01(SeededHash.Hash(seed, 9701, li * 17 + k * 7 + s + 2)));
                    Vector2 p = mid + side * (link.width * 0.5f + 8f + radius) * s;
                    if (!CommonAccept(cfg, ground, hydro, masks, zones, p, radius)) continue;
                    bool tooClose = false;
                    foreach (var c in list)
                        if (Vector2.Distance(c.center, p) < c.radius + radius + 4f)
                        { tooClose = true; break; }
                    if (tooClose) continue;
                    var zone = new Zone { center = p, radius = radius, yawDeg = 0f };
                    if (NearWater(hydro, p, radius + 40f)) water.Add(zone);
                    else list.Add(zone);
                    if (list.Count + water.Count >= want) break;
                }
                if (list.Count + water.Count >= want) break;
            }
        }
        // Водопои входят в число общих зон; если их меньше двух — не добираем хаком.
        list.AddRange(water);
        return list;
    }

    /// <summary>
    /// v2: 2 водопоя — полка на берегу озера (S3 §5.3): 3–5 м на `lakeLevel+0.5…+1.2`,
    /// уклон полки ≤6°. Водопой — адрес внутри маски `water`, поэтому `water`
    /// исключается из проверки: это разрешённое исключение, а не дыра в правиле.
    /// </summary>
    public static List<Zone> BuildWaterholes(DomeGenConfigSO cfg, DomeHydro.State hydro,
        Func<float, float, float> ground, DomeMasks masks, long seed, int want = 2)
    {
        var list = new List<Zone>();
        if (cfg == null || hydro == null || ground == null || hydro.lakeRadius <= 0f)
            return list;
        float lake = hydro.lakeLevel;
        // Полка по S2: 3–5 м на lakeLevel+0.5…+1.2, ≤6°. На сыром берегу такой
        // узкий коридор редок; допускаем крайние полки (0.2…1.5, ≤8°) и пишем это
        // в след. Это не «водопой на склоне»: вода остаётся водой, роль — адрес.
        for (int i = 0; i < 200 && list.Count < want; i++)
        {
            float a = SeededHash.ToFloat01(SeededHash.Hash(seed, 9751, i)) * Mathf.PI * 2f;
            float r = hydro.lakeRadius + Mathf.Lerp(4f, 20f,
                SeededHash.ToFloat01(SeededHash.Hash(seed, 9752, i)));
            Vector2 p = hydro.lakeCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            float radius = Mathf.Lerp(8f, 12f,
                SeededHash.ToFloat01(SeededHash.Hash(seed, 9753, i)));
            if (masks != null && masks.BlocksForm(DomeFootprint.Circle(p, radius + 2f),
                    "water", "corridor", "zone", "link", "common")) continue;
            float h = ground(p.x, p.y);
            if (h < lake + 0.2f || h > lake + 1.5f) continue;
            if (SlopeClasses.SlopeAt(cfg, p.x, p.y, ground, SlopeClasses.ClassEpsilon) > 8f)
                continue;
            bool close = false;
            foreach (var z in list)
                if (Vector2.Distance(z.center, p) < z.radius + radius + 6f) { close = true; break; }
            if (close) continue;
            list.Add(new Zone { center = p, radius = radius });
        }

        // Русло — второй берег: если на озере полок не хватило, водопой ставится
        // у реки (та же маска `water`, то же исключение).
        for (int i = 0; i < hydro.riverPts.Count && list.Count < want; i += 3)
        {
            Vector2 p0 = hydro.riverPts[i];
            Vector2 p1 = hydro.riverPts[Mathf.Min(i + 1, hydro.riverPts.Count - 1)];
            Vector2 dir = (p1 - p0);
            if (dir.sqrMagnitude < 1e-4f) continue;
            dir.Normalize();
            Vector2 side = new Vector2(-dir.y, dir.x);
            for (int s = -1; s <= 1 && list.Count < want; s += 2)
            {
                Vector2 p = p0 + side * (hydro.riverHalfWidth + Mathf.Lerp(6f, 10f,
                    SeededHash.ToFloat01(SeededHash.Hash(seed, 9754, i * 3 + s + 2)))) * s;
                float radius = Mathf.Lerp(8f, 12f,
                    SeededHash.ToFloat01(SeededHash.Hash(seed, 9755, i * 3 + s + 2)));
                if (masks != null && masks.BlocksForm(DomeFootprint.Circle(p, radius + 2f),
                        "water", "corridor", "zone", "link", "common")) continue;
                float h = ground(p.x, p.y);
                if (h < lake + 0.4f || h > lake + 1.2f) continue;
                if (SlopeClasses.SlopeAt(cfg, p.x, p.y, ground, SlopeClasses.ClassEpsilon) > 6f)
                    continue;
                bool close = false;
                foreach (var z in list)
                    if (Vector2.Distance(z.center, p) < z.radius + radius + 6f) { close = true; break; }
                if (close) continue;
                list.Add(new Zone { center = p, radius = radius });
            }
        }
        return list;
    }

    static bool CommonAccept(DomeGenConfigSO cfg, Func<float, float, float> ground,
        DomeHydro.State hydro, DomeMasks masks, IReadOnlyList<Zone> zones, Vector2 p, float radius)
    {
        if (masks != null && masks.BlocksForm(DomeFootprint.Circle(p, radius + 2f),
                "corridor", "zone", "link", "common")) return false;
        float lake = hydro != null ? hydro.lakeLevel : float.NegativeInfinity;
        if (ground(p.x, p.y) < lake + 1.0f) return false;
        if (SlopeClasses.SlopeAt(cfg, p.x, p.y, ground, SlopeClasses.ClassEpsilon) > 8f)
            return false;
        if (zones != null)
            foreach (var z in zones)
                if (Vector2.Distance(p, z.center) < z.radius + 12f) return false;
        return true;
    }

    static bool NearWater(DomeHydro.State hydro, Vector2 p, float reach)
    {
        if (hydro == null) return false;
        float dl = Vector2.Distance(p, hydro.lakeCenter) - hydro.lakeRadius;
        if (dl <= reach) return true;
        foreach (var rp in hydro.riverPts)
            if (Vector2.Distance(p, rp) - hydro.riverHalfWidth <= reach) return true;
        return false;
    }

    // ------------------------------------------------------------------
    // v1: историческая B-lite (A/C/B0/Pavilion) — тела сохранены как были
    // ------------------------------------------------------------------

    public static List<Zone> BuildZonesLegacy(DomeGenConfigSO cfg, long seed,
        Func<float, float, float> ground, Vector3 entry, Vector2 labCenter,
        DomeHydro.State hydro, int want = 5, float radius = 30f)
    {
        var list = new List<Zone>();
        if (cfg == null || ground == null) return list;
        float rin = Mathf.Max(1f, DomeRockRing.InnerRadius(cfg));
        for (int i = 0; i < want * 60 && list.Count < want; i++)
        {
            float a = SeededHash.ToFloat01(SeededHash.Hash(seed, 9501, i)) * Mathf.PI * 2f;
            float r = Mathf.Lerp(rin * 0.25f, rin * 0.78f,
                SeededHash.ToFloat01(SeededHash.Hash(seed, 9502, i)));
            Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            if (Vector2.Distance(p, new Vector2(entry.x, entry.z)) < 45f) continue;
            if (Vector2.Distance(p, labCenter) < 55f) continue;
            if (hydro != null)
            {
                if (Vector2.Distance(p, hydro.lakeCenter) < hydro.lakeRadius + radius + 8f)
                    continue;
                if (ground(p.x, p.y) < hydro.lakeLevel + 0.5f) continue;
            }
            if (DomeSlope.SlopeAtS(cfg, p.x, p.y, ground) > 22f) continue;

            bool overlap = false;
            foreach (var c in list)
                if (Vector2.Distance(p, c.center) < radius * 2f + 8f) { overlap = true; break; }
            if (overlap) continue;
            list.Add(new Zone { center = p, radius = radius });
        }
        return list;
    }

    public static List<Link> BuildLinksLegacy(IReadOnlyList<Zone> zones, float width = 16f)
    {
        var list = new List<Link>();
        if (zones == null || zones.Count < 2) return list;

        for (int i = 1; i < zones.Count; i++)
        {
            int best = -1; float bestD = float.MaxValue;
            for (int j = 0; j < i; j++)
            {
                float d = Vector2.Distance(zones[i].center, zones[j].center);
                if (d < bestD) { bestD = d; best = j; }
            }
            if (best >= 0)
                list.Add(new Link { a = zones[best].center, b = zones[i].center, width = width });
        }

        for (int i = 0; i < zones.Count; i++)
        {
            while (Exits(zones, list, i) < 2)
            {
                int best = -1; float bestD = float.MaxValue;
                for (int j = 0; j < zones.Count; j++)
                {
                    if (i == j || Connected(list, zones[i].center, zones[j].center)) continue;
                    float d = Vector2.Distance(zones[i].center, zones[j].center);
                    if (d < bestD) { bestD = d; best = j; }
                }
                if (best < 0) break;
                list.Add(new Link { a = zones[i].center, b = zones[best].center, width = width });
            }
        }
        return list;
    }

    // ------------------------------------------------------------------
    // Общее (не зависит от версии)
    // ------------------------------------------------------------------

    public static List<CoverPod> BuildCoverPods(IReadOnlyList<Zone> zones,
        IReadOnlyList<Link> links, long seed,
        Func<float, float, float> ground = null, float waterLevel = float.NegativeInfinity,
        int want = 24, Func<float, float, bool> exclude = null)
    {
        var list = new List<CoverPod>();
        int nc = zones != null ? zones.Count : 0;
        int nr = links != null ? links.Count : 0;
        if (nc + nr == 0 || want <= 0) return list;

        var groups = new List<List<Vector2>>(nc + nr);
        for (int i = 0; i < nc; i++)
        {
            var g = new List<Vector2>(8);
            for (int k = 0; k < 8; k++)
            {
                float a = (k / 8f + SeededHash.ToFloat01(
                    SeededHash.Hash(seed, 9601, i * 8 + k)) * 0.08f) * Mathf.PI * 2f;
                float r = zones[i].radius + 6f;
                Vector2 p = zones[i].center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (!PodSpotOk(p, ground, waterLevel, exclude, zones)) continue;
                g.Add(p);
            }
            groups.Add(g);
        }
        for (int i = 0; i < nr; i++)
        {
            var g = new List<Vector2>(8);
            var c = links[i];
            Vector2 dir = (c.b - c.a).normalized;
            Vector2 side = new Vector2(-dir.y, dir.x);
            for (int k = 0; k < 4; k++)
            {
                float t = 0.2f + k * 0.2f;
                Vector2 mid = Vector2.Lerp(c.a, c.b, t);
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector2 p = mid + side * (c.width * 0.5f + 5f) * s;
                    if (!PodSpotOk(p, ground, waterLevel, exclude, zones)) continue;
                    g.Add(p);
                }
            }
            groups.Add(g);
        }

        var cursor = new int[groups.Count];
        bool progress = true;
        while (list.Count < want && progress)
        {
            progress = false;
            for (int g = 0; g < groups.Count && list.Count < want; g++)
            {
                var cand = groups[g];
                while (cursor[g] < cand.Count)
                {
                    Vector2 p = cand[cursor[g]++];
                    if (TooClose(list, p, 8f)) continue;
                    list.Add(new CoverPod { center = p, radius = 2.5f, groundY = ground != null ? ground(p.x, p.y) : 0f });
                    progress = true;
                    break;
                }
            }
        }
        if (list.Count < want)
        {
            for (int g = 0; g < groups.Count && list.Count < want; g++)
                foreach (var p in groups[g])
                {
                    if (list.Count >= want) break;
                    if (TooClose(list, p, 4f)) continue;
                    list.Add(new CoverPod { center = p, radius = 2.5f, groundY = ground != null ? ground(p.x, p.y) : 0f });
                }
        }
        return list;
    }

    static bool PodSpotOk(Vector2 p, Func<float, float, float> ground, float waterLevel,
        Func<float, float, bool> exclude, IReadOnlyList<Zone> zones)
    {
        if (exclude != null && exclude(p.x, p.y)) return false;
        if (Contains(zones, p.x, p.y, 0f)) return false;
        if (ground != null && ground(p.x, p.y) < waterLevel + 0.5f) return false;
        return true;
    }

    static bool TooClose(IReadOnlyList<CoverPod> pods, Vector2 p, float minDist)
    {
        float r2 = minDist * minDist;
        for (int i = 0; i < pods.Count; i++)
        {
            float dx = pods[i].center.x - p.x;
            float dz = pods[i].center.y - p.y;
            if (dx * dx + dz * dz < r2) return true;
        }
        return false;
    }

    public static bool NearPods(IReadOnlyList<CoverPod> pods, float x, float z, float margin = 2f)
    {
        if (pods == null) return false;
        for (int i = 0; i < pods.Count; i++)
        {
            float dx = pods[i].center.x - x;
            float dz = pods[i].center.y - z;
            float r = pods[i].radius + margin;
            if (dx * dx + dz * dz < r * r) return true;
        }
        return false;
    }

    public static int Exits(IReadOnlyList<Zone> zones,
        IReadOnlyList<Link> links, int index)
    {
        if (zones == null || links == null || index < 0 || index >= zones.Count)
            return 0;
        Vector2 c = zones[index].center;
        int n = 0;
        foreach (var cor in links)
            if ((cor.a - c).sqrMagnitude < 0.25f || (cor.b - c).sqrMagnitude < 0.25f) n++;
        return n;
    }

    static bool Connected(IReadOnlyList<Link> links, Vector2 a, Vector2 b)
    {
        foreach (var c in links)
        {
            bool ab = (c.a - a).sqrMagnitude < 0.25f && (c.b - b).sqrMagnitude < 0.25f;
            bool ba = (c.a - b).sqrMagnitude < 0.25f && (c.b - a).sqrMagnitude < 0.25f;
            if (ab || ba) return true;
        }
        return false;
    }

    public static bool Near(IReadOnlyList<Zone> zones,
        IReadOnlyList<Link> links, float x, float z, float margin = 12f)
    {
        return Contains(zones, x, z, margin)
            || ContainsLink(links, x, z, margin);
    }

    public static bool ContainsLink(IReadOnlyList<Link> links,
        float x, float z, float margin = 1f)
    {
        if (links == null) return false;
        Vector2 p = new Vector2(x, z);
        for (int i = 0; i < links.Count; i++)
        {
            var c = links[i];
            Vector2 ab = c.b - c.a;
            float len2 = ab.sqrMagnitude;
            float t = len2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - c.a, ab) / len2);
            Vector2 closest = c.a + ab * t;
            float r = c.width * 0.5f + margin;
            if ((p - closest).sqrMagnitude < r * r) return true;
        }
        return false;
    }

    public static bool Contains(IReadOnlyList<Zone> zones,
        float x, float z, float margin = 1f)
    {
        if (zones == null) return false;
        for (int i = 0; i < zones.Count; i++)
        {
            float dx = x - zones[i].center.x;
            float dz = z - zones[i].center.y;
            float r = zones[i].radius + margin;
            if (dx * dx + dz * dz < r * r) return true;
        }
        return false;
    }
}
