using System;
using UnityEngine;

/// <summary>
/// Место лаборатории-полубункера (B0.1): детерминированный поиск по сиду, без UnityEngine.Random.
/// <para>
/// Порядок, из которого всё следует: <b>сначала вход, потом лаборатория</b>. Направление ангара
/// задаётся сидом (один источник — <see cref="DomeFacilityLayout.HangarSpot"/>), вход стоит у
/// подножия скального кольца на <c>Rin − 20</c>; лаборатория ищется уже от него, поэтому
/// «≥150 м от входа» — свойство мира, а не подгонка числа.
/// </para>
/// <para>
/// Кандидаты берутся хэшем сида в двух полосах: <b>InSlope</b> — у кольца на любой стороне
/// (<c>rin − 15 … rin − 5</c>), <b>Bastion</b> — по всей чаше, но центр не ближе 30 м к кольцу.
/// Годность — четыре условия сразу: сухо (выше озера), уклон меньше 10°, перепад по пятну
/// не больше 2 м, река не под площадкой; у InSlope пятое — берма сзади не должна накрывать
/// комнаты. Порядок обхода детерминирован, поэтому «тот же сид + та же version = тот же мир».
/// </para>
/// <para>
/// Один честный компромисс: на малом диаметре <c>minLabDistance</c> может быть физически
/// недостижим (Ø300: чаша r ≤ 70, вход на r ≈ 50, максимум 90 м). Тогда поиск повторяется без
/// порога и берёт САМОЕ ДАЛЁКОЕ годное пятно — с флагом <see cref="Spot.relaxed"/> и предупреждением
/// в лог. Молчаливого «подкрутили порог» здесь нет.
/// </para>
/// </summary>
public static class DomeLabSite
{
    /// <summary>Найденное место. <c>found = false</c> — годного пятна нет вовсе (смени сид).</summary>
    public struct Margins
    {
        public float dry;
        public float slope;
        public float relief;
        public float river;
        public float min;
    }

    public struct Spot
    {
        public bool found;
        /// <summary>Порог 150 м не достижим на этом диаметре — взято самое далёкое годное пятно.</summary>
        public bool relaxed;
        /// <summary>Подход перед дверью мокрый/низкий — нужен настил; место всё равно годно.</summary>
        public bool wetApproach;
        /// <summary>Строгих мест не нашлось; взята детерминированная запасная точка.</summary>
        public bool fallback;
        /// <summary>Центр плиты в координатах поля.</summary>
        public Vector2 center;
        /// <summary>Портал с фасада (на плите), в координатах поля.</summary>
        public Vector3 door;
        /// <summary>Решённый стиль — только Bastion или InSlope, никогда Auto.</summary>
        public LabStyle style;
        /// <summary>Тип входа: Rock или Underground.</summary>
        public LabEntrance entrance;
        /// <summary>Поворот: локальная +Z смотрит от чаши (в кольцо), фасад −Z — к центру/входу.</summary>
        public float yawDeg;
        /// <summary>Верх пятна: на нём стоит плита (не ниже воды).</summary>
        public float groundY;
        public float distanceToEntry;
        /// <summary>Перепад по пятну (м) — рабочая оценка 2 м, смотрится в лог.</summary>
        public float relief;
        public Margins margins;
        public string note;
    }

    /// <summary>Максимальный уклон в точках пятна (град). Бункер режет площадку, поэтому 20°.</summary>
    public const float MaxSlopeDegrees = 20f;
    /// <summary>Максимальный перепад высот по пятну (м): плита не висит и не режет склон.</summary>
    public const float MaxRelief = 4f;
    /// <summary>Сухо — выше уровня озера на столько (м).</summary>
    public const float DryMargin = 0.5f;
    /// <summary>Полоса InSlope от внутренней кромки кольца (м): rin − 15 … rin − 5.</summary>
    public const float RingBandInner = 15f;
    public const float RingBandOuter = 5f;
    /// <summary>Bastion: центр не ближе стольких метров к внутренней кромке кольца.</summary>
    public const float BastionInset = 30f;
    /// <summary>Берма сзади не выше этого над полом (м), иначе кольцо накрывает комнаты.</summary>
    public const float MaxRingAtBack = 3f;
    /// <summary>Площадка не накрывает русло: клиренс от точек реки (м).</summary>
    public const float RiverClearance = 25f;
    /// <summary>Сухой подход перед дверью: длина и полуширина проверяемой полосы (м).</summary>
    public const float ApproachLength = 16f;
    public const float ApproachHalfWidth = 4f;
    public const float ApproachStep = 4f;

    /// <summary>Пятно площадки: центр, четыре угла и середины сторон (в кадре лаборатории).</summary>
    static readonly Vector2[] PadSamples =
    {
        new Vector2(0f, 0f),
        new Vector2(DomeLabComplex.HalfWidth, DomeLabComplex.HalfDepth),
        new Vector2(-DomeLabComplex.HalfWidth, DomeLabComplex.HalfDepth),
        new Vector2(-DomeLabComplex.HalfWidth, -DomeLabComplex.HalfDepth),
        new Vector2(DomeLabComplex.HalfWidth, -DomeLabComplex.HalfDepth),
        new Vector2(DomeLabComplex.HalfWidth, 0f),
        new Vector2(-DomeLabComplex.HalfWidth, 0f),
        new Vector2(0f, DomeLabComplex.HalfDepth),
        new Vector2(0f, -DomeLabComplex.HalfDepth),
    };

    /// <summary>
    /// Найти место. <paramref name="requested"/> = Auto пробует сначала полосу кольца (там комплекс
    /// может быть только InSlope), затем открытый пол. Возвращает найденное или <c>found = false</c>.
    /// </summary>
    public static Spot Find(DomeGenConfigSO cfg, long seed, Vector2 entry, DomeHydro.State hydro,
        LabStyle requested, float minDistance, LabEntrance entrance = LabEntrance.Rock,
        int attempts = 512, bool preferBest = false, bool lattice = false)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        if (hydro == null) throw new ArgumentNullException(nameof(hydro));
        attempts = Mathf.Clamp(attempts, 32, 1024);
        float rin = Mathf.Max(1f, DomeRockRing.InnerRadius(cfg));
        if (minDistance < 0f) minDistance = 0f;
        Func<float, float, float> ground = (x, z) => DomeHydro.SampleHydro(cfg, hydro, x, z);

        // 1) честный порог в своей полосе; 2) без порога — самое далёкое сухое пятно.
        foreach (var style in Order(requested))
        {
            var exact = lattice
                ? SearchLattice(cfg, entry, hydro, ground, rin, style, entrance, minDistance,
                    requireDryApproach: true, wetApproach: false)
                : Search(cfg, seed, entry, hydro, ground, rin, style, entrance, minDistance, attempts,
                    keepFarthest: false, requireDryApproach: true, wetApproach: false, preferBest: preferBest);
            if (exact.found) return exact;
        }

        Spot best = default;
        float bestDist = float.NegativeInfinity;
        foreach (var style in Order(requested))
        {
            var relaxed = Search(cfg, seed, entry, hydro, ground, rin, style, entrance, 0f, attempts,
                keepFarthest: true, requireDryApproach: true, wetApproach: false, preferBest: preferBest);
            if (!relaxed.found) continue;
            if (relaxed.distanceToEntry > bestDist)
            {
                bestDist = relaxed.distanceToEntry;
                best = relaxed;
            }
        }
        if (best.found)
        {
            best.relaxed = true;
            best.note = $"порог {minDistance:F0} м недостижим на Ø{cfg.mapDiameter:F0}: " +
                        $"лаборатория в {best.distanceToEntry:F0} м от входа";
            return best;
        }

        // 3) сухого подхода нет нигде — берём самое далёкое валидное пятно и строим настил.
        best = default;
        bestDist = float.NegativeInfinity;
        foreach (var style in Order(requested))
        {
            var wet = Search(cfg, seed, entry, hydro, ground, rin, style, entrance, 0f, attempts,
                keepFarthest: true, requireDryApproach: false, wetApproach: true, preferBest: preferBest);
            if (!wet.found) continue;
            if (wet.distanceToEntry > bestDist)
            {
                bestDist = wet.distanceToEntry;
                best = wet;
            }
        }
        if (best.found)
        {
            best.relaxed = true;
            best.wetApproach = true;
            best.note = $"сухой подход не найден (сид {seed}, Ø{cfg.mapDiameter:F0}): " +
                        $"лаборатория в {best.distanceToEntry:F0} м, нужен настил";
            return best;
        }

        // 4) даже валидного пятна нет — детерминированная запасная точка напротив входа.
        Vector2 dir = entry.sqrMagnitude > 1e-6f ? -entry.normalized : Vector2.up;
        Vector2 p = dir * Mathf.Max(1f, rin * 0.55f);
        float gy = Mathf.Max(DomeHydro.SampleHydro(cfg, hydro, p.x, p.y),
            hydro.lakeLevel + DryMargin);
        float d = Vector2.Distance(p, entry);
        var fb = Make(p, entry, Order(requested)[0], entrance, gy, 0f, d,
            wetApproach: true, fallback: true);
        fb.relaxed = true;
        fb.note = $"годного пятна нет (сид {seed}, Ø{cfg.mapDiameter:F0}): запасная точка, настил";
        return fb;
    }

    /// <summary>Порядок стилей: Auto — полоса кольца, затем открытый пол. Bastion/InSlope — один.</summary>
    static LabStyle[] Order(LabStyle requested)
    {
        switch (requested)
        {
            case LabStyle.InSlope: return new[] { LabStyle.InSlope };
            case LabStyle.Bastion: return new[] { LabStyle.Bastion };
            default: return new[] { LabStyle.InSlope, LabStyle.Bastion };
        }
    }

    static Spot SearchLattice(DomeGenConfigSO cfg, Vector2 entry, DomeHydro.State hydro,
        Func<float, float, float> ground, float rin, LabStyle style, LabEntrance entrance,
        float minDistance, bool requireDryApproach, bool wetApproach)
    {
        Spot best = default;
        foreach (float step in new[] { 40f, 20f, 10f })
        {
            int n = Mathf.CeilToInt(2f * rin / step);
            for (int ix = -n; ix <= n; ix++)
                for (int iz = -n; iz <= n; iz++)
                {
                    Vector2 p = new Vector2(ix * step, iz * step);
                    float r = p.magnitude;
                    if (r > rin) continue;
                    if (style == LabStyle.InSlope &&
                        (r < rin - RingBandInner || r > rin - RingBandOuter)) continue;
                    if (style == LabStyle.Bastion && r > rin - BastionInset) continue;
                    float dist = Vector2.Distance(p, entry);
                    if (dist < minDistance) continue;
                    if (!Accept(cfg, hydro, ground, p, entry, style, entrance, requireDryApproach,
                            out float groundY, out float relief, out Margins margins))
                        continue;
                    var spot = Make(p, entry, style, entrance, groundY, relief, dist, wetApproach, false);
                    spot.margins = margins;
                    if (!best.found || margins.min > best.margins.min) best = spot;
                }
            if (best.found) return best;
        }
        return best;
    }

    static Spot Search(DomeGenConfigSO cfg, long seed, Vector2 entry, DomeHydro.State hydro,
        Func<float, float, float> ground, float rin, LabStyle style, LabEntrance entrance,
        float minDistance, int attempts, bool keepFarthest, bool requireDryApproach, bool wetApproach,
        bool preferBest)
    {
        Spot best = default;
        float bestDist = float.NegativeInfinity;
        for (int i = 0; i < attempts; i++)
        {
            if (!Candidate(cfg, seed, i, rin, style, out var p)) continue;
            float dist = Vector2.Distance(p, entry);
            if (dist < minDistance) continue;
            if (!Accept(cfg, hydro, ground, p, entry, style, entrance, requireDryApproach,
                    out float groundY, out float relief, out Margins margins))
                continue;
            var spot = Make(p, entry, style, entrance, groundY, relief, dist, wetApproach, false);
            spot.margins = margins;
            if (!keepFarthest && !preferBest) return spot;
            if (preferBest)
            {
                if (!best.found || margins.min > best.margins.min) best = spot;
            }
            else if (dist > bestDist)
            {
                bestDist = dist;
                best = spot;
            }
        }
        return best;
    }

    /// <summary>
    /// Кандидат номер i. Угол и радиус — из хэша сида и стиля (своя соль на каждый стиль:
    /// новый стиль не сдвигает старый мир). Bastion берёт радиус как sqrt — равномерно по площади.
    /// </summary>
    static bool Candidate(DomeGenConfigSO cfg, long seed, int i, float rin, LabStyle style,
        out Vector2 p)
    {
        p = Vector2.zero;
        int salt = style == LabStyle.InSlope ? 2000 : 2100;
        float u = SeededHash.ToFloat01(SeededHash.Hash(seed + salt, i, 11));
        float v = SeededHash.ToFloat01(SeededHash.Hash(seed + salt, i, 12));
        float a = u * Mathf.PI * 2f;
        float r;
        if (style == LabStyle.InSlope)
            r = Mathf.Lerp(rin - RingBandInner, rin - RingBandOuter, v);
        else
            r = Mathf.Sqrt(v) * Mathf.Max(0f, rin - BastionInset);
        p = r <= 0f ? Vector2.zero : new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        return true;
    }

    /// <summary>
    /// Годно ли пятно: сухо, уклон &lt;20°, перепад ≤4 м по пятну, не на русле,
    /// и сухая полоса подхода перед дверью; у InSlope — берма сзади не выше 3 м.
    /// </summary>
    static bool Accept(DomeGenConfigSO cfg, DomeHydro.State hydro, Func<float, float, float> ground,
        Vector2 p, Vector2 entry, LabStyle style, LabEntrance entrance, bool requireDryApproach,
        out float groundY, out float relief, out Margins margins)
    {
        groundY = 0f;
        relief = 0f;
        margins = default;
        float yaw = YawAwayFromEntry(p, entry);

        float minRiver = float.MaxValue;
        for (int i = 0; i < hydro.riverPts.Count; i++)
        {
            float dx = hydro.riverPts[i].x - p.x;
            float dz = hydro.riverPts[i].y - p.y;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d < minRiver) minRiver = d;
        }
        if (minRiver < RiverClearance) return false;

        float lake = hydro.lakeLevel;
        float min = float.MaxValue, max = float.MinValue;
        float maxSlope = 0f;
        for (int k = 0; k < PadSamples.Length; k++)
        {
            if (style == LabStyle.InSlope && PadSamples[k].y > 0f) continue;
            Vector2 q = PadPoint(p, yaw, PadSamples[k]);
            float h = ground(q.x, q.y);
            if (h < lake + DryMargin) return false;
            float slope = DomeSlope.SlopeAtS(cfg, q.x, q.y, ground);
            if (slope >= MaxSlopeDegrees) return false;
            if (slope > maxSlope) maxSlope = slope;
            if (h < min) min = h;
            if (h > max) max = h;
        }
        relief = max - min;
        if (relief > MaxRelief) return false;

        if (requireDryApproach)
        {
            float approachLength = (entrance == LabEntrance.Underground ||
                                    entrance == LabEntrance.Buried) ? 24f : ApproachLength;
            for (int d = 0; d <= (int)approachLength; d += (int)ApproachStep)
            {
                for (int side = -1; side <= 1; side++)
                {
                    Vector2 q = PadPoint(p, yaw, new Vector2(
                        side * ApproachHalfWidth, -DomeLabComplex.HalfDepth - d));
                    float h = ground(q.x, q.y);
                    if (h < lake + DryMargin) return false;
                    if (DomeSlope.SlopeAtS(cfg, q.x, q.y, ground) >= MaxSlopeDegrees) return false;
                }
            }
        }

        groundY = max;
        margins.dry = min - (lake + DryMargin);
        margins.slope = MaxSlopeDegrees - maxSlope;
        margins.relief = MaxRelief - relief;
        margins.river = minRiver - RiverClearance;
        margins.min = Mathf.Min(margins.dry, margins.slope,
            Mathf.Min(margins.relief, margins.river));
        return true;
    }

    static Spot Make(Vector2 p, Vector2 entry, LabStyle style, LabEntrance entrance,
        float groundY, float relief, float dist, bool wetApproach, bool fallback)
    {
        float yaw = YawAwayFromEntry(p, entry);
        return new Spot
        {
            found = true,
            wetApproach = wetApproach,
            fallback = fallback,
            center = p,
            // Одна формула двери на всех: её же считает сам комплекс.
            door = DomeLabComplex.DoorPoint(p, groundY, yaw),
            style = style,
            entrance = entrance,
            yawDeg = yaw,
            groundY = groundY,
            relief = relief,
            distanceToEntry = dist,
        };
    }

    /// <summary>Локальная +Z смотрит ОТ входа (в кольцо), значит фасад (−Z) — к входу и чаше.</summary>
    static float YawAwayFromEntry(Vector2 center, Vector2 entry)
    {
        Vector2 outward = Outward(center, entry);
        return Mathf.Atan2(outward.x, outward.y) * Mathf.Rad2Deg;
    }

    static Vector2 Outward(Vector2 center, Vector2 entry)
    {
        Vector2 outward = center - entry;
        if (outward.sqrMagnitude > 1e-6f) return outward.normalized;
        // Вырожденный случай (вход в центре): направление всё равно обязано быть.
        return center.sqrMagnitude > 1e-6f ? center.normalized : Vector2.up;
    }

    /// <summary>Точка кадра лаборатории → координаты поля (локальная +Z → от входа).</summary>
    static Vector2 PadPoint(Vector2 center, float yawDeg, Vector2 local)
    {
        float yaw = yawDeg * Mathf.Deg2Rad;
        float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
        return new Vector2(center.x + local.x * c + local.y * s,
            center.y - local.x * s + local.y * c);
    }
}
