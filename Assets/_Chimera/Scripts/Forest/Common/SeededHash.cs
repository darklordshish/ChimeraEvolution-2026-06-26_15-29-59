using System;

/// <summary>
/// Детерминированный 2D-хэш (финализатор splitmix64). Чистая функция: seed + координаты → ulong.
/// Без UnityEngine — тестируется без сцены. Лес, слайс s1.
/// </summary>
public static class SeededHash
{
    public static ulong Hash(long seed, int x, int y)
    {
        unchecked
        {
            ulong z = (ulong)seed + 0x9E3779B97F4A7C15UL;
            z ^= (ulong)(x * 0x9E3779B1);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z ^= (ulong)(y * 0xC2B2AE35);
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    /// <summary>Хэш → [0, 1). Берутся старшие 53 бита.</summary>
    public static float ToFloat01(ulong h)
    {
        return (float)((h >> 11) * (1.0 / 9007199254740992.0));
    }

    /// <summary>Хэш → [-1, 1).</summary>
    public static float ToFloatSigned(ulong h)
    {
        return ToFloat01(h) * 2f - 1f;
    }
}
