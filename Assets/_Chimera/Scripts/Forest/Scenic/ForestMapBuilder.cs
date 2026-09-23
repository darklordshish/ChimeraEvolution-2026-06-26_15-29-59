using System;
using UnityEngine;

/// <summary>
/// Детектор рельефа: цвета клеток + маска крутизны + статистики. Чистый, без сцены.
/// Зеркалит WorldRules.CheckGrid (согласованность — тестом), но отдаёт картинку, а не список.
/// Слой логовищ — только по реальным LairSite (s2b), фейковые точки запрещены.
/// Лес, слайс s5.
/// </summary>
public static class ForestMapBuilder
{
    public struct Stats
    {
        public float minH;
        public float maxH;
        public float maxSlope;
        public int steepCells;
        public int totalCells;
    }

    static readonly Color Valley = new Color(0.29f, 0.49f, 0.23f);
    static readonly Color Rock = new Color(0.5f, 0.5f, 0.5f);
    static readonly Color Dry = new Color(0.66f, 0.56f, 0.37f);
    static readonly Color SteepTint = new Color(0.85f, 0.15f, 0.1f);

    /// <summary>
    /// Цвет рельефа по нормализованной высоте t [0, 1] (0 — дно, 1 — пик):
    /// сочная низина → серый камень (дольше) → сухая охра. Кость ОТМЕНЕНА (s10d):
    /// белизна высокогорья читалась снегом, а павильон тёплый.
    /// </summary>
    public static Color ReliefColor(float t)
    {
        t = t < 0f ? 0f : (t > 1f ? 1f : t);
        return t < 0.6f
            ? Color.Lerp(Valley, Rock, t / 0.6f)
            : Color.Lerp(Rock, Dry, (t - 0.6f) / 0.4f);
    }

    /// <summary>Базовый рельеф: сочная низина → серый камень → сухая охра (без снега).</summary>
    public static Color[] BuildColors(WorldGenConfigSO cfg, int res, float step)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        var colors = new Color[res * res];
        for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                float h = WorldHeightField.SampleHeight(cfg, ix * step, iz * step);
                float t = h / Math.Max(cfg.amplitude, 1e-6f) * 0.5f + 0.5f;
                colors[iz * res + ix] = ReliefColor(t);
            }
        return colors;
    }

    /// <summary>Крутые клетки (уклон > maxSlopeDegrees конфига) — отдельным слоем.</summary>
    public static bool[] BuildSteepMask(WorldGenConfigSO cfg, int res, float step)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        var mask = new bool[res * res];
        for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
                mask[iz * res + ix] = SlopeAt(cfg, ix * step, iz * step, step) > cfg.maxSlopeDegrees;
        return mask;
    }

    public static Stats BuildStats(WorldGenConfigSO cfg, int res, float step)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        var st = new Stats { minH = float.MaxValue, maxH = float.MinValue, totalCells = res * res };
        for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                float h = WorldHeightField.SampleHeight(cfg, ix * step, iz * step);
                if (h < st.minH) st.minH = h;
                if (h > st.maxH) st.maxH = h;
                float slope = SlopeAt(cfg, ix * step, iz * step, step);
                if (slope > st.maxSlope) st.maxSlope = slope;
                if (slope > cfg.maxSlopeDegrees) st.steepCells++;
            }
        return st;
    }

    /// <summary>Хэш по цветам клеток (не по байтам PNG — кодировщик платформенно плывёт).</summary>
    public static ulong ColorsHash(Color[] colors)
    {
        if (colors == null) throw new ArgumentNullException(nameof(colors));
        unchecked
        {
            ulong h = 1469598103934665603UL;
            foreach (var c in colors)
            {
                h ^= FloatBits(c.r) + 0x9E3779B97F4A7C15UL + (h << 6) + (h >> 2);
                h ^= FloatBits(c.g) + 0x9E3779B97F4A7C15UL + (h << 6) + (h >> 2);
                h ^= FloatBits(c.b) + 0x9E3779B97F4A7C15UL + (h << 6) + (h >> 2);
            }
            return h;
        }
    }

    // Зеркало центральных разностей WorldHeightField (там нет поточечного уклона;
    // трогать s1-файл из s5-ветки нельзя — 6 строк живут здесь с этой пометкой).
    static float SlopeAt(WorldGenConfigSO cfg, float x, float z, float step)
    {
        float gx = (WorldHeightField.SampleHeight(cfg, x + step, z)
            - WorldHeightField.SampleHeight(cfg, x - step, z)) / (2f * step);
        float gz = (WorldHeightField.SampleHeight(cfg, x, z + step)
            - WorldHeightField.SampleHeight(cfg, x, z - step)) / (2f * step);
        return (float)(Math.Atan(Math.Sqrt(gx * gx + gz * gz)) * (180.0 / Math.PI));
    }

    static uint FloatBits(float v)
    {
        return BitConverter.ToUInt32(BitConverter.GetBytes(v), 0);
    }
}
