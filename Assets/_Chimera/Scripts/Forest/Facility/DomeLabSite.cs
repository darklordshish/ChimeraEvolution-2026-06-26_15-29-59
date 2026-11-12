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
        /// <summary>Решённый стиль — только Bastion (v1) или InSlope (v2), никогда Auto.</summary>
        public LabStyle style;
        /// <summary>Тип входа: Rock, Underground или Buried (Auto разрешается по рельефу).</summary>
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

    /// <summary>
    /// G7 шаг 2 (1A): центр плато станции — 927 м от `Entry` по хорде; `LabDoor`
    /// вынесена на 18 м к входу, поэтому хорда двери — 909 м. Это якорь, а не поиск.
    /// </summary>
    public const float PlateauCenterChord = 927f;
    public const float PlateauDoorChord = PlateauCenterChord - DomeLabComplex.HalfDepth;

    /// <summary>
    /// Провизорный регион станции (`stationReserve`): пятно + blend меряется по верху
    /// плато 70×55, подход 16/24 м и регион целиком — по низу плато 144×129.
    /// </summary>
    public const float ReserveWidth = 144f;
    public const float ReserveDepth = 129f;
    public const float PlateauTopWidth = 70f;
    public const float PlateauTopDepth = 55f;

    /// <summary>v2: сухо выше озера на 1.0 м (S3 §4.1/И4, в v1 исторические 0.5).</summary>
    public const float DryMarginV2 = 1.0f;

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
    /// G7 шаг 2: провизорный `stationReserve` — ДО форм. 1A: якорь — точка на хорде
    /// `Entry → 909 м` (`LabDoor`), центр плато — 927 м. Резерв не задаёт `platformY`,
    /// не строит геометрию и не является станцией; окончательная приёмка —
    /// <see cref="Confirm"/> после `ShapeGround`.
    /// </summary>
    public static Spot Reserve(DomeGenConfigSO cfg, long seed, Vector2 entry,
        DomeHydro.State hydro, LabStyle requested, float minDistance, LabEntrance entrance)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        if (hydro == null) throw new ArgumentNullException(nameof(hydro));

        // 1A: якорь — точка на хорде 909/927 м. Направление выбирается детерминированно
        // ДО форм по тем же критериям места (сухо/уклон/перепад), а не «первое годное
        // где угодно»: резерв — регион станции, формы его уважают.
        LabStyle style = ResolveStyle(requested);
        float rin = DomeRockRing.InnerRadius(cfg);
        // 1A: старт — северный сектор 90° (направление опорного маршрута 03.11).
        // Дальше — детерминированный обход по 1.4°, чтобы не ставить станцию в воду.
        int start = 64;
        int count = 256;

        for (int i = 0; i < count; i++)
        {
            int k = (start + i) % 256;
            float a = k * Mathf.PI * 2f / 256f;
            Vector2 center = entry +
                new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * PlateauCenterChord;
            // Пятно станции внутри кольца: резерв — регион, но сам комплекс не в стене.
            if (center.magnitude + DomeLabComplex.PadHalfWidth + 1f > rin)
                continue;
            float yaw = YawAwayFromEntry(center, entry);
            Func<float, float, float> provisional = (x, z) =>
                ProvisionalGround(cfg, hydro, center, x, z);
            LabEntrance ent = entrance == LabEntrance.Auto
                ? ResolveEntrance(cfg, center, yaw, provisional)
                : entrance;
            if (!Accept(cfg, hydro, provisional, center, entry, style,
                    ent == LabEntrance.Auto ? LabEntrance.Underground : ent,
                    requireDryApproach: true, dryMargin: DryMarginV2,
                    out float gy, out float relief, out _, out _))
                continue;
            var s = Make(center, entry, style, ent, gy, relief,
                Vector2.Distance(center, entry), false, false);
            s.note = $"reserve: Entry→center {s.distanceToEntry:F0}, " +
                     $"LabDoor {Vector2.Distance(new Vector2(s.door.x, s.door.z), entry):F0}, " +
                     $"style={style}, entrance={ent}";
            s.margins = default;
            return s;
        }

        return new Spot
        {
            found = false,
            note = $"reserve: no dry shoulder at {PlateauCenterChord:F0} м " +
                   $"(внутри кольца {rin:F0} м)",
        };
    }

    /// <summary>
    /// G7 шаг 2: подтвердить станцию внутри резерва уже на `ShapeGround`.
    /// Ищется не «самое ровное место», а годное плечо/склон: кандидаты детерминированно
    /// обходят верх плато. Нет места — `found = false` с причиной; запасной точки
    /// в центр/напротив входа нет (это v1-поведение, шаг 2 его не трогает).
    /// </summary>
    public static Spot Confirm(DomeGenConfigSO cfg, DomeHydro.State hydro,
        Func<float, float, float> ground, Vector2 entry, Spot reserve,
        LabStyle style, LabEntrance entrance)
    {
        if (cfg == null || hydro == null || ground == null || !reserve.found)
            return new Spot { found = false, note = "reserve: no candidate" };

        LabStyle resolvedStyle = ResolveStyle(style);
        float yaw = YawAwayFromEntry(reserve.center, entry);
        float maxX = Mathf.Max(0f, (PlateauTopWidth - DomeLabComplex.HalfWidth * 2f) * 0.5f);
        float maxZ = Mathf.Max(0f, (PlateauTopDepth - DomeLabComplex.HalfDepth * 2f) * 0.5f);
        float[] offsets = { 0f, 0.5f, -0.5f, 1f, -1f };
        string why = "нет годного пятна на ShapeGround";
        foreach (float fx in offsets)
            foreach (float fz in offsets)
            {
                Vector2 p = PadPoint(reserve.center, yaw, new Vector2(fx * maxX, fz * maxZ));
                if (!InReserve(reserve, p)) continue;
                LabEntrance ent = entrance == LabEntrance.Auto
                    ? ResolveEntrance(cfg, p, yaw, ground)
                    : entrance;
                if (!Accept(cfg, hydro, ground, p, entry, resolvedStyle, ent,
                        requireDryApproach: true, dryMargin: DryMarginV2,
                        out float gy, out float relief, out Margins margins, out string reason))
                {
                    why = $"кандидат ({p.x:F0};{p.y:F0}): {reason}";
                    continue;
                }
                // ≥150 м по `LabDoor` (при 1A — тавтология, но проверка остаётся).
                Vector3 door = DomeLabComplex.DoorPoint(p, gy, yaw);
                if (Vector2.Distance(new Vector2(door.x, door.z), entry) < 150f)
                {
                    why = "LabDoor ближе 150 м к Entry";
                    continue;
                }
                var s = Make(p, entry, resolvedStyle, ent, gy, relief,
                    Vector2.Distance(p, entry), false, false);
                s.margins = margins;
                s.note = $"reserve confirmed on ShapeGround: Entry→LabCenter " +
                         $"{s.distanceToEntry:F0} м, LabDoor " +
                         $"{Vector2.Distance(new Vector2(s.door.x, s.door.z), entry):F0} м, " +
                         $"style={resolvedStyle}, entrance={ent}";
                return s;
            }

        return new Spot
        {
            found = false,
            center = reserve.center,
            door = reserve.door,
            style = resolvedStyle,
            entrance = entrance == LabEntrance.Auto
                ? ResolveEntrance(cfg, reserve.center, yaw, ground)
                : entrance,
            yawDeg = reserve.yawDeg,
            distanceToEntry = reserve.distanceToEntry,
            note = "station=none: " + why,
        };
    }

    /// <summary>v2 не строит `Bastion`: Auto и явный Bastion разрешаются в InSlope.</summary>
    static LabStyle ResolveStyle(LabStyle requested) => LabStyle.InSlope;

    /// <summary>
    /// Провизорный пол резерва: Hydro0 + плато станции (форма S2 №4) в его центре.
    /// Так резерв проверяется на том рельефе, который будет построен, а не на сыром.
    /// </summary>
    static float ProvisionalGround(DomeGenConfigSO cfg, DomeHydro.State hydro,
        Vector2 center, float x, float z)
    {
        float h = DomeHydro.SampleHydro(cfg, hydro, x, z);
        var plateau = DomeComposition.StationPlateau();
        plateau.center = center;
        float baseH = DomeHydro.SampleHydro(cfg, hydro, center.x, center.y);
        float dry = hydro.lakeLevel + DryMarginV2 + 0.25f;
        plateau.height = Mathf.Clamp(dry - baseH, 2.5f, plateau.height);
        return plateau.Apply(h, x, z);
    }

    /// <summary>Точка внутри провизорного региона станции (`stationReserve`).</summary>
    public static bool InReserve(Spot reserve, Vector2 p)    {
        if (!reserve.found) return false;
        float dx = p.x - reserve.center.x, dz = p.y - reserve.center.y;
        float yaw = reserve.yawDeg * Mathf.Deg2Rad;
        float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
        float lx = dx * c - dz * s;
        float lz = dx * s + dz * c;
        return Mathf.Abs(lx) <= ReserveWidth * 0.5f + 0.01f &&
               Mathf.Abs(lz) <= ReserveDepth * 0.5f + 0.01f;
    }

    /// <summary>
    /// `Auto` по рельефу: Buried на крутом/склонном лице, Underground на спуске,
    /// Rock на скальной толще. Считается по пятну И подходу (24 м), а не по одной точке:
    /// ровная плита на верху плато иначе всегда давала бы Rock.
    /// </summary>
    static LabEntrance ResolveEntrance(DomeGenConfigSO cfg, Vector2 center, float yawDeg,
        Func<float, float, float> ground)
    {
        float maxSlope = 0f;
        for (int k = 0; k < PadSamples.Length; k++)
        {
            Vector2 q = PadPoint(center, yawDeg, PadSamples[k]);
            maxSlope = Mathf.Max(maxSlope, DomeSlope.SlopeAtS(cfg, q.x, q.y, ground));
        }
        const float approach = 24f;
        for (int d = 0; d <= (int)approach; d += (int)ApproachStep)
            for (int side = -1; side <= 1; side++)
            {
                Vector2 q = PadPoint(center, yawDeg, new Vector2(
                    side * ApproachHalfWidth, -DomeLabComplex.HalfDepth - d));
                maxSlope = Mathf.Max(maxSlope, DomeSlope.SlopeAtS(cfg, q.x, q.y, ground));
            }
        if (maxSlope > 18f) return LabEntrance.Buried;
        if (maxSlope > 8f) return LabEntrance.Underground;
        return LabEntrance.Rock;
    }

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
                            DryMargin, out float groundY, out float relief, out Margins margins,
                            out _))
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
                    DryMargin, out float groundY, out float relief, out Margins margins,
                    out _))
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
        float dryMargin, out float groundY, out float relief, out Margins margins, out string why)
    {
        groundY = 0f;
        relief = 0f;
        margins = default;
        why = null;
        float yaw = YawAwayFromEntry(p, entry);

        float minRiver = float.MaxValue;
        for (int i = 0; i < hydro.riverPts.Count; i++)
        {
            float dx = hydro.riverPts[i].x - p.x;
            float dz = hydro.riverPts[i].y - p.y;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d < minRiver) minRiver = d;
        }
        if (minRiver < RiverClearance)
        {
            why = $"river {minRiver:F1} < {RiverClearance:F0}";
            return false;
        }

        float lake = hydro.lakeLevel;
        float min = float.MaxValue, max = float.MinValue;
        float maxSlope = 0f;
        for (int k = 0; k < PadSamples.Length; k++)
        {
            if (style == LabStyle.InSlope && PadSamples[k].y > 0f) continue;
            Vector2 q = PadPoint(p, yaw, PadSamples[k]);
            float h = ground(q.x, q.y);
            if (h < lake + dryMargin)
            {
                why = $"dry {h:F2} < {lake + dryMargin:F2}";
                return false;
            }
            float slope = DomeSlope.SlopeAtS(cfg, q.x, q.y, ground);
            if (slope >= MaxSlopeDegrees)
            {
                why = $"slope {slope:F1} ≥ {MaxSlopeDegrees:F0}";
                return false;
            }
            if (slope > maxSlope) maxSlope = slope;
            if (h < min) min = h;
            if (h > max) max = h;
        }
        relief = max - min;
        if (relief > MaxRelief)
        {
            why = $"relief {relief:F1} > {MaxRelief:F0}";
            return false;
        }

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
                    if (h < lake + dryMargin)
                    {
                        why = $"approach dry {h:F2} < {lake + dryMargin:F2} d={d}";
                        return false;
                    }
                    float slope = DomeSlope.SlopeAtS(cfg, q.x, q.y, ground);
                    if (slope >= MaxSlopeDegrees)
                    {
                        why = $"approach slope {slope:F1} ≥ {MaxSlopeDegrees:F0} d={d}";
                        return false;
                    }
                }
            }
        }

        groundY = max;
        margins.dry = min - (lake + dryMargin);
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
