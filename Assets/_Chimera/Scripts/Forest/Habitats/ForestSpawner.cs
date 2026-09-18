using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Хук спавнера логовищ: очередь сайта → живые звери через ChimeraFactory.
/// Ничего существующего не меняет: виды инжектом, смерти — подпиской onDeath.
/// Сайты тикаются здесь же (сцена); тест дёргает DrainOnce напрямую.
/// Лес, слайс s2b.
/// </summary>
public class ForestSpawner : MonoBehaviour
{
    public LairRegistry registry;
    public List<SpeciesSO> speciesPool = new List<SpeciesSO>();

    int spawnCounter;

    /// <summary>Последний рождённый (для тестов и наблюдателей; прошлого не храним).</summary>
    public CreatureBody lastSpawn { get; private set; }

    public void Configure(LairRegistry reg, List<SpeciesSO> pool)
    {
        registry = reg;
        speciesPool = pool;
    }

    public void Tick(float dt)
    {
        if (registry == null) return;
        foreach (var site in registry.Sites)
        {
            if (site == null) continue;
            site.Tick(dt);
            DrainOnce(site);
        }
    }

    /// <summary>Родить одного из очереди. False — очередь цела (тест это проверяет).</summary>
    public bool DrainOnce(LairSite site)
    {
        if (site == null || site.pendingSpawns <= 0) return false;
        if (site.population >= site.capacity) return false;
        if (site.IsExhausted) return false;
        SpeciesSO species = FindSpecies(site.speciesName);
        if (species == null) return false;
        Vector3 pos = SpawnPos(site);
        CreatureBody body = ChimeraFactory.Spawn(
            species,
            speciesPool != null ? speciesPool.ToArray() : new SpeciesSO[0],
            pos,
            species.speciesName + "-lair",
            null,
            null);
        if (body == null) return false;
        lastSpawn = body;
        site.ConsumeSpawn();
        Health health = body.GetComponent<Health>();
        if (health != null) health.onDeath.AddListener(() => OnSpawnedDeath(site, body));
        return true;
    }

    void OnSpawnedDeath(LairSite home, CreatureBody body)
    {
        if (home == null || body == null) return;
        home.ReportDeath();
        Vector3 deathPos = body.transform.position;
        if (registry == null) return;
        foreach (var s in registry.Sites)
        {
            if (s == null || s.speciesName != home.speciesName) continue;
            if (Vector3.Distance(s.transform.position, deathPos) <= Mathf.Max(s.fearRadius, 0f))
                s.ReportKill(deathPos);
        }
    }

    Vector3 SpawnPos(LairSite site)
    {
        float angle = SeededHash.ToFloat01(SeededHash.Hash(spawnCounter, 1, 7)) * Mathf.PI * 2f;
        float radius = site.spawnRadius
            * Mathf.Sqrt(SeededHash.ToFloat01(SeededHash.Hash(spawnCounter, 2, 77)));
        spawnCounter++;
        Vector3 c = site.transform.position;
        return new Vector3(c.x + Mathf.Cos(angle) * radius, c.y, c.z + Mathf.Sin(angle) * radius);
    }

    SpeciesSO FindSpecies(string speciesName)
    {
        if (speciesPool == null) return null;
        foreach (var s in speciesPool)
            if (s != null && s.speciesName == speciesName) return s;
        return null;
    }
}
