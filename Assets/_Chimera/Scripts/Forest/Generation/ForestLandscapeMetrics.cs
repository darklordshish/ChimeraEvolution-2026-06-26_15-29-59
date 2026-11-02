using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S5 «верификация»: ЕДИНСТВЕННАЯ реализация метрик M1–M16 по формулам S1/S2/S3/S4.
/// <para>
/// Здесь нет своих порогов приёмки: пороги живут в
/// <c>Docs/superpowers/specs/2026-11-01-landshaft-s5-verifikaciya.md</c> §3/§6
/// (и цитируются у S1/S2/S4). Класс отвечает только за измерение; ворота
/// (<c>ForestLandscapeGateTests</c>) берут числа у S1/S2/S4 и проверяют их здесь.
/// </para>
/// <para>
/// Один пол — <c>SampleGround</c> (S3 §2.4). Уклоны меряются при двух ε: «поле»
/// <see cref="SlopeClasses.ClassEpsilon"/> = 1 м (что задал автор формы) и «видимый»
/// <c>meshStep</c> (что ходит игрок). Классы уклона — три, не два: ≤22° проходимо,
/// 22–35° лазаемо, &gt;35° стена (решения геймдизайнера 01.11).
/// </para>
/// </summary>
public static class ForestLandscapeMetrics
{
    // ------------------------------------------------------------------
    // M1: уклон и три класса
    // ------------------------------------------------------------------

    public sealed class SlopeResult
    {
        public float radius;
        public int samples;
        public float p50Field, p95Field, p99Field;
        public float p50Mesh, p95Mesh, p99Mesh;
        public float maxField, maxMesh;
        public float passableField, climbableField, wallField;
        public float passableMesh, climbableMesh, wallMesh;
        /// <summary>max|∇H| по формам композиции (полевая линейка), градусы.</summary>
        public float maxGradPerForm;
        /// <summary>Три класса разбивают семплы без пересечений.</summary>
        public bool threeClassesDistinct;
        /// <summary>Если p95 ≤ 20°, доля >22° обязана быть ≤ 5 % (S5 §3.1 п. 4).</summary>
        public bool pairCompatible;

        public override string ToString() =>
            $"slope(field) p50={p50Field:F2} p95={p95Field:F2} p99={p99Field:F2} " +
            $"≤22={passableField:P2} 22-35={climbableField:P2} >35={wallField:P2} " +
            $"| slope(mesh {p50Mesh:F2}/{p95Mesh:F2}/{p99Mesh:F2}) " +
            $"≤22={passableMesh:P2} 22-35={climbableMesh:P2} >35={wallMesh:P2} " +
            $"| maxForm={maxGradPerForm:F1}°";
    }

    /// <summary>
    /// M1: уклон на диске <paramref name="radius"/> (обычно <c>rin</c>).
    /// Доли классов считаются от попавших в диск узлов; sum = 1.
    /// </summary>
    public static SlopeResult Slope(Func<float, float, float> ground, DomeGenConfigSO cfg,
        float radius, int res, float meshStep, IReadOnlyList<DomeForm> forms = null)
    {
        var result = new SlopeResult { radius = radius };
        if (ground == null || cfg == null || radius <= 0f) return result;
        var field = SlopeSamples(ground, cfg, radius, res, SlopeClasses.ClassEpsilon);
        var mesh = SlopeSamples(ground, cfg, radius, res, Mathf.Max(0.05f, meshStep));
        result.samples = field.Count;
        if (result.samples != mesh.Count) return result;
        int passF = 0, climbF = 0, wallF = 0, passM = 0, climbM = 0, wallM = 0;
        float maxF = 0f, maxM = 0f;
        for (int i = 0; i < field.Count; i++)
        {
            float sf = field[i], sm = mesh[i];
            maxF = Mathf.Max(maxF, sf);
            maxM = Mathf.Max(maxM, sm);
            switch (SlopeClasses.Classify(sf, cfg))
            {
                case SlopeClass.Passable: passF++; break;
                case SlopeClass.Climbable: climbF++; break;
                default: wallF++; break;
            }
            switch (SlopeClasses.Classify(sm, cfg))
            {
                case SlopeClass.Passable: passM++; break;
                case SlopeClass.Climbable: climbM++; break;
                default: wallM++; break;
            }
        }
        if (result.samples == 0) return result;
        result.p50Field = Percentile(field, 0.50f);
        result.p95Field = Percentile(field, 0.95f);
        result.p99Field = Percentile(field, 0.99f);
        result.p50Mesh = Percentile(mesh, 0.50f);
        result.p95Mesh = Percentile(mesh, 0.95f);
        result.p99Mesh = Percentile(mesh, 0.99f);
        result.maxField = maxF;
        result.maxMesh = maxM;
        result.passableField = (float)passF / result.samples;
        result.climbableField = (float)climbF / result.samples;
        result.wallField = (float)wallF / result.samples;
        result.passableMesh = (float)passM / result.samples;
        result.climbableMesh = (float)climbM / result.samples;
        result.wallMesh = (float)wallM / result.samples;
        result.threeClassesDistinct =
            Mathf.Abs(result.passableField + result.climbableField + result.wallField - 1f) < 1e-4f &&
            Mathf.Abs(result.passableMesh + result.climbableMesh + result.wallMesh - 1f) < 1e-4f;
        result.pairCompatible = result.p95Field > 20f || result.wallField + result.climbableField <= 0.05f + 1e-4f;
        result.maxGradPerForm = MaxFormSlope(ground, cfg, forms);
        return result;
    }

    /// <summary>
    /// Сырые уклоны на диске при заданной линейке ε. Нужны инварианту «доля &gt; t не
    /// растёт с t» и тестам измерителя; ворота берут готовые перцентили из <see cref="Slope"/>.
    /// </summary>
    public static List<float> SlopeSamples(Func<float, float, float> ground,
        DomeGenConfigSO cfg, float radius, int res, float epsilon)
    {
        var list = new List<float>();
        if (ground == null || cfg == null || radius <= 0f) return list;
        res = Mathf.Clamp(res, 8, 512);
        float step = 2f * radius / (res - 1f);
        for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                float x = -radius + ix * step;
                float z = -radius + iz * step;
                if (x * x + z * z > radius * radius) continue;
                list.Add(SlopeClasses.SlopeAt(cfg, x, z, ground, epsilon));
            }
        return list;
    }

    /// <summary>Доля значений выше порога (для инварианта совместности пары p95/&gt;22°).</summary>
    public static float ShareAbove(IReadOnlyList<float> values, float threshold)
    {
        if (values == null || values.Count == 0) return 0f;
        int n = 0;
        foreach (float v in values) if (v > threshold) n++;
        return (float)n / values.Count;
    }

    /// <summary>
    /// M1: <c>max|∇H|</c> внутри носителя каждой формы — полевая линейка.
    /// Форма без носителя (H=0) пропускается. Возвращает максимум по формам.
    /// </summary>
    public static float MaxFormSlope(Func<float, float, float> ground, DomeGenConfigSO cfg,
        IReadOnlyList<DomeForm> forms, int samples = 24)
    {
        if (ground == null || cfg == null || forms == null || forms.Count == 0) return 0f;
        float worst = 0f;
        foreach (var f in forms)
        {
            if (f.height <= 1e-3f) continue;
            float r = f.diameter * 0.5f + f.Run;
            float step = 2f * r / Mathf.Max(4, samples - 1);
            for (int iz = 0; iz < samples; iz++)
                for (int ix = 0; ix < samples; ix++)
                {
                    float x = f.center.x - r + ix * step;
                    float z = f.center.y - r + iz * step;
                    if (f.U(x, z) >= 1f) continue;
                    // Свой профиль формы, а не сумма с каньоном/террасой: иначе
                    // «форма-стена» ложно краснеет от чужого рельефа под ней.
                    float s = SlopeClasses.SlopeAt(cfg, x, z,
                        (a, b) => f.Apply(0f, a, b), SlopeClasses.ClassEpsilon);
                    if (s > worst) worst = s;
                }
        }
        return worst;
    }

    // ------------------------------------------------------------------
    // M2: энергия λ > 250 м
    // ------------------------------------------------------------------

    /// <summary>
    /// M2: `Var(box(H0)) / Var(H0)` — доля дисперсии, которую держит окно
    /// <paramref name="window"/> м. S1 §6.2 требует ≥ 0.60.
    /// </summary>
    public static float LongEnergy(Func<float, float, float> h0, float radius, int res,
        float window = 250f)
    {
        if (h0 == null || radius <= 0f) return 0f;
        res = Mathf.Clamp(res, 16, 512);
        float step = 2f * radius / (res - 1f);
        var h = new float[res * res];
        for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
                h[iz * res + ix] = h0(-radius + ix * step, -radius + iz * step);
        double mean = Mean(h), varAll = Variance(h, mean);
        if (varAll <= 1e-9) return 0f;
        var box = BoxFilter(h, res, Mathf.Max(1, Mathf.RoundToInt(window * 0.5f / step)));
        double varBox = Variance(box, Mean(box));
        return (float)(varBox / varAll);
    }

    // ------------------------------------------------------------------
    // M3: покрытие мезо 300 × 300
    // ------------------------------------------------------------------

    /// <summary>
    /// M3: доля окон <paramref name="window"/> м внутри диска, где
    /// <c>p90 − p10 ≥ threshold</c> (S1 §6.2: ≥60 % при 12 м).
    /// </summary>
    public static float MesoCoverage(Func<float, float, float> ground, float radius, int res,
        out int windows, out int good, float window = 300f, float threshold = 12f)
    {
        windows = 0; good = 0;
        if (ground == null || radius <= 0f) return 0f;
        res = Mathf.Clamp(res, 32, 512);
        float step = 2f * radius / (res - 1f);
        var h = new float[res * res];
        for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
                h[iz * res + ix] = ground(-radius + ix * step, -radius + iz * step);

        int half = Mathf.Max(1, Mathf.RoundToInt(window * 0.5f / step));
        int stride = Mathf.Max(1, Mathf.RoundToInt(window * 0.5f / step));
        var buffer = new List<float>(4 * half * half);
        for (int cz = 0; cz < res; cz += stride)
            for (int cx = 0; cx < res; cx += stride)
            {
                float x = -radius + cx * step;
                float z = -radius + cz * step;
                if (x * x + z * z > radius * radius) continue;
                windows++;
                buffer.Clear();
                for (int iz = Mathf.Max(0, cz - half); iz <= Mathf.Min(res - 1, cz + half); iz++)
                    for (int ix = Mathf.Max(0, cx - half); ix <= Mathf.Min(res - 1, cx + half); ix++)
                        buffer.Add(h[iz * res + ix]);
                if (buffer.Count < 8) continue;
                float p10 = Percentile(buffer, 0.10f);
                float p90 = Percentile(buffer, 0.90f);
                if (p90 - p10 >= threshold) good++;
            }
        return windows > 0 ? (float)good / windows : 0f;
    }

    // ------------------------------------------------------------------
    // M3b: локальный охват 20–100 м
    // ------------------------------------------------------------------

    /// <summary>
    /// M3b: доля точек диска, где перепад в окне <paramref name="window"/> м ≥ 3 м.
    /// Это НЕ «мезо» S1 (у S1 мезо 300–600 м) — читаемость на ходу (дыра 18–85 м).
    /// Порог S5 — ≥60 % (ворота G2).
    /// </summary>
    public static float LocalRelief(Func<float, float, float> ground, float radius, int res,
        float window = 40f, float threshold = 3f)
    {
        if (ground == null || radius <= 0f) return 0f;
        res = Mathf.Clamp(res, 32, 512);
        float step = 2f * radius / (res - 1f);
        var h = new float[res * res];
        for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
                h[iz * res + ix] = ground(-radius + ix * step, -radius + iz * step);
        int half = Mathf.Max(1, Mathf.RoundToInt(window * 0.5f / step));
        int inside = 0, good = 0;
        for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                float x = -radius + ix * step;
                float z = -radius + iz * step;
                if (x * x + z * z > radius * radius) continue;
                inside++;
                float min = float.MaxValue, max = float.MinValue;
                for (int jz = Mathf.Max(0, iz - half); jz <= Mathf.Min(res - 1, iz + half); jz++)
                    for (int jx = Mathf.Max(0, ix - half); jx <= Mathf.Min(res - 1, ix + half); jx++)
                    {
                        float v = h[jz * res + jx];
                        if (v < min) min = v;
                        if (v > max) max = v;
                    }
                if (max - min >= threshold) good++;
            }
        return inside > 0 ? (float)good / inside : 0f;
    }

    // ------------------------------------------------------------------
    // M4 / M4b: H/D форм и баланс P/N
    // ------------------------------------------------------------------

    public struct FormInfo
    {
        public string name;
        public DomeFormSign sign;
        public float height;
        public float run;
        public float aspect;
        public float maxSlope;
    }

    /// <summary>
    /// M4: <c>H/D = H/(D − бровка)</c> по каждой форме ИЗ ПОЛЯ (список форм композиции),
    /// а не из <see cref="DomeRelief.State"/>. Числа — отчёт; ворот по диапазонам нет.
    /// </summary>
    public static List<FormInfo> Landforms(IReadOnlyList<DomeForm> forms)
    {
        var list = new List<FormInfo>();
        if (forms == null) return list;
        foreach (var f in forms)
            list.Add(new FormInfo
            {
                name = f.name,
                sign = f.sign,
                height = f.height,
                run = f.Run,
                aspect = f.Aspect,
                maxSlope = f.MaxSlopeDegrees,
            });
        return list;
    }

    public sealed class BalanceResult
    {
        public double positiveVolume;
        public double negativeVolume;
        public float ratio;
        public bool measurable;
    }

    /// <summary>
    /// M4b: баланс P/N по ОБЪЁМУ (интеграл |H| по пятну формы), а не <c>ΣP/ΣN</c> в метрах.
    /// S1 §6.2 требует 0.80–1.20.
    /// </summary>
    public static BalanceResult Balance(IReadOnlyList<DomeForm> forms)
    {
        var r = new BalanceResult();
        if (forms == null) return r;
        foreach (var f in forms)
        {
            double v = DomeForms.VolumeOf(f);
            if (f.sign == DomeFormSign.Lift) r.positiveVolume += v;
            else r.negativeVolume += v;
        }
        r.measurable = r.positiveVolume > 1e-3 && r.negativeVolume > 1e-3;
        r.ratio = r.measurable ? (float)(r.positiveVolume / r.negativeVolume) : 0f;
        return r;
    }

    // ------------------------------------------------------------------
    // M5: изломы и радиус кривизны
    // ------------------------------------------------------------------

    public sealed class SeamResult
    {
        public float maxSlopeJump;
        public float minCurvatureRadius;
        public int rays;
        public float step;
    }

    /// <summary>
    /// M5: `|Δslope|` на 1 м по лучам от центра (числовой излом поля) и минимальный
    /// радиус кривизны форм (S1 §3.6: R ≥ 5 м, стены отдельно).
    /// </summary>
    public static SeamResult Seams(Func<float, float, float> ground, DomeGenConfigSO cfg,
        float radius, IReadOnlyList<DomeForm> forms = null, int rays = 8, float step = 1f)
    {
        var r = new SeamResult { rays = rays, step = step };
        if (ground == null || cfg == null || radius <= 0f) return r;
        float worst = 0f;
        if (forms != null && forms.Count > 0)
        {
            // M5 — излом ФОРМЫ, а не края площадки станции. Сканируем поперёк
            // каждой формы (у капсулы — поперёк оси), как S1 `Forms_C1_AtSeams`.
            foreach (var f in forms)
            {
                if (f.height <= 1e-3f) continue;
                Vector2 axis = f.kind == DomeFormKind.Capsule &&
                               f.axis.sqrMagnitude > 1e-8f
                    ? f.axis.normalized
                    : Vector2.right;
                Vector2 perp = new Vector2(-axis.y, axis.x);
                float half = f.diameter * 0.5f + f.Run;
                float prev = float.NaN;
                // Свой профиль формы, а не сумма с каньоном/площадкой: M5 — C¹ формы
                // (S1 `Forms_C1_AtSeams`), а не шов мира. Швы мира ловит M1 (стены).
                Func<float, float, float> own = (x, z) => f.Apply(0f, x, z);
                for (float t = -half; t <= half; t += step)
                {
                    float x = f.center.x + perp.x * t;
                    float z = f.center.y + perp.y * t;
                    float s = SlopeClasses.SlopeAt(cfg, x, z, own, SlopeClasses.ClassEpsilon);
                    if (!float.IsNaN(prev)) worst = Mathf.Max(worst, Mathf.Abs(s - prev));
                    prev = s;
                }
            }
        }
        else
        {
            for (int k = 0; k < rays; k++)
            {
                float a = k * Mathf.PI * 2f / rays;
                float dx = Mathf.Cos(a) * step, dz = Mathf.Sin(a) * step;
                float sx = 0f, sz = 0f;
                float prev = float.NaN;
                int n = Mathf.FloorToInt(radius / step);
                for (int i = 0; i < n; i++)
                {
                    float s = SlopeClasses.SlopeAt(cfg, sx, sz, ground, SlopeClasses.ClassEpsilon);
                    if (!float.IsNaN(prev)) worst = Mathf.Max(worst, Mathf.Abs(s - prev));
                    prev = s;
                    sx += dx; sz += dz;
                }
            }
        }
        r.maxSlopeJump = worst;
        r.minCurvatureRadius = float.PositiveInfinity;
        if (forms != null)
            foreach (var f in forms)
                if (f.height > 1e-3f)
                    r.minCurvatureRadius = Mathf.Min(r.minCurvatureRadius, f.CurvatureRadius);
        if (float.IsInfinity(r.minCurvatureRadius)) r.minCurvatureRadius = 0f;
        return r;
    }

    // ------------------------------------------------------------------
    // M13: лазаемая вертикаль
    // ------------------------------------------------------------------

    public sealed class ClimbResult
    {
        public float shareClimbable;
        public float shareWall;
        public float faceLengthAvg;
        public int faces;
        public int ledges;       // уступы ≤ 0.5 м
        public int ledgesHigh;   // уступы > 0.5 м
        public bool bypass;      // обход без способности по проходимому классу
    }

    /// <summary>
    /// M13: доля 22–35°, длина/высота лазаемых граней, уступы и наличие обхода.
    /// Геймплей — Л3; до механики это отчёт (G2 не блокирует).
    /// </summary>
    public static ClimbResult Climb(Func<float, float, float> ground, DomeGenConfigSO cfg,
        float radius, int res, Vector2 from, Vector2 to, SlopeResult slope = null,
        int rays = 8)
    {
        var r = new ClimbResult();
        if (ground == null || cfg == null) return r;
        slope = slope ?? Slope(ground, cfg, radius, res, Mathf.Max(1f, radius * 0.01f));
        r.shareClimbable = slope.climbableField;
        r.shareWall = slope.wallField;

        float total = 0f;
        int faces = 0;
        const float step = 1f;
        for (int k = 0; k < rays; k++)
        {
            float a = k * Mathf.PI * 2f / rays;
            float dx = Mathf.Cos(a) * step, dz = Mathf.Sin(a) * step;
            float sx = 0f, sz = 0f;
            float prevH = ground(sx, sz);
            float run = 0f;
            int n = Mathf.FloorToInt(radius / step);
            for (int i = 1; i < n; i++)
            {
                float nx = sx + dx, nz = sz + dz;
                float h = ground(nx, nz);
                float dh = Mathf.Abs(h - prevH);
                if (dh <= 0.5f) r.ledges++; else r.ledgesHigh++;
                float s = SlopeClasses.SlopeAt(cfg, nx, nz, ground, SlopeClasses.ClassEpsilon);
                if (SlopeClasses.Classify(s, cfg) == SlopeClass.Climbable)
                {
                    run += step;
                }
                else if (run > 0f)
                {
                    total += run; faces++;
                    run = 0f;
                }
                prevH = h;
                sx = nx; sz = nz;
            }
            if (run > 0f) { total += run; faces++; }
        }
        r.faces = faces;
        r.faceLengthAvg = faces > 0 ? total / faces : 0f;
        r.bypass = BypassExists(ground, cfg, radius, Mathf.Clamp(res, 32, 192), from, to);
        return r;
    }

    /// <summary>4-связный обход по клеткам класса «проходимо» между двумя адресами.</summary>
    public static bool BypassExists(Func<float, float, float> ground, DomeGenConfigSO cfg,
        float radius, int res, Vector2 from, Vector2 to)
    {
        if (ground == null || cfg == null || radius <= 0f) return false;
        res = Mathf.Clamp(res, 16, 256);
        float step = 2f * radius / (res - 1f);
        var open = new bool[res * res];
        for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                float x = -radius + ix * step;
                float z = -radius + iz * step;
                if (x * x + z * z > radius * radius) continue;
                float s = SlopeClasses.SlopeAt(cfg, x, z, ground, SlopeClasses.ClassEpsilon);
                open[iz * res + ix] = SlopeClasses.Classify(s, cfg) == SlopeClass.Passable;
            }
        int sx = Mathf.Clamp(Mathf.RoundToInt((from.x + radius) / step), 0, res - 1);
        int sz = Mathf.Clamp(Mathf.RoundToInt((from.y + radius) / step), 0, res - 1);
        int tx = Mathf.Clamp(Mathf.RoundToInt((to.x + radius) / step), 0, res - 1);
        int tz = Mathf.Clamp(Mathf.RoundToInt((to.y + radius) / step), 0, res - 1);
        if (!open[sz * res + sx] || !open[tz * res + tx]) return false;
        var seen = new bool[res * res];
        var queue = new Queue<int>();
        queue.Enqueue(sz * res + sx);
        seen[sz * res + sx] = true;
        while (queue.Count > 0)
        {
            int c = queue.Dequeue();
            if (c == tz * res + tx) return true;
            int cx = c % res, cz = c / res;
            Try(cx + 1, cz); Try(cx - 1, cz); Try(cx, cz + 1); Try(cx, cz - 1);
            void Try(int x, int z)
            {
                if (x < 0 || z < 0 || x >= res || z >= res) return;
                int i = z * res + x;
                if (!open[i] || seen[i]) return;
                seen[i] = true;
                queue.Enqueue(i);
            }
        }
        return false;
    }

    // ------------------------------------------------------------------
    // M9 / M9b: вода и логова
    // ------------------------------------------------------------------

    public sealed class WaterLairResult
    {
        public int lairs;
        public int dry;
        public int outsideRing;
        public float waterP50;
        public float waterMax;
        public float spreadMin;
        public int waterholes;
        public int drinkPoints;
        public int approachesOk;
        public int coverOk;
        public float fordDepth;
        public float bankSlopeMax;
        public float waterWidthMin;
    }

    /// <summary>
    /// M9: логово сухо, вне кольца, расстояние до воды p50/max, разнос.
    /// M9b: водопой — питьевые точки, ≥2 подхода, укрытие в 12 м, брод ≤0.6 м,
    /// берег ≤10°, ширина ≥6 м. G5 B — ОТЧЁТ и долг B1, не блок S3.
    /// </summary>
    public static WaterLairResult WaterLairs(IReadOnlyList<Vector3> lairs,
        IReadOnlyList<ForestSpace.Zone> waterholes,
        IReadOnlyList<ForestSpace.CoverPod> pods, DomeHydro.State hydro,
        Func<float, float, float> ground, DomeGenConfigSO cfg, float rin)
    {
        var r = new WaterLairResult();
        if (hydro == null || ground == null) return r;
        r.lairs = lairs != null ? lairs.Count : 0;
        var distances = new List<float>(r.lairs);
        float minSpread = float.MaxValue;
        for (int i = 0; i < r.lairs; i++)
        {
            Vector3 p = lairs[i];
            float h = ground(p.x, p.z);
            if (h > hydro.lakeLevel + 0.5f) r.dry++;
            if (new Vector2(p.x, p.z).magnitude < rin) r.outsideRing++;
            float d = DistanceToWater(hydro, new Vector2(p.x, p.z));
            distances.Add(d);
            for (int j = i + 1; j < r.lairs; j++)
                minSpread = Mathf.Min(minSpread,
                    Vector2.Distance(new Vector2(p.x, p.z), new Vector2(lairs[j].x, lairs[j].z)));
        }
        r.waterP50 = distances.Count > 0 ? Percentile(distances, 0.50f) : 0f;
        r.waterMax = 0f;
        foreach (float d in distances) r.waterMax = Mathf.Max(r.waterMax, d);
        r.spreadMin = float.IsInfinity(minSpread) ? 0f : minSpread;

        if (waterholes == null) return r;
        r.waterholes = waterholes.Count;
        r.approachesOk = int.MaxValue;
        r.coverOk = 0;
        r.bankSlopeMax = 0f;
        r.waterWidthMin = float.MaxValue;
        r.fordDepth = Mathf.Min(hydro.lakeDepth, hydro.riverDepth);
        foreach (var w in waterholes)
        {
            r.drinkPoints++;
            r.waterWidthMin = Mathf.Min(r.waterWidthMin, w.radius * 2f);
            float cover = float.MaxValue;
            if (pods != null)
                foreach (var p in pods)
                    cover = Mathf.Min(cover, Vector2.Distance(p.center, w.center));
            if (cover <= 12f) r.coverOk++;
            int approaches = 0;
            const int dirs = 8;
            for (int k = 0; k < dirs; k++)
            {
                float a = k * Mathf.PI * 2f / dirs;
                float x = w.center.x + Mathf.Cos(a) * (w.radius + 6f);
                float z = w.center.y + Mathf.Sin(a) * (w.radius + 6f);
                if (ground(x, z) <= hydro.lakeLevel + 0.2f) continue;
                float s = SlopeClasses.SlopeAt(cfg, x, z, ground, SlopeClasses.ClassEpsilon);
                r.bankSlopeMax = Mathf.Max(r.bankSlopeMax, s);
                if (s <= 10f) approaches++;
            }
            r.approachesOk = Mathf.Min(r.approachesOk, approaches);
        }
        if (r.approachesOk == int.MaxValue) r.approachesOk = 0;
        if (float.IsInfinity(r.waterWidthMin)) r.waterWidthMin = 0f;
        return r;
    }

    /// <summary>Расстояние до ближайшего уреза воды: озеро/русло.</summary>
    public static float DistanceToWater(DomeHydro.State hydro, Vector2 p)
    {
        if (hydro == null) return float.MaxValue;
        float d = Vector2.Distance(p, hydro.lakeCenter) - hydro.lakeRadius;
        foreach (var rp in hydro.riverPts)
            d = Mathf.Min(d, Vector2.Distance(p, rp) - hydro.riverHalfWidth);
        return d;
    }

    // ------------------------------------------------------------------
    // M7 / M6: видимость и ориентир
    // ------------------------------------------------------------------

    public sealed class LosResult
    {
        public int zones;
        public int zonesWithSixRays;
        public float corridorAlongMin;
        public float corridorAcrossMin;
        public int routePoints;
        public float edgeShare;
        public float fieldP50;
    }

    /// <summary>
    /// M7: LOS по 8 азимутам, два разреза (рельеф и рельеф+стволы+поды).
    /// POI/зона — ≥0.8·R в ≥6/8; коридор вдоль ≥28 м, поперёк ≥16 м.
    /// M6: ориентир — кромка в 80–150 м с ≥80 % точек маршрута (LOS по рельефу).
    /// </summary>
    public static LosResult Los(IReadOnlyList<ForestSpace.Zone> zones,
        IReadOnlyList<ForestSpace.Link> links, IReadOnlyList<Vector3> trunks,
        IReadOnlyList<ForestSpace.CoverPod> pods, Func<float, float, float> ground,
        Vector3 entry, Vector3 labDoor)
    {
        var r = new LosResult();
        if (ground == null) return r;
        if (zones != null)
        {
            r.zones = zones.Count;
            foreach (var z in zones)
            {
                Vector3 c = new Vector3(z.center.x, ground(z.center.x, z.center.y) + 1.6f, z.center.y);
                int ok = 0;
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI * 2f / 8f;
                    Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    float dist = ClearDistance(c, d, z.radius, ground, trunks, pods);
                    if (dist >= 0.8f * z.radius) ok++;
                }
                if (ok >= 6) r.zonesWithSixRays++;
            }
        }

        r.corridorAlongMin = float.MaxValue;
        r.corridorAcrossMin = float.MaxValue;
        if (links != null)
            foreach (var l in links)
            {
                Vector2 mid = (l.a + l.b) * 0.5f;
                Vector3 c = new Vector3(mid.x, ground(mid.x, mid.y) + 1.6f, mid.y);
                Vector2 dir = (l.b - l.a).normalized;
                Vector3 along = new Vector3(dir.x, 0f, dir.y);
                Vector3 across = new Vector3(-dir.y, 0f, dir.x);
                float a1 = ClearDistance(c, along, 60f, ground, trunks, pods);
                float a2 = ClearDistance(c, -along, 60f, ground, trunks, pods);
                float s1 = ClearDistance(c, across, 40f, ground, trunks, pods);
                float s2 = ClearDistance(c, -across, 40f, ground, trunks, pods);
                r.corridorAlongMin = Mathf.Min(r.corridorAlongMin, a1 + a2);
                r.corridorAcrossMin = Mathf.Min(r.corridorAcrossMin, s1 + s2);
            }
        if (float.IsInfinity(r.corridorAlongMin)) r.corridorAlongMin = 0f;
        if (float.IsInfinity(r.corridorAcrossMin)) r.corridorAcrossMin = 0f;

        // M6: 20 точек маршрута Entry → LabDoor, шаг 20 м.
        Vector2 e = new Vector2(entry.x, entry.z);
        Vector2 d2 = new Vector2(labDoor.x, labDoor.z);
        float route = Vector2.Distance(e, d2);
        if (route < 1f) return r;
        int points = Mathf.Max(1, Mathf.Min(20, Mathf.FloorToInt(route / 20f)));
        int withEdge = 0;
        var field = new List<float>();
        for (int i = 0; i < points; i++)
        {
            Vector2 p = Vector2.Lerp(e, d2, (i + 0.5f) / points);
            Vector3 c = new Vector3(p.x, ground(p.x, p.y) + 1.6f, p.y);
            bool edge = false;
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI * 2f / 8f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float far = ClearDistance(c, dir, 150f, ground, null, null);
                field.Add(far);
                float near = ClearDistance(c, dir, 80f, ground, null, null);
                if (far >= 80f && near < 150f) edge = true;
            }
            if (edge) withEdge++;
            r.routePoints++;
        }
        r.edgeShare = r.routePoints > 0 ? (float)withEdge / r.routePoints : 0f;
        r.fieldP50 = field.Count > 0 ? Percentile(field, 0.50f) : 0f;
        return r;
    }

    /// <summary>Длина свободного луча (м) до рельефа/ствола/пода; шаг 2 м.</summary>
    public static float ClearDistance(Vector3 origin, Vector3 dir, float maxDistance,
        Func<float, float, float> ground, IReadOnlyList<Vector3> trunks,
        IReadOnlyList<ForestSpace.CoverPod> pods)
    {
        if (ground == null) return 0f;
        Vector3 prev = origin;
        for (float d = 2f; d <= maxDistance; d += 2f)
        {
            Vector3 p = origin + dir.normalized * d;
            bool blocked = ForestSight.TerrainBlocks(prev, p, ground, 0f, 8) ||
                           ForestSight.TrunksBlock(prev, p, trunks) ||
                           ForestSight.CoverBlocks(prev, p, pods);
            if (blocked) return d - 2f;
            prev = p;
        }
        return maxDistance;
    }

    // ------------------------------------------------------------------
    // M8: POI/зоны (не боевые арены)
    // ------------------------------------------------------------------

    public sealed class PoiZoneResult
    {
        public int zones;
        public int smooth;
        public int twoApproaches;
        public int noTrunks;
        public int covered;
        public float slopeMax;
        public float corridorWidthP10;
        public float corridorWidthP50;
    }

    /// <summary>
    /// M8: ровное пятно ≤5°, 2 подхода ≥6 м, 0 стволов в пятне, под ≤12 м,
    /// ширина коридора — ЗАМЕР (p10/p50), а не декларация `c.width`.
    /// Число и размер POI/зон — данные S2/S3; 4–6 боевых арен снято решением 13.
    /// </summary>
    public static PoiZoneResult PoiZones(IReadOnlyList<ForestSpace.Zone> zones,
        IReadOnlyList<Vector3> trunks, IReadOnlyList<ForestSpace.CoverPod> pods,
        IReadOnlyList<ForestSpace.Link> links, Func<float, float, float> ground,
        DomeGenConfigSO cfg)
    {
        var r = new PoiZoneResult();
        if (zones == null || ground == null || cfg == null) return r;
        r.zones = zones.Count;
        foreach (var z in zones)
        {
            float worst = SlopeClasses.SlopeAt(cfg, z.center.x, z.center.y, ground,
                SlopeClasses.ClassEpsilon);
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI * 2f / 8f;
                float x = z.center.x + Mathf.Cos(a) * (z.radius + 20f);
                float y = z.center.y + Mathf.Sin(a) * (z.radius + 20f);
                worst = Mathf.Max(worst, SlopeClasses.SlopeAt(cfg, x, y, ground,
                    SlopeClasses.ClassEpsilon));
            }
            r.slopeMax = Mathf.Max(r.slopeMax, worst);
            if (worst <= 5f) r.smooth++;

            int approaches = 0;
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI * 2f / 8f;
                float x = z.center.x + Mathf.Cos(a) * (z.radius + 6f);
                float y = z.center.y + Mathf.Sin(a) * (z.radius + 6f);
                float s = SlopeClasses.SlopeAt(cfg, x, y, ground, SlopeClasses.ClassEpsilon);
                if (s <= cfg.WalkMaxDegrees) approaches++;
            }
            if (approaches >= 2) r.twoApproaches++;

            bool trunk = false;
            if (trunks != null)
                foreach (var t in trunks)
                    if (Vector2.Distance(new Vector2(t.x, t.z), z.center) < z.radius)
                    { trunk = true; break; }
            if (!trunk) r.noTrunks++;

            // «Под ≤12 м» — от КРАЯ зоны, а не от центра: под стоит на R+6,
            // и расстояние до центра R+6 не значит «укрытия нет».
            bool cover = false;
            if (pods != null)
                foreach (var p in pods)
                    if (Vector2.Distance(p.center, z.center) - z.radius <= 12f)
                    { cover = true; break; }
            if (cover) r.covered++;
        }

        var widths = new List<float>();
        if (links != null)
            foreach (var l in links)
            {
                Vector2 mid = (l.a + l.b) * 0.5f;
                Vector2 dir = (l.b - l.a).normalized;
                Vector2 side = new Vector2(-dir.y, dir.x);
                float nearest = float.MaxValue;
                if (trunks != null)
                    foreach (var t in trunks)
                    {
                        Vector2 rel = new Vector2(t.x, t.z) - mid;
                        // Только стволы В ОКНЕ вдоль оси: дерево в 100 м вдоль ленты
                        // не делает коридор узким в середине.
                        if (Mathf.Abs(Vector2.Dot(rel, dir)) > 10f) continue;
                        nearest = Mathf.Min(nearest, Mathf.Abs(Vector2.Dot(rel, side)));
                    }
                widths.Add(nearest < 1e6f ? nearest * 2f : l.width);
            }
        r.corridorWidthP10 = widths.Count > 0 ? Percentile(widths, 0.10f) : 0f;
        r.corridorWidthP50 = widths.Count > 0 ? Percentile(widths, 0.50f) : 0f;
        return r;
    }

    // ------------------------------------------------------------------
    // M14: лесные пятна и плотность
    // ------------------------------------------------------------------

    public sealed class PatchResult
    {
        public int patchCount;
        public float patchAreaShare;
        public float localDensityPerKm2;
        public float globalDensityPerKm2;
        public float crownCover;
        public int trunksInForbidden;
        public string note;
    }

    /// <summary>
    /// M14: число/площадь пятен, локальная плотность внутри пятна, глобальная — отчёт;
    /// 0 стволов в пятне POI/зоны и R12 от осевой, на воде и >35°.
    /// Пятно — 8-связная группа ячеек 100 м, где локальная плотность ≥ 2000 дер./км²
    /// (рабочее определение отчёта, не второй порог приёмки). Порог плотности
    /// 3000–3500 и площадь пятен — G8.
    /// </summary>
    public static PatchResult Patches(IReadOnlyList<Vector3> trunks, float radius,
        IReadOnlyList<ForestSpace.Zone> zones, IReadOnlyList<Vector2> poiAxes,
        DomeHydro.State hydro, Func<float, float, float> ground, DomeGenConfigSO cfg,
        float crownCover)
    {
        var r = new PatchResult { crownCover = crownCover };
        if (trunks == null || ground == null || cfg == null) return r;
        const float cell = 100f;
        int n = Mathf.Max(2, Mathf.CeilToInt(2f * radius / cell));
        float origin = -n * cell * 0.5f;
        var count = new int[n * n];
        var area = new float[n * n];
        foreach (var t in trunks)
        {
            int ix = Mathf.FloorToInt((t.x - origin) / cell);
            int iz = Mathf.FloorToInt((t.z - origin) / cell);
            if (ix < 0 || iz < 0 || ix >= n || iz >= n) continue;
            count[iz * n + ix]++;
        }
        for (int iz = 0; iz < n; iz++)
            for (int ix = 0; ix < n; ix++)
            {
                float x = origin + (ix + 0.5f) * cell;
                float z = origin + (iz + 0.5f) * cell;
                if (x * x + z * z > radius * radius) continue;
                area[iz * n + ix] = cell * cell;
            }

        var patch = new bool[n * n];
        for (int i = 0; i < n * n; i++)
        {
            float km2 = area[i] / 1_000_000f;
            if (km2 <= 0f) continue;
            float density = count[i] / km2;
            patch[i] = density >= 2000f;
        }
        var seen = new bool[n * n];
        var stack = new Stack<int>();
        int patches = 0, patchTrees = 0;
        double patchKm2 = 0;
        for (int i = 0; i < n * n; i++)
        {
            if (!patch[i] || seen[i]) continue;
            patches++;
            seen[i] = true;
            stack.Push(i);
            while (stack.Count > 0)
            {
                int c = stack.Pop();
                patchTrees += count[c];
                patchKm2 += area[c] / 1_000_000f;
                int cx = c % n, cz = c / n;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int x = cx + dx, z = cz + dz;
                        if (x < 0 || z < 0 || x >= n || z >= n) continue;
                        int j = z * n + x;
                        if (!patch[j] || seen[j]) continue;
                        seen[j] = true;
                        stack.Push(j);
                    }
            }
        }
        double diskKm2 = Mathf.PI * radius * radius / 1_000_000f;
        r.patchCount = patches;
        r.patchAreaShare = diskKm2 > 0 ? (float)(patchKm2 / diskKm2) : 0f;
        r.localDensityPerKm2 = patchKm2 > 0 ? (float)(patchTrees / patchKm2) : 0f;
        r.globalDensityPerKm2 = diskKm2 > 0 ? (float)(trunks.Count / diskKm2) : 0f;

        // Запреты: зоны, осевые (R12), вода, >35°.
        foreach (var t in trunks)
        {
            bool bad = false;
            if (zones != null)
                foreach (var z in zones)
                    if (Vector2.Distance(new Vector2(t.x, t.z), z.center) < z.radius)
                    { bad = true; break; }
            if (!bad && poiAxes != null)
                foreach (var p in poiAxes)
                    if (Vector2.Distance(new Vector2(t.x, t.z), p) < 12f) { bad = true; break; }
            if (!bad && hydro != null)
            {
                if (Vector2.Distance(new Vector2(t.x, t.z), hydro.lakeCenter) <
                    hydro.lakeRadius + 2f) bad = true;
                for (int i = 1; i < hydro.riverPts.Count && !bad; i++)
                    if (DomeFootprint.DistanceToSegment(new Vector2(t.x, t.z),
                            hydro.riverPts[i - 1], hydro.riverPts[i]) <
                        hydro.riverHalfWidth + 2f) bad = true;
            }
            if (!bad)
            {
                float s = SlopeClasses.SlopeAt(cfg, t.x, t.z, ground, SlopeClasses.ClassEpsilon);
                if (s > cfg.WallMinDegrees) bad = true;
            }
            if (bad) r.trunksInForbidden++;
        }
        r.note = $"cells={n}×{n} локальная плотность порога пятна 2000/км² (рабочее определение)";
        return r;
    }

    // ------------------------------------------------------------------
    // M15: дороги база → ворота → зоны
    // ------------------------------------------------------------------

    public sealed class RoadResult
    {
        public bool built;
        public int primaryPois;
        public int components;
        public int loops;
        public float lengthKm;
        public float slopeP95;
        public float slopeMax;
        public float widthP10;
        public int fords;
        public string note;
    }

    /// <summary>
    /// M15: все primary POI в одной компоненте, 1–2 петли с POI, уклон/ширина/брод,
    /// сеть 2–4 км. Пороги — S4 §5.1–§5.3; S5 только меряет.
    /// </summary>
    public static RoadResult Roads(ForestRoadNetwork net, IReadOnlyList<ForestPoi> pois,
        Func<float, float, float> ground, DomeGenConfigSO cfg)
    {
        var r = new RoadResult();
        if (net == null || net.roads == null || net.roads.Count == 0)
        {
            r.note = "дороги не построены (S4 слой отсутствует)";
            return r;
        }
        r.built = true;
        r.loops = net.loops;
        r.lengthKm = net.length / 1000f;
        var widths = new List<float>();
        var slopes = new List<float>();
        foreach (var road in net.roads)
        {
            widths.Add(road.width);
            if (road.ford) r.fords++;
            if (road.points == null) continue;
            for (int i = 1; i < road.points.Count; i++)
            {
                Vector2 a = road.points[i - 1], b = road.points[i];
                float len = Vector2.Distance(a, b);
                if (len < 0.1f) continue;
                float ha = ground(a.x, a.y), hb = ground(b.x, b.y);
                slopes.Add(Mathf.Atan(Mathf.Abs(hb - ha) / len) * Mathf.Rad2Deg);
            }
        }
        r.widthP10 = widths.Count > 0 ? Percentile(widths, 0.10f) : 0f;
        r.slopeP95 = slopes.Count > 0 ? Percentile(slopes, 0.95f) : 0f;
        foreach (float s in slopes) r.slopeMax = Mathf.Max(r.slopeMax, s);

        // Компоненты primary POI: Base / Gate / Zone. Ребро — дорога, у которой
        // концы попадают к разным POI (ближайший в радиусе 25 м).
        var nodes = new List<ForestPoi>();
        if (pois != null)
            foreach (var p in pois)
                if (p.kind == ForestPoiKind.Base || p.kind == ForestPoiKind.Gate ||
                    p.kind == ForestPoiKind.Zone)
                    nodes.Add(p);
        r.primaryPois = nodes.Count;
        var parent = new int[Mathf.Max(1, nodes.Count)];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);
        void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[b] = a; }
        foreach (var road in net.roads)
        {
            if (road.points == null || road.points.Count < 2) continue;
            // POI может лежать в СЕРЕДИНЕ ленты, а не только на конце: дорога проходит
            // через ворота и продолжается. Поэтому собираем все POI у ленты, а не два конца.
            int first = -1;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (DistanceToPolyline(nodes[i].pos, road.points) > 25f) continue;
                if (first < 0) first = i;
                else Union(first, i);
            }
        }
        var roots = new HashSet<int>();
        for (int i = 0; i < nodes.Count; i++) roots.Add(Find(i));
        r.components = nodes.Count > 0 ? roots.Count : 0;
        r.note = $"POI {nodes.Count}, компонент {r.components}, петель {r.loops}, " +
                 $"длина {r.lengthKm:F2} км";
        return r;
    }

    static float DistanceToPolyline(Vector2 p, IReadOnlyList<Vector2> points)
    {
        if (points == null || points.Count == 0) return float.MaxValue;
        float best = float.MaxValue;
        for (int i = 1; i < points.Count; i++)
            best = Mathf.Min(best, DomeFootprint.DistanceToSegment(p, points[i - 1], points[i]));
        if (points.Count == 1) best = Vector2.Distance(p, points[0]);
        return best;
    }

    // ------------------------------------------------------------------
    // M16: гидроэрозия B (ключ, время, fallback)
    // ------------------------------------------------------------------

    public sealed class HydroResult
    {
        public string key;
        public string status; // ok | missing | timeout | off
        public float bakeMs;
        public ulong hash;
        public int resolution;
        public bool deterministic;
    }

    /// <summary>
    /// M16: одно запекание на ключ `(seed, worldVersion, erosionVersion, resolution)`;
    /// первый запуск ≤60 с, повторная сборка 0 мс; тот же ассет и отпечаток;
    /// fallback `missing|timeout` виден, не ошибка.
    /// </summary>
    public static HydroResult Hydro(ForestBuilder b)
    {
        if (b == null || b.Erosion == null)
            return new HydroResult { status = "off" };
        return Hydro(b.Erosion, b.ResolvedSeed, b.WorldVersion);
    }

    /// <summary>M16 без <see cref="ForestBuilder"/>: синтетика и повторная сборка.</summary>
    public static HydroResult Hydro(DomeErosion.State e, long seed, int worldVersion)
    {
        var r = new HydroResult { status = "off" };
        if (e == null) return r;
        r.resolution = e.resolution;
        r.key = DomeErosion.Key(seed, worldVersion, e.erosionVersion, e.resolution);
        r.bakeMs = e.hydraulicSeconds * 1000f;
        r.hash = e.deltaSum;
        string s = e.hydraulicStatus ?? "off";
        if (s.StartsWith("missing")) r.status = "missing";
        else if (s.StartsWith("timeout")) r.status = "timeout";
        else if (s == "baked" || s == "cache" || s == "ok") r.status = "ok";
        else r.status = s == "off" ? "off" : "missing";
        r.deterministic = r.hash != 0UL;
        return r;
    }

    // ------------------------------------------------------------------
    // M11: бюджет
    // ------------------------------------------------------------------

    public sealed class BudgetResult
    {
        public int renderers;
        public long triangles;
        public int colliders;
        public int trees;
        public int deadLogs;
    }

    /// <summary>
    /// M11: счётчики мира. Абсолютные потолки — S2 §5.2 / §G8: ≤8k рендереров,
    /// ≤0.8M треугольников, ≤12 с сборка.
    /// </summary>
    public static BudgetResult Budget(ForestBuilder b)
    {
        var r = new BudgetResult();
        if (b == null) return r;
        r.trees = b.Trees;
        r.deadLogs = b.DeadLogs;
        r.colliders = b.TreeColliders;
        if (b.BuiltRoot == null) return r;
        var renderers = b.BuiltRoot.GetComponentsInChildren<Renderer>(true);
        r.renderers = renderers.Length;
        foreach (var rend in renderers)
        {
            if (rend is MeshRenderer)
            {
                var mf = rend.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    r.triangles += mf.sharedMesh.triangles.Length / 3;
            }
            else if (rend is SkinnedMeshRenderer smr)
            {
                if (smr.sharedMesh != null)
                    r.triangles += smr.sharedMesh.triangles.Length / 3;
            }
        }
        return r;
    }

    // ------------------------------------------------------------------
    // Инструменты
    // ------------------------------------------------------------------

    /// <summary>Значение перцентиля по копии (не портит вход).</summary>
    public static float Percentile(IReadOnlyList<float> values, float p)
    {
        if (values == null || values.Count == 0) return 0f;
        var copy = new float[values.Count];
        for (int i = 0; i < values.Count; i++) copy[i] = values[i];
        Array.Sort(copy);
        int i2 = Mathf.Clamp(Mathf.RoundToInt(p * (copy.Length - 1)), 0, copy.Length - 1);
        return copy[i2];
    }

    static double Mean(float[] v)
    {
        double s = 0;
        foreach (float x in v) s += x;
        return v.Length > 0 ? s / v.Length : 0;
    }

    static double Variance(float[] v, double mean)
    {
        double s = 0;
        foreach (float x in v) { double d = x - mean; s += d * d; }
        return v.Length > 0 ? s / v.Length : 0;
    }

    /// <summary>Box-фильтр через интегральное изображение; окно у края обрезается.</summary>
    static float[] BoxFilter(float[] v, int n, int half)
    {
        half = Mathf.Max(1, half);
        var integral = new double[(n + 1) * (n + 1)];
        for (int z = 0; z < n; z++)
        {
            double row = 0;
            for (int x = 0; x < n; x++)
            {
                row += v[z * n + x];
                integral[(z + 1) * (n + 1) + (x + 1)] =
                    integral[z * (n + 1) + (x + 1)] + row;
            }
        }
        var outv = new float[n * n];
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int x0 = Mathf.Max(0, x - half), x1 = Mathf.Min(n - 1, x + half);
                int z0 = Mathf.Max(0, z - half), z1 = Mathf.Min(n - 1, z + half);
                double sum = integral[(z1 + 1) * (n + 1) + (x1 + 1)]
                           - integral[z0 * (n + 1) + (x1 + 1)]
                           - integral[(z1 + 1) * (n + 1) + x0]
                           + integral[z0 * (n + 1) + x0];
                int count = (x1 - x0 + 1) * (z1 - z0 + 1);
                outv[z * n + x] = (float)(sum / count);
            }
        return outv;
    }
}
