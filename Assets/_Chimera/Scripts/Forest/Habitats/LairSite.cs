using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Логово/нора/гнездо/лёжка как объект с капом: адрес популяции, страха и возврата домой.
/// Только данные + учёт; спавном занимается хук (s2b) через публичный API — этот класс
/// никого не спавнит и сцену не меняет. Лес, слайс s2.
/// <para>
/// ЗАКОН ЖИЛЬЦА (спека `2026-11-13-forest-logovo-po-zakonam` §3, §4.1): жилец — живое тело, у которого это логово
/// домом (`CreatureBody.home`). Занятость — число живых жильцов, а не рождённых здесь. Умер или сменил дом —
/// перестал быть жильцом сам, без подписок и отчётов. Состав, вид рождения и психика в закон не входят.
/// </para>
/// </summary>
public class LairSite : MonoBehaviour
{
    /// <summary>Den логово / Burrow нора / Nest гнездо / Rest лёжка.</summary>
    public enum LairAreaType
    {
        Den,
        Burrow,
        Nest,
        Rest
    }

    [Header("Кто и где")]
    public string speciesName = "Волк";
    public LairAreaType areaType = LairAreaType.Den;
    public float homeRadius = 12f;
    public float spawnRadius = 6f;
    public float fearRadius = 18f;
    public List<Vector3> entrances = new List<Vector3>();

    /// <summary>
    /// Рожать ли из этого места. Выключенное место живёт: страх тает, события идут,
    /// но очередь НЕ капает (сова без тела, закрытое логово не копит толпу к включению).
    /// </summary>
    [Header("Спавн")]
    public bool spawnEnabled = true;

    [Header("Мощность логова")]
    public int tier = 2;
    public int capacity = 4;
    public float respawnSeconds = 30f;

    [Header("Страх места (0..1)")]
    public float fearPerKill = 0.34f;
    public float fearDecayPerSecond = 0.02f;
    public float exhaustFear = 0.7f;

    [NonSerialized] public int pendingSpawns;
    [NonSerialized] public float fear;
    [NonSerialized] public float timer;

    // КРЫШИ s6: место под ВЕРНУВШИХСЯ (не выводок) — отдельный счётчик, иначе врёт учёт.
    // Крыша: population + reserved + queue <= capacity.
    readonly HashSet<int> reservedHolders = new HashSet<int>();

    public int ReservedCount => reservedHolders.Count;

    /// <summary>Держать крышу за вернувшегося. Идемпотентен (повтор того же — no-op).</summary>
    public bool TryReserve(int ownerId)
    {
        if (IsFull) return false;
        return reservedHolders.Add(ownerId);
    }

    public void Release(int ownerId)
    {
        reservedHolders.Remove(ownerId);
    }

    public event Action Exhausted;
    public event Action Recovered;

    // ЖИЛЬЦЫ: кто взял логово домом. Занятость считается по ним, а мёртвые и сменившие дом выпадают при счёте.
    readonly List<CreatureBody> residents = new List<CreatureBody>();

    /// <summary>Живые жильцы — тела, у которых это логово домом.</summary>
    public IReadOnlyList<CreatureBody> Residents { get { Prune(); return residents; } }

    /// <summary>Занятость: число живых жильцов (закон жильца, а не счёт рождений).</summary>
    public int population { get { Prune(); return residents.Count; } }

    /// <summary>
    /// Тело берёт логово домом. Уже жилец — да; полное логово — нет. Прежний дом тело покидает: жилец — только у
    /// одного логова. Кто решает, куда идти, — психика (механики); логово только принимает или отказывает.
    /// </summary>
    public bool Claim(CreatureBody body)
    {
        if (body == null) return false;
        Prune();
        if (residents.Contains(body)) { body.home = this; return true; }
        if (residents.Count + reservedHolders.Count >= capacity) return false;
        if (body.home != null && body.home != this) body.home.Leave(body);
        residents.Add(body);
        body.home = this;
        return true;
    }

    /// <summary>Тело уходит из логова: перестаёт быть жильцом, дом обнуляется, если был этим логовом.</summary>
    public void Leave(CreatureBody body)
    {
        residents.Remove(body);
        if (body != null && body.home == this) body.home = null;
    }

    void Prune()
    {
        for (int i = residents.Count - 1; i >= 0; i--)
        {
            var r = residents[i];
            if (r == null || r.home != this || IsDead(r)) residents.RemoveAt(i);
        }
    }

    static bool IsDead(CreatureBody body)
    {
        var hp = body.GetComponent<Health>();
        return hp != null && hp.IsDead;
    }

    /// <summary>
    /// СВИДЕТЕЛИ (спека §4.3): смерть в точке пугает логово, если её видел или слышал кто-то из жильцов (дальность его
    /// зрения или слуха), или если она случилась у самого порога (радиус страха). Вид жертвы и вид логова не
    /// сравниваются. Сама жертва свидетелем не считается.
    /// </summary>
    public bool Witnessed(Vector3 deathPos, CreatureBody victim)
    {
        if (Vector3.Distance(transform.position, deathPos) <= Mathf.Max(fearRadius, 0f)) return true;
        foreach (var r in Residents)
        {
            if (r == victim) continue;
            var senses = r.GetComponent<Senses>();
            if (senses == null) continue;
            float reach = Mathf.Max(senses.Range(SenseKind.Hearing), senses.Range(SenseKind.Sight));
            if (Vector3.Distance(r.transform.position, deathPos) <= reach) return true;
        }
        return false;
    }

    /// <summary>Страх от увиденной смерти; мелкое логово (tier 1) хрупче.</summary>
    public void Scare()
    {
        bool was = IsExhausted;
        fear = LairRules.AddFear(fear, fearPerKill * 2f / Mathf.Max(tier, 1));
        if (!was && IsExhausted) Exhausted?.Invoke();
    }

    public bool IsFull => population + pendingSpawns + reservedHolders.Count >= capacity;
    public bool IsExhausted => LairRules.IsExhausted(fear, exhaustFear);

    public bool IsNearHome(Vector3 pos)
    {
        return Vector3.Distance(transform.position, pos) <= Mathf.Max(homeRadius, 0f);
    }

    /// <summary>Смерть у порога: пугает только в радиусе страха (частный случай `Witnessed` без жильцов).</summary>
    public void ReportKill(Vector3 pos)
    {
        if (Vector3.Distance(transform.position, pos) > Mathf.Max(fearRadius, 0f)) return;
        Scare();
    }

    /// <summary>
    /// Хук спавнера забирает готовый слот очереди. Блокируется только занятыми телами
    /// (жильцы + резерв): полная ОЧЕРЕДЬ — снимаема, иначе хук встанет. Жильцом рождённого
    /// делает `Claim` — занятость считает дом, а не этот слот.
    /// </summary>
    public int ConsumeSpawn()
    {
        if (pendingSpawns <= 0) return 0;
        if (population + reservedHolders.Count >= capacity) return 0;
        pendingSpawns--;
        return 1;
    }

    /// <summary>dt явный (без Time.*) — тикается менеджером, тестируется без PlayMode.</summary>
    public void Tick(float dt)
    {
        if (dt <= 0f) return;
        bool was = IsExhausted;
        fear = LairRules.DecayFear(fear, dt, fearDecayPerSecond);
        if (was && !IsExhausted) Recovered?.Invoke();
        // Место без спавна всё равно живёт: страх тает и события идут. А очередь не растёт —
        // иначе в кадр включения из логова вылезла бы накопленная толпа.
        if (!spawnEnabled) return;
        if (IsFull || IsExhausted) return;
        timer -= dt;
        if (timer <= 0f)
        {
            pendingSpawns++;
            timer = respawnSeconds > 0f ? respawnSeconds : 1f;
        }
    }
}
