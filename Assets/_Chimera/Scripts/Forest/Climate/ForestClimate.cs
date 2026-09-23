using UnityEngine;

/// <summary>
/// Живая погода/время: тикает часы, зеркалит состояние в статики для чувств.
/// Хук читает только статики (дефолт ясно/день — без климата поведение прежнее).
/// Статики сбрасываются ResetStatic в TearDown тестов (прецедент ResetCatalog).
/// Лес, слайс s3b.
/// </summary>
public class ForestClimate : MonoBehaviour
{
    public WeatherState state;
    public float timeMinutes = 10f;

    /// <summary>Игровых минут в секунду: сутки за 30 реальных минут.</summary>
    public float timeScale = 1f / 60f;

    public static WeatherState CurrentState;
    public static float CurrentTimeMinutes = 10f;

    void Update()
    {
        timeMinutes += Time.deltaTime * timeScale;
        if (timeMinutes >= DayNightCycle.DayLengthMinutes) timeMinutes -= DayNightCycle.DayLengthMinutes;
        CurrentState = state;
        CurrentTimeMinutes = timeMinutes;
    }

    /// <summary>Множитель дальности чувства: умножается поверх, не подменяет.</summary>
    public static float RangeMult(SenseKind kind)
    {
        switch (kind)
        {
            case SenseKind.Sight:
                return WeatherModifier.VisibilityMult(CurrentState.kind) * (IsNight() ? 0.7f : 1f);
            case SenseKind.Thermal:
                return 1f;
            case SenseKind.Scent:
                return WeatherModifier.SmellMult(CurrentState.kind);
            case SenseKind.Hearing:
                return WeatherModifier.HearingMult(CurrentState.kind);
            default:
                return 1f;
        }
    }

    public static float CueDurationMult()
    {
        return WeatherModifier.CueLoudnessMult(CurrentState.kind);
    }

    public static float ScentLifetimeMult()
    {
        return WeatherModifier.ScentLifetimeMult(CurrentState.kind);
    }

    public static bool IsNight()
    {
        return DayNightCycle.IsNight(CurrentTimeMinutes);
    }

    public static void ResetStatic()
    {
        CurrentState = new WeatherState { kind = WeatherKind.Clear };
        CurrentTimeMinutes = 10f;
    }
}
