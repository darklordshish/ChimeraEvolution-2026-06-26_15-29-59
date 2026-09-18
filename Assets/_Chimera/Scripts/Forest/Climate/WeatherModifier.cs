using UnityEngine;

/// <summary>
/// Чистые множители погоды. Умножаются поверх чисел чувств, не подменяют их.
/// Числа каналов (дождь маскирует и смывает, туман режет видимость) — от связки s3:
/// художественный + боевой сошлись, что инверсия (бафф слуха в дождь) — баг стелса.
/// ЗАПРЕТ: никогда не умножать chargeSpeed/speed/duration доставок (коммит-фаза) —
/// move×0.9 снега бьёт только заход в окно WindowMin/Max. Лес, слайс s3.
/// </summary>
public static class WeatherModifier
{
    /// <summary>Множитель дальности видимости.</summary>
    public static float VisibilityMult(WeatherKind kind)
    {
        switch (kind)
        {
            case WeatherKind.Rain: return 0.7f;
            case WeatherKind.Fog: return 0.45f;
            case WeatherKind.Snow: return 0.8f;
            default: return 1f;
        }
    }

    /// <summary>Множитель дальности слуха.</summary>
    public static float HearingMult(WeatherKind kind)
    {
        switch (kind)
        {
            case WeatherKind.Rain: return 0.75f;
            case WeatherKind.Fog: return 0.8f;
            case WeatherKind.Snow: return 0.9f;
            case WeatherKind.Wind: return 0.9f;
            default: return 1f;
        }
    }

    /// <summary>Множитель дальности нюха (дождь смывает следы — 0).</summary>
    public static float SmellMult(WeatherKind kind)
    {
        switch (kind)
        {
            case WeatherKind.Rain: return 0f;
            case WeatherKind.Fog: return 0.5f;
            case WeatherKind.Snow: return 0.7f;
            case WeatherKind.Wind: return 0.6f;
            default: return 1f;
        }
    }

    /// <summary>
    /// Множитель времени жизни запаховых точек. Дождь обязан резать lifetime следов,
    /// а не только нос: иначе тропят по следу после ливня.
    /// </summary>
    public static float ScentLifetimeMult(WeatherKind kind)
    {
        switch (kind)
        {
            case WeatherKind.Rain: return 0f;
            case WeatherKind.Fog: return 0.5f;
            case WeatherKind.Snow: return 0.8f;
            case WeatherKind.Wind: return 0.6f;
            default: return 1f;
        }
    }

    /// <summary>Множитель скорости хода (снег вязнет).</summary>
    public static float MoveMult(WeatherKind kind)
    {
        return kind == WeatherKind.Snow ? 0.9f : 1f;
    }

    /// <summary>
    /// Буст сигнала объявления приёма (тон/громкость) в плохую видимость.
    /// Третий канал уже есть (тон волны = цвет приёма) — не дублируем, только бустим.
    /// </summary>
    public static float CueLoudnessMult(WeatherKind kind)
    {
        switch (kind)
        {
            case WeatherKind.Rain: return 1.25f;
            case WeatherKind.Fog: return 1.25f;
            default: return 1f;
        }
    }

    /// <summary>
    /// Высотный туман (приём Mistlands): внизу густо, наверху чисто.
    /// Плотность монотонно спадает с высотой, кламп [0, 1].
    /// </summary>
    public static float FogDensityAt(float heightY, float baseDensity, float falloffPerMeter)
    {
        float d = baseDensity - falloffPerMeter * heightY;
        return d < 0f ? 0f : (d > 1f ? 1f : d);
    }
}
