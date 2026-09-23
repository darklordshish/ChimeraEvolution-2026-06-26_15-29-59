using UnityEngine;

/// <summary>
/// Орбитальная механика павильона (s10c): сутки DayNightCycle (30 мин: день 21 / ночь 9),
/// солнце днём по дуге восток→запад, луна — ночью в противофазе. Чистая функция —
/// риг (свет/туман/шейдер) ест её выход. Ночь режет только видимость света, не геометрию.
/// </summary>
public static class DomeCelestial
{
    public struct SkyState
    {
        public Vector3 sunDir;
        public Vector3 moonDir;
        public float dayT; // 1 день, 0 ночь, рампы на рассвете/закате
        public float duskT; // пояс заката/рассвета у горизонта (0 днём и ночью)
        public float fogDensity;
        public Color fogColor;
    }

    public static SkyState StateAt(float timeMinutes)
    {
        float t = timeMinutes % DayNightCycle.DayLengthMinutes;
        if (t < 0f) t += DayNightCycle.DayLengthMinutes;
        float dayT;
        float sunElev, sunAzim;
        if (t < DayNightCycle.DayMinutes)
        {
            float u = t / DayNightCycle.DayMinutes; // 0 рассвет … 1 закат
            float ramp = Mathf.Min(Mathf.Clamp01(u / 0.07f), Mathf.Clamp01((1f - u) / 0.07f));
            dayT = 0.15f + 0.85f * ramp;
            sunElev = Mathf.Sin(u * Mathf.PI) * 65f;
            sunAzim = Mathf.Lerp(-90f, 90f, u);
        }
        else
        {
            dayT = 0f;
            sunElev = -20f;
            sunAzim = 0f;
        }
        var sunDir = AzimElev(sunAzim, sunElev);
        // Луна в противофазе: кульминация в полночь.
        float nightT = DayNightCycle.IsNight(t) ? 1f : 0f;
        float mu = DayNightCycle.IsNight(t)
            ? (t - DayNightCycle.DayMinutes) / (DayNightCycle.DayLengthMinutes - DayNightCycle.DayMinutes)
            : 0f;
        var moonDir = AzimElev(Mathf.Lerp(-90f, 90f, mu), nightT > 0f ? Mathf.Sin(mu * Mathf.PI) * 50f : -20f);
        // Туман: база по дню/ночи + приземные пики на рассвете/закате (вердикт художника).
        // Масштаб Ø2000: Exp2 ~0.001 (на 190м песочнице было 0.012 — там это «верх дня»,
        // здесь такая плотность даёт белое молоко уже на 300м, поймано кадром s10c).
        float dawnDusk = DayNightCycle.IsNight(t) ? 0f : PeakNear(t, 0f) + PeakNear(t, DayNightCycle.DayMinutes);
        float duskT = Mathf.Clamp01(dawnDusk);
        float fog = Mathf.Lerp(0.0018f, 0.0009f, dayT) + 0.0012f * duskT;
        var fogCol = Color.Lerp(new Color(0.10f, 0.12f, 0.18f), new Color(0.78f, 0.82f, 0.84f), dayT);
        return new SkyState { sunDir = sunDir, moonDir = moonDir, dayT = dayT, duskT = duskT, fogDensity = fog, fogColor = fogCol };
    }

    static float PeakNear(float t, float mark)
    {
        float d = Mathf.Min(Mathf.Abs(t - mark), DayNightCycle.DayLengthMinutes - Mathf.Abs(t - mark));
        return Mathf.Clamp01(1f - d / 2.5f);
    }

    static Vector3 AzimElev(float azimDeg, float elevDeg)
    {
        float az = azimDeg * Mathf.Deg2Rad;
        float el = elevDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), -Mathf.Cos(az) * Mathf.Cos(el)).normalized;
    }
}
