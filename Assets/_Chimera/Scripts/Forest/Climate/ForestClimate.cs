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

    [Header("B1.2: смена погоды по таблице")]
    public bool autoWeather = false;
    public float weatherChangeMinutes = 8f;
    public long weatherSeed = 1;
    public WeatherTable weatherTable;

    public static WeatherState CurrentState;
    public static float CurrentTimeMinutes = 10f;

    float nextVisual;
    float nextWeatherAt;

    void Update()
    {
        timeMinutes += Time.deltaTime * timeScale;
        if (timeMinutes >= DayNightCycle.DayLengthMinutes) timeMinutes -= DayNightCycle.DayLengthMinutes;
        CurrentState = state;
        CurrentTimeMinutes = timeMinutes;

        if (autoWeather && timeMinutes >= nextWeatherAt)
        {
            int step = Mathf.Max(0, Mathf.FloorToInt(timeMinutes / Mathf.Max(0.1f, weatherChangeMinutes)));
            state = new WeatherState { kind = PickWeather(weatherTable, weatherSeed, step) };
            nextWeatherAt = timeMinutes + Mathf.Max(0.1f, weatherChangeMinutes);
            EnsureRain();
            ApplyVisuals();
        }

        if (skyRoot != null && Time.time >= nextVisual)
        {
            ApplyVisuals();
            nextVisual = Time.time + Mathf.Max(0.1f, visualInterval);
        }
    }

    public void StartWeatherClock()
    {
        nextWeatherAt = timeMinutes + Mathf.Max(0.1f, weatherChangeMinutes);
    }

    void EnsureRain()
    {
        if (state.kind != WeatherKind.Rain || rain != null || skyRoot == null) return;
        rain = DomeRain.BuildPreview(skyRoot, 0f, 0f);
    }

    /// <summary>Детерминированный выбор погоды по таблице; пустая таблица — Clear.</summary>
    public static WeatherKind PickWeather(WeatherTable table, long seed, int step)
    {
        float sum = table.clearWeight + table.rainWeight + table.fogWeight
                    + table.snowWeight + table.windWeight;
        if (sum <= 0f) return WeatherKind.Clear;
        float roll = SeededHash.ToFloat01(SeededHash.Hash(seed, 9001, step));
        float acc = 0f;
        foreach (var kind in new[] { WeatherKind.Clear, WeatherKind.Rain, WeatherKind.Fog,
                     WeatherKind.Snow, WeatherKind.Wind })
        {
            acc += WeatherTable.ChanceFor(table, kind);
            if (roll <= acc) return kind;
        }
        return WeatherKind.Clear;
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
