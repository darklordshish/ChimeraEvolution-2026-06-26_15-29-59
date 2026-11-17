using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Смерть игрока БЕЗ перезагрузки сцены (спека вечной эволюции 2026-11-12, §5 п.1.2–1.4): Жив → Лежит → Встаёт → Жив.
///     ЛЕЖИТ — управление и атака сняты, игрок неуязвим, для чувств NPC его нет (`Perception.PlayerHidden`), мир идёт
/// ускоренно (`WorldClock.BeginSleep`) до конца срока «сна». ВСТАЁТ — `Health.Revive`, тело возвращается на точку смерти
/// (CharacterController гасится на время переноса: он перебивает позицию), короткая неуязвимость без управления.
/// Мир, эволюция NPC и сборка игрока не трогаются. Итог — строка на экране (`EndlessHud`): сколько прошло игровых часов,
/// доля химер среди NPC до и после, кто убил. Health на игроке держим с выключенным Destroy On Death.
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerDeath : MonoBehaviour
{
    public enum Phase { Alive, Lying, Rising }

    [SerializeField, Tooltip("Сколько секунд игрок встаёт: неуязвим, но без управления. 0 — по умолчанию")]
    float risingSeconds = 1.5f;
    [SerializeField, Tooltip("Если в сцене нет WorldClock: сколько секунд лежать (реальных). 0 — по умолчанию")]
    float fallbackLieSeconds = 3f;
    [SerializeField, Tooltip("Сколько секунд держать итог на экране после подъёма. 0 — по умолчанию")]
    float summarySeconds = 10f;

    /// <summary>Итог последней смерти.</summary>
    public struct Report
    {
        public double hoursAway;
        public ChimeraCensus.Snapshot before, after;
        public string killer;
        public override string ToString() =>
            $"Прошло {hoursAway:0.#} игр. ч. Химер среди NPC: было {before}, стало {after}. Тебя убил: {killer}.";
    }

    static PlayerDeath current;
    public static PlayerDeath Current => current;
    /// <summary>Игрок лежит или встаёт — для тех, кому он сейчас недоступен (конструктор).</summary>
    public static bool IsDown => current != null && current.State != Phase.Alive;

    public Phase State { get; private set; } = Phase.Alive;
    public Report LastReport { get; private set; }
    public int Deaths { get; private set; }

    Health health;
    CharacterController cc;
    Vector3 deathPos;
    Quaternion deathRot;
    string killerName;
    double hoursAtDeath;
    ChimeraCensus.Snapshot censusBefore;

    float Rising => risingSeconds > 0f ? risingSeconds : 1.5f;
    float FallbackLie => fallbackLieSeconds > 0f ? fallbackLieSeconds : 3f;
    float SummaryTime => summarySeconds > 0f ? summarySeconds : 10f;

    void Awake()
    {
        current = this;
        health = GetComponent<Health>();
        cc = GetComponent<CharacterController>();
        health.onDeath.AddListener(OnDeath);
    }

    void OnDestroy()
    {
        if (health != null) health.onDeath.RemoveListener(OnDeath);
        if (current == this) { current = null; Perception.PlayerHidden = false; }
    }

    void OnDeath()
    {
        if (State != Phase.Alive) return;
        Time.timeScale = 1f; // на случай, если смерть совпала с хитстопом
        State = Phase.Lying;
        Deaths++;
        deathPos = transform.position;
        deathRot = transform.rotation;
        killerName = DescribeKiller(health.LastAttacker);
        censusBefore = ChimeraCensus.Take();
        Perception.PlayerHidden = true; // NPC перестают видеть, слышать и чуять
        health.Invulnerable = true;
        SetControl(false);
        StartCoroutine(Lie());
    }

    IEnumerator Lie()
    {
        var clock = WorldClock.Instance;
        if (clock != null)
        {
            hoursAtDeath = clock.Hours;
            clock.BeginSleep();
            while (clock.IsSleeping)
            {
                EndlessHud.SetDeathText(
                    $"ТЫ ПАЛ — мир идёт без тебя\nсон: {clock.SleepProgress01 * 100f:0}% · прошло {clock.Hours - hoursAtDeath:0.#} игр. ч.\n" +
                    $"химер среди NPC: было {censusBefore} · тебя убил: {killerName}");
                yield return null;
            }
        }
        else
        {
            hoursAtDeath = WorldClock.Now;
            EndlessHud.SetDeathText($"ТЫ ПАЛ — мир идёт без тебя\nтебя убил: {killerName}");
            yield return new WaitForSecondsRealtime(FallbackLie);
        }
        yield return Rise();
    }

    IEnumerator Rise()
    {
        State = Phase.Rising;
        // перенос на точку смерти: CharacterController перебивает позицию, поэтому гасим его на время
        if (cc != null) cc.enabled = false;
        float feet = cc != null ? transform.position.y - cc.bounds.min.y : 0f; // от корня до подошв (корень у видов разный)
        transform.SetPositionAndRotation(RisePoint(deathPos, feet), deathRot);
        if (cc != null) cc.enabled = true;

        ClearDamageOverTime();
        health.Revive();
        health.Invulnerable = true; // пока встаём — не бьют (Revive сбрасывает флаг, ставим обратно)

        LastReport = new Report
        {
            hoursAway = WorldClock.Now - hoursAtDeath,
            before = censusBefore,
            after = ChimeraCensus.Take(),
            killer = killerName,
        };
        EndlessHud.SetDeathText("ТЫ ВСТАЁШЬ\n" + LastReport, SummaryTime);

        yield return new WaitForSecondsRealtime(Rising);

        health.Invulnerable = false;
        SetControl(true);
        Perception.PlayerHidden = false;
        State = Phase.Alive;
    }

    /// <summary>Поставщик «здесь вода» (лес подставляет свою; null — воды не знаем, решает только навмеш).</summary>
    public static System.Func<Vector3, bool> IsWaterAt;
    public const float RiseSearchRadius = 30f;

    /// <summary>Куда встать после смерти: точка смерти, если она на проходимом навмеше и не под водой; иначе ближайшая
    /// проходимая точка в <see cref="RiseSearchRadius"/> м; навмеша нет совсем — точка смерти как есть.</summary>
    public static Vector3 RisePoint(Vector3 death, float feetOffset = 0f)
    {
        bool water = IsWaterAt != null && IsWaterAt(death);
        if (!water && NavMesh.SamplePosition(death, out var near, 1f + feetOffset, NavMesh.AllAreas)
            && Mathf.Abs(near.position.x - death.x) < 0.5f && Mathf.Abs(near.position.z - death.z) < 0.5f)
            return death;
        for (float r = 5f; r <= RiseSearchRadius + 0.01f; r += 5f)
            if (NavMesh.SamplePosition(death, out var hit, r, NavMesh.AllAreas) && (IsWaterAt == null || !IsWaterAt(hit.position)))
                return hit.position + Vector3.up * feetOffset;
        return death;
    }

    // яд и кровь тикали бы дальше и убили бы встающего повторно
    void ClearDamageOverTime()
    {
        if (TryGetComponent<Venom>(out var v)) Destroy(v);
        if (TryGetComponent<Bleed>(out var b)) Destroy(b);
    }

    void SetControl(bool on)
    {
        if (TryGetComponent<PlayerController>(out var pc)) pc.enabled = on;
        if (TryGetComponent<PlayerAttack>(out var pa)) pa.enabled = on;
        if (TryGetComponent<PlayerInputDriver>(out var drv)) drv.enabled = on;
    }

    // кто убил: последний ударивший. Рядового называем по доминанте состава, химеру — химерой, босса — его именем
    static string DescribeKiller(Health killer)
    {
        if (killer == null) return "неизвестно (яд, голод или своя неосторожность)";
        if (killer.TryGetComponent<CreatureBody>(out var kb))
        {
            bool boss = killer.GetComponent<Bossness>() != null;
            var dom = kb.MostKin(out _);
            string n = boss ? killer.gameObject.name : dom != null ? dom.speciesName : "химера";
            return !boss && ChimeraCensus.IsChimeric(kb) ? n + " (с чужими органами)" : n;
        }
        return killer.gameObject.name;
    }
}
