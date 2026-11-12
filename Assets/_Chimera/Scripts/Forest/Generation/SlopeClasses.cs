using System;
using UnityEngine;

/// <summary>Класс уклона (S1 §11.3): проходимо / лазаемо / стена.</summary>
public enum SlopeClass
{
    Passable = 0,
    Climbable = 1,
    Wall = 2,
}

/// <summary>
/// S1 §11.3: единственное определение класса уклона.
/// <para>
/// <c>s = atan‖∇H‖</c> при <b>одной</b> линейке — <see cref="ClassEpsilon"/> = 1 м.
/// Границы берутся из конфига (<see cref="DomeGenConfigSO.walkMaxDegrees"/>,
/// <see cref="DomeGenConfigSO.WallMinDegrees"/> = существующего 35), литералов в коде нет.
/// Класс — свойство ФИНАЛЬНОГО поля, а не формы: его считают по сумме <c>H0 + формы</c>.
/// </para>
/// </summary>
public static class SlopeClasses
{
    /// <summary>Одна линейка на проект: ε = 1 м (S1 §11.3).</summary>
    public const float ClassEpsilon = 1f;

    public static SlopeClass Classify(float slopeDegrees, DomeGenConfigSO cfg)
    {
        float walk = cfg != null ? cfg.WalkMaxDegrees : DomeGenConfigSO.DefaultWalkMaxDegrees;
        float wall = cfg != null ? cfg.WallMinDegrees : DomeGenConfigSO.DefaultWallMinDegrees;
        if (slopeDegrees <= walk) return SlopeClass.Passable;
        if (slopeDegrees <= wall) return SlopeClass.Climbable;
        return SlopeClass.Wall;
    }

    /// <summary>Класс в точке по финальному полю (сумма H0 и форм, после масок).</summary>
    public static SlopeClass At(DomeGenConfigSO cfg, float x, float z, Func<float, float, float> height)
    {
        return Classify(SlopeAt(cfg, x, z, height, ClassEpsilon), cfg);
    }

    /// <summary>Уклон центральными разностями при заданном ε (диагностика второго ε — отдельно).</summary>
    public static float SlopeAt(DomeGenConfigSO cfg, float x, float z,
        Func<float, float, float> height, float epsilon)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        if (height == null) throw new ArgumentNullException(nameof(height));
        float e = Mathf.Max(0.05f, epsilon);
        float gx = (height(x + e, z) - height(x - e, z)) / (2f * e);
        float gz = (height(x, z + e) - height(x, z - e)) / (2f * e);
        return Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// Доли площади классов на диске <paramref name="radius"/> (сетка res×res).
    /// Доли считаются от попавших в диск узлов; <c>passable + climbable + wall = 1</c>.
    /// </summary>
    public static void AreaShares(DomeGenConfigSO cfg, Func<float, float, float> height,
        float radius, int res, out float passable, out float climbable, out float wall)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        if (height == null) throw new ArgumentNullException(nameof(height));
        res = Mathf.Clamp(res, 8, 512);
        float step = 2f * radius / (res - 1);
        int n = 0, p = 0, c = 0, w = 0;
        for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                float x = -radius + ix * step;
                float z = -radius + iz * step;
                if (x * x + z * z > radius * radius) continue;
                n++;
                switch (At(cfg, x, z, height))
                {
                    case SlopeClass.Passable: p++; break;
                    case SlopeClass.Climbable: c++; break;
                    default: w++; break;
                }
            }
        passable = n > 0 ? (float)p / n : 0f;
        climbable = n > 0 ? (float)c / n : 0f;
        wall = n > 0 ? (float)w / n : 0f;
    }
}
