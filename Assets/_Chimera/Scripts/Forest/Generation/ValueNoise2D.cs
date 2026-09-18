using System;

/// <summary>
/// Value-noise 2D + FBM поверх SeededHash. Детерминирован: те же (seed, x, y) → то же значение.
/// Выход Noise/Fbm — строго [-1, 1] (FBM нормирован на сумму амплитуд). Лес, слайс s1.
/// </summary>
public static class ValueNoise2D
{
    /// <summary>Билинейный value-noise со сглаживанием smootherstep. Выход [-1, 1].</summary>
    public static float Noise(long seed, float x, float y)
    {
        int xi = FastFloor(x);
        int yi = FastFloor(y);
        float xf = x - xi;
        float yf = y - yi;
        float u = Quintic(xf);
        float v = Quintic(yf);
        float a = SeededHash.ToFloatSigned(SeededHash.Hash(seed, xi, yi));
        float b = SeededHash.ToFloatSigned(SeededHash.Hash(seed, xi + 1, yi));
        float c = SeededHash.ToFloatSigned(SeededHash.Hash(seed, xi, yi + 1));
        float d = SeededHash.ToFloatSigned(SeededHash.Hash(seed, xi + 1, yi + 1));
        return Lerp(Lerp(a, b, u), Lerp(c, d, u), v);
    }

    /// <summary>
    /// Фрактал: октавы с lacunarity=2, gain=0.5. У каждой октавы свой сдвиг сида,
    /// поэтому добавление деталей позже не меняет грубую форму. Выход [-1, 1].
    /// </summary>
    public static float Fbm(long seed, float x, float y, int octaves)
    {
        float sum = 0f;
        float amp = 1f;
        float ampSum = 0f;
        float freq = 1f;
        for (int i = 0; i < octaves; i++)
        {
            sum += Noise(seed + i * 1013904223L, x * freq, y * freq) * amp;
            ampSum += amp;
            amp *= 0.5f;
            freq *= 2f;
        }
        return ampSum > 0f ? sum / ampSum : 0f;
    }

    static int FastFloor(float v)
    {
        int i = (int)v;
        return v < i ? i - 1 : i;
    }

    static float Quintic(float t)
    {
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }
}
