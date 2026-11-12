using System;
using UnityEngine;

/// <summary>
/// ИГРОВЫЕ ЧАСЫ — ОДИН носитель игрового времени (спека вечной эволюции 2026-11-12, §5 п.1.3). Тюнинг-объект в сцене:
/// длина суток, срок «сна» после смерти игрока в игровых часах, ускорение мира на время сна.
///     Время копится из масштабированного `Time.deltaTime`, поэтому хитстоп и пауза конструктора его честно
/// останавливают, а ускорение мира (`Time.timeScale`) ускоряет и часы — отдельного множителя нет, второго
/// носителя времени нет. Кулдаун босса (`BossChimeraSpawner`) и итог смерти (`PlayerDeath`) считают по этим часам:
/// иначе «сон» игрока мир не двигал бы. Фаза суток — чистая функция `DayNightCycle` (лес), здесь только мост.
///     ВЫБОР: ускорение — `Time.timeScale` с потолком ×8 (NavMesh и CharacterController на большем шаге
/// пробивают стены), а не пересчёт без симуляции — мир во сне честно живёт и эволюционирует. Предел подбирается на плейтесте.
/// </summary>
public class WorldClock : MonoBehaviour
{
    /// <summary>Потолок ускорения мира: выше — физика и NavMesh проскакивают препятствия.</summary>
    public const float MaxTimeScale = 8f;
    public const float DefaultDayMinutes = 12f;   // реальных минут на игровые сутки (ЧЕРНОВИК, тюнинг по плейтесту)
    public const float DefaultSleepHours = 6f;    // срок «сна» в игровых часах
    public const float DefaultSleepSpeedup = 8f;  // ×; 6 ч при сутках 12 мин = 180 с мира → ~22 с реальных

    [SerializeField, Tooltip("Реальных минут на игровые сутки. 0 — по умолчанию")]
    float dayLengthMinutes = DefaultDayMinutes;
    [SerializeField, Tooltip("Срок «сна» после смерти игрока, игровых часов. 0 — по умолчанию")]
    float sleepGameHours = DefaultSleepHours;
    [SerializeField, Tooltip("Во сколько раз ускоряется мир на время сна (потолок ×8). 0 — по умолчанию")]
    float sleepSpeedup = DefaultSleepSpeedup;

    double hours;          // игровые часы с начала сессии
    bool sleeping;
    double sleepStart, sleepUntil;
    bool ownsTimeScale;    // мы подняли timeScale — нам и вернуть

    static WorldClock instance;
    public static WorldClock Instance => instance != null ? instance : (instance = FindAnyObjectByType<WorldClock>());

    /// <summary>Сон окончился (срок вышел или `EndSleep`).</summary>
    public event Action SleepEnded;

    // ноль в старом объекте сцены читаем как «не настроено» (гоча проекта про новые SerializeField)
    public float DayLengthMinutes => dayLengthMinutes > 0f ? dayLengthMinutes : DefaultDayMinutes;
    public float SleepGameHours => sleepGameHours > 0f ? sleepGameHours : DefaultSleepHours;
    public float SleepSpeedup => Mathf.Clamp(sleepSpeedup > 0f ? sleepSpeedup : DefaultSleepSpeedup, 1f, MaxTimeScale);

    public double Hours => hours;
    public int Day => (int)Math.Floor(hours / 24.0) + 1;
    public float HourOfDay => (float)(hours - Math.Floor(hours / 24.0) * 24.0);
    public float HoursPerSecond => HoursPerRealSecond(DayLengthMinutes);
    public bool IsSleeping => sleeping;
    public float SleepProgress01 => sleeping && sleepUntil > sleepStart ? Mathf.Clamp01((float)((hours - sleepStart) / (sleepUntil - sleepStart))) : 0f;

    /// <summary>Фаза суток [0,1) через `DayNightCycle` леса: сутки здесь любой длины, лес меряет их своими 30 минутами.</summary>
    public float Phase01 => DayNightCycle.Phase01(HourOfDay / 24f * DayNightCycle.DayLengthMinutes);
    public bool IsNight => DayNightCycle.IsNight(HourOfDay / 24f * DayNightCycle.DayLengthMinutes);

    /// <summary>Игровые часы «сейчас» — для тех, кто сравнивает сроки (кулдаун босса). Без часов в сцене — по `Time`
    /// с темпом по умолчанию: тесты и сцены без часов продолжают работать.</summary>
    public static double Now => Instance != null ? Instance.hours : Time.timeAsDouble * HoursPerRealSecond(DefaultDayMinutes);

    // ── чистая арифметика (EditMode-тесты) ──────────────────────────────────────────────────────

    /// <summary>Игровых часов за одну секунду масштабированного времени.</summary>
    public static float HoursPerRealSecond(float dayMinutes) => 24f / (Mathf.Max(0.01f, dayMinutes) * 60f);

    /// <summary>Сколько реальных секунд длится сон: часы ÷ (часов в секунду × ускорение).</summary>
    public static float SleepRealSeconds(float sleepHours, float dayMinutes, float speedup) =>
        sleepHours / (HoursPerRealSecond(dayMinutes) * Mathf.Clamp(speedup, 1f, MaxTimeScale));

    /// <summary>Тюнинг из кода (тесты, дев-панель): любое значение ≤ 0 — по умолчанию.</summary>
    public void Configure(float dayMinutes, float sleepHoursValue, float speedup)
    {
        dayLengthMinutes = dayMinutes; sleepGameHours = sleepHoursValue; sleepSpeedup = speedup;
    }

    /// <summary>Продвинуть часы на `scaledDelta` секунд масштабированного времени. Update зовёт её с `Time.deltaTime`.</summary>
    public void Advance(float scaledDelta)
    {
        if (scaledDelta <= 0f) return;
        hours += scaledDelta * HoursPerSecond;
        if (sleeping && hours >= sleepUntil) EndSleep();
    }

    /// <summary>Начать «сон»: мир ускоряется (Update ведёт timeScale) до конца срока. `gameHours` ≤ 0 — срок по тюнингу.</summary>
    public void BeginSleep(float gameHours = 0f)
    {
        float h = gameHours > 0f ? gameHours : SleepGameHours;
        sleeping = true;
        sleepStart = hours;
        sleepUntil = hours + h;
    }

    public void EndSleep()
    {
        if (!sleeping) return;
        sleeping = false;
        RestoreTimeScale();
        SleepEnded?.Invoke();
    }

    void Awake() => instance = this;

    void Update()
    {
        Advance(Time.deltaTime);
        // во сне держим ускорение каждый кадр: хитстоп сбрасывает timeScale в 1 и сбил бы его молча.
        // Пауза конструктора (timeScale 0) сну не мешает — лежащему Tab закрыт, а чужую паузу не перебиваем
        if (sleeping && !ConstructorUI.IsOpen)
        {
            Time.timeScale = SleepSpeedup;
            ownsTimeScale = true;
        }
    }

    void RestoreTimeScale()
    {
        if (!ownsTimeScale) return;
        ownsTimeScale = false;
        if (!ConstructorUI.IsOpen) Time.timeScale = 1f;
    }

    void OnDisable() { if (sleeping) { sleeping = false; RestoreTimeScale(); } }
    void OnDestroy() { if (instance == this) instance = null; }
}
