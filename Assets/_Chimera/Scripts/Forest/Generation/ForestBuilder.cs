using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

/// <summary>
/// Точка подключения павильона — 1:1 как арена (ArenaWalls): висит на пустом объекте
/// в центре мира, сид — сериализованное поле (+ randomSeed-флаг как у арены),
/// размер — поле mapDiameter (аналог arenaSide). Срез A: полная сборка мира в Awake
/// через DomeWorld (чанки, вода, кольцо, устья/гнездо, ясень, корпуса, флора, свод),
/// NavMesh тайлами и постановка игрока во дворе лаборатории.
/// Срез C: точки купола становятся настоящими LairSite — устья и гнёзда змей в реестре,
/// совиное гнездо в реестре без спавна, ForestSpawner с землёй под зверя.
/// Срез B (B0): чанк — поле (сетка обязана делить диаметр), вход у подножия кольца
/// (игрок на world.Entry + каменный портал), ясень можно выключить (на Ø400 он в скале).
/// B0.1: лаборатория — единый ЗАКРЫТЫЙ бункер (LabStyle Bastion/InSlope, случайное место
/// по сиду не ближе minLabDistance к входу, сплошное перекрытие, искусственный свет,
/// ходибельный интерьер) за флагом buildLabComplex; при false стоит прежний лабиринт —
/// сцены A и C не меняются.
/// Лес, срез A (24.10) + C + B0 + B0.1.
/// </summary>
[RequireComponent(typeof(NavMeshSurface))]
public class ForestBuilder : MonoBehaviour
{
    [Header("Сид (виден в Inspector, пишется в лог с инструкцией — как ArenaWalls)")]
    public long seed = 1337;
    public bool randomSeed = false;

    [Header("Размер (аналог arenaSide: один параметр масштабирует всё)")]
    public float mapDiameter = 2000f;
    [Tooltip("0 = старые 700; иначе целевое число деревьев на стенд")]
    public int treeCount = 0;
    [Tooltip("Пятна леса/поляны; A/C по умолчанию выключены")]
    public bool patchyFlora = false;

    // Сетка чанков начинается в −D/2 и шагает на chunkSize, поэтому chunkSize ОБЯЗАН
    // делить диаметр: при Ø400 и 250 внутри чаши остаётся дыра, а чанк торчит за кольцо.
    // Ноль — «не настроено»: новое сериализованное поле у компонента из сцены приходит
    // нулём (гоча), и тогда берём диаметр/8 (Ø2000 → 250, Ø400 → 50), а НЕ дефолт 250.
    [Header("Чанк (м): 0 — диаметр/8, но не мельче 50")]
    public float chunkSize = 250f;

    [Header("Рельеф: квадов на чанк (16 — превью-редколесье)")]
    public int terrainQuads = 16;

    [Header("Сборка при старте (выключить — соберёшь Build() вручную)")]
    public bool buildOnAwake = true;

    [Header("Конфиг (необязательно: иначе дефолты DomeGenConfigSO)")]
    public DomeGenConfigSO config;

    [Header("Игрок (необязательно: иначе двор лаборатории)")]
    public Transform playerSpawn;

    [Header("Вход у подножия кольца (B): игрок на world.Entry + каменный портал")]
    public bool spawnAtEntry = false;

    [Header("Ясень и гнездо совы (B выключает: на Ø400 дерево встаёт в скалу)")]
    public bool buildAsh = true;

    [Header("Овраги B1.1: выемки, упавшие стволы, визуальные гнёзда (без LairSite)")]
    public bool buildRavines = false;

    [Header("География 27.10: терраса и каньон (Ø2000)")]
    public bool buildTerrace = false;
    public bool buildCanyon = false;
    /// <summary>Петля A: северная дверь станции (Bastion). По умолчанию выключена, A/C не меняются.</summary>
    public bool buildStationLoop = false;
    /// <summary>Холм станции: lift-слой вокруг Buried; по умолчанию выключен, A/C не меняются.</summary>
    public bool buildStationHill = false;

    [Header("Лаборатория B0.1: единый закрытый бункер вместо старого лабиринта")]
    public bool buildLabComplex = false;

    [Header("B0.3: генерировать план интерьера (fixed/mirror)")]
    public bool buildLabPlan = false;
    public int labPlanVersion = 1;

    [Header("B1.2: климат — время и погода (B0.3)")]
    public bool buildClimate = false;
    public WeatherKind climateWeather = WeatherKind.Clear;
    public float climateTimeMinutes = 10f;
    public float climateTimeScale = 1f / 60f;
    public bool climateAutoWeather = false;
    public float climateChangeMinutes = 8f;
    public float climateBlendSeconds = 20f;
    ForestClimate climate;

    [Header("Стиль оболочки: Bastion (наземный) / InSlope (в склоне) / Auto")]
    public LabStyle labStyle = LabStyle.Bastion;

    [Header("Вход B0.2: Rock (портал в скале) / Underground (спуск) / Auto")]
    public LabEntrance labEntrance = LabEntrance.Rock;

    // Ноль = «не настроено»: новое сериализованное поле у компонента из сцены приходит
    // нулём (гоча проекта), а порог 150 м — решение B0.1, не случайное число.
    [Header("Комплекс не ближе стольких метров к входу (0 — 150)")]
    public float minLabDistance = DefaultMinLabDistance;

    [Header("Логова и спавнер (пусто — логова копят очередь, но не рожают)")]
    public List<SpeciesSO> speciesPool = new List<SpeciesSO>();

    [Header("Змея: префаб с цепью тела (фабрика цепь не вешает)")]
    public GameObject snakePrefab;

    [Header("Змеиные логовища: устья и гнёзда (числа тюнинга)")]
    public int snakeCapacity = DefaultSnakeCapacity;
    public int snakeTier = DefaultSnakeTier;
    public float snakeHomeRadius = DefaultSnakeHomeRadius;
    public float snakeSpawnRadius = DefaultSnakeSpawnRadius;
    public float snakeFearRadius = DefaultSnakeFearRadius;
    public float snakeRespawnSeconds = DefaultSnakeRespawn;

    // Дефолты среза C — ОДНИМ местом: поля в сцене уже лежат, а новое сериализованное
    // поле у такого компонента приходит НУЛЁМ (гоча проекта). Ноль читаем как
    // «не настроено» и подставляем дефолт, иначе логова молча получат capacity 0.
    const int DefaultSnakeCapacity = 3;
    const int DefaultSnakeTier = 2;
    const float DefaultSnakeHomeRadius = 12f;
    const float DefaultSnakeSpawnRadius = 3f;
    const float DefaultSnakeFearRadius = 18f;
    const float DefaultSnakeRespawn = 30f;
    /// <summary>Порог B0.1: лаборатория не ближе стольких метров к входу.</summary>
    const float DefaultMinLabDistance = 150f;

    public int ResolvedSeed { get; private set; }
    public GameObject BuiltRoot { get; private set; }
    public DomeHydro.State Hydro => world != null ? world.Hydro : null;
    public DomeFacilityLayout.Facility Facility => world != null ? world.Facility : null;
    public DomeAshSite.AshSpot Ash => world != null ? world.Ash : default;
    public DomeHill.State Hill => world != null ? world.Hill : default;
    public bool HasAsh => world != null && world.HasAsh;
    public LairSite NestSite => world != null ? world.NestSite : null;
    public int Chunks => world != null ? world.Chunks : 0;
    public int Trees => world != null ? world.Trees : 0;
    public int TreeColliders => world != null ? world.TreeColliders : 0;
    public int CoverPodsBuilt => world != null ? world.CoverPodsBuilt : 0;
    public float CrownCover => world != null ? world.CrownCover : 0f;
    public IReadOnlyList<ForestSpace.Clearing> Clearings => world != null ? world.Clearings : new List<ForestSpace.Clearing>();
    public IReadOnlyList<ForestSpace.Corridor> Corridors => world != null ? world.Corridors : new List<ForestSpace.Corridor>();
    public IReadOnlyList<ForestSpace.CoverPod> CoverPods => world != null ? world.CoverPods : new List<ForestSpace.CoverPod>();
    public int Ravines => world != null ? world.Ravines.Count : 0;
    public IReadOnlyList<DomeRavines.Ravine> RavineList =>
        world != null ? world.Ravines : new List<DomeRavines.Ravine>();
    public DomeTerrace.State Terrace => world != null ? world.Terrace : default;
    public DomeCanyon.State Canyon => world != null ? world.Canyon : default;
    public int FallenTrees => world != null ? world.FallenTrees : 0;
    public int VisualNests => world != null ? world.VisualNests : 0;
    public Vector3 SpawnPoint { get; private set; }
    /// <summary>Вход мира (срез B): у подножия скального кольца, внутри чаши. До SpawnPlayer готов.</summary>
    public Vector3 Entry => world != null ? world.Entry : default;
    /// <summary>Центр лаборатории в координатах поля: двор A/C или плита комплекса B0.1.</summary>
    public Vector2 LabCenter => world != null ? world.LabCenter : Vector2.zero;
    /// <summary>Портал комплекса B0.1 в координатах поля (как Entry); default у старого лабиринта.</summary>
    public Vector3 LabDoor => world != null ? world.LabDoor : default;
    public Vector3 LabAtrium => world != null ? world.LabAtrium : default;
    public Vector3 LabNorthDoor => world != null ? world.LabNorthDoor : default;
    public SamplingContract Contract => world != null ? world.Contract : default;
    public GenTrace Trace => world != null ? world.Trace : null;
    public ulong Fingerprint { get; private set; }
    public string FingerprintHex => WorldFingerprint.Hex(Fingerprint);
    /// <summary>Какой стиль комплекс получил на самом деле (Auto разрешён в один из двух).</summary>
    public LabStyle ResolvedLabStyle => world != null ? world.ResolvedLabStyle : labStyle;
    public LabEntrance ResolvedLabEntrance => world != null ? world.ResolvedLabEntrance : labEntrance;
    /// <summary>Построенный комплекс (null при старом лабиринте A/C).</summary>
    public DomeLabComplex.Result LabComplex => world != null ? world.LabComplex : null;
    public LabPlan LabPlan => world != null ? world.LabPlan : null;
    public float SampleGround(float x, float z) => world != null ? world.SampleGround(x, z) : 0f;
    public float RawGround(float x, float z) => world != null ? world.RawGround(x, z) : 0f;

    /// <summary>Логова этого мира в порядке постановки: змеиные, затем совиное — ровно то, что в реестре.</summary>
    public IReadOnlyList<LairSite> Lairs => lairs;
    /// <summary>Спавнер логовищ (живёт на этом же объекте, появляется вместе с миром).</summary>
    public ForestSpawner Spawner => spawner;
    /// <summary>Реестр логовищ (живёт на этом же объекте).</summary>
    public LairRegistry Registry => lairRegistry;

    DomeWorld world;
    DomeGenConfigSO ownedConfig;
    LairRegistry lairRegistry;
    ForestSpawner spawner;
    readonly List<LairSite> lairs = new List<LairSite>();
    bool building;

    void Awake()
    {
        if (buildOnAwake) Build();
    }

    /// <summary>
    /// Собрать мир. Повторный вызов пересобирает с нуля. Ошибки сборки логируются,
    /// но не валят Awake: пустой мир лучше, чем мёртвая сцена.
    /// </summary>
    public void Build()
    {
        if (building) return;
        building = true;
        try
        {
            BuildInternal();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Forest] сборка мира не удалась: {e}");
            Clear();
        }
        finally
        {
            building = false;
        }
    }

    void BuildInternal()
    {
        Clear();

        ownedConfig = config != null ? Instantiate(config) : ScriptableObject.CreateInstance<DomeGenConfigSO>();
        ownedConfig.randomSeed = false;
        ownedConfig.mapDiameter = mapDiameter;
        ownedConfig.chunkSize = ResolveChunkSize(mapDiameter);
        ownedConfig.treeCount = treeCount > 0 ? treeCount : 0;
        ownedConfig.patchyFlora = patchyFlora;

        var issues = DomeGenRules.CheckConfig(ownedConfig);
        if (issues.Count > 0)
        {
            Debug.LogError($"[Forest] конфиг купола не сходится: {string.Join("; ", issues)}");
            DestroyOwnedConfig();
            return;
        }

        long chosen;
        if (randomSeed)
        {
            // Как у арены: сид от тика, но с перебором — поле без озера/реки не мир.
            long baseSeed = (uint)Environment.TickCount;
            chosen = 0L;
            bool found = false;
            for (int attempt = 0; attempt < 8 && !found; attempt++)
            {
                long candidate = baseSeed + attempt;
                ownedConfig.seed = candidate;
                try
                {
                    DomeHydro.Build(ownedConfig, 256);
                    chosen = candidate;
                    found = true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Forest] сид {candidate} не дал воду: {e.Message}");
                }
            }
            if (!found)
            {
                Debug.LogError("[Forest] 8 сидов подряд не дали пригодный мир — сборка отменена");
                DestroyOwnedConfig();
                return;
            }
            seed = chosen;
            Debug.Log($"ForestBuilder: случайный сид {seed} (для воспроизведения — выключи randomSeed и впиши его)");
        }
        else
        {
            chosen = seed;
            Debug.Log($"[Forest] фикс-сид {chosen}");
        }

        ownedConfig.seed = chosen;
        ResolvedSeed = unchecked((int)chosen);
        BuiltRoot = new GameObject($"{chosen}_ForestWorld");
        BuiltRoot.transform.SetParent(transform, false);

        world = new DomeWorld();
        // buildEntry — это «поставить вход-портал»: сцена, которая просит спавн у входа,
        // хочет и сам вход видимым. Отдельного рубильника у портала нет намеренно.
        // B0.1: стиль и порог лаборатории идут в мир; 0 в пороге — «не настроено».
        world.Build(BuiltRoot, ownedConfig, terrainQuads, chosen, buildAsh, spawnAtEntry,
            labStyle, minLabDistance > 0f ? minLabDistance : DefaultMinLabDistance,
            buildLabComplex, labEntrance, buildRavines, buildLabPlan, labPlanVersion,
            buildTerrace, buildCanyon, buildStationLoop, buildStationHill);

        var surface = GetComponent<NavMeshSurface>();
        world.BuildNavMesh(surface);

        SpawnPlayer();

        BuildLairs();
        Fingerprint = WorldFingerprint.Compute(this);
        SetupClimate();

        int parts = world.Facility != null ? world.Facility.parts.Count
            : world.LabComplex != null ? world.LabComplex.parts : 0;
        string lab;
        if (world.LabComplex != null)
        {
            float labToEntry = Vector2.Distance(
                world.LabCenter, new Vector2(world.Entry.x, world.Entry.z));
            lab = $"комплекс {world.LabComplex.style} в {labToEntry:F0} м от входа, " +
                  $"деталей {world.LabComplex.parts} " +
                  $"(интерьер {world.LabComplex.interiorParts}, оболочка {world.LabComplex.exteriorParts}), " +
                  $"ламп {world.LabComplex.lights}, портал {world.LabDoor}, " +
                  $"план {world.LabComplex.planTemplate} v{world.LabComplex.planVersion}" +
                  (world.LabComplex.planFallback ? " fallback" : "");
        }
        else
        {
            lab = $"старый лабиринт, деталей {parts}";
        }
        Debug.Log($"[Forest] мир {chosen}: чанков {world.Chunks}, деревьев {world.Trees}, " +
                  $"устий {world.Mouths.Count}, гнёзд {world.Nests.Count}, {lab}, " +
                  $"ясень {(world.HasAsh ? "есть" : "нет")}, " +
                  $"гнездо совы {(world.NestSite != null ? "есть" : "нет")}, " +
                  $"оврагов {world.Ravines.Count}, стволов {world.FallenTrees}, визуальных гнёзд {world.VisualNests}, " +
                  $"вход {world.Entry}");
    }

    /// <summary>
    /// Чанк, делящий диаметр. Ноль в поле — «не настроено» (новое сериализованное поле
    /// у компонента из сцены приходит нулём): диаметр/8 даёт 250 на Ø2000 — прежний мир A,
    /// — а дефолт 250 на Ø400 оставил бы дыру в чаше. Нижняя граница 50 — правило DomeGenRules.
    /// </summary>
    float ResolveChunkSize(float diameter)
    {
        if (chunkSize > 0f) return chunkSize;
        return Mathf.Max(50f, diameter / 8f);
    }

    /// <summary>
    /// Игрок — в точку playerSpawn (если задан), в world.Entry (если сцена просит вход)
    /// или в центр двора лаборатории. CharacterController гасим на время телепорта:
    /// иначе он вернёт позицию.
    /// </summary>
    void SpawnPlayer()
    {
        if (world == null) return;
        Vector3 basePos = playerSpawn != null ? playerSpawn.position
                        : spawnAtEntry ? world.Entry
                        : world.SpawnPoint;
        // Вход — это портал; стоять В нём нельзя (камера внутри косяка). Игрок
        // появляется на 6 м в чашу от входа, лицом к лесу; портал остаётся за спиной.
        if (spawnAtEntry && playerSpawn == null)
        {
            Vector2 inward = -new Vector2(basePos.x, basePos.z);
            if (inward.sqrMagnitude > 1e-4f)
            {
                inward.Normalize();
                basePos += new Vector3(inward.x, 0f, inward.y) * 6f;
            }
        }
        Vector3 target = new Vector3(basePos.x, world.SampleGround(basePos.x, basePos.z) + 1f, basePos.z);
        SpawnPoint = target;

        var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        CharacterController controller = player != null ? player.GetComponent<CharacterController>() : null;
        Transform targetTransform = player != null ? player.transform : null;
        if (targetTransform == null)
        {
            controller = UnityEngine.Object.FindAnyObjectByType<CharacterController>();
            if (controller != null) targetTransform = controller.transform;
        }
        if (targetTransform == null)
        {
            Debug.Log("[Forest] игрок не найден — мир ждёт, точка спавна запомнена");
            return;
        }

        if (controller != null) controller.enabled = false;
        targetTransform.position = target;
        if (controller != null) controller.enabled = true;
    }

    /// <summary>
    /// Срез C: точки купола → настоящие логова. Устье — логово змей (Den), гнездо у устья —
    /// тоже змеиное (Nest). Ни мешей, ни коллайдеров тут нет: LairSite — АДРЕС популяции,
    /// страха и возврата домой, поэтому NavMesh он не задевает (бейк был выше).
    /// Совиное гнездо уже стоит в кроне ясеня (DomeWorld), ему остаётся регистрация без спавна.
    /// </summary>
    void BuildLairs()
    {
        if (world == null) return;

        lairRegistry = GetComponent<LairRegistry>();
        if (lairRegistry == null) lairRegistry = gameObject.AddComponent<LairRegistry>();

        int mouths = 0;
        foreach (var m in world.Mouths)
        {
            // Логово — не в скале портала, а на ровной земле перед ним. DomeSlope ищет
            // площадку в 5 м, но у кольца она ещё под скалой: змея рождается в камне и
            // её не видно. BowlPad отодвигает адрес в чашу, пока кольцо не перестанет
            // быть выше поля. Вход остаётся точкой самого устья.
            Vector2 down = new Vector2(Mathf.Cos(m.facing), Mathf.Sin(m.facing));
            Vector2 pad = BowlPad(m.pos + down * 5f);
            float y = Mathf.Max(world.RingGround(pad.x, pad.y),
                                world.SampleGround(pad.x, pad.y));
            var site = MakeSnakeLair($"Логово_устье_{mouths}", FieldToWorld(pad.x, y, pad.y),
                LairSite.LairAreaType.Den);
            float yMouth = world.RingGround(m.pos.x, m.pos.y);
            site.entrances.Add(FieldToWorld(m.pos.x, yMouth, m.pos.y));
            mouths++;
        }

        int nests = 0;
        foreach (var n in world.Nests)
        {
            // Гнёзда тоже могут стоять на кромке кольца. Тот же BowlPad: адрес — в чаше,
            // каирн остаётся на месте. Высота — верхняя из скалы и поля, чтобы не под водой.
            Vector2 pad = BowlPad(n.pos);
            float y = Mathf.Max(world.RingGround(pad.x, pad.y),
                                world.SampleGround(pad.x, pad.y));
            MakeSnakeLair($"Логово_гнездо_{nests}", FieldToWorld(pad.x, y, pad.y),
                LairSite.LairAreaType.Nest);
            nests++;
        }

        if (world.NestSite != null)
        {
            // Сова в реестре есть, а спавна нет: тела филина в игре ещё не существует,
            // и рожать «Сову» из пула было бы пустым телом. Страх при этом живёт и тает.
            world.NestSite.spawnEnabled = false;
            lairRegistry.Register(world.NestSite);
            lairs.Add(world.NestSite); // Lairs и Registry держат ОДНО и то же множество
        }

        spawner = GetComponent<ForestSpawner>();
        if (spawner == null) spawner = gameObject.AddComponent<ForestSpawner>();
        spawner.snakePrefab = snakePrefab;
        spawner.Configure(lairRegistry, speciesPool, GroundAtWorld);
        spawner.autoTick = true;

        if (speciesPool == null || speciesPool.Count == 0)
            Debug.LogWarning("[Forest] пул видов пуст: логова стоят и копят очередь, но рожать некому — " +
                             "впиши виды в speciesPool у ForestBuilder");

        Debug.Log($"[Forest] логова: змеиных {mouths + nests} (устий {mouths}, гнёзд {nests}), " +
                  $"совиное {(world.NestSite != null ? "есть (без спавна)" : "нет")}, " +
                  $"в реестре {lairRegistry.Count}, пул {(speciesPool != null ? speciesPool.Count : 0)}, " +
                  $"autoTick {spawner.autoTick}");
    }

    LairSite MakeSnakeLair(string name, Vector3 worldPos, LairSite.LairAreaType areaType)
    {
        var go = new GameObject(name);
        go.transform.SetParent(world.Root.transform, false);
        go.transform.position = worldPos; // FieldToWorld уже перевёл в мировые
        var site = go.AddComponent<LairSite>();
        site.speciesName = "Змея";
        site.areaType = areaType;
        // Ноль = «не настроено» (новое поле у компонента из сцены приходит нулём).
        site.capacity = snakeCapacity > 0 ? snakeCapacity : DefaultSnakeCapacity;
        site.tier = snakeTier > 0 ? snakeTier : DefaultSnakeTier;
        site.homeRadius = snakeHomeRadius > 0f ? snakeHomeRadius : DefaultSnakeHomeRadius;
        site.spawnRadius = snakeSpawnRadius > 0f ? snakeSpawnRadius : DefaultSnakeSpawnRadius;
        site.fearRadius = snakeFearRadius > 0f ? snakeFearRadius : DefaultSnakeFearRadius;
        site.respawnSeconds = snakeRespawnSeconds > 0f ? snakeRespawnSeconds : DefaultSnakeRespawn;
        lairRegistry.Register(site);
        lairs.Add(site);
        return site;
    }

    /// <summary>
    /// Отодвинуть адрес логова от скального кольца в чашу. Кольцо считается «выше поля»,
    /// пока RingField больше SampleGround: тогда точка ещё в камне, и змея рождается под ним.
    /// Шагаем к центру по 5 м, пока кольцо не перестанет доминировать.
    /// </summary>
    Vector2 BowlPad(Vector2 start)
    {
        Vector2 p = start;
        for (int i = 0; i < 12; i++)
        {
            float ring = world.RingGround(p.x, p.y);
            float field = world.SampleGround(p.x, p.y);
            if (ring <= field + 0.5f) return p;
            if (p.sqrMagnitude < 1f) break;
            p += -p.normalized * 5f;
        }
        return p;
    }

    /// <summary>Координаты поля → мировые. Корень мира может стоять не в нуле; в нуле — тождество.</summary>
    Vector3 FieldToWorld(float x, float y, float z)
    {
        return world.Root.transform.TransformPoint(new Vector3(x, y, z));
    }

    /// <summary>
    /// Земля для спавнера. Приходит МИРОВАЯ точка (сайт живёт в мировых, как игрок),
    /// а поле мира считает в координатах корня — потому переводим туда и обратно.
    /// Высота — верхняя из скалы кольца и поля (см. гнёзда): не ниже видимой поверхности.
    /// </summary>
    float GroundAtWorld(float x, float z)
    {
        if (world == null || world.Root == null || ownedConfig == null) return 0f;
        var root = world.Root.transform;
        Vector3 local = root.InverseTransformPoint(new Vector3(x, 0f, z));
        float y = Mathf.Max(world.RingGround(local.x, local.z),
                            world.SampleGround(local.x, local.z));
        return root.TransformPoint(new Vector3(local.x, y, local.z)).y;
    }

    /// <summary>
    /// Снести мир и всё, что он создал. Сериализованные поля (сид, диаметр, конфиг)
    /// не трогаются — иначе повторный Build соберёт не тот мир.
    /// </summary>
    public void Clear()
    {
        Fingerprint = 0UL;
        // Порядок важен: тик → живые → адреса → мир.
        // 1) autoTick off: следующий Update не должен Tick-нуть по снесённым сайтам.
        // 2) ClearSpawned: рождённые звери НЕ дети корня мира (префаб/фабрика кладут их
        //    в корень сцены), поэтому world.Clear их не снимет — снимаем сами, и снимаем
        //    ПОКА ИХ `home` ЖИВ: снести сначала сайты значило бы оставить живого зверя
        //    с `home` на снесённый LairSite (долг среза C — ровно это).
        // 3) реестр и список логов — адреса уходят раньше самих сайтов (сайты снесёт world).
        // 4) world.Clear — сам мир.
        if (spawner != null)
        {
            spawner.autoTick = false;
            spawner.ClearSpawned();
        }
        if (lairRegistry != null) lairRegistry.Clear();
        lairs.Clear();

        if (climate != null)
        {
            climate.enabled = false;
            climate.skyRoot = null;
            climate.wetMaterials = null;
            climate.rain = null;
            climate.lab = null;
            climate.ground = null;
            ForestClimate.LabZone = null;
        }
        if (world != null)
        {
            world.Clear();
            world = null;
        }
        else if (BuiltRoot != null)
        {
            if (Application.isPlaying) BuiltRoot.SetActive(false);
            DestroyObject(BuiltRoot, !Application.isPlaying);
        }
        BuiltRoot = null;

        var surface = GetComponent<NavMeshSurface>();
        if (surface != null && surface.navMeshData != null)
        {
            surface.RemoveData();
            DestroyObject(surface.navMeshData, !Application.isPlaying);
            surface.navMeshData = null;
        }

        DestroyOwnedConfig();
        SpawnPoint = default;
        DomeSkyRig.Reset();
        ForestClimate.ResetStatic();
    }

    void SetupClimate()
    {
        if (!buildClimate || world == null)
        {
            if (climate != null) climate.enabled = false;
            return;
        }
        if (climate == null)
        {
            climate = GetComponent<ForestClimate>();
            if (climate == null) climate = gameObject.AddComponent<ForestClimate>();
        }
        climate.enabled = true;
        climate.state = new WeatherState { kind = climateWeather };
        climate.timeMinutes = climateTimeMinutes;
        climate.timeScale = climateTimeScale;
        climate.autoWeather = climateAutoWeather;
        climate.weatherChangeMinutes = climateChangeMinutes;
        climate.weatherBlendSeconds = climateBlendSeconds;
        climate.weatherSeed = ResolvedSeed;
        climate.weatherTable = DomeWeather.PavilionTable();
        climate.SetWeatherImmediate(climateWeather);
        climate.StartWeatherClock();
        climate.skyRoot = world.Root;
        climate.lab = world.LabComplex;
        climate.SetGround(world.SampleGround, world.Hydro.lakeLevel);
        ForestClimate.LabZone = world.LabComplex;
        var wet = new List<Material>(world.Materials);
        wet.AddRange(DomeFlora.Materials);
        climate.wetMaterials = wet;
        if (climateWeather == WeatherKind.Rain && climate.rain == null)
            climate.rain = DomeRain.BuildPreview(world.Root, 0f, 0f, world.SampleGround);
        climate.ApplyVisuals();
    }

    void DestroyOwnedConfig()
    {
        if (ownedConfig == null) return;
        DestroyObject(ownedConfig, !Application.isPlaying);
        ownedConfig = null;
    }

    void OnDestroy()
    {
        Clear();
    }

    static void DestroyObject(UnityEngine.Object obj, bool immediate)
    {
        if (obj == null) return;
        if (immediate) UnityEngine.Object.DestroyImmediate(obj);
        else UnityEngine.Object.Destroy(obj);
    }
}
