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
    public DomeLabComplex.Result lab;
    public float visualInterval = 0.5f;

    [Header("B1.3: высотный климат")]
    public System.Func<float, float, float> ground;
    public float lakeLevel;
    public float heightFalloffPerMeter = 0.04f;

    [Header("B1.2: смена погоды по таблице")]
    public bool autoWeather = false;
    public float weatherChangeMinutes = 8f;
    public float weatherBlendSeconds = 20f;
    public long weatherSeed = 1;
    public WeatherTable weatherTable;

    public WeatherKind currentKind;
    public float WetAmount { get; private set; }
    public float FogAmount { get; private set; }
    public int WeatherStep { get; private set; }

    public static WeatherState CurrentState;
    public static float CurrentTimeMinutes = 10f;
    public static DomeLabComplex.Result LabZone;

    float nextVisual;
    float weatherElapsed;
    bool lastInside;
    WeatherKind fromKind, toKind;
    float blendStart;
    bool blending;

    void Update()
    {
        float dt = Time.deltaTime * timeScale;
        timeMinutes = Mathf.Repeat(timeMinutes + dt, DayNightCycle.DayLengthMinutes);
        CurrentState = state;
        CurrentTimeMinutes = timeMinutes;

        if (autoWeather)
        {
            // Отдельный таймер: абсолютная минута после wrap (30 → 0) больше не нужна.
            weatherElapsed += dt;
            float change = Mathf.Max(0.1f, weatherChangeMinutes);
            int guard = 0;
            while (weatherElapsed >= change && guard++ < 64)
            {
                weatherElapsed -= change;
                WeatherStep++;
                BeginWeather(PickWeather(weatherTable, weatherSeed, WeatherStep));
            }
        }

        bool inside = lab != null && Camera.main != null
            && DomeLabComplex.Contains(lab, Camera.main.transform.position);
        if (inside != lastInside)
        {
            ApplyVisuals();
            lastInside = inside;
            nextVisual = Time.time + Mathf.Max(0.1f, visualInterval);
        }
        else if (skyRoot != null && Time.time >= nextVisual)
        {
            ApplyVisuals();
            nextVisual = Time.time + Mathf.Max(0.1f, visualInterval);
        }
    }

    /// <summary>Пол и уровень воды павильона: климат читает высоту над озером.</summary>
    public void SetGround(System.Func<float, float, float> sampler, float lakeY)
    {
        ground = sampler;
        lakeLevel = lakeY;
        if (rain != null) rain.SetGround(sampler);
    }

    public static float HeightFog(float groundY, float lakeY, float falloffPerMeter = 0.04f)
    {
        return WeatherModifier.HeightFogMult(groundY - lakeY, falloffPerMeter);
    }

    public void SetWeatherImmediate(WeatherKind kind)
    {
        currentKind = kind;
        fromKind = toKind = kind;
        state = new WeatherState { kind = kind };
        CurrentState = state;
        CurrentTimeMinutes = timeMinutes;
        blending = false;
        WetAmount = WetFor(kind);
        FogAmount = FogFor(kind);
        EnsureRain();
    }

    public void BeginWeather(WeatherKind kind)
    {
        fromKind = currentKind;
        currentKind = kind;
        toKind = kind;
        state = new WeatherState { kind = kind };
        CurrentState = state;
        CurrentTimeMinutes = timeMinutes;
        blendStart = Time.time;
        blending = true;
        EnsureRain();
        ApplyVisuals();
    }

    public static float WetFor(WeatherKind kind) => kind == WeatherKind.Rain ? 1f : 0f;
    public static float FogFor(WeatherKind kind) => kind == WeatherKind.Fog ? 1f : 0f;

    public void StartWeatherClock()
    {
        weatherElapsed = 0f;
        WeatherStep = 0;
    }

    void EnsureRain()
    {
        if (state.kind != WeatherKind.Rain || rain != null || skyRoot == null) return;
        rain = DomeRain.BuildPreview(skyRoot, 0f, 0f, ground);
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
        ApplyVisuals(Camera.main != null ? Camera.main.transform.position : Vector3.zero);
    }

    public void ApplyVisuals(Vector3 viewPoint)
    {
        bool inside = lab != null && DomeLabComplex.Contains(lab, viewPoint);
        float t = 1f;
        if (blending)
        {
            float span = Mathf.Max(0.01f, weatherBlendSeconds);
            t = Mathf.Clamp01((Time.time - blendStart) / span);
            if (t >= 1f) blending = false;
        }
        WeatherKind a = blending ? fromKind : currentKind;
        WeatherKind b = blending ? toKind : currentKind;
        WetAmount = Mathf.Lerp(WetFor(a), WetFor(b), t);
        FogAmount = Mathf.Lerp(FogFor(a), FogFor(b), t);

        if (skyRoot != null)
            DomeSkyRig.ApplyPhase(skyRoot, timeMinutes, false);

        if (wetMaterials != null)
            DomeWeather.ApplyWet(wetMaterials, WetAmount);

        // Туман павильона — не только кривая суток: погодный туман усиливает плотность.
        if (FogAmount > 0f)
            RenderSettings.fogDensity = Mathf.Max(RenderSettings.fogDensity, 0.0008f)
                                        * (1f + 1.5f * FogAmount);

        // B1.3: высота над озером, а не мировая Y. Низина/каньон туманятся,
        // плато остаётся читаемым; внутри станции это всё равно гасится ниже.
        if (ground != null)
        {
            float heightAboveLake = ground(viewPoint.x, viewPoint.z) - lakeLevel;
            float band = WeatherModifier.HeightFogMult(heightAboveLake, heightFalloffPerMeter);
            RenderSettings.fogDensity *= Mathf.Lerp(0.25f, 1f, band);
        }

        // Внутри станции нет внешнего тумана и дождя: это закрытый объём.
        if (inside)
        {
            RenderSettings.fog = false;
            RenderSettings.fogDensity = 0f;
        }

        if (rain != null)
        {
            bool wetWeather = (blending && (a == WeatherKind.Rain || b == WeatherKind.Rain))
                              || (!blending && currentKind == WeatherKind.Rain);
            rain.gameObject.SetActive(wetWeather && !inside);
        }
    }

    /// <summary>Внутри станции внешняя погода/ночь не действуют на чувства.</summary>
    public static bool InsideLab(Vector3 worldPos)
    {
        return LabZone != null && DomeLabComplex.Contains(LabZone, worldPos);
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

    public static float RangeMult(SenseKind kind, Vector3 worldPos)
    {
        return InsideLab(worldPos) ? 1f : RangeMult(kind);
    }

    public static float CueDurationMult()
    {
        return WeatherModifier.CueLoudnessMult(CurrentState.kind);
    }

    public static float CueDurationMult(Vector3 worldPos)
    {
        return InsideLab(worldPos) ? 1f : CueDurationMult();
    }

    public static float ScentLifetimeMult()
    {
        return WeatherModifier.ScentLifetimeMult(CurrentState.kind);
    }

    public static float ScentLifetimeMult(Vector3 worldPos)
    {
        return InsideLab(worldPos) ? 1f : ScentLifetimeMult();
    }

    public static bool IsNight()
    {
        return DayNightCycle.IsNight(CurrentTimeMinutes);
    }

    public static void ResetStatic()
    {
        CurrentState = new WeatherState { kind = WeatherKind.Clear };
        CurrentTimeMinutes = 10f;
        LabZone = null;
    }
}
