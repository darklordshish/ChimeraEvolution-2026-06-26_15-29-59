using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

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
    /// Тикать себя в Update. ВЫКЛ по умолчанию намеренно: существующие тесты дёргают Tick
    /// явно, и самопроизвольный тик ломал бы их арифметику. В мире среза C ForestBuilder включает.
    /// </summary>
    public bool autoTick = false;

    /// <summary>Земля в МИРОВЫХ (x,z) → мировой Y. null — берём Y логова (поведение до среза C).</summary>
    Func<float, float, float> groundSampler;

    // Радиусы поиска проходимой точки: сначала рядом с точкой рождения, потом — у самого адреса логова.
    const float NavSnapRadius = 2f;

    int spawnCounter;

    /// <summary>
    /// ВСЕ живые тела, рождённые этим спавнером. Нужен для уборки при пересборке мира:
    /// звери — не дети корня мира (префаб/фабрика кладут их в корень сцены), поэтому
    /// `DomeWorld.Clear` их не снимает, и без списка они переживают `ForestBuilder.Clear`
    /// с `home` на уже снесённый `LairSite`. Чужие тела сюда не попадают.
    /// </summary>
    readonly List<CreatureBody> spawned = new List<CreatureBody>();

    // Внятное предупреждение о пуле — ОДИН раз на вид, а не каждый тик.
    readonly HashSet<string> warnedMissingSpecies = new HashSet<string>();

    /// <summary>Сколько тел спавнер родил и ещё не убрал (наблюдателям и тестам). Мёртвые не считаются:
    /// тело с нулевым HP — уже не жилец, его снимает `Health.destroyOnDeath`, а учёт логова ведёт `ReportDeath`.</summary>
    public int SpawnedCount
    {
        get
        {
            PruneDead();
            return spawned.Count;
        }
    }

    /// <summary>Последний рождённый (для тестов и наблюдателей; прошлого не храним).</summary>
    public CreatureBody lastSpawn { get; private set; }

    /// <summary>Старый вызов (два аргумента) остаётся: земля не задана — спавн по Y логова.</summary>
    public void Configure(LairRegistry reg, List<SpeciesSO> pool, Func<float, float, float> ground = null)
    {
        registry = reg;
        speciesPool = pool;
        groundSampler = ground;
        spawnCounter = 0;     // новый мир — новый детерминированный ряд точек рождения
        warnedMissingSpecies.Clear();
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
        if (species == null) { WarnMissingSpecies(site.speciesName); return false; }

        // Все виды — через фабрику, змея тоже: цепь звеньев фабрика вешает сама (пакет А, `f2b24c8d`).
        Vector3 pos = SpawnPos(site);
        CreatureBody body = ChimeraFactory.Spawn(
            species,
            speciesPool != null ? speciesPool.ToArray() : new SpeciesSO[0],
            pos,
            species.speciesName + "-lair",
            null,
            null);
        if (body == null) return false;
        spawned.Add(body); // свой живой зверь — уборщику на пересборке мира
        lastSpawn = body;
        site.ConsumeSpawn();
        site.Claim(body); // закон жильца: новорождённый — жилец места рождения, дом можно сменить
        Health health = body.GetComponent<Health>();
        if (health != null) health.onDeath.AddListener(() => OnSpawnedDeath(body));
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
        spawnCounter = 0; // следующий мир начнёт ряд точек рождения с нуля

        foreach (var body in doomed)
        {
            if (body == null) continue;              // уже снесён кем-то (смерть, чужой Clear)
            GameObject go = body.gameObject;
            if (go == null) continue;                // ...или ушёл вместе с родителем
            Kill(go);
        }
    }

    /// <summary>
    /// Смерть зверя логова. Место в логове освобождается само (мёртвый не жилец, `LairSite.population`), а страх — по
    /// закону свидетелей (спека «логово по законам» §4.3): пугаются логова, чьи жильцы смерть видели или слышали, или
    /// у чьего порога она случилась. Вид жертвы не сравнивается с видом логова.
    /// <para>Лес знает смерти тех, кого родил; смерти босса и игрока придут, когда у механик будет общий сигнал смерти.</para>
    /// </summary>
    void OnSpawnedDeath(CreatureBody body)
    {
        spawned.Remove(body); // мёртвый не жилец: SpawnedCount не должен его считать
        if (body == null) return;
        ReportDeathAt(body.transform.position, body);
    }

    /// <summary>Смерть в точке — всем логовам реестра по закону свидетелей.</summary>
    public void ReportDeathAt(Vector3 deathPos, CreatureBody victim)
    {
        if (registry == null) return;
        foreach (var s in registry.Sites)
            if (s != null && s.Witnessed(deathPos, victim)) s.Scare();
    }

    /// <summary>Снять из списка всё, что уже мертво или снесено, не дожидаясь смерти с колбэком
    /// (снесли чужим Destroy, выключили объект). Список только сокращается.</summary>
    void PruneDead()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            var b = spawned[i];
            if (b == null) { spawned.RemoveAt(i); continue; }
            var hp = b.GetComponent<Health>();
            if (hp != null && hp.Current > 0) continue; // живой
            spawned.RemoveAt(i);
        }
    }

    /// <summary>Точка рождения: детерминированный разброс по сиду + Земля из семплера + ПРОХОДИМАЯ точка.
    /// Навмеш испечён → ставим зверя на него (в скале/на круче логова он бы застрял); навмеша рядом нет
    /// (стенд/тест без бейка) → доверяем земле, как до среза C.</summary>
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
        var pos = new Vector3(x, y, z);

        NavMeshHit hit;
        if (NavMesh.SamplePosition(pos, out hit, NavSnapRadius, NavMesh.AllAreas))
            return Grounded(hit.position);
        // Точка в камне/на круче: берём ближайшую проходимую у самого адреса логова (его уже отодвинули в чашу).
        if (NavMesh.SamplePosition(c, out hit, Mathf.Max(site.spawnRadius * 2f, NavSnapRadius * 2f), NavMesh.AllAreas))
            return Grounded(hit.position);
        return pos;
    }

    Vector3 Grounded(Vector3 p)
    {
        if (groundSampler == null) return p;
        p.y = groundSampler(p.x, p.z);
        return p;
    }
    /// <summary>Вид логова нечем родить: его нет в пуле. Предупреждение — один раз на вид (иначе каждый тик
    /// спамил бы консоль), и в нём назван выход.</summary>
    void WarnMissingSpecies(string species)
    {
        if (!warnedMissingSpecies.Add(species)) return;
        Debug.LogWarning(
            $"ForestSpawner: вида «{species}» нет в speciesPool — логово не может родить. " +
            "Добавь вид в ForestBuilder.speciesPool.", this);
    }

    static void Kill(GameObject go)
    {
        if (go == null) return;
        go.SetActive(false); // гоча: гасим сразу — с этого кадра тело не тикает
        if (Application.isPlaying) UnityEngine.Object.Destroy(go);
        else UnityEngine.Object.DestroyImmediate(go);
    }

    SpeciesSO FindSpecies(string speciesName)
    {
        if (speciesPool == null) return null;
        foreach (var s in speciesPool)
            if (s != null && s.speciesName == speciesName) return s;
        return null;
    }
}
