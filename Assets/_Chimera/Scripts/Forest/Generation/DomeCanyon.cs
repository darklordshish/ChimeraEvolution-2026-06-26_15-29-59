using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Каньон большого павильона (Task 1 спеки 27.10): чистая геометрия поля.
/// Полилиния «устье у озера → исток у плато» (axis[0] — mouth, axis[last] — lip),
/// вырезка с непрерывным плечом, дно не ниже <c>lakeLevel + 0.45</c>.
/// <para>
/// Никаких UnityEngine.Random / DateTime / Guid: только <see cref="SeededHash"/> и
/// <see cref="DomeGenConfigSO"/>. Слой не знает о мешах, воде и сцене — <see cref="Apply"/>,
/// <see cref="Contains"/> и <see cref="StreamPath"/> суть чистые функции от <see cref="State"/>.
/// </para>
/// <para>
/// Порядок оси выбран по спеке: <c>axis[0]</c> — устье (низ, у озера), <c>axis[last]</c> —
/// исток/лип (верх, у плато). <see cref="StreamPath"/> отдаёт её в направлении течения.
/// </para>
/// </summary>
public static class DomeCanyon
{
    public struct State
    {
        public bool valid;
        public Vector2 mouth;
        public Vector2 lip;
        public List<Vector2> axis;
        public float halfWidth;
        public float depthHead;
        public float depthMouth;
        public float fallHeight;
        public float rim;
        public Vector2 pool;
        public float poolRadius;
    }

    // Рабочие цели спеки, не нормы. Локальные дефолты Task 1: DomeGenConfigSO не меняется.
    public const float MinLength = 150f;
    public const float MaxLength = 250f;
    // На террасе исток дальше: каньон большого павильона до 600 м, не B1.1-ложбина.
    public const float AnchoredMaxLength = 600f;
    public const float MinHalfWidth = 7f;
    public const float MaxHalfWidth = 12f;
    public const float MinDepth = 8f;
    public const float MaxDepth = 18f;
    public const float MinFall = 12f;
    public const float MaxFall = 18f;

    // Защита зон (метры, не u): кольцо, вход, пятно лаборатории, русло, озеро.
    public const float RingMargin = 20f;
    public const float EntryClearance = 12f;
    public const float LabClearance = 35f;
    public const float RiverClearance = 8f;
    public const float LakeGap = 2f;

    const int Attempts = 192;
    const float ShoulderFactor = 1.2f;
    const float EndFadeMeters = 8f;
    const float FloorAboveLake = 0.45f;

    /// <summary>Детерминированный бросок: seed + версия cfg + соль + номер попытки.</summary>
    static float Roll(long seed, int version, int salt, int attempt)
    {
        return SeededHash.ToFloat01(SeededHash.Hash(seed, salt + version * 7919, attempt));
    }

    /// <summary>
    /// Собрать каньон. Детерминированно (тот же seed + cfg.version → то же состояние);
    /// при невозможности вернуть <c>valid = false</c> (и никого не трогать).
    /// <para>
    /// Устье ставится у озера, ось уходит от него к плато/кольцу. Каньон не входит в
    /// озеро, русло, скальное кольцо, пятно лаборатории и вход. Если кандидатов нет —
    /// вызывающий обязан трактовать это как «каньона нет», а не как ошибку.
    /// </para>
    /// </summary>
    public static State Build(DomeGenConfigSO cfg, long seed, DomeHydro.State hydro,
        Vector2 entry, Vector2 labCenter, float rin, DomeTerrace.State terrace = default)
    {
        var none = new State { valid = false };
        if (cfg == null || hydro == null) return none;
        if (hydro.lakeRadius <= 0f || hydro.riverPts == null) return none;

        // 0 = «не настроено»: радиус кольца берём от диаметра карты.
        float radius = rin > 0f ? rin : cfg.mapDiameter * 0.5f;
        if (radius <= 0f) radius = 1000f;
        float lakeR = hydro.lakeRadius;
        int version = cfg.version;
        bool anchored = terrace.valid && terrace.radius > 0f;
        Vector2 lipTarget = default;
        if (anchored)
        {
            Vector2 toLake = hydro.lakeCenter - terrace.center;
            if (toLake.sqrMagnitude < 1e-6f) anchored = false;
            else lipTarget = terrace.center + toLake.normalized * (terrace.radius * 0.8f);
        }

        bool hasBest = false;
        State best = none;
        float bestScore = float.NegativeInfinity;

        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            float angle = Roll(seed, version, 8101, attempt) * Mathf.PI * 2f;
            Vector2 outward = anchored
                ? (terrace.center - hydro.lakeCenter).normalized
                : new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            float halfWidth = Mathf.Lerp(MinHalfWidth, MaxHalfWidth,
                Roll(seed, version, 8102, attempt));
            float length = Mathf.Lerp(MinLength, MaxLength,
                Roll(seed, version, 8103, attempt));
            float depthMouth = Mathf.Lerp(MinDepth, 12f,
                Roll(seed, version, 8104, attempt));
            float depthHead = Mathf.Max(depthMouth, Mathf.Lerp(12f, MaxDepth,
                Roll(seed, version, 8105, attempt)));
            float meander = Mathf.Lerp(0.12f, 0.30f,
                Roll(seed, version, 8106, attempt));
            if (anchored) meander = 0.05f;
            float phase = Roll(seed, version, 8107, attempt) * Mathf.PI * 2f;
            float poolRadius = Mathf.Lerp(6f, 9f,
                Roll(seed, version, 8108, attempt));

            // Устье — у озера, но ВНЕ его чаши: полуширина каньона плюс запас.
            float mouthDist = lakeR + halfWidth + LakeGap +
                Mathf.Lerp(6f, 24f, Roll(seed, version, 8109, attempt));
            Vector2 mouth = hydro.lakeCenter + outward * mouthDist;

            if (anchored)
                length = Mathf.Clamp(Vector2.Distance(mouth, lipTarget), MinLength, AnchoredMaxLength);
            List<Vector2> axis = MakeAxis(mouth, outward, length, meander, phase);

            Vector2 lip = axis[axis.Count - 1];
            if (anchored && Vector2.Distance(lip, lipTarget) > terrace.radius * 0.35f)
                continue;
            if (!FitsRing(axis, radius)) continue;
            if (!AvoidsLake(axis, hydro.lakeCenter, lakeR, halfWidth)) continue;

            float entryDist = DistanceToPolyline(axis, entry);
            float labDist = DistanceToPolyline(axis, labCenter);
            float entryClear = entryDist - halfWidth - EntryClearance;
            float labClear = labDist - halfWidth - LabClearance;
            if (entryClear < 0f || labClear < 0f) continue;

            float riverDist = MinDistanceToPoints(hydro.riverPts, axis);
            float riverClear = riverDist - halfWidth - hydro.riverHalfWidth - RiverClearance;
            if (riverClear < 0f) continue;

            // Исток не ниже устья: поле павильона
            // местами плоское, и требовать «+3 м» значит терять годные сиды (проверено
            // на сиде 24: 0 из 192). Перепад водопада досоздаёт вырезка, а высоты
            // ручья всё равно читаются через SampleGround.
            float drop = DomeHydro.SampleHydro(cfg, hydro, lip.x, lip.y)
                       - DomeHydro.SampleHydro(cfg, hydro, mouth.x, mouth.y);
            if (!anchored && drop < 0f) continue;

            float poolOffset = Mathf.Min(poolRadius * 1.4f, length * 0.3f);
            Vector2 pool = PointAtDistance(axis, length - poolOffset);
            // Перепад водопада — рабочая цель спеки (12–18 м). Само полотно и его
            // посадка — Task 2; поле даёт только лип, плёс и вырезку.
            float fall = Mathf.Lerp(MinFall, MaxFall,
                Roll(seed, version, 8110, attempt));
            if (anchored) fall = Mathf.Max(fall, terrace.height);

            float score = Mathf.Min(entryClear, Mathf.Min(labClear, riverClear))
                        + length * 0.001f;
            if (!hasBest || score > bestScore)
            {
                hasBest = true;
                bestScore = score;
                best = new State
                {
                    valid = true,
                    mouth = mouth,
                    lip = lip,
                    axis = axis,
                    halfWidth = halfWidth,
                    depthHead = depthHead,
                    depthMouth = depthMouth,
                    fallHeight = fall,
                    rim = Mathf.Lerp(10f, 16f, Roll(seed, version, 8111, attempt)),
                    pool = pool,
                    poolRadius = poolRadius,
                };
            }
        }

        return hasBest ? best : none;
    }

    /// <summary>
    /// Вырезка каньона из сырого поля. Вне пятна — тождество; внутри — профиль с
    /// непрерывным плечом. Дно не ниже <c>lakeLevel + 0.45</c> (если сырое поле ниже —
    /// пол поднимается до этой отметки). <c>valid = false</c> — тождество.
    /// </summary>
    public static float Apply(float raw, State s, float x, float z, float lakeLevel)
    {
        if (!s.valid || s.axis == null || s.axis.Count < 2) return raw;

        float along, lateral;
        if (!NearestOnAxis(s.axis, x, z, out along, out lateral)) return raw;

        float width = Mathf.Max(s.halfWidth, 0.01f);
        float shoulder = width * ShoulderFactor;
        if (lateral >= width + shoulder) return raw;

        float across;
        if (lateral <= width)
        {
            float u = 1f - lateral / width;
            across = u * u * (3f - 2f * u);
        }
        else
        {
            // Борт: не просто срез, а поднятый вал, иначе на низком поле
            // каньон читается канавой. На кромке (lateral == width) вал = 0.
            float u = 1f - (lateral - width) / shoulder;
            across = u * u * (3f - 2f * u);
            float rimK = 1f - Mathf.Abs((lateral - width) / shoulder - 0.5f) * 2f;
            rimK = Mathf.Clamp01(rimK);
            rimK = rimK * rimK * (3f - 2f * rimK);
            return raw + s.rim * rimK;
        }

        float length = PolyLength(s.axis);
        if (length <= 1e-4f) return raw;

        // Плавные торцы: на устье и на липе вырезка сходит на нет, без вертикальной стенки.
        float fade = Mathf.Min(EndFadeMeters, length * 0.1f);
        float endK = 1f;
        if (fade > 1e-4f)
        {
            if (along < fade)
            {
                float u = along / fade;
                endK = u * u * (3f - 2f * u);
            }
            else if (along > length - fade)
            {
                float u = (length - along) / fade;
                endK = u * u * (3f - 2f * u);
            }
        }

        float t = Mathf.Clamp01(along / length);
        float depth = Mathf.Lerp(s.depthMouth, s.depthHead, t);
        float carve = depth * across * endK;

        // Дно не ниже воды + 0.45 — правило слоя, даже если сырое поле там ниже:
        // исключение только «вне пятна», где возвращается raw до этой строки.
        return Mathf.Max(raw - carve, lakeLevel + FloorAboveLake);
    }

    /// <summary>
    /// Точка внутри пятна каньона. <paramref name="margin"/> — в МЕТРАХ: расширяет
    /// полуширину, а не параметр u. Точка ровно на расширенной кромке считается снаружи
    /// (строгое неравенство): «в 2 м от кромки при margin = 2» — не каньон.
    /// </summary>
    public static bool Contains(State s, float x, float z, float margin = 0f)
    {
        if (!s.valid || s.axis == null || s.axis.Count < 2) return false;

        float along, lateral;
        if (!NearestOnAxis(s.axis, x, z, out along, out lateral)) return false;

        float reach = Mathf.Max(0f, s.halfWidth + margin);
        return lateral < reach - 1e-3f;
    }

    /// <summary>
    /// Линия дна/ручья от липа к устью (вниз по течению). Каждый шаг оси продвигается
    /// вдоль базового направления (меандр ≤ 0.30 рад), поэтому полилиния монотонна по
    /// удалению от озера. Высоты дна берутся не отсюда, а из <see cref="Apply"/> /
    /// SampleGround — слой поля остаётся одним источником высот.
    /// Пустой список при <c>valid = false</c>.
    /// </summary>
    public static List<Vector2> StreamPath(State s)
    {
        var path = new List<Vector2>();
        if (!s.valid || s.axis == null) return path;
        for (int i = s.axis.Count - 1; i >= 0; i--) path.Add(s.axis[i]);
        return path;
    }

    static List<Vector2> MakeAxis(Vector2 mouth, Vector2 outward, float length,
        float meander, float phase)
    {
        int steps = Mathf.Clamp(Mathf.RoundToInt(length / 14f), 10, 24);
        float step = length / steps;
        float baseAngle = Mathf.Atan2(outward.y, outward.x);

        var axis = new List<Vector2>(steps + 1) { mouth };
        Vector2 p = mouth;
        for (int i = 1; i <= steps; i++)
        {
            // cos(meander) > 0 при принятой амплитуде: шаг всегда «вперёд», без петель.
            float a = baseAngle + meander * Mathf.Sin(phase + i * 0.9f);
            p += new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * step;
            axis.Add(p);
        }
        return axis;
    }

    static bool FitsRing(List<Vector2> axis, float rin)
    {
        float limit = rin - RingMargin;
        for (int i = 0; i < axis.Count; i++)
            if (axis[i].magnitude > limit) return false;
        return true;
    }

    static bool AvoidsLake(List<Vector2> axis, Vector2 lakeCenter, float lakeR, float halfWidth)
    {
        float limit = lakeR + halfWidth + LakeGap;
        for (int i = 0; i < axis.Count; i++)
            if (Vector2.Distance(axis[i], lakeCenter) < limit) return false;
        return true;
    }

    static float DistanceToPolyline(List<Vector2> axis, Vector2 p)
    {
        if (axis == null || axis.Count == 0) return float.PositiveInfinity;
        if (axis.Count == 1) return Vector2.Distance(p, axis[0]);

        float best = float.PositiveInfinity;
        for (int i = 1; i < axis.Count; i++)
        {
            Vector2 a = axis[i - 1];
            Vector2 ab = axis[i] - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-8f
                ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2)
                : 0f;
            float d = (p - (a + ab * t)).sqrMagnitude;
            if (d < best) best = d;
        }
        return Mathf.Sqrt(best);
    }

    static float MinDistanceToPoints(List<Vector2> points, List<Vector2> axis)
    {
        float best = float.PositiveInfinity;
        for (int i = 0; i < points.Count; i++)
        {
            float d = DistanceToPolyline(axis, points[i]);
            if (d < best) best = d;
        }
        return best;
    }

    static Vector2 PointAtDistance(List<Vector2> axis, float distance)
    {
        if (axis == null || axis.Count == 0) return Vector2.zero;
        float left = Mathf.Max(0f, distance);
        for (int i = 1; i < axis.Count; i++)
        {
            float seg = Vector2.Distance(axis[i - 1], axis[i]);
            if (seg <= 1e-6f) continue;
            if (left <= seg)
                return Vector2.Lerp(axis[i - 1], axis[i], left / seg);
            left -= seg;
        }
        return axis[axis.Count - 1];
    }

    static float PolyLength(List<Vector2> axis)
    {
        float len = 0f;
        for (int i = 1; i < axis.Count; i++)
            len += Vector2.Distance(axis[i - 1], axis[i]);
        return len;
    }

    /// <summary>
    /// Ближайшая точка оси: <paramref name="along"/> — расстояние от устья (axis[0]),
    /// <paramref name="lateral"/> — расстояние до линии. Без аллокаций.
    /// </summary>
    static bool NearestOnAxis(List<Vector2> axis, float x, float z,
        out float along, out float lateral)
    {
        along = 0f;
        lateral = float.PositiveInfinity;
        float bestSq = float.PositiveInfinity;
        float accum = 0f;

        for (int i = 1; i < axis.Count; i++)
        {
            Vector2 a = axis[i - 1];
            Vector2 ab = axis[i] - a;
            float segLen2 = ab.sqrMagnitude;
            float segLen = Mathf.Sqrt(segLen2);
            if (segLen <= 1e-6f) continue;

            float t = Mathf.Clamp01(((x - a.x) * ab.x + (z - a.y) * ab.y) / segLen2);
            float px = a.x + ab.x * t;
            float pz = a.y + ab.y * t;
            float dx = x - px;
            float dz = z - pz;
            float d2 = dx * dx + dz * dz;

            if (d2 < bestSq)
            {
                bestSq = d2;
                along = accum + segLen * t;
                lateral = Mathf.Sqrt(d2);
            }
            accum += segLen;
        }

        return bestSq < float.PositiveInfinity;
    }
}
