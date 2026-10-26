using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Живая погода/время: тикает часы, зеркалит состояние в статики для чувств.
/// Хук читает только статики (дефолт ясно/день — без климата поведение прежнее).
/// B1.2: если задан skyRoot, климат применяет фазу неба, мокрость и дождь.
/// Статики сбрасываются ResetStatic в TearDown тестов (прецедент ResetCatalog).
/// Лес, слайс s3b + B1.2.
/// </summary>
public class ForestClimate : MonoBehaviour
{
    public WeatherState state;
    public float timeMinutes = 10f;

    /// <summary>Игровых минут в секунду: сутки за 30 реальных минут.</summary>
    public float timeScale = 1f / 60f;

    [Header("B1.2: визуал климата (необязательно)")]
    public GameObject skyRoot;
    public IReadOnlyList<Material> wetMaterials;
    public DomeRain rain;
    public float visualInterval = 0.5f;

    public static WeatherState CurrentState;
    public static float CurrentTimeMinutes = 10f;

    float nextVisual;

    void Update()
    {
        timeMinutes += Time.deltaTime * timeScale;
        if (timeMinutes >= DayNightCycle.DayLengthMinutes) timeMinutes -= DayNightCycle.DayLengthMinutes;
        CurrentState = state;
        CurrentTimeMinutes = timeMinutes;

        if (skyRoot != null && Time.time >= nextVisual)
        {
            ApplyVisuals();
            nextVisual = Time.time + Mathf.Max(0.1f, visualInterval);
        }
    }

    /// <summary>Применить текущую погоду/время к небу, материалам и дождю.</summary>
    public void ApplyVisuals()
    {
        if (skyRoot != null)
            DomeSkyRig.ApplyPhase(skyRoot, timeMinutes, false);

        float wet = state.kind == WeatherKind.Rain ? 1f : 0f;
        if (wetMaterials != null)
            DomeWeather.ApplyWet(wetMaterials, wet);

        // Туман павильона — не только кривая суток: погодный туман усиливает плотность.
        if (state.kind == WeatherKind.Fog)
            RenderSettings.fogDensity *= 1.8f;

        if (rain != null)
            rain.gameObject.SetActive(state.kind == WeatherKind.Rain);
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
