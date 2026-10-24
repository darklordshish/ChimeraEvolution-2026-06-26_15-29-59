using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Хук спавнера логовищ: очередь сайта → живые звери через ChimeraFactory.
/// Ничего существующего не меняет: виды инжектом, смерти — подпиской onDeath.
/// Сайты тикаются здесь же (сцена); тест дёргает DrainOnce напрямую.
/// Лес, слайс s2b. Срез C: autoTick (по умолчанию ВЫКЛ — тесты тикают вручную)
/// и семплер земли, чтобы зверь не рождался на высоте логова на склоне.
/// </summary>
public class ForestSpawner : MonoBehaviour
{
    public LairRegistry registry;
    public List<SpeciesSO> speciesPool = new List<SpeciesSO>();

    /// <summary>
    /// Готовая особь змеи. `ChimeraFactory` собирает тело из состава, но у змеи тело —
    /// ЦЕПЬ (`SnakeBodyChain`), а фабрика цепь не вешает: рождённая змея невидима
    /// (рендереры морфа выключены). Пока цепь не переехала в фабрику, лесной спавнер
    /// для «Змеи» берёт префаб; остальные виды идут прежним путём через фабрику.
    /// </summary>
    public GameObject snakePrefab;

    /// <summary>
    /// Тикать себя в Update. ВЫКЛ по умолчанию намеренно: существующие тесты дёргают Tick
    /// явно, и самопроизвольный тик ломал бы их арифметику. В мире среза C ForestBuilder включает.
    /// </summary>
    public bool autoTick = false;

    /// <summary>Земля в МИРОВЫХ (x,z) → мировой Y. null — берём Y логова (поведение до среза C).</summary>
    Func<float, float, float> groundSampler;

    int spawnCounter;

    /// <summary>
    /// ВСЕ живые тела, рождённые этим спавнером. Нужен для уборки при пересборке мира:
    /// звери — не дети корня мира (префаб/фабрика кладут их в корень сцены), поэтому
    /// `DomeWorld.Clear` их не снимает, и без списка они переживают `ForestBuilder.Clear`
    /// с `home` на уже снесённый `LairSite`. Чужие тела сюда не попадают.
    /// </summary>
    readonly List<CreatureBody> spawned = new List<CreatureBody>();

    /// <summary>Сколько тел спавнер родил и ещё не убрал (наблюдателям и тестам).</summary>
    public int SpawnedCount => spawned.Count;

    /// <summary>Последний рождённый (для тестов и наблюдателей; прошлого не храним).</summary>
    public CreatureBody lastSpawn { get; private set; }

    /// <summary>Старый вызов (два аргумента) остаётся: земля не задана — спавн по Y логова.</summary>
    public void Configure(LairRegistry reg, List<SpeciesSO> pool, Func<float, float, float> ground = null)
    {
        registry = reg;
        speciesPool = pool;
        groundSampler = ground;
    }

    void Update()
    {
        if (autoTick) Tick(Time.deltaTime);
    }

    public void Tick(float dt)
    {
        if (registry == null) return;
        foreach (var site in registry.Sites)
        {
            if (site == null) continue;
            site.Tick(dt);                    // страх и восстановление — всегда
            if (!site.spawnEnabled) continue; // а рожать — только у включённых
            DrainOnce(site);
        }
    }

    /// <summary>Родить одного из очереди. False — очередь цела (тест это проверяет).</summary>
    public bool DrainOnce(LairSite site)
    {
        if (site == null || site.pendingSpawns <= 0) return false;
        if (!site.spawnEnabled) return false;
        if (site.population + site.ReservedCount >= site.capacity) return false; // крыша занята телами
        if (site.IsExhausted) return false;
        SpeciesSO species = FindSpecies(site.speciesName);
        if (species == null) return false;
        Vector3 pos = SpawnPos(site);
        CreatureBody body;
        if (snakePrefab != null && site.speciesName == "Змея")
        {
            // Цепь змеи живёт в префабе; фабрика её не собирает (см. поле snakePrefab).
            var go = UnityEngine.Object.Instantiate(snakePrefab, pos, Quaternion.identity);
            body = go.GetComponent<CreatureBody>();
        }
        else
        {
            body = ChimeraFactory.Spawn(
                species,
                speciesPool != null ? speciesPool.ToArray() : new SpeciesSO[0],
                pos,
                species.speciesName + "-lair",
                null,
                null);
        }
        if (body == null) return false;
        spawned.Add(body); // свой живой зверь — уборщику на пересборке мира
        lastSpawn = body;
        body.home = site; // s6: точка возврата — свой дом с рождения
        site.ConsumeSpawn();
        Health health = body.GetComponent<Health>();
        if (health != null) health.onDeath.AddListener(() => OnSpawnedDeath(site, body));
        return true;
    }

    /// <summary>
    /// Снести всех рождённых этим спавнером и забыть их. Зовётся из `ForestBuilder.Clear`
    /// ДО `world.Clear`: звери не под корнем мира, мир их не снимет, и, если снести сначала
    /// сайты, живые тела останутся с `home` на снесённый `LairSite` (и начнут к нему ходить).
    /// <para>
    /// Только свои: игрок и чужие тела в списке не значатся. Идемпотентно — повторный вызов
    /// на пустом списке ничего не делает; уже снесённые записи пропускаются (Unity-null).
    /// </para>
    /// </summary>
    public void ClearSpawned()
    {
        // Список чистим ДО сноса: если OnDestroy тела заглянет сюда, он увидит пустоту,
        // а не половину списка. lastSpawn тоже отдаём раньше — на снесённое не смотрим.
        var doomed = new List<CreatureBody>(spawned);
        spawned.Clear();
        lastSpawn = null;

        foreach (var body in doomed)
        {
            if (body == null) continue;              // уже снесён кем-то (смерть, чужой Clear)
            GameObject go = body.gameObject;
            if (go == null) continue;                // ...или ушёл вместе с родителем
            go.SetActive(false);                     // гоча: гасим сразу — с этого кадра тело не тикает
            if (Application.isPlaying) UnityEngine.Object.Destroy(go);
            else UnityEngine.Object.DestroyImmediate(go);
        }
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
        float x = c.x + Mathf.Cos(angle) * radius;
        float z = c.z + Mathf.Sin(angle) * radius;
        // Y логова — только запасной путь: на склоне кольца он врёт (зверь в воздухе/в скале).
        float y = groundSampler != null ? groundSampler(x, z) : c.y;
        return new Vector3(x, y, z);
    }

    SpeciesSO FindSpecies(string speciesName)
    {
        if (speciesPool == null) return null;
        foreach (var s in speciesPool)
            if (s != null && s.speciesName == speciesName) return s;
        return null;
    }
}
