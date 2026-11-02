using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Набор масок мира (S3 §3.2): каждая — габарит, а не точка. Форма обязана спрашивать
/// «пересекаю ли я», а не «далеко ли мой центр». Кольцо — полоса радиусов, поэтому
/// отдельное правило <see cref="OverlapsRing"/>: его нельзя выразить кругом.
/// <para>
/// Две стороны одной проверки: станция/вход/ворота/рампа/вода/маршрут/кольцо известны
/// ДО landform и читаются формой; зоны/остовы/общие зоны/ясень/логова появляются ПОСЛЕ
/// <c>ShapeGround</c>, и для них POI отвергает пятно формы.
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

    /// <summary>Маски, которые форма (landform/terrace/canyon/ravines) не имеет права задеть.</summary>
    public static readonly string[] FormProtected =
    {
        "station", "entry", "gate", "ramp", "water", "mainRoute", "corridor",
        "zone", "shell", "common", "ash", "lair", "terrace", "canyon", "ravine",
    };

    /// <summary>
    /// S4: слот эрозии стоит ДО <c>ShapeGround</c> и ПОСЛЕ hill. Эти маски эрозия
    /// обязана уважать, а приёмка зон/связей/POI — читать уже эродированный пол.
    /// Маска — контракт, не второй пол: S4 вставляет дельту в <c>ShapeGround</c>.
    /// </summary>
    public static readonly string[] ErosionKeeps =
    {
        "station", "entry", "gate", "ramp", "water", "ring", "zone", "corridor",
        "common", "ash", "lair",
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

    /// <summary>Пересекается с любой из названных масок.</summary>
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

    /// <summary>Кольцо — полоса <c>[rin−30, rout]</c>: пересечение по диапазону расстояний.</summary>
    public bool OverlapsRing(DomeFootprint foot)
    {
        if (!HasRing) return false;
        foot.DistanceRangeFromOrigin(out float min, out float max);
        return max >= RingInner && min <= RingOuter;
    }

    /// <summary>Точка в любой из названных масок (для выреза холма, а не для формы).</summary>
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

    /// <summary>Запрет формы: любая защищённая маска или кольцо.</summary>
    public bool BlocksForm(DomeFootprint foot) => OverlapsRing(foot) || OverlapsAny(foot, FormProtected);

    /// <summary>
    /// То же, но с исключениями: зона обязана пересекать свои же связи (`corridor`),
    /// иначе её нельзя поставить вовсе. Исключения — осознанные адреса-исключения,
    /// а не «перечень, чтобы система не трогала»: без них проверка противоречит себе.
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
    /// Носитель формы S1 как габарит: диск — круг <c>D/2</c>, капсула/каньон — капсула
    /// с радиусом <c>Run</c>. Так форма проверяется тем же пересечением, что и место.
    /// </summary>
    public static DomeFootprint ToFootprint(DomeForm f)
    {
        if (f.kind == DomeFormKind.Capsule || f.kind == DomeFormKind.Canyon)
        {
            Vector2 axis = f.axis.sqrMagnitude > 1e-8f ? f.axis.normalized : Vector2.right;
            return DomeFootprint.Capsule(f.center - axis * f.halfLength,
                f.center + axis * f.halfLength, f.Run);
        }
        return DomeFootprint.Circle(f.center, f.diameter * 0.5f);
    }

    public bool BlocksForm(DomeForm form) => BlocksForm(ToFootprint(form));

    /// <summary>
    /// Остов разрешён внутри открытой зоны (S2 §10.1): лежит в её пятне, занимает
    /// ≤25 % площади и стоит у кромки (центр не ближе 0.55·R к краю). Это НЕ пересечение,
    /// а разрешённое вложение — потому отдельное правило, а не маска.
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

    /// <summary>Общая зона: на тропе или не дальше 30 м от неё (S3 §3.2).</summary>
    public static bool CommonNearPath(DomeFootprint common, Vector2 pathFrom, Vector2 pathTo,
        float maxDistance = 30f)
    {
        float d = DomeFootprint.DistanceToSegment(common.a, pathFrom, pathTo);
        return d <= maxDistance + common.radius;
    }

    /// <summary>Открытая зона: радиус пятна R = 24…34 м (S2 §10.1), маска — R+20.</summary>
    public static DomeFootprint ZoneMask(Vector2 center, float radius)
    {
        float r = Mathf.Clamp(radius, 24f, 34f);
        return DomeFootprint.Circle(center, r + 20f);
    }

    /// <summary>Остов: пятно ≤10×8 м + 3 м обломков, у кромки зоны.</summary>
    public static DomeFootprint ShellMask(Vector2 center, float yawDeg, float halfWidth,
        float halfDepth)
    {
        return DomeFootprint.Obb(center, yawDeg,
            Mathf.Min(halfWidth, 5f) + 3f, Mathf.Min(halfDepth, 4f) + 3f);
    }

    /// <summary>Общая зона: R8–18 м + 2 м зазор.</summary>
    public static DomeFootprint CommonMask(Vector2 center, float radius)
    {
        return DomeFootprint.Circle(center, Mathf.Clamp(radius, 8f, 18f) + 2f);
    }
}
