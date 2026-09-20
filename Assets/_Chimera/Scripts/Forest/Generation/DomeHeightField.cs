using System;
using UnityEngine;

/// <summary>
/// Поле высот павильона (s10a): FBM-база + ridged-гребни с маской гор + domain warp.
/// Чистые функции в мировых координатах — чанк-готовы: соседние чанки семплят то же поле,
/// швы сходятся 1:1 по построению. Потоки развязаны сдвигом сида (не сдвигают s1-поле).
/// Выход — строго в ±amplitude (коэффициенты подобраны с запасом).
/// </summary>
public static class DomeHeightField
{
    const long BaseSalt = 0x51F15EEDL;
    const long RidgeSalt = 0x1F3D5B79L;
    const long MaskSalt = 0x77AA55CCL;
    const long WarpSaltX = 0x2B4D8A1FL;
    const long WarpSaltZ = 0x6C9E3D77L;

    /// <summary>
    /// Blittable-параметры поля: копируются в джобу (ScriptableObject в джобу нельзя).
    /// Один источник математики — SampleRaw: скаляр и джоба считают им.
    /// </summary>
    public struct DomeHeightParams
    {
        public long seed;
        public int version;
        public int octaves;
        public int ridgedOctaves;
        public int warpOctaves;
        public float warpStrength;
        public float baseFrequency;
        public float amplitude;

        public static DomeHeightParams From(DomeGenConfigSO cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            return new DomeHeightParams
            {
                seed = cfg.seed,
                version = cfg.version,
                octaves = cfg.octaves,
                ridgedOctaves = cfg.ridgedOctaves,
                warpOctaves = cfg.warpOctaves,
                warpStrength = cfg.warpStrength,
                baseFrequency = cfg.baseFrequency,
                amplitude = cfg.amplitude,
            };
        }
    }

    public static float SampleHeight(DomeGenConfigSO cfg, float x, float z)
    {
        return SampleRaw(DomeHeightParams.From(cfg), x, z);
    }

    public static float SampleRaw(DomeHeightParams p, float x, float z)
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

    /// <summary>Хэш сетки — якорь теста детерминизма (как WorldHeightField.SampleGridHash).</summary>
    public static ulong SampleGridHash(DomeGenConfigSO cfg, int resolution, float step)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        unchecked
        {
            ulong h = ((ulong)cfg.seed + 0x9E3779B97F4A7C15UL) ^ ((ulong)(uint)cfg.version << 1);
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
