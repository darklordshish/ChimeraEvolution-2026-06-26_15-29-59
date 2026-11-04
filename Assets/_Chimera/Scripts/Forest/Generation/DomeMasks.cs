using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// РќР°Р±РѕСЂ РјР°СЃРѕРє РјРёСЂР° (S3 В§3.2): РєР°Р¶РґР°СЏ вЂ” РіР°Р±Р°СЂРёС‚, Р° РЅРµ С‚РѕС‡РєР°. Р¤РѕСЂРјР° РѕР±СЏР·Р°РЅР° СЃРїСЂР°С€РёРІР°С‚СЊ
/// В«РїРµСЂРµСЃРµРєР°СЋ Р»Рё СЏВ», Р° РЅРµ В«РґР°Р»РµРєРѕ Р»Рё РјРѕР№ С†РµРЅС‚СЂВ». РљРѕР»СЊС†Рѕ вЂ” РїРѕР»РѕСЃР° СЂР°РґРёСѓСЃРѕРІ, РїРѕСЌС‚РѕРјСѓ
/// РѕС‚РґРµР»СЊРЅРѕРµ РїСЂР°РІРёР»Рѕ <see cref="OverlapsRing"/>: РµРіРѕ РЅРµР»СЊР·СЏ РІС‹СЂР°Р·РёС‚СЊ РєСЂСѓРіРѕРј.
/// <para>
/// Р”РІРµ СЃС‚РѕСЂРѕРЅС‹ РѕРґРЅРѕР№ РїСЂРѕРІРµСЂРєРё: СЃС‚Р°РЅС†РёСЏ/РІС…РѕРґ/РІРѕСЂРѕС‚Р°/СЂР°РјРїР°/РІРѕРґР°/РјР°СЂС€СЂСѓС‚/РєРѕР»СЊС†Рѕ РёР·РІРµСЃС‚РЅС‹
/// Р”Рћ landform Рё С‡РёС‚Р°СЋС‚СЃСЏ С„РѕСЂРјРѕР№; Р·РѕРЅС‹/РѕСЃС‚РѕРІС‹/РѕР±С‰РёРµ Р·РѕРЅС‹/СЏСЃРµРЅСЊ/Р»РѕРіРѕРІР° РїРѕСЏРІР»СЏСЋС‚СЃСЏ РџРћРЎР›Р•
/// <c>ShapeGround</c>, Рё РґР»СЏ РЅРёС… POI РѕС‚РІРµСЂРіР°РµС‚ РїСЏС‚РЅРѕ С„РѕСЂРјС‹.
/// </para>
/// </summary>
public sealed class DomeMasks
{
    struct Named
    {
        public string name;
        public DomeFootprint foot;
    }

    readonly List<Named> masks = new List<Named>();

    public bool HasRing { get; private set; }
    public float RingInner { get; private set; }
    public float RingOuter { get; private set; }

    /// <summary>РњР°СЃРєРё, РєРѕС‚РѕСЂС‹Рµ С„РѕСЂРјР° (landform/terrace/canyon/ravines) РЅРµ РёРјРµРµС‚ РїСЂР°РІР° Р·Р°РґРµС‚СЊ.</summary>
    public static readonly string[] FormProtected =
    {
        "station", "stationReserve", "entry", "gate", "ramp", "water", "mainRoute", "corridor",
        "zone", "shell", "common", "ash", "lair", "terrace", "canyon", "ravine", "landform",
    };

    /// <summary>
    /// S4: СЃР»РѕС‚ СЌСЂРѕР·РёРё СЃС‚РѕРёС‚ Р”Рћ <c>ShapeGround</c> Рё РџРћРЎР›Р• hill. Р­С‚Рё РјР°СЃРєРё СЌСЂРѕР·РёСЏ
    /// РѕР±СЏР·Р°РЅР° СѓРІР°Р¶Р°С‚СЊ, Р° РїСЂРёС‘РјРєР° Р·РѕРЅ/СЃРІСЏР·РµР№/POI вЂ” С‡РёС‚Р°С‚СЊ СѓР¶Рµ СЌСЂРѕРґРёСЂРѕРІР°РЅРЅС‹Р№ РїРѕР».
    /// РњР°СЃРєР° вЂ” РєРѕРЅС‚СЂР°РєС‚, РЅРµ РІС‚РѕСЂРѕР№ РїРѕР»: S4 РІСЃС‚Р°РІР»СЏРµС‚ РґРµР»СЊС‚Сѓ РІ <c>ShapeGround</c>.
    /// </summary>
    public static readonly string[] ErosionKeeps =
    {
        "water", "landform", "terrace", "canyon", "ravine", "stationReserve",
        // холм — не именованная маска: DomeWorld проверяет его DomeHill.Contains;
        // площадка/рампа/щель/POI строятся ПОСЛЕ эрозии и в маску не входят (S4 D4).
    };

    public DomeMasks RingBand(float inner, float outer)
    {
        HasRing = true;
        RingInner = Mathf.Min(inner, outer);
        RingOuter = Mathf.Max(inner, outer);
        return this;
    }

    public DomeMasks Add(string name, DomeFootprint foot)
    {
        if (string.IsNullOrEmpty(name)) return this;
        masks.Add(new Named { name = name, foot = foot });
        return this;
    }

    public bool Has(string name)
    {
        foreach (var m in masks) if (m.name == name) return true;
        return false;
    }

    public bool TryGet(string name, out DomeFootprint foot)
    {
        foreach (var m in masks)
            if (m.name == name) { foot = m.foot; return true; }
        foot = default;
        return false;
    }

    public bool Overlaps(string name, DomeFootprint foot)
    {
        foreach (var m in masks)
            if (m.name == name && DomeFootprints.Overlaps(m.foot, foot)) return true;
        return false;
    }

    /// <summary>РџРµСЂРµСЃРµРєР°РµС‚СЃСЏ СЃ Р»СЋР±РѕР№ РёР· РЅР°Р·РІР°РЅРЅС‹С… РјР°СЃРѕРє.</summary>
    public bool OverlapsAny(DomeFootprint foot, params string[] names)
    {
        foreach (var m in masks)
        {
            if (names != null && names.Length > 0 && System.Array.IndexOf(names, m.name) < 0)
                continue;
            if (DomeFootprints.Overlaps(m.foot, foot)) return true;
        }
        return false;
    }

    /// <summary>РљРѕР»СЊС†Рѕ вЂ” РїРѕР»РѕСЃР° <c>[rinв€’30, rout]</c>: РїРµСЂРµСЃРµС‡РµРЅРёРµ РїРѕ РґРёР°РїР°Р·РѕРЅСѓ СЂР°СЃСЃС‚РѕСЏРЅРёР№.</summary>
    public bool OverlapsRing(DomeFootprint foot)
    {
        if (!HasRing) return false;
        foot.DistanceRangeFromOrigin(out float min, out float max);
        return max >= RingInner && min <= RingOuter;
    }

    /// <summary>РўРѕС‡РєР° РІ Р»СЋР±РѕР№ РёР· РЅР°Р·РІР°РЅРЅС‹С… РјР°СЃРѕРє (РґР»СЏ РІС‹СЂРµР·Р° С…РѕР»РјР°, Р° РЅРµ РґР»СЏ С„РѕСЂРјС‹).</summary>
    public bool ContainsPoint(Vector2 p, params string[] names)
    {
        foreach (var m in masks)
        {
            if (names != null && names.Length > 0 && System.Array.IndexOf(names, m.name) < 0)
                continue;
            if (m.foot.Contains(p)) return true;
        }
        return false;
    }

    /// <summary>
    /// Горячий путь дорог: без `params`-массива и `Array.IndexOf` на каждую клетку
    /// сетки 4 м (262k вызовов × маски давали 8.7 с только на BuildGrid).
    /// </summary>
    public bool ContainsPoint(Vector2 p, string n1)
    {
        foreach (var m in masks)
            if (m.name == n1 && m.foot.Contains(p)) return true;
        return false;
    }

    public bool ContainsPoint(Vector2 p, string n1, string n2, string n3, string n4, string n5)
    {
        foreach (var m in masks)
        {
            if (m.name != n1 && m.name != n2 && m.name != n3 && m.name != n4 && m.name != n5)
                continue;
            if (m.foot.Contains(p)) return true;
        }
        return false;
    }

    /// <summary>РРјСЏ РїРµСЂРІРѕР№ РјР°СЃРєРё, РІ РєРѕС‚РѕСЂСѓСЋ РїРѕРїР°Р» РіР°Р±Р°СЂРёС‚ (РґРёР°РіРЅРѕСЃС‚РёРєР° РѕС‚РєР°Р·Р° С„РѕСЂРјС‹).</summary>
    public string FirstConflict(DomeFootprint foot)
    {
        if (OverlapsRing(foot)) return "ring";
        foreach (var m in masks)
            if (DomeFootprints.Overlaps(m.foot, foot)) return m.name;
        return null;
    }

    /// <summary>Р—Р°РїСЂРµС‚ С„РѕСЂРјС‹: Р»СЋР±Р°СЏ Р·Р°С‰РёС‰С‘РЅРЅР°СЏ РјР°СЃРєР° РёР»Рё РєРѕР»СЊС†Рѕ.</summary>
    public bool BlocksForm(DomeFootprint foot) => OverlapsRing(foot) || OverlapsAny(foot, FormProtected);

    /// <summary>
    /// РўРѕ Р¶Рµ, РЅРѕ СЃ РёСЃРєР»СЋС‡РµРЅРёСЏРјРё: Р·РѕРЅР° РѕР±СЏР·Р°РЅР° РїРµСЂРµСЃРµРєР°С‚СЊ СЃРІРѕРё Р¶Рµ СЃРІСЏР·Рё (`corridor`),
    /// РёРЅР°С‡Рµ РµС‘ РЅРµР»СЊР·СЏ РїРѕСЃС‚Р°РІРёС‚СЊ РІРѕРІСЃРµ. РСЃРєР»СЋС‡РµРЅРёСЏ вЂ” РѕСЃРѕР·РЅР°РЅРЅС‹Рµ Р°РґСЂРµСЃР°-РёСЃРєР»СЋС‡РµРЅРёСЏ,
    /// Р° РЅРµ В«РїРµСЂРµС‡РµРЅСЊ, С‡С‚РѕР±С‹ СЃРёСЃС‚РµРјР° РЅРµ С‚СЂРѕРіР°Р»Р°В»: Р±РµР· РЅРёС… РїСЂРѕРІРµСЂРєР° РїСЂРѕС‚РёРІРѕСЂРµС‡РёС‚ СЃРµР±Рµ.
    /// </summary>
    public bool BlocksForm(DomeFootprint foot, params string[] skip)
    {
        if (OverlapsRing(foot)) return true;
        foreach (var m in masks)
        {
            if (skip != null && System.Array.IndexOf(skip, m.name) >= 0) continue;
            if (DomeFootprints.Overlaps(m.foot, foot)) return true;
        }
        return false;
    }

    /// <summary>
    /// РќРѕСЃРёС‚РµР»СЊ С„РѕСЂРјС‹ S1 РєР°Рє РіР°Р±Р°СЂРёС‚: РґРёСЃРє вЂ” РєСЂСѓРі <c>D/2</c>, РєР°РїСЃСѓР»Р°/РєР°РЅСЊРѕРЅ вЂ” РєР°РїСЃСѓР»Р°
    /// СЃ СЂР°РґРёСѓСЃРѕРј <c>Run</c>. РўР°Рє С„РѕСЂРјР° РїСЂРѕРІРµСЂСЏРµС‚СЃСЏ С‚РµРј Р¶Рµ РїРµСЂРµСЃРµС‡РµРЅРёРµРј, С‡С‚Рѕ Рё РјРµСЃС‚Рѕ.
    /// </summary>
    public static DomeFootprint ToFootprint(DomeForm f)
    {
        if (f.kind == DomeFormKind.Capsule || f.kind == DomeFormKind.Canyon)
        {
            Vector2 axis = f.axis.sqrMagnitude > 1e-8f ? f.axis.normalized : Vector2.right;
            // Р Р°РґРёСѓСЃ = РїРѕР»СѓС€РёСЂРёРЅР° РїРѕ РїРѕРґРѕС€РІРµ: Р±СЂРѕРІРєР° (crest) РІС…РѕРґРёС‚ РІ РЅРѕСЃРёС‚РµР»СЊ.
            return DomeFootprint.Capsule(f.center - axis * f.halfLength,
                f.center + axis * f.halfLength, f.Run + f.crest * 0.5f);
        }
        return DomeFootprint.Circle(f.center, f.diameter * 0.5f);
    }

    public bool BlocksForm(DomeForm form) => BlocksForm(ToFootprint(form));

    /// <summary>
    /// РћСЃС‚РѕРІ СЂР°Р·СЂРµС€С‘РЅ РІРЅСѓС‚СЂРё РѕС‚РєСЂС‹С‚РѕР№ Р·РѕРЅС‹ (S2 В§10.1): Р»РµР¶РёС‚ РІ РµС‘ РїСЏС‚РЅРµ, Р·Р°РЅРёРјР°РµС‚
    /// в‰¤25 % РїР»РѕС‰Р°РґРё Рё СЃС‚РѕРёС‚ Сѓ РєСЂРѕРјРєРё (С†РµРЅС‚СЂ РЅРµ Р±Р»РёР¶Рµ 0.55В·R Рє РєСЂР°СЋ). Р­С‚Рѕ РќР• РїРµСЂРµСЃРµС‡РµРЅРёРµ,
    /// Р° СЂР°Р·СЂРµС€С‘РЅРЅРѕРµ РІР»РѕР¶РµРЅРёРµ вЂ” РїРѕС‚РѕРјСѓ РѕС‚РґРµР»СЊРЅРѕРµ РїСЂР°РІРёР»Рѕ, Р° РЅРµ РјР°СЃРєР°.
    /// </summary>
    public static bool ShellFitsZone(DomeFootprint shell, Vector2 zoneCenter, float zoneRadius)
    {
        if (shell.kind != DomeFootKind.Obb || zoneRadius <= 0f) return false;
        Rect box = shell.Aabb;
        var corners = new[]
        {
            new Vector2(box.xMin, box.yMin), new Vector2(box.xMax, box.yMin),
            new Vector2(box.xMax, box.yMax), new Vector2(box.xMin, box.yMax),
        };
        foreach (var c in corners)
            if ((c - zoneCenter).magnitude > zoneRadius) return false;

        float shellArea = 4f * shell.halfWidth * shell.halfDepth;
        float zoneArea = Mathf.PI * zoneRadius * zoneRadius;
        if (shellArea > 0.25f * zoneArea) return false;

        return (shell.a - zoneCenter).magnitude >= 0.55f * zoneRadius;
    }

    /// <summary>РћР±С‰Р°СЏ Р·РѕРЅР°: РЅР° С‚СЂРѕРїРµ РёР»Рё РЅРµ РґР°Р»СЊС€Рµ 30 Рј РѕС‚ РЅРµС‘ (S3 В§3.2).</summary>
    public static bool CommonNearPath(DomeFootprint common, Vector2 pathFrom, Vector2 pathTo,
        float maxDistance = 30f)
    {
        float d = DomeFootprint.DistanceToSegment(common.a, pathFrom, pathTo);
        return d <= maxDistance + common.radius;
    }

    /// <summary>РћС‚РєСЂС‹С‚Р°СЏ Р·РѕРЅР°: СЂР°РґРёСѓСЃ РїСЏС‚РЅР° R = 24вЂ¦34 Рј (S2 В§10.1), РјР°СЃРєР° вЂ” R+20.</summary>
    public static DomeFootprint ZoneMask(Vector2 center, float radius)
    {
        float r = Mathf.Clamp(radius, 24f, 34f);
        return DomeFootprint.Circle(center, r + 20f);
    }

    /// <summary>РћСЃС‚РѕРІ: РїСЏС‚РЅРѕ в‰¤10Г—8 Рј + 3 Рј РѕР±Р»РѕРјРєРѕРІ, Сѓ РєСЂРѕРјРєРё Р·РѕРЅС‹.</summary>
    public static DomeFootprint ShellMask(Vector2 center, float yawDeg, float halfWidth,
        float halfDepth)
    {
        return DomeFootprint.Obb(center, yawDeg,
            Mathf.Min(halfWidth, 5f) + 3f, Mathf.Min(halfDepth, 4f) + 3f);
    }

    /// <summary>РћР±С‰Р°СЏ Р·РѕРЅР°: R8вЂ“18 Рј + 2 Рј Р·Р°Р·РѕСЂ.</summary>
    public static DomeFootprint CommonMask(Vector2 center, float radius)
    {
        return DomeFootprint.Circle(center, Mathf.Clamp(radius, 8f, 18f) + 2f);
    }
}
