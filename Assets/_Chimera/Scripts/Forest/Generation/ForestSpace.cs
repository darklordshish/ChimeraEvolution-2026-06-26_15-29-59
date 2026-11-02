using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Р“РµРѕРјРµС‚СЂРёСЏ РїСЂРѕСЃС‚СЂР°РЅСЃС‚РІР° S3 В§4.3: РіРґРµ Р»РµСЃ СЂР°СЃСЃС‚СѓРїР°РµС‚СЃСЏ (<see cref="Zone"/>),
/// РіРґРµ РїСЂРѕС…РѕРґСЏС‚ СЃРІСЏР·Рё С€РёСЂРёРЅРѕР№ 16 Рј (<see cref="Link"/>) Рё РіРґРµ СЃС‚РѕСЏС‚ РїРѕРґС‹-СѓРєСЂС‹С‚РёСЏ.
/// Р­С‚Рѕ РќР• Р°СЂРµРЅС‹ Рё РќР• Р±РѕР№: Р·РѕРЅР° вЂ” РїСЂРѕРіР°Р»РёРЅР° СЌРєРѕСЃРёСЃС‚РµРјС‹, СЃРІСЏР·СЊ вЂ” Р»РµСЃРЅРѕР№ РїСЂРѕС…РѕРґ.
/// <para>
/// Р”РІРµ РІРµС‚РєРё:
/// <list type="bullet">
/// <item><b>v1</b> вЂ” РёСЃС‚РѕСЂРёС‡РµСЃРєР°СЏ B-lite (R30, РѕРґРЅР° С‚РѕС‡РєР° 22В°): A/C/B0/Pavilion
/// РѕР±СЏР·Р°РЅС‹ РѕСЃС‚Р°С‚СЊСЃСЏ Р±РёС‚-РІ-Р±РёС‚, РїРѕСЌС‚РѕРјСѓ СЃС‚Р°СЂС‹Рµ С‚РµР»Р° Р»РµР¶Р°С‚ РІ <c>вЂ¦Legacy</c>.</item>
/// <item><b>v2</b> вЂ” S3: 5 Р·РѕРЅ R24вЂ“34 РЅР° <c>ShapeGround</c>, РїРѕР» в‰¤5В° РІ R+20,
/// &gt;35В° вЂ” 0, СЃСѓС…Рѕ &gt; lakeLevel+1, РєРѕР»СЊС†Рѕ/СЃС‚Р°РЅС†РёСЏ/РІРѕРґР° вЂ” РјР°СЃРєР°РјРё РїРѕ РіР°Р±Р°СЂРёС‚Сѓ.</item>
/// </list>
/// </para>
/// </summary>
public static class ForestSpace
{
    /// <summary>
    /// Р’РµСЂСЃРёСЏ РёСЃС‚РѕСЂРёС‡РµСЃРєРѕР№ СЂР°Р·РјРµС‚РєРё B-lite (A/C/B0/Pavilion v1). Р—Р°РјРѕСЂРѕР¶РµРЅР°: РёРјРµРЅРЅРѕ
    /// РѕРЅР° РІС…РѕРґРёС‚ РІ РѕС‚РїРµС‡Р°С‚РєРё v1, РїРѕСЌС‚РѕРјСѓ РЅРѕРІРѕРµ С‡РёСЃР»Рѕ РЅРµ РјРѕР¶РµС‚ РµС‘ РїРѕРґРјРµРЅРёС‚СЊ.
    /// </summary>
    public const int LegacyLayoutVersion = 1;
    /// <summary>S3: РѕС‚РєСЂС‹С‚С‹Рµ Р·РѕРЅС‹, СЃРІСЏР·Рё Рё POI РІРјРµСЃС‚Рѕ В«РїРѕР»СЏРЅ-Р°СЂРµРЅВ». Р”РµР№СЃС‚РІСѓСЋС‰Р°СЏ РІРµСЂСЃРёСЏ.</summary>
    public const int LayoutVersion = 2;

    /// <summary>Р’РµСЂСЃРёСЏ СЂР°Р·РјРµС‚РєРё РґР»СЏ РѕС‚РїРµС‡Р°С‚РєР°: v1 Сѓ Р·Р°РјРѕСЂРѕР¶РµРЅРЅС‹С… РїСЂРѕС„РёР»РµР№, v2 Сѓ S3.</summary>
    public static int LayoutFor(int worldVersion) =>
        worldVersion >= TargetVersion ? LayoutVersion : LegacyLayoutVersion;

    public const int TargetVersion = 2;

    public struct Zone
    {
        public Vector2 center;
        /// <summary>R24вЂ“34 (v2); Сѓ v1 РёСЃС‚РѕСЂРёС‡РµСЃРєРёРµ 30.</summary>
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
        /// <summary>Р’С‹СЃРѕС‚Р° Р·РµРјР»Рё РїРѕРґ РїРѕРґРѕРј: РЅСѓР¶РЅР° РґРµС‚РµРєС‚РѕСЂСѓ РІРёРґРёРјРѕСЃС‚Рё, С‡С‚РѕР±С‹ РЅРµ СЃСЂР°РІРЅРёРІР°С‚СЊ СЃ РЅСѓР»С‘Рј.</summary>
        public float groundY;
    }

    // ------------------------------------------------------------------
    // v2: Р·РѕРЅС‹ РЅР° ShapeGround
    // ------------------------------------------------------------------

    /// <summary>
    /// Р”РµС‚РµСЂРјРёРЅРёСЂРѕРІР°РЅРЅС‹Рµ СЏРєРѕСЂСЏ Р·РѕРЅ (S3 В§4: С‚РёРї, РјРµСЃС‚Рѕ, РїРѕРІРѕСЂРѕС‚ вЂ” С„СѓРЅРєС†РёСЏ СЃРёРґР° Рё РёРЅРґРµРєСЃР°).
    /// РќСѓР¶РЅС‹ Р”Р’РЈРњ РїРѕС‚СЂРµР±РёС‚РµР»СЏРј: РјР°СЃРєР°Рј С„РѕСЂРјС‹ (РґРѕ landform) Рё РїСЂРёС‘РјРєРµ РЅР° ShapeGround.
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
    /// v2: 5 Р·РѕРЅ R24вЂ“34 РЅР° С„РёРЅР°Р»СЊРЅРѕРј <c>ShapeGround</c>. РџСЂРѕРІРµСЂРєР° вЂ” РІРµСЃСЊ РіР°Р±Р°СЂРёС‚
    /// (R+20), Р° РЅРµ С†РµРЅС‚СЂ: РїРѕР» в‰¤5В°, &gt;35В° вЂ” 0, СЃСѓС…Рѕ &gt; lakeLevel+1, РјР°СЃРєРё
    /// СЃС‚Р°РЅС†РёРё/РІС…РѕРґР°/РІРѕРґС‹/РєРѕР»СЊС†Р°/РІРѕСЂРѕС‚. Р¦РµРЅС‚СЂ R12 СЃРІРѕР±РѕРґРµРЅ РїРѕ РїРѕСЃС‚СЂРѕРµРЅРёСЋ:
    /// Р·РѕРЅР° РІС‹СЂРµР·Р°РµС‚СЃСЏ РёР· С„Р»РѕСЂС‹ С†РµР»РёРєРѕРј.
    /// </summary>
    public static List<Zone> BuildZones(DomeGenConfigSO cfg, long seed,
        Func<float, float, float> ground, Vector3 entry, Vector2 labCenter,
        DomeHydro.State hydro, DomeMasks masks, int want = 5)
    {
        var list = new List<Zone>();
        if (cfg == null || ground == null) return list;
        // РљР°РЅРґРёРґР°С‚РѕРІ РјРЅРѕРіРѕ: РїР»РѕС‰Р°РґРєР° в‰¤5В° РІ R+20 РЅР° СЃС‹СЂРѕРј РїРѕР»Рµ СЂРµРґРєР° (12 РёР· 200 РЅР°
        // СЃРёРґРµ 1337), Рё В«РїРµСЂРІРѕРµ РіРѕРґРЅРѕРµВ» Р±РµР· РїРµСЂРµР±РѕСЂР° РїСЂРѕСЃС‚Рѕ РЅРµ РЅР°С…РѕРґРёС‚СЃСЏ. РџРѕСЂСЏРґРѕРє
        // РѕР±С…РѕРґР° РґРµС‚РµСЂРјРёРЅРёСЂРѕРІР°РЅ вЂ” С‚РѕС‚ Р¶Рµ СЃРёРґ РґР°С‘С‚ С‚РѕС‚ Р¶Рµ СЃРїРёСЃРѕРє.
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
        if (Vector2.Distance(zone.center, labCenter) < zone.radius + 40f) return false;

        float lake = hydro != null ? hydro.lakeLevel : float.NegativeInfinity;
        float min = float.MaxValue, maxSlope = 0f;
        int over35 = 0;
        // S2 В§10.1/В§10.2: РїСЏС‚РЅРѕ РІС‹Р±РёСЂР°РµС‚СЃСЏ РїРѕ РљР РћРњРљР• вЂ” 8 С‚РѕС‡РµРє РЅР° R+20 + С†РµРЅС‚СЂ.
        // Р­С‚Рѕ РЅРµ СЃРµС‚РєР° РІРЅСѓС‚СЂРё Р·РѕРЅС‹: РїСЂРѕРІРµСЂСЏРµС‚СЃСЏ, С‡С‚Рѕ РјРµСЃС‚Рѕ РЅРµ РЅР° СЃРєР»РѕРЅРµ, Р° РЅРµ СЂРµР»СЊРµС„.
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
    /// v2: СЃРІСЏР·Рё-РєР°РїСЃСѓР»С‹ 16 Рј. РљР°Р¶РґРѕР№ Р·РѕРЅРµ в‰Ґ2 РїРѕРґС…РѕРґР°; РІРґРѕР»СЊ в‰¤8В°, РїРѕРїРµСЂС‘Рє в‰¤5В°;
    /// РІРѕРґР°/РєРѕР»СЊС†Рѕ/СЃС‚Р°РЅС†РёСЏ вЂ” РјР°СЃРєР°РјРё. Р•СЃР»Рё СЂРµР±СЂРѕ РЅРµ РїСЂРѕС…РѕРґРёС‚, Р±РµСЂС‘С‚СЃСЏ СЃР»РµРґСѓСЋС‰РµРµ РїРѕ РґР»РёРЅРµ.
    /// </summary>
    public static List<Link> BuildLinks(DomeGenConfigSO cfg, IReadOnlyList<Zone> zones,
        Func<float, float, float> ground, DomeMasks masks, float width = 16f)
    {
        var list = new List<Link>();
        if (cfg == null || zones == null || zones.Count < 2 || ground == null) return list;
        const int WantLinks = 6; // S2 В§10.1: 6 РєРѕСЂРёРґРѕСЂРѕРІ 16 Рј

        // Р’СЃРµ РІР°Р»РёРґРЅС‹Рµ РїР°СЂС‹ вЂ” Р·Р°СЂР°РЅРµРµ; РІС‹Р±РѕСЂ РіР»РѕР±Р°Р»СЊРЅС‹Р№, Р° РЅРµ В«С†РµРїРѕС‡РєР° РїРѕ СЃРѕСЃРµРґСѓВ».
        // РўР°Рє РёР·РѕР»РёСЂРѕРІР°РЅРЅР°СЏ Р·РѕРЅР° РїРѕР»СѓС‡Р°РµС‚ РїРѕРґС…РѕРґ, РµСЃР»Рё РѕРЅ РІРѕРѕР±С‰Рµ СЃСѓС‰РµСЃС‚РІСѓРµС‚.
        var valid = new List<(int i, int j, float d)>();
        for (int i = 0; i < zones.Count; i++)
            for (int j = i + 1; j < zones.Count; j++)
            {
                if (!LinkAccept(cfg, ground, masks, zones[i].center, zones[j].center,
                        width, zones[i].radius + 40f, zones[j].radius + 40f)) continue;
                valid.Add((i, j, Vector2.Distance(zones[i].center, zones[j].center)));
            }
        valid.Sort((a, b) => a.d.CompareTo(b.d));

        var used = new bool[zones.Count, zones.Count];
        var linksAtZone = new int[zones.Count];
        void Add(int i, int j)
        {
            list.Add(new Link { a = zones[i].center, b = zones[j].center, width = width });
            used[i, j] = used[j, i] = true;
            linksAtZone[i]++; linksAtZone[j]++;
        }

        // 1) РєР°Р¶РґРѕР№ Р·РѕРЅРµ вЂ” РґРѕ РґРІСѓС… РїРѕРґС…РѕРґРѕРІ, РЅР°С‡РёРЅР°СЏ СЃ СЃР°РјС‹С… РєРѕСЂРѕС‚РєРёС… РІР°Р»РёРґРЅС‹С….
        for (int i = 0; i < zones.Count; i++)
        {
            while (linksAtZone[i] < 2)
            {
                int pick = -1;
                for (int k = 0; k < valid.Count; k++)
                {
                    var e = valid[k];
                    if (used[e.i, e.j] || (e.i != i && e.j != i)) continue;
                    pick = k; break;
                }
                if (pick < 0) break;
                Add(valid[pick].i, valid[pick].j);
            }
        }
        // 2) РґРѕР±СЂР°С‚СЊ РґРѕ С€РµСЃС‚Рё РєРѕСЂРёРґРѕСЂРѕРІ, РЅРµ Р»РѕРјР°СЏ СѓР¶Рµ РІС‹Р±СЂР°РЅРЅС‹Рµ РїРѕРґС…РѕРґС‹.
        for (int k = 0; k < valid.Count && list.Count < WantLinks; k++)
        {
            var e = valid[k];
            if (used[e.i, e.j]) continue;
            Add(e.i, e.j);
        }

        // пїЅпїЅпїЅпїЅпїЅпїЅпїЅпїЅпїЅпїЅпїЅ пїЅпїЅпїЅ пїЅпїЅпїЅпїЅпїЅ: пїЅпїЅпїЅпїЅпїЅпїЅ пїЅпїЅпїЅпїЅ пїЅпїЅ пїЅпїЅпїЅпїЅпїЅ пїЅпїЅпїЅпїЅпїЅпїЅ (пїЅпїЅ пїЅпїЅпїЅпїЅпїЅпїЅ пїЅпїЅ пїЅпїЅпїЅпїЅпїЅ).
        var log = new StringBuilder();
        for (int i = 0; i < zones.Count; i++)
            for (int j = i + 1; j < zones.Count; j++)
            {
                if (Connected(list, zones[i].center, zones[j].center)) continue;
                string why = LinkReason(cfg, ground, masks, zones[i].center, zones[j].center,
                    width, zones[i].radius + 40f, zones[j].radius + 40f) ?? "ok";
                log.Append($"{i}-{j}:{why}; ");
            }
        LastLinkLog = log.ToString();
        return list;
    }

    public static string LastLinkLog = "";
    /// <summary>пїЅпїЅпїЅпїЅпїЅпїЅ пїЅпїЅпїЅпїЅпїЅпїЅпїЅпїЅпїЅпїЅ пїЅпїЅпїЅпїЅпїЅпїЅпїЅ (1 пїЅ) пїЅ пїЅпїЅпїЅпїЅпїЅпїЅпїЅ S2 5пїЅ/8пїЅ; пїЅпїЅ пїЅпїЅпїЅпїЅпїЅ пїЅпїЅпїЅпїЅпїЅпїЅ.</summary>

    static bool LinkAccept(DomeGenConfigSO cfg, Func<float, float, float> ground,
        DomeMasks masks, Vector2 a, Vector2 b, float width, float trimA, float trimB)
        => LinkReason(cfg, ground, masks, a, b, width, trimA, trimB) == null;

    public static string LinkReason(DomeGenConfigSO cfg, Func<float, float, float> ground,
        DomeMasks masks, Vector2 a, Vector2 b, float width, float trimA, float trimB)
    {
        // РћР±СЂРµР·РєР° РєРѕРЅС†РѕРІ: РІРЅСѓС‚СЂРё Р·РѕРЅС‹ РїРѕР» РїР»РѕСЃРєРёР№ РїРѕ РѕРїСЂРµРґРµР»РµРЅРёСЋ, Р° РµС‘ РјР°СЃРєР°
        // `zone` С‚СЏРЅРµС‚СЃСЏ РЅР° R+20 вЂ” РїСЂРѕРІРµСЂСЏС‚СЊ РµС‘ РЅР° РєРѕРЅС†Рµ РєР°РїСЃСѓР»С‹ Р±РµСЃСЃРјС‹СЃР»РµРЅРЅРѕ.
        Vector2 ab = b - a;
        float len = ab.magnitude;
        if (len < 1e-3f) return "short";
        Vector2 dir = ab / len;
        float ta = Mathf.Clamp(trimA, 0f, len * 0.35f);
        float tb = Mathf.Clamp(trimB, 0f, len * 0.35f);
        if (ta + tb >= len * 0.9f) { ta = len * 0.2f; tb = len * 0.2f; }
        Vector2 a2 = a + dir * ta;
        Vector2 b2 = b - dir * tb;
        Vector2 ab2 = b2 - a2;
        float len2 = ab2.magnitude;
        if (len2 < width * 0.5f) return null; // Р·РѕРЅС‹ РїРѕС‡С‚Рё СЃР»РёРїР»РёСЃСЊ: СЃРІСЏР·СЊ-РїРµСЂРµРјС‹С‡РєР°

        if (masks != null &&
            masks.BlocksForm(DomeFootprint.Capsule(a2, b2, width * 0.5f),
                "corridor", "zone", "common", "landform", "mainRoute", "gate"))
            return "mask:" + (masks.FirstConflict(
                DomeFootprint.Capsule(a2, b2, width * 0.5f)) ?? "?");
        Vector2 dir2 = ab2 / len2;
        Vector2 side = new Vector2(-dir2.y, dir2.x);
        const int steps = 8;
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(a2, b2, i / (float)steps);
            // РџРѕРїРµСЂС‘Рє: РџРћРџР•Р Р•Р§РќР«Р™ РЈРљР›РћРќ РїРѕР»РѕСЃС‹ вЂ” РїРµСЂРµРїР°Рґ РјРµР¶РґСѓ РїР»РµС‡Р°РјРё / С€РёСЂРёРЅР°,
            // Р° РЅРµ Р»РѕРєР°Р»СЊРЅС‹Р№ СѓРєР»РѕРЅ РєР°Р¶РґРѕР№ РєСЂРѕРјРєРё (С‚РѕС‚ РјРµСЂРёР» РјРёРєСЂРѕСЂРµР»СЊРµС„, Р° РЅРµ РїСЂРѕС„РёР»СЊ).
            Vector2 left = p + side * (width * 0.5f);
            Vector2 right = p - side * (width * 0.5f);
            float cross = Mathf.Atan2(Mathf.Abs(ground(left.x, left.y) - ground(right.x, right.y)),
                width) * Mathf.Rad2Deg;
            if (cross > 5f) return $"cross={cross:F1}";
            // Р’РґРѕР»СЊ: РЅР°СЃС‚РѕСЏС‰РёР№ РїСЂРѕРґРѕР»СЊРЅС‹Р№ СѓРєР»РѕРЅ (РїРѕРґСЉС‘Рј / С€Р°Рі), Р° РЅРµ СЂР°Р·РЅРѕСЃС‚СЊ
            // Р»РѕРєР°Р»СЊРЅС‹С… СѓРєР»РѕРЅРѕРІ вЂ” С‚Р° РјРµСЂР° РЅРёС‡РµРіРѕ РЅРµ Р·РЅР°С‡РёС‚ Рё Р·Р°РїРёСЂР°Р»Р° СЃРІСЏР·Рё.
            if (i < steps)
            {
                Vector2 p2 = Vector2.Lerp(a2, b2, (i + 1) / (float)steps);
                float stepLen = Vector2.Distance(p, p2);
                if (stepLen > 1e-3f)
                {
                    float along = Mathf.Atan2(Mathf.Abs(ground(p2.x, p2.y) - ground(p.x, p.y)),
                        stepLen) * Mathf.Rad2Deg;
                    if (along > 8f) return $"along={along:F1}";
                }
            }
        }
        return null;
    }

    /// <summary>
    /// v2: РѕР±С‰РёРµ Р·РѕРЅС‹ (R8вЂ“18) РЅР° С‚СЂРѕРїРµ РёР»Рё в‰¤30 Рј РѕС‚ РЅРµС‘. Р”РІРµ Р±Р»РёР¶Р°Р№С€РёРµ Рє РІРѕРґРµ
    /// РїРѕРјРµС‡Р°СЋС‚СЃСЏ РєР°Рє РІРѕРґРѕРїРѕР№ (S3 В§4: В«2 РёР· commonВ»).
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
        // Р’РѕРґРѕРїРѕРё РІС…РѕРґСЏС‚ РІ С‡РёСЃР»Рѕ РѕР±С‰РёС… Р·РѕРЅ; РµСЃР»Рё РёС… РјРµРЅСЊС€Рµ РґРІСѓС… вЂ” РЅРµ РґРѕР±РёСЂР°РµРј С…Р°РєРѕРј.
        list.AddRange(water);
        return list;
    }

    /// <summary>
    /// v2: 2 РІРѕРґРѕРїРѕСЏ вЂ” РїРѕР»РєР° РЅР° Р±РµСЂРµРіСѓ РѕР·РµСЂР° (S3 В§5.3): 3вЂ“5 Рј РЅР° `lakeLevel+0.5вЂ¦+1.2`,
    /// СѓРєР»РѕРЅ РїРѕР»РєРё в‰¤6В°. Р’РѕРґРѕРїРѕР№ вЂ” Р°РґСЂРµСЃ РІРЅСѓС‚СЂРё РјР°СЃРєРё `water`, РїРѕСЌС‚РѕРјСѓ `water`
    /// РёСЃРєР»СЋС‡Р°РµС‚СЃСЏ РёР· РїСЂРѕРІРµСЂРєРё: СЌС‚Рѕ СЂР°Р·СЂРµС€С‘РЅРЅРѕРµ РёСЃРєР»СЋС‡РµРЅРёРµ, Р° РЅРµ РґС‹СЂР° РІ РїСЂР°РІРёР»Рµ.
    /// </summary>
    public static List<Zone> BuildWaterholes(DomeGenConfigSO cfg, DomeHydro.State hydro,
        Func<float, float, float> ground, DomeMasks masks, long seed, int want = 2)
    {
        var list = new List<Zone>();
        if (cfg == null || hydro == null || ground == null || hydro.lakeRadius <= 0f)
            return list;
        float lake = hydro.lakeLevel;
        // РџРѕР»РєР° РїРѕ S2: 3вЂ“5 Рј РЅР° lakeLevel+0.5вЂ¦+1.2, в‰¤6В°. РќР° СЃС‹СЂРѕРј Р±РµСЂРµРіСѓ С‚Р°РєРѕР№
        // СѓР·РєРёР№ РєРѕСЂРёРґРѕСЂ СЂРµРґРѕРє; РґРѕРїСѓСЃРєР°РµРј РєСЂР°Р№РЅРёРµ РїРѕР»РєРё (0.2вЂ¦1.5, в‰¤8В°) Рё РїРёС€РµРј СЌС‚Рѕ
        // РІ СЃР»РµРґ. Р­С‚Рѕ РЅРµ В«РІРѕРґРѕРїРѕР№ РЅР° СЃРєР»РѕРЅРµВ»: РІРѕРґР° РѕСЃС‚Р°С‘С‚СЃСЏ РІРѕРґРѕР№, СЂРѕР»СЊ вЂ” Р°РґСЂРµСЃ.
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

        // Р СѓСЃР»Рѕ вЂ” РІС‚РѕСЂРѕР№ Р±РµСЂРµРі: РµСЃР»Рё РЅР° РѕР·РµСЂРµ РїРѕР»РѕРє РЅРµ С…РІР°С‚РёР»Рѕ, РІРѕРґРѕРїРѕР№ СЃС‚Р°РІРёС‚СЃСЏ
        // Сѓ СЂРµРєРё (С‚Р° Р¶Рµ РјР°СЃРєР° `water`, С‚Рѕ Р¶Рµ РёСЃРєР»СЋС‡РµРЅРёРµ).
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
                "corridor", "zone", "link", "common", "landform", "mainRoute", "gate")) return false;
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
    // v1: РёСЃС‚РѕСЂРёС‡РµСЃРєР°СЏ B-lite (A/C/B0/Pavilion) вЂ” С‚РµР»Р° СЃРѕС…СЂР°РЅРµРЅС‹ РєР°Рє Р±С‹Р»Рё
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
    // РћР±С‰РµРµ (РЅРµ Р·Р°РІРёСЃРёС‚ РѕС‚ РІРµСЂСЃРёРё)
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
