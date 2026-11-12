using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Поле высот павильона (s10a): FBM-база + ridged-гребни с маской гор + domain warp.
/// S1 (2026-11-01): у поля ДВЕ ветки, и v1 остаётся бит-в-бит.
/// <list type="bullet">
/// <item>v1 — прежняя формула (соль <c>version</c>): A/C/B0 и все замороженные хэши.</item>
/// <item>v2 — три полосы <c>H0 = Σ A_b·N_b(W_b)</c>: макро / мезо / микро, warp своей полосы,
/// ridged только на мезо и только под маской, соль <c>worldVersion</c>.</item>
/// </list>
/// Чистые функции в мировых координатах — чанк-готовы: соседние чанки семплят то же поле,
/// швы сходятся 1:1 по построению. Джоба считает ТЕМ ЖЕ <see cref="SampleRaw"/>.
/// </summary>
public static class DomeHeightField
{
    const long BaseSalt = 0x51F15EEDL;
    const long RidgeSalt = 0x1F3D5B79L;
    const long MaskSalt = 0x77AA55CCL;
    const long WarpSaltX = 0x2B4D8A1FL;
    const long WarpSaltZ = 0x6C9E3D77L;

    // Соли v2 — отдельные: ветки не смешиваются, v1-хэш не зависит от них.
    const long MacroSalt = 0x3A17C9E1L;
    const long MesoSalt = 0x5D2E7B43L;
    const long MicroSalt = 0x71C4A9D5L;
    const long MacroWarpX = 0x1B8F3D21L;
    const long MacroWarpZ = 0x4C6A2E97L;
    const long MesoWarpX = 0x2F9D5B13L;
    const long MesoWarpZ = 0x6E3B8A57L;
    const long MicroWarpX = 0x7A1C4E89L;
    const long MicroWarpZ = 0x3D5F9B21L;
    const long MesoMaskSalt = 0x58C1E7A3L;

    /// <summary>Опорный диаметр полос: λ и P заданы в метрах на Ø2000 и масштабируются D/2000.</summary>
    public const float BandReferenceDiameter = 2000f;

    /// <summary>Радиус опорного диска (rin на Ø2000) — на нём центрируются полосы.</summary>
    const float BandReferenceRadius = 920f;

    /// <summary>
    /// Константа нормировки (S1 §3.2: «нормировочный коэффициент — константа, не max по сетке»).
    /// Value-noise после октавной нормировки слишком гладкий: p95 уклона выходил 6.4° при
    /// цели 15–19°. Мягкое насыщение растягивает рабочую часть распределения к ±1, НЕ выходя
    /// за конверт P/2; число подобрано пробой на поле.
    /// </summary>
    const float BandNormalization = 3.55f;

    /// <summary>
    /// Blittable-параметры поля: копируются в джобу (ScriptableObject в джобу нельзя).
    /// Один источник математики — SampleRaw: скаляр и джоба считают им.
    /// </summary>
    public struct DomeHeightParams
    {
        public long seed;
        public int version;
        public int worldVersion;
        public float mapScale;

        public int octaves;
        public int ridgedOctaves;
        public int warpOctaves;
        public float warpStrength;
        public float baseFrequency;
        public float amplitude;

        public float macroLambda;
        public float macroAmplitude;
        public int macroOctaves;
        public float macroWarp;

        public float mesoLambda;
        public float mesoAmplitude;
        public int mesoOctaves;
        public float mesoWarp;
        public int mesoRidged;

        public float microLambda;
        public float microAmplitude;
        public int microOctaves;
        public float microWarp;

        // DC-центрирование каждой полосы на опорном диске (S1 §3.7: |mean H0| ≤ 0.5 м).
        public float dcMacro;
        public float dcMeso;
        public float dcMicro;

        public static DomeHeightParams From(DomeGenConfigSO cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            var p = new DomeHeightParams
            {
                seed = cfg.seed,
                version = cfg.version,
                worldVersion = cfg.WorldVersion,
                mapScale = Mathf.Max(0.01f, cfg.mapDiameter / BandReferenceDiameter),
                octaves = cfg.octaves,
                ridgedOctaves = cfg.ridgedOctaves,
                warpOctaves = cfg.warpOctaves,
                warpStrength = cfg.warpStrength,
                baseFrequency = cfg.baseFrequency,
                amplitude = cfg.amplitude,
                macroLambda = cfg.macroLambda,
                macroAmplitude = cfg.macroAmplitude,
                macroOctaves = cfg.macroOctaves,
                macroWarp = cfg.macroWarp,
                mesoLambda = cfg.mesoLambda,
                mesoAmplitude = cfg.mesoAmplitude,
                mesoOctaves = cfg.mesoOctaves,
                mesoWarp = cfg.mesoWarp,
                mesoRidged = cfg.mesoRidged ? 1 : 0,
                microLambda = cfg.microLambda,
                microAmplitude = cfg.microAmplitude,
                microOctaves = cfg.microOctaves,
                microWarp = cfg.microWarp,
            };
            if (p.worldVersion >= 2)
            {
                var dc = DcFor(p);
                p.dcMacro = dc.x;
                p.dcMeso = dc.y;
                p.dcMicro = dc.z;
            }
            return p;
        }
    }

    // Кэш DC-центрирования: ключ — (seed, worldVersion, параметры полос). From зовут
    // на каждый семпл (Hydro/чанки), поэтому квадратура считается ОДИН раз на конфиг.
    static readonly Dictionary<ulong, Vector3> DcCache = new Dictionary<ulong, Vector3>();

    static Vector3 DcFor(DomeHeightParams p)
    {
        ulong key = DcKey(p);
        if (DcCache.TryGetValue(key, out var cached)) return cached;

        // Опорный диск в нормированных координатах: mapScale=1, радиус 920 м.
        var p0 = p;
        p0.mapScale = 1f;
        long seed = p0.seed ^ ((long)p0.worldVersion * 7919L);
        const int n = 48;
        float step = 2f * BandReferenceRadius / (n - 1f);
        double sum0 = 0, sum1 = 0, sum2 = 0;
        int count = 0;
        for (int iz = 0; iz < n; iz++)
            for (int ix = 0; ix < n; ix++)
            {
                float x = -BandReferenceRadius + ix * step;
                float z = -BandReferenceRadius + iz * step;
                if (x * x + z * z > BandReferenceRadius * BandReferenceRadius) continue;
                sum0 += Normalize(RawBand(p0, seed, x, z, 0));
                sum1 += Normalize(RawBand(p0, seed, x, z, 1));
                sum2 += Normalize(RawBand(p0, seed, x, z, 2));
                count++;
            }
        var dc = count > 0
            ? new Vector3((float)(sum0 / count), (float)(sum1 / count), (float)(sum2 / count))
            : Vector3.zero;
        DcCache[key] = dc;
        return dc;
    }

    static ulong DcKey(DomeHeightParams p)
    {
        unchecked
        {
            ulong h = 14695981039346656037UL;
            MixKey(ref h, (ulong)p.seed);
            MixKey(ref h, (ulong)(uint)p.worldVersion);
            MixKey(ref h, (ulong)FloatBits(p.macroLambda));
            MixKey(ref h, (ulong)p.macroOctaves);
            MixKey(ref h, (ulong)FloatBits(p.macroWarp));
            MixKey(ref h, (ulong)FloatBits(p.mesoLambda));
            MixKey(ref h, (ulong)p.mesoOctaves);
            MixKey(ref h, (ulong)FloatBits(p.mesoWarp));
            MixKey(ref h, (ulong)p.mesoRidged);
            MixKey(ref h, (ulong)FloatBits(p.microLambda));
            MixKey(ref h, (ulong)p.microOctaves);
            MixKey(ref h, (ulong)FloatBits(p.microWarp));
            return h;
        }
    }

    static uint FloatBits(float v) => BitConverter.ToUInt32(BitConverter.GetBytes(v), 0);

    static void MixKey(ref ulong h, ulong v)
    {
        h ^= v + 0x9E3779B97F4A7C15UL + (h << 6) + (h >> 2);
    }

    public static float SampleHeight(DomeGenConfigSO cfg, float x, float z)
    {
        return SampleRaw(DomeHeightParams.From(cfg), x, z);
    }

    public static float SampleRaw(DomeHeightParams p, float x, float z)
    {
        return p.worldVersion >= 2 ? SampleBands(p, x, z) : SampleV1(p, x, z);
    }

    /// <summary>
    /// v1 — прежняя формула (HEAD 885c7338), НЕ трогать: её числа заморожены S0.
    /// </summary>
    static float SampleV1(DomeHeightParams p, float x, float z)
    {
        long seed = p.seed ^ ((long)p.version * 7919L);
        float f = p.baseFrequency;

        float wx = 0f, wz = 0f;
        if (p.warpOctaves > 0 && p.warpStrength > 0f)
        {
            wx = ValueNoise2D.Fbm(seed + WarpSaltX, x * f, z * f, p.warpOctaves);
            wz = ValueNoise2D.Fbm(seed + WarpSaltZ, x * f, z * f, p.warpOctaves);
        }
        float px = x + wx * p.warpStrength;
        float pz = z + wz * p.warpStrength;

        float b = ValueNoise2D.Fbm(seed + BaseSalt, px * f, pz * f, p.octaves);
        float mask = ValueNoise2D.Fbm(seed + MaskSalt, x * f * 0.25f, z * f * 0.25f, 3);
        float m = Mathf.Clamp01((mask - 0.05f) / 0.5f);
        float r = 0f;
        if (p.ridgedOctaves > 0)
        {
            float n = ValueNoise2D.Fbm(seed + RidgeSalt, px * f * 2f, pz * f * 2f, p.ridgedOctaves);
            float ridge = 1f - Mathf.Abs(n);
            r = ridge * ridge;
        }
        return p.amplitude * (0.55f * b + 0.65f * (r * m - 0.35f * m) - 0.12f * (1f - m));
    }

    /// <summary>
    /// v2 — три zero-mean полосы. Соль — <c>worldVersion</c>, <c>version</c> не участвует:
    /// смена версии поля не должна означать смену формулы v1.
    /// </summary>
    static float SampleBands(DomeHeightParams p, float x, float z)
    {
        long seed = p.seed ^ ((long)p.worldVersion * 7919L);
        return SampleBand(p, seed, x, z, 0)
             + SampleBand(p, seed, x, z, 1)
             + SampleBand(p, seed, x, z, 2);
    }

    static float SampleBand(DomeHeightParams p, long seed, float x, float z, int band)
    {
        BandSpec(p, band, out float lambda, out float amplitude, out int octaves,
            out float warpFrac, out long salt, out long warpSaltX, out long warpSaltZ, out bool ridged);
        if (lambda <= 0f || amplitude <= 0f || octaves <= 0) return 0f;

        float raw = RawBand(p, seed, x, z, band);
        float dc = band == 0 ? p.dcMacro : band == 1 ? p.dcMeso : p.dcMicro;
        // DC снят ПОСЛЕ насыщения: mean(Normalize) у полосы виден на опорном диске,
        // а вычитать его до насыщения — значит оставить остаток от нечётности.
        float v = Mathf.Clamp(Normalize(raw) - dc, -1f, 1f);
        return (amplitude * 0.5f) * v; // A = P/2
    }

    /// <summary>Нормированная полоса ДО амплитуды; DC-центрирование считается по ней.</summary>
    static float RawBand(DomeHeightParams p, long seed, float x, float z, int band)
    {
        BandSpec(p, band, out float lambda, out float amplitude, out int octaves,
            out float warpFrac, out long salt, out long warpSaltX, out long warpSaltZ, out bool ridged);
        if (lambda <= 0f || amplitude <= 0f || octaves <= 0) return 0f;

        float inv = 1f / lambda;
        float wx = x, wz = z;
        if (warpFrac > 0f)
        {
            float aw = warpFrac * lambda;
            wx += ValueNoise2D.Fbm(seed + warpSaltX, x * inv, z * inv, 1) * aw;
            wz += ValueNoise2D.Fbm(seed + warpSaltZ, x * inv, z * inv, 1) * aw;
        }

        float n = ValueNoise2D.Fbm(seed + salt, wx * inv, wz * inv, octaves);
        if (ridged)
        {
            // Маска — отдельная полоса (не тот же шум): тогда E[r−1/3]·m = 0.
            float mask = ValueNoise2D.Fbm(seed + MesoMaskSalt, x * inv * 0.5f, z * inv * 0.5f, 2);
            float m = Mathf.Clamp01((mask - 0.10f) / 0.50f);
            if (m > 0f)
            {
                float r = 1f - Mathf.Abs(n);
                float centred = r * r - (1f / 3f); // E[(1−|n|)²] = 1/3
                n = Mathf.Lerp(n, centred, m);
            }
        }
        return n;
    }

    /// <summary>
    /// Мягкое насыщение к ±1: растягивает рабочую часть value-noise (иначе поле слишком
    /// гладкое), но не выходит за конверт <c>|N| ≤ 1</c>. Нечётная функция — mean не
    /// сдвигает; в нуле производная = нормировочной константе.
    /// </summary>
    static float Normalize(float n)
    {
        float g = n * BandNormalization;
        return g / Mathf.Sqrt(1f + g * g);
    }

    /// <summary>Смещение domain warp полосы (м): для детектора «warp не складывает поле».</summary>
    public static Vector2 WarpOffset(DomeHeightParams p, int band, float x, float z)
    {
        BandSpec(p, band, out float lambda, out float amplitude, out int octaves,
            out float warpFrac, out long salt, out long warpSaltX, out long warpSaltZ, out bool ridged);
        if (lambda <= 0f || amplitude <= 0f || octaves <= 0 || warpFrac <= 0f) return Vector2.zero;
        long seed = p.seed ^ ((long)p.worldVersion * 7919L);
        float inv = 1f / lambda;
        float aw = warpFrac * lambda;
        return new Vector2(
            ValueNoise2D.Fbm(seed + warpSaltX, x * inv, z * inv, 1) * aw,
            ValueNoise2D.Fbm(seed + warpSaltZ, x * inv, z * inv, 1) * aw);
    }

    /// <summary>Сумма амплитуд полос — конверт |H0| v2 (≤63 м на Ø2000 по S1 §4).</summary>
    public static float BandEnvelope(DomeHeightParams p)
    {
        float s = p.mapScale > 0f ? p.mapScale : 1f;
        return 0.5f * (p.macroAmplitude + p.mesoAmplitude + p.microAmplitude) * s;
    }

    /// <summary>Самая короткая волна октавы в v2 — контракт <c>lambdaMin</c>.</summary>
    public static float MinBandLambda(DomeHeightParams p)
    {
        float s = p.mapScale > 0f ? p.mapScale : 1f;
        float macro = p.macroLambda * s / Mathf.Pow(2f, Mathf.Max(0, p.macroOctaves - 1));
        float meso = p.mesoLambda * s / Mathf.Pow(2f, Mathf.Max(0, p.mesoOctaves - 1));
        float micro = p.microLambda * s / Mathf.Pow(2f, Mathf.Max(0, p.microOctaves - 1));
        return Mathf.Min(macro, Mathf.Min(meso, micro));
    }

    /// <summary>Объявленный наклон полосы: πP/λ — потолок полосы (S1 §3.2).</summary>
    public static float BandSlopeBudget(float amplitude, float lambda)
    {
        return lambda > 0f ? Mathf.PI * amplitude / lambda : 0f;
    }

    static void BandSpec(DomeHeightParams p, int band, out float lambda, out float amplitude,
        out int octaves, out float warpFrac, out long salt, out long warpX, out long warpZ, out bool ridged)
    {
        float s = p.mapScale > 0f ? p.mapScale : 1f;
        ridged = false;
        switch (band)
        {
            case 0:
                lambda = p.macroLambda * s;
                amplitude = p.macroAmplitude * s;
                octaves = p.macroOctaves;
                warpFrac = p.macroWarp;
                salt = MacroSalt;
                warpX = MacroWarpX;
                warpZ = MacroWarpZ;
                break;
            case 1:
                lambda = p.mesoLambda * s;
                amplitude = p.mesoAmplitude * s;
                octaves = p.mesoOctaves;
                warpFrac = p.mesoWarp;
                salt = MesoSalt;
                warpX = MesoWarpX;
                warpZ = MesoWarpZ;
                ridged = p.mesoRidged != 0;
                break;
            default:
                lambda = p.microLambda * s;
                amplitude = p.microAmplitude * s;
                octaves = p.microOctaves;
                warpFrac = p.microWarp;
                salt = MicroSalt;
                warpX = MicroWarpX;
                warpZ = MicroWarpZ;
                break;
        }
    }

    /// <summary>Хэш сетки — якорь теста детерминизма (как WorldHeightField.SampleGridHash).</summary>
    public static ulong SampleGridHash(DomeGenConfigSO cfg, int resolution, float step)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        unchecked
        {
            ulong h = ((ulong)cfg.seed + 0x9E3779B97F4A7C15UL) ^ ((ulong)(uint)cfg.version << 1);
            // v1-ветка обязана остаться бит-в-бит: версия поля входит в хэш только у v2.
            if (cfg.WorldVersion > 1)
            {
                ulong wv = (ulong)(uint)cfg.WorldVersion;
                h ^= wv * 0x9E3779B97F4A7C15UL + (h << 6) + (h >> 2);
            }
            for (int iz = 0; iz < resolution; iz++)
                for (int ix = 0; ix < resolution; ix++)
                {
                    float height = SampleHeight(cfg, ix * step, iz * step);
                    uint bits = BitConverter.ToUInt32(BitConverter.GetBytes(height), 0);
                    h ^= bits + 0x9E3779B97F4A7C15UL + (h << 6) + (h >> 2);
                }
            return h;
        }
    }
}
