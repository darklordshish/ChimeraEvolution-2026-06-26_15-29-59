/// <summary>
/// Сутки леса: 30 минут (день 21 / ночь 9, как Valheim). Фаза — чистая функция времени:
/// детерминирована, циклична, отрицательные/большие t — корректно. Лес, слайс s3.
/// </summary>
public static class DayNightCycle
{
    public const float DayLengthMinutes = 30f;
    public const float DayMinutes = 21f;

    /// <summary>Фаза суток [0, 1): 0 — рассвет, 0.7 — закат, дальше ночь.</summary>
    public static float Phase01(float timeMinutes)
    {
        float m = timeMinutes % DayLengthMinutes;
        if (m < 0f) m += DayLengthMinutes;
        return m / DayLengthMinutes;
    }

    public static bool IsNight(float timeMinutes)
    {
        // Напрямую по минутам, а не через Phase01: 21/30 во float даёт 0.6999…,
        // и обратный пересчёт 0.7·30 < 21 ронял бы границу заката.
        float m = timeMinutes % DayLengthMinutes;
        if (m < 0f) m += DayLengthMinutes;
        return m >= DayMinutes;
    }
}
