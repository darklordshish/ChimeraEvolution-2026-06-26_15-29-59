using System;
using UnityEngine;

/// <summary>
/// Поле высот мира: высота = FBM(seed, x·freq, z·freq) · amplitude. Чистые функции,
/// семплирование — в мировых координатах (пересборка 1:1 при том же сиде).
/// Потребитель — террейн-аппликатор (s1b); ему же принадлежит проверка связности по NavMesh.
/// SampleGridHash — только для тестов/инструментов, не вызывать покадрово. Лес, слайс s1.
/// </summary>
public static class WorldHeightField
{
    public static float SampleHeight(WorldGenConfigSO cfg, float x, float z)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        return ValueNoise2D.Fbm(cfg.seed, x * cfg.baseFrequency, z * cfg.baseFrequency, cfg.octaves)
            * cfg.amplitude;
    }

    /// <summary>Хэш сетки высот resolution×resolution с шагом step — якорь теста детерминизма.</summary>
    public static ulong SampleGridHash(WorldGenConfigSO cfg, int resolution, float step)
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

    /// <summary>Максимальный уклон сетки в градусах (центральные разности, шаг step).</summary>
    public static float MaxSlopeDegrees(WorldGenConfigSO cfg, int resolution, float step)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        float maxSlope = 0f;
        for (int iz = 0; iz < resolution; iz++)
            for (int ix = 0; ix < resolution; ix++)
            {
                float hx0 = SampleHeight(cfg, (ix - 1) * step, iz * step);
                float hx1 = SampleHeight(cfg, (ix + 1) * step, iz * step);
                float hz0 = SampleHeight(cfg, ix * step, (iz - 1) * step);
                float hz1 = SampleHeight(cfg, ix * step, (iz + 1) * step);
                float gx = (hx1 - hx0) / (2f * step);
                float gz = (hz1 - hz0) / (2f * step);
                float slope = (float)(Math.Atan(Math.Sqrt(gx * gx + gz * gz)) * (180.0 / Math.PI));
                if (slope > maxSlope) maxSlope = slope;
            }
        return maxSlope;
    }
}
