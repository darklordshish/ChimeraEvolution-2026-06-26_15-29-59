using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Логово/нора/гнездо/лёжка как объект с капом: адрес популяции, страха и возврата домой.
/// Только данные + учёт; спавном занимается хук (s2b) через публичный API — этот класс
/// никого не спавнит и сцену не меняет. В сцену в срезе s2 не кладётся.
/// Лес, слайс s2.
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

    [Header("Мощность логова")]
    public int tier = 2;
    public int capacity = 4;
    public float respawnSeconds = 30f;

    [Header("Страх места (0..1)")]
    public float fearPerKill = 0.34f;
    public float fearDecayPerSecond = 0.02f;
    public float exhaustFear = 0.7f;

    [NonSerialized] public int population;
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

    public bool IsFull => population + pendingSpawns + reservedHolders.Count >= capacity;
    public bool IsExhausted => LairRules.IsExhausted(fear, exhaustFear);

    public bool IsNearHome(Vector3 pos)
    {
        return Vector3.Distance(transform.position, pos) <= Mathf.Max(homeRadius, 0f);
    }

    /// <summary>Килл пугает только в радиусе страха; мелкое логово (tier 1) хрупче.</summary>
    public void ReportKill(Vector3 pos)
    {
        if (Vector3.Distance(transform.position, pos) > Mathf.Max(fearRadius, 0f)) return;
        bool was = IsExhausted;
        fear = LairRules.AddFear(fear, fearPerKill * 2f / Mathf.Max(tier, 1));
        if (!was && IsExhausted) Exhausted?.Invoke();
    }

    public void ReportDeath()
    {
        population = Mathf.Max(0, population - 1);
    }

    /// <summary>
    /// Хук спавнера забирает готовый слот. Блокируется только занятыми телами
    /// (population + reserved): полная ОЧЕРЕДЬ — снимаема, иначе хук встанет.
    /// Рост очереди и резерв блокируются полной крышей (см. IsFull).
    /// </summary>
    public int ConsumeSpawn()
    {
        if (pendingSpawns <= 0) return 0;
        if (population + reservedHolders.Count >= capacity) return 0;
        pendingSpawns--;
        population++;
        return 1;
    }

    /// <summary>dt явный (без Time.*) — тикается менеджером, тестируется без PlayMode.</summary>
    public void Tick(float dt)
    {
        if (dt <= 0f) return;
        bool was = IsExhausted;
        fear = LairRules.DecayFear(fear, dt, fearDecayPerSecond);
        if (was && !IsExhausted) Recovered?.Invoke();
        if (IsFull || IsExhausted) return;
        timer -= dt;
        if (timer <= 0f)
        {
            pendingSpawns++;
            timer = respawnSeconds > 0f ? respawnSeconds : 1f;
        }
    }
}
