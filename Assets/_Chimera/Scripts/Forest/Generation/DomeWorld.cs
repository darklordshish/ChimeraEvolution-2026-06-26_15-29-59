using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Рантайм-мир павильона: один экземпляр = один собранный мир под переданным корнем.
/// Порядок — как в спеке среза A: гидрология → вход → место лаборатории → чанки с
/// коллайдерами → скальное кольцо → вода → устья/гнёзда → ясень и гнездо → корпуса →
/// флора → свод/звёзды/солнце.
/// Никакой статики превью и никакого Find: корень и все ссылки — поля экземпляра.
/// Чанки, флора, корпуса и спавн игрока сидят на SampleGround — ровной площадке
/// лаборатории; вода, устья и ясень — на сыром поле.
/// Срез B (B0): ясень можно не строить, а вход — точку у подножия кольца — строить
/// с каменным порталом-маркером (без коллайдеров: NavMesh он не задевает).
/// B0.1: лаборатория — ЕДИНЫЙ закрытый бункер (плита 46×36, четыре комнаты вокруг
/// закрытого зала 14×14 с обходом) в случайном месте по сиду, не ближе 150 м к входу;
/// площадка мира едет за комплексом, а старый лабиринт остаётся на флаге
/// <c>buildLabComplex = false</c> (сцены A/C).
/// Лес, срез A + C + B0 + B0.1.
/// </summary>
public class DomeWorld
{
    /// <summary>Полная ровная площадка лаборатории (радиус, м).</summary>
    public const float LabRadius = 25f;
    /// <summary>Плоско до R20 (двор и арена), дальше 5 м плавного блендинга к полю.</summary>
    public const float LabFlatRadius = 20f;
    /// <summary>
    /// Насколько вход отстоит внутрь от внутренней кромки кольца (м): Ø400 → r ≈ 100,
    /// Ø2000 → r ≈ 900. Это точка у ПОДНОЖИЯ кольца, а не портал в нём.
    /// </summary>
    public const float EntryInset = 20f;
    /// <summary>
    /// Круг прежнего превью (R30). У комплекса B0.1 исключение флоры — ПРЯМОУГОЛЬНИК
    /// площадки (<see cref="InsideLabPad"/>), а не круг: пятно 46×36 + запас 2 м.
    /// </summary>
    public const float LabClearingRadius = 30f;
    /// <summary>Лаборатория не ближе стольких метров к входу (решение B0.1).</summary>
    public const float DefaultMinLabDistance = 150f;

    public GameObject Root { get; private set; }
    public DomeHydro.State Hydro { get; private set; }
    public DomeAshSite.AshSpot Ash { get; private set; }
    public bool HasAsh { get; private set; }
    public DomeFacilityLayout.Facility Facility { get; private set; }
    /// <summary>Комплекс B0.1 (null, когда стоит старый лабиринт A/C).</summary>
    public DomeLabComplex.Result LabComplex { get; private set; }
    /// <summary>B0.3: план интерьера, выбранный до сборки.</summary>
    public LabPlan LabPlan { get; private set; }
    /// <summary>Центр лаборатории в координатах поля: двор A/C или плита комплекса.</summary>
    public Vector2 LabCenter { get; private set; }
    /// <summary>Портал комплекса (координаты поля); default у старого лабиринта.</summary>
    public Vector3 LabDoor { get; private set; }
    /// <summary>Северный выход станции для петли A; default — нет двери.</summary>
    public Vector3 LabNorthDoor { get; private set; }
    /// <summary>Подход к лаборатории мокрый: у комплекса построен настил.</summary>
    public bool LabWetApproach { get; private set; }
    /// <summary>Решённый вход B0.2: Rock или Underground.</summary>
    public LabEntrance ResolvedLabEntrance { get; private set; }
    /// <summary>Решённый облик комплекса (Bastion или InSlope).</summary>
    public LabStyle ResolvedLabStyle { get; private set; }
    public List<DomeSlope.MouthSpot> Mouths { get; private set; } = new List<DomeSlope.MouthSpot>();
    public List<DomeSlope.NestSpot> Nests { get; private set; } = new List<DomeSlope.NestSpot>();
    public List<DomeRavines.Ravine> Ravines { get; private set; } = new List<DomeRavines.Ravine>();
    public DomeTerrace.State Terrace { get; private set; }
    public DomeCanyon.State Canyon { get; private set; }
    public int FallenTrees { get; private set; }
    public int VisualNests { get; private set; }
    public LairSite NestSite { get; private set; }
    /// <summary>
    /// Точка постановки игрока: центр двора/атриума лаборатории, земля + 1 м.
    /// </summary>
    public Vector3 SpawnPoint { get; private set; }
    /// <summary>
    /// Вход мира (срез B): у подножия скального кольца внутри чаши, на земле. Готов
    /// до постановки игрока, поэтому ForestBuilder со spawnAtEntry может на него встать.
    /// Считается ДО лаборатории: направление ангара (сид) задаёт, с какой стороны вход.
    /// </summary>
    public Vector3 Entry { get; private set; }
    public int Chunks { get; private set; }
    public int Trees { get; private set; }
    public float PlatformY => platformY;
    public float MapDiameter => cfg != null ? cfg.mapDiameter : 0f;

    DomeGenConfigSO cfg;
    float platformY;
    /// <summary>Кадр площадки под комплекс: поворот плиты (у старого лабиринта не работает).</summary>
    float padYaw;
    /// <summary>Площадка прямоугольная (комплекс) или круглая (старый лабиринт/двор A).</summary>
    bool padRect;
    NavMeshSurface navSurface;
    readonly List<Material> mats = new List<Material>();
    public IReadOnlyList<Material> Materials => mats;
    readonly List<Mesh> meshes = new List<Mesh>();

    /// <summary>
    /// Собрать мир под root. root должен быть свежим (ForestBuilder создаёт "<seed>_ForestWorld");
    /// cfg остаётся собственностью вызывающего и здесь не уничтожается.
    /// <para>
    /// buildAsh=false — не строить ясень и совиное гнездо (HasAsh=false, Ash=default,
    /// NestSite=null): на Ø400 дерево встаёт в скальное кольцо, а срез B его ещё не проверяет.
    /// buildEntry=true — вычислить Entry и поставить каменный портал-маркер у подножия кольца.
    /// </para>
    /// <para>
    /// buildLabComplex=true — B0.1: лаборатория ищется по сиду (не ближе minLabDistance к входу)
    /// и строится единым комплексом; false — прежний лабиринт из боксов в центре (A/C).
    /// </para>
    /// </summary>
    public void Build(GameObject root, DomeGenConfigSO cfg, int terrainQuads, long seed,
        bool buildAsh = true, bool buildEntry = false,
        LabStyle labStyle = LabStyle.Bastion, float minLabDistance = DefaultMinLabDistance,
        bool buildLabComplex = false, LabEntrance labEntrance = LabEntrance.Rock,
        bool buildRavines = false, bool buildLabPlan = false, int labPlanVersion = 1,
        bool buildTerrace = false, bool buildCanyon = false, bool buildStationLoop = false)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        if (Root != null) Clear();

        Root = root;
        this.cfg = cfg;
        Hydro = DomeHydro.Build(cfg, 256);

        // 1) ВХОД — ДО лаборатории. Ангар (его угол задан сидом) показывает, с какой стороны
        //    света вход; сама точка — у подножия кольца внутри чаши. Ни площадки, ни корпусов
        //    для этого не нужно, поэтому лабораторию можно ставить «от входа», а не наоборот.
        Vector2 entryXZ = EntryPointXZ(seed);
        ResolvedLabEntrance = labEntrance == LabEntrance.Auto ? LabEntrance.Rock : labEntrance;

        // 2) Место лаборатории: B0.1 — поиск по сиду; A/C — двор в центре мира.
        float minDistance = minLabDistance > 0f ? minLabDistance : DefaultMinLabDistance;
        var site = default(DomeLabSite.Spot);
        if (buildLabComplex)
        {
            site = DomeLabSite.Find(cfg, seed, entryXZ, Hydro, labStyle, minDistance,
                ResolvedLabEntrance);
            if (!site.found)
            {
                Debug.LogError($"[Forest] лаборатории нет места: {site.note} — остаётся старый лабиринт");
                buildLabComplex = false;
            }
        }

        if (buildLabComplex)
        {
            LabCenter = site.center;
            ResolvedLabStyle = site.style;
            LabWetApproach = site.wetApproach;
            padYaw = site.yawDeg;
            padRect = true;
            // Плита стоит на верхней точке пятна (поиск её и вернул): не висит и не режет склон.
            platformY = Mathf.Max(site.groundY, Hydro.lakeLevel + 0.5f);
            // Подземный вход: пол ниже подхода на перепад рампы; земля над ним — позже.
            if (ResolvedLabEntrance == LabEntrance.Underground)
                platformY = Mathf.Max(site.groundY - DomeLabComplex.UndergroundDrop,
                    Hydro.lakeLevel + 0.5f);
            if (site.relaxed || site.wetApproach || site.fallback)
                Debug.LogWarning($"[Forest] лаборатория: {site.note}");
            Debug.Log($"[Forest] лаборатория: {site.style} в {site.distanceToEntry:F0} м от входа, " +
                      $"центр ({site.center.x:F0}; {site.center.y:F0}), перепад {site.relief:F1} м, " +
                      $"площадка {platformY:F1} м, {(site.wetApproach ? "мокрый подход/настил" : "сухой подход")}");
        }
        else
        {
            LabCenter = Vector2.zero;
            ResolvedLabStyle = LabStyle.Bastion;
            LabWetApproach = false;
            padYaw = 0f;
            padRect = false;
            // Цель площадки: не ниже уровня озера, иначе центр двора уйдёт под воду.
            platformY = Mathf.Max(
                DomeHydro.SampleHydro(cfg, Hydro, 0f, 0f),
                Hydro.lakeLevel + 0.5f);
        }

        if (buildTerrace)
            Terrace = DomeTerrace.Build(cfg, seed, LabCenter, entryXZ, Hydro,
                DomeRockRing.InnerRadius(cfg));
        if (buildCanyon)
            Canyon = DomeCanyon.Build(cfg, seed, Hydro, entryXZ, LabCenter,
                DomeRockRing.InnerRadius(cfg), Terrace);
        if (buildRavines)
            Ravines = DomeRavines.Build(cfg, seed, LabCenter, entryXZ, Hydro,
                DomeRockRing.InnerRadius(cfg));

        BuildChunks(terrainQuads);
        BuildRockRing();
        BuildWater();
        BuildMouthsAndNests(seed);
        if (buildAsh) BuildAshAndNest(seed);
        else
        {
            // Явно, а не «и так дефолт»: поле читается как факт мира снаружи.
            HasAsh = false;
            Ash = default;
            NestSite = null;
        }
        if (buildLabComplex)
            LabPlan = buildLabPlan
                ? LabPlanGenerator.Generate(seed, labPlanVersion, ResolvedLabEntrance, ResolvedLabStyle)
                : LabPlanFixed.Create();
        BuildFacility(seed, buildLabComplex, buildStationLoop);

        // SpawnPoint — двор старого лабиринта или закрытый зал комплекса. У комплекса
        // верх плиты на Plinth выше площадки: игрок встаёт на плиту, а не в её толщу.
        float spawnGround = SampleGround(LabCenter.x, LabCenter.y);
        if (LabComplex != null)
            spawnGround = Mathf.Max(spawnGround, platformY + DomeLabComplex.Plinth);
        SpawnPoint = new Vector3(LabCenter.x, spawnGround + 1f, LabCenter.y);

        BuildEntry(buildEntry, entryXZ);

        // Вход — не чаща: вокруг Entry оставляем поляну R12, иначе игрок рождается
        // в стволе и не видит ни входа, ни леса. Пятно комплекса тоже не засаживаем:
        // дерево в стене или на крыше — не «густой лес», а дырка в бункере.
        Func<float, float, bool> labPad = (x, z) => LabComplex != null && InsideLabPad(x, z, 0f);
        Func<float, float, bool> clearing = (x, z) =>
        {
            if (Vector2.Distance(new Vector2(x, z), new Vector2(Entry.x, Entry.z)) < 12f)
                return true;
            if (DomeRavines.Contains(Ravines, x, z, 1.2f)) return true;
            if (LabComplex == null) return false;
            if (labPad(x, z)) return true;
            // Перед LabDoor — спокойный участок: дверь видна целиком, деревья
            // обрамляют сбоку, а не закрывают вход (правило дизайн-линии 25.10).
            // Мокрый подход длиннее — настил не должен прорастать стволами.
            float approachClear = (LabWetApproach || ResolvedLabEntrance == LabEntrance.Underground)
                ? 32f : 14f;
            return Vector2.Distance(new Vector2(x, z), new Vector2(LabDoor.x, LabDoor.z)) < approachClear;
        };
        // Вырубка гасит деревья; подлесок живёт в R12/R14 и пятнах, но не под плитой
        // комплекса (12 см плиты — трава торчала бы в зале).
        Func<float, float, bool> undergrowthExclude = (x, z) => labPad(x, z) || InsideRamp(x, z);
        Trees = DomeFlora.Build(Root, cfg, Hydro, seed, SampleGround, clearing, undergrowthExclude);
        if (Ravines.Count > 0)
        {
            DomeRavines.BuildWalls(Root, Ravines, SampleGround,
                Flat(DomePalette.Rock * 0.65f), meshes);
            FallenTrees = DomeRavines.BuildFallenTrees(Root, Ravines, seed, SampleGround,
                Flat(DomePalette.Bark), meshes);
            VisualNests = DomeRavines.BuildNests(Root, Ravines, seed, SampleGround,
                Flat(DomePalette.ConcreteDark), Flat(DomePalette.Olive), meshes);
        }
        BuildSky(seed);
    }

    /// <summary>
    /// Пол мира: вырезанное гидрологией поле + плоская площадка лаборатории
    /// (плоско до R20, плавный блендинг к сырому полю на краю). Вода, устья и ясень
    /// эту функцию НЕ используют.
    /// </summary>
    /// <summary>Сырое поле без площадки/рампы/оврагов — только для диагностики.</summary>
    public float RawGround(float x, float z) =>
        cfg != null && Hydro != null ? DomeHydro.SampleHydro(cfg, Hydro, x, z) : 0f;

    public float RingGround(float x, float z) =>
        DomeSlope.RingField(cfg, x, z, SampleGround);

    public float SampleGround(float x, float z)
    {
        if (cfg == null || Hydro == null) return 0f;
        float raw = DomeHydro.SampleHydro(cfg, Hydro, x, z);
        raw = DomeTerrace.Apply(raw, Terrace, x, z);
        raw = DomeCanyon.Apply(raw, Canyon, x, z, Hydro.lakeLevel);
        raw = DomeRavines.Apply(raw, Ravines, x, z, Hydro.lakeLevel);

        // Подземный вход: вырезаем пологую траншею-рампу от двери к лесной площадке.
        if (padRect && ResolvedLabEntrance == LabEntrance.Underground)
        {
            float dx = x - LabCenter.x, dz = z - LabCenter.y;
            float yaw = padYaw * Mathf.Deg2Rad;
            float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
            float lx = dx * c - dz * s;
            float lz = dx * s + dz * c;
            float zDoor = -DomeLabComplex.HalfDepth;
            float zOuter = zDoor - DomeLabComplex.RampLength;
            if (Mathf.Abs(lx) <= DomeLabComplex.RampWidth * 0.5f &&
                lz <= zDoor && lz >= zOuter)
            {
                float rampT = Mathf.Clamp01((zDoor - lz) / DomeLabComplex.RampLength);
                return Mathf.Lerp(platformY, platformY + DomeLabComplex.UndergroundDrop, rampT);
            }
        }

        float u = PadWeight(x, z);
        if (u <= 0f) return platformY;
        float t = u * u * (3f - 2f * u);
        return Mathf.Lerp(platformY, raw, t);
    }

    /// <summary>
    /// 0 — внутри площадки (пол = platformY), 1 — сырое поле. Двор A/C — круг R20/R25
    /// вокруг центра; у комплекса B0.1 к нему добавляется прямоугольник плиты с полем
    /// <see cref="DomeLabComplex.PadBlend"/> — площадка там, где плоско ХОТЯ БЫ по одному
    /// из двух (минимум веса). Так угол плиты не повисает над сырым склоном.
    /// </summary>
    float PadWeight(float x, float z)
    {
        float dx = x - LabCenter.x, dz = z - LabCenter.y;
        float d = Mathf.Sqrt(dx * dx + dz * dz);
        float u = Mathf.InverseLerp(LabFlatRadius, LabRadius, d);
        if (!padRect) return u;

        float yaw = padYaw * Mathf.Deg2Rad;
        float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
        float lx = dx * c - dz * s; // поле → кадр плиты
        float lz = dx * s + dz * c;
        float q = Mathf.Max(Mathf.Abs(lx) - DomeLabComplex.PadHalfWidth,
            Mathf.Abs(lz) - DomeLabComplex.PadHalfDepth);
        float ur = Mathf.InverseLerp(0f, DomeLabComplex.PadBlend, q);
        return Mathf.Min(u, ur);
    }

    /// <summary>
    /// Точка внутри прямоугольной площадки комплекса (поле → кадр плиты). Для старого
    /// лабиринта (круг) возвращает круг прежнего превью — но зовётся это только у комплекса.
    /// </summary>
    bool InsideLabPad(float x, float z, float margin)
    {
        if (!padRect)
            return Vector2.Distance(new Vector2(x, z), LabCenter) < LabClearingRadius;
        float dx = x - LabCenter.x, dz = z - LabCenter.y;
        float yaw = padYaw * Mathf.Deg2Rad;
        float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
        float lx = dx * c - dz * s; // поле → кадр плиты
        float lz = dx * s + dz * c;
        return Mathf.Abs(lx) <= DomeLabComplex.PadHalfWidth + margin &&
               Mathf.Abs(lz) <= DomeLabComplex.PadHalfDepth + margin;
    }

    /// <summary>
    /// Запечь NavMesh тайлами по коллайдерам мира. Поверхность — компонент ForestBuilder;
    /// collectObjects=Children означает, что корень мира должен быть её ребёнком.
    /// </summary>
    /// <summary>Точка в полосе подземной рампы (B0.2b): там не растёт подлесок.</summary>
    bool InsideRamp(float x, float z)
    {
        if (!padRect || ResolvedLabEntrance != LabEntrance.Underground) return false;
        float dx = x - LabCenter.x, dz = z - LabCenter.y;
        float yaw = padYaw * Mathf.Deg2Rad;
        float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
        float lx = dx * c - dz * s;
        float lz = dx * s + dz * c;
        float zDoor = -DomeLabComplex.HalfDepth;
        float zOuter = zDoor - DomeLabComplex.RampLength;
        return Mathf.Abs(lx) <= DomeLabComplex.RampWidth * 0.5f + 0.8f &&
               lz <= zDoor && lz >= zOuter;
    }

    public void BuildNavMesh(NavMeshSurface surface)
    {
        if (surface == null || cfg == null)
        {
            Debug.LogWarning("[Forest] NavMesh не запечён: мир не собран или NavMeshSurface не передан");
            return;
        }
        ClearNavData();
        // Поверхность могла остаться с прошлой сборки: снимаем и уничтожаем старые данные,
        // иначе BuildNavMesh перезапишет ссылку и старый ассет утечёт.
        if (surface.navMeshData != null)
        {
            surface.RemoveData();
            DestroyObject(surface.navMeshData, !Application.isPlaying);
            surface.navMeshData = null;
        }
        navSurface = surface;
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.overrideVoxelSize = true;
        surface.voxelSize = 0.35f;
        surface.overrideTileSize = true;
        surface.tileSize = 32;
        surface.center = Vector3.zero;
        surface.size = new Vector3(cfg.mapDiameter + 400f, 200f, cfg.mapDiameter + 400f);
        surface.BuildNavMesh();
        if (surface.navMeshData == null)
            Debug.LogError("[Forest] NavMesh пуст: нет коллайдеров или бейк не прошёл");
    }

    /// <summary>
    /// Снести всё, что создал мир: NavMeshData, корень с мешами и материалами,
    /// кэши флоры/корпусов, мокрость и статику неба. Чужое (cfg, поверхность) не трогает.
    /// </summary>
    public void Clear()
    {
        ClearNavData();

        if (Root != null)
        {
            if (Application.isPlaying) Root.SetActive(false); // Destroy отложен: гасим сразу
            DestroyObject(Root, !Application.isPlaying);
            Root = null;
        }
        // Сначала вернуть сухие цвета живым материалам, потом уничтожать их.
        DomeWeather.ResetWet();
        foreach (var mat in mats)
            if (mat != null) DestroyObject(mat, !Application.isPlaying);
        mats.Clear();
        foreach (var mesh in meshes)
            if (mesh != null) DestroyObject(mesh, !Application.isPlaying);
        meshes.Clear();

        DomeFlora.ReleaseCaches();
        DomeFacilityPlacer.ReleaseCaches();
        DomeLabComplex.ReleaseCaches();
        DomeSkyRig.Reset();

        Hydro = null;
        Ash = default;
        HasAsh = false;
        Facility = null;
        LabComplex = null;
        LabPlan = null;
        LabCenter = Vector2.zero;
        LabDoor = default;
        LabNorthDoor = default;
        LabWetApproach = false;
        ResolvedLabStyle = LabStyle.Bastion;
        padYaw = 0f;
        padRect = false;
        Mouths = new List<DomeSlope.MouthSpot>();
        Nests = new List<DomeSlope.NestSpot>();
        NestSite = null;
        SpawnPoint = default;
        Entry = default;
        Chunks = 0;
        Trees = 0;
        Ravines = new List<DomeRavines.Ravine>();
        Terrace = default;
        Canyon = default;
        FallenTrees = 0;
        VisualNests = 0;
        platformY = 0f;
        cfg = null;
    }

    void ClearNavData()
    {
        if (navSurface == null) return;
        navSurface.RemoveData();
        if (navSurface.navMeshData != null)
        {
            DestroyObject(navSurface.navMeshData, !Application.isPlaying);
            navSurface.navMeshData = null;
        }
        navSurface = null;
    }

    void BuildChunks(int terrainQuads)
    {
        var mat = Flat(DomePalette.Terrain);
        int n = DomeGenRules.ChunkCount(cfg);
        float half = cfg.mapDiameter * 0.5f;
        int built = 0;
        for (int cz = 0; cz < n; cz++)
            for (int cx = 0; cx < n; cx++)
            {
                // Павильон круглый: квадратные углы сетки за кольцом не нужны
                // (они торчали зелёными квадратами в топдаун-кадре). Центр чанка
                // за радиусом — чанк целиком вне чаши.
                float centerX = -half + (cx + 0.5f) * cfg.chunkSize;
                float centerZ = -half + (cz + 0.5f) * cfg.chunkSize;
                if (new Vector2(centerX, centerZ).magnitude > half) continue;
                var mesh = DomeChunkMesh.BuildChunkMesh(cfg, cx, cz, terrainQuads, SampleGround);
                Track(mesh);
                var go = Part($"Chunk_{cx}_{cz}", mesh, mat);
                var collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                built++;
            }
        Chunks = built;
    }

    void BuildRockRing()
    {
        var mat = Flat(DomePalette.Rock);
        var mesh = DomeRockRing.BuildRingMesh(cfg, 256, 6);
        Track(mesh);
        var go = Part("RockRing", mesh, mat);
        var collider = go.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
    }

    void BuildWater()
    {
        var mat = Flat(DomePalette.Water);
        // Озеро и река — прежнее поведение; плёс/полотно/ручей добавляются только при
        // живом каньоне и читают тот же SampleGround, что чанки и NavMesh (Task 2).
        DomeWaterMesh.Build(Root.transform, cfg, Hydro, Canyon, SampleGround, mat, meshes);
    }

    void BuildMouthsAndNests(long seed)
    {
        // Устья стоят на скальном кольце: площадку лаборатории сюда не тянем.
        Func<float, float, float> ringGround = RingGround;
        Mouths = DomeSlope.FindMouths(cfg, 3, 96, ringGround);
        Nests = DomeSlope.FindNests(cfg, seed, Mouths, 2, ringGround);
        // Гнездо — каирн на земле. Оно не знает про пятно комплекса (DomeSlope про него
        // не спрашивают), и на B0.1 каирн мог бы встать в атриуме. Убираем только те,
        // что попали в пятно: остальные логова и их число не трогаем. A/C не задет —
        // фильтр работает лишь когда комплекс есть.
        if (padRect && Nests.Count > 0)
        {
            int before = Nests.Count;
            // Пятно прямоугольное: круг R29 срезал бы углы 46×36, а гнездо в углу
            // площадки — такое же гнездо в стене, как и под самой плитой.
            Nests.RemoveAll(n => InsideLabPad(n.pos.x, n.pos.y, 4f));
            if (Nests.Count != before)
                Debug.Log($"[Forest] гнёзд снято с пятна лаборатории: {before - Nests.Count}");
        }
        if (Mouths.Count == 0)
        {
            Debug.LogWarning("[Forest] устьев нет на этом рельефе (смени сид)");
            return;
        }
        var rock = Flat(DomePalette.Rock);
        var dark = Flat(DomePalette.Dark);
        var nest = Flat(DomePalette.Nest);
        DomeMouthBuilder.Build(Root.transform, cfg, Mouths, Nests, ringGround, rock, dark, nest, meshes,
            (x, z) => padRect && InsideLabPad(x, z, 4f));
    }

    void BuildAshAndNest(long seed)
    {
        try
        {
            // Место ясеня считается ОТ лаборатории, а не от центра мира: в B0.1 двор уехал,
            // и «100–400 м от лаборатории» — это расстояние до комплекса.
            bool standLab = LabCenter.sqrMagnitude > 0.01f;
            float ashScale = standLab ? DomeAshSite.ScaleFor(cfg) : 1f;
            var spot = DomeAshSite.FindSpot(cfg, seed, LabCenter, Hydro, 96, ashScale, standLab,
                (x, z) => DomeRavines.Contains(Ravines, x, z, 1.4f), SampleGround);
            // Ясень стоит по тому же рельефу, что и всё остальное (с оврагами/рампой).
            float gy = SampleGround(spot.pos.x, spot.pos.y);
            spot.groundY = gy;
            Ash = spot;
            HasAsh = true;

            var bark = Flat(DomePalette.Bark);
            var crown = Flat(DomePalette.Crown);
            var owl = Flat(DomePalette.Owl);
            DomeAshSite.BuildGeometry(Root.transform, new Vector3(spot.pos.x, gy, spot.pos.y),
                bark, crown, owl, meshes, ashScale);

            var nestGo = new GameObject("AshNest");
            nestGo.transform.SetParent(Root.transform, false);
            var site = nestGo.AddComponent<LairSite>();
            // Гнездо на 36 м по OTVET; вбок — потому что ствол 16 м в поперечнике
            // (на 36 м радиус ≈ 5 м, присада в 12 м от оси уже снаружи).
            var nestLocal = new Vector3(spot.pos.x + DomeAshSite.NestLateral * ashScale,
                gy + DomeAshSite.NestHeight * ashScale, spot.pos.y);
            DomeAshSite.ConfigureNest(site, Root.transform.TransformPoint(nestLocal));
            NestSite = site;
        }
        catch (Exception e)
        {
            HasAsh = false;
            Ash = default;
            NestSite = null;
            Debug.LogWarning($"[Forest] ясень/гнездо не поставлены: {e.Message}");
        }
    }

    /// <summary>
    /// Вход мира (срез B): от ангара к центру, на EntryInset внутрь от внутренней кромки
    /// кольца, высота — по земле мира. Ангар стоит ЗА кольцом (D/2 + 40), сквозь которое
    /// хода нет, поэтому вход — у подножия кольца внутри чаши, а ангар остаётся кулисой.
    /// Направление берём от ангара: он и задаёт, с какой стороны света вход.
    /// <para>
    /// B0.1: точка СЧИТАЕТСЯ ДО лаборатории (<see cref="EntryPointXZ"/>) — вход и есть начало
    /// координат замысла, лаборатория ставится «не ближе 150 м от него», а не наоборот.
    /// </para>
    /// </summary>
    void BuildEntry(bool buildMarker, Vector2 p)
    {
        Entry = new Vector3(p.x, SampleGround(p.x, p.y), p.y);
        if (!buildMarker) return;
        Vector2 inward = p.sqrMagnitude > 1e-6f ? p.normalized : Vector2.down;
        BuildEntryMarker(inward);
    }

    /// <summary>
    /// Точка входа в координатах поля: направление от ангара (угол — из сида, один источник
    /// <see cref="DomeFacilityLayout.HangarSpot"/>), расстояние — Rin − EntryInset.
    /// Ни площадки, ни корпусов не требует: поэтому вход известен раньше лаборатории.
    /// </summary>
    Vector2 EntryPointXZ(long seed)
    {
        Vector2 dir = -DomeFacilityLayout.HangarSpot(cfg, seed); // от ангара к центру
        if (dir.sqrMagnitude < 1e-6f) dir = Vector2.down;
        dir.Normalize();
        float radius = Mathf.Max(0f, DomeRockRing.InnerRadius(cfg) - EntryInset);
        return dir * radius;
    }

    /// <summary>
    /// Маркер входа: два косяка, перемычка и тёмная плита проёма — каменная «дверь»
    /// у подножия кольца. Лицом к центру, чтобы её было видно с той стороны, откуда
    /// приходит игрок. Коллайдеров НЕТ намеренно: это визуальный ориентир, а не стена —
    /// иначе он вырезал бы дырку в NavMesh и запирал подход.
    /// </summary>
    void BuildEntryMarker(Vector2 inward)
    {
        var gate = new GameObject("EntryGate");
        gate.transform.SetParent(Root.transform, false);
        gate.transform.localPosition = Entry;
        gate.transform.localRotation =
            Quaternion.LookRotation(new Vector3(inward.x, 0f, inward.y), Vector3.up);

        const float openingHalf = 3f;   // проём 6 м
        const float jambHalf = 1.2f;    // толщина косяка по 2.4 м
        const float depth = 1.1f;       // глубина по 2.2 м
        const float height = 7f;

        var rock = Flat(DomePalette.Rock);
        var dark = Flat(DomePalette.Dark);

        var jamb = FloraMeshKit.Slab(jambHalf, height, depth);
        Track(jamb);
        Part("EntryJambL", jamb, rock, gate.transform).transform.localPosition =
            new Vector3(-(openingHalf + jambHalf), 0f, 0f);
        Part("EntryJambR", jamb, rock, gate.transform).transform.localPosition =
            new Vector3(openingHalf + jambHalf, 0f, 0f);

        var lintel = FloraMeshKit.Slab(openingHalf + jambHalf * 2f, 1.2f, depth + 0.1f);
        Track(lintel);
        Part("EntryLintel", lintel, rock, gate.transform).transform.localPosition =
            new Vector3(0f, height, 0f);

        // Проём — тёмная плита ЗА плоскостью рамы (к кольцу): с чаши видна глубина.
        var recess = FloraMeshKit.Slab(openingHalf, height * 0.92f, 0.15f);
        Track(recess);
        Part("EntryRecess", recess, dark, gate.transform).transform.localPosition =
            new Vector3(0f, 0f, -0.6f);
    }

    /// <summary>
    /// Корпуса. <paramref name="buildLabComplex"/> = false — прежняя раскладка A/C (двор с
    /// ареной, клетками, хирургией и отдыхом отдельными боксами) в центре мира.
    /// true — единый закрытый бункер B0.1 в найденном месте: плита, четыре комнаты вокруг
    /// закрытого зала, сплошное перекрытие, искусственный свет; старый <c>Facility</c> тогда null.
    /// </summary>
    void BuildFacility(long seed, bool buildLabComplex, bool buildStationLoop = false)
    {
        if (buildLabComplex)
        {
            LabComplex = DomeLabComplex.Build(Root, LabCenter, platformY, padYaw,
                ResolvedLabStyle, ResolvedLabEntrance, LabWetApproach, LabPlan, buildStationLoop);
            DomeLabDamage.Apply(LabComplex, seed, 1, LabPlan);
            LabDoor = LabComplex.door;
            LabNorthDoor = LabComplex.northDoor;
            Facility = null;
            return;
        }

        Facility = DomeFacilityLayout.Build(cfg, seed, LabCenter);
        Vector2 lab = Facility.labCenter;
        // Во дворе — только площадка лаборатории (RingField там даёт сырое поле и
        // поднимает пол над выровненной землёй). В кольце (ангар) — высшее из
        // площадки и кольца: там детали уже стоят в мировых координатах.
        Func<float, float, float> ground = (x, z) =>
        {
            float d = Vector2.Distance(new Vector2(x, z), lab);
            if (d <= LabRadius + 10f) return SampleGround(x, z);
            return Mathf.Max(SampleGround(x, z), RingGround(x, z));
        };
        DomeFacilityPlacer.Build(Root, Facility, ground);
        LabComplex = null;
        LabDoor = default;
        LabNorthDoor = default;
    }

    void BuildSky(long seed)
    {
        float rv = DomeGenRules.VaultRadius(cfg);
        var vaultCenter = new Vector3(0f, DomeVault.CenterY(rv), 0f);

        var skyShader = Shader.Find("Chimera/DomeSky");
        var starShader = Shader.Find("Chimera/DomeFlat");
        if (skyShader == null || starShader == null)
            Debug.LogWarning("[Forest] шейдеры свода не найдены: проверь Always Included Shaders в билде");
        var skyMat = new Material(skyShader != null ? skyShader : Shader.Find("Universal Render Pipeline/Unlit"));
        if (skyMat.HasProperty("_Center")) skyMat.SetVector("_Center", vaultCenter);
        else skyMat.color = new Color(0.55f, 0.70f, 0.88f);
        mats.Add(skyMat);
        var vault = DomeVault.BuildVaultMesh(rv, 128, 24);
        Track(vault);
        Part("Vault", vault, skyMat);

        var starMat = new Material(starShader != null ? starShader : Shader.Find("Universal Render Pipeline/Unlit"));
        if (starMat.HasProperty("_Level")) starMat.SetFloat("_Level", 0f);
        mats.Add(starMat);
        var stars = DomeStars.BuildStarMesh(seed, 400, vaultCenter, rv * 0.985f, rv * 0.004f);
        Track(stars);
        Part("Stars", stars, starMat);

        var sun = new GameObject("DomeSun");
        sun.transform.SetParent(Root.transform, false);
        var light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.shadows = LightShadows.Soft;

        DomeSkyRig.Reset();
        DomeSkyRig.ApplyPhase(Root, 10.5f);
    }

    Material Flat(Color color)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        mat.SetFloat("_Smoothness", 0f);
        mats.Add(mat);
        return mat;
    }

    GameObject Part(string name, Mesh mesh, Material mat)
    {
        return Part(name, mesh, mat, Root.transform);
    }

    GameObject Part(string name, Mesh mesh, Material mat, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    void Track(Mesh mesh)
    {
        if (mesh != null) meshes.Add(mesh);
    }

    static void DestroyObject(UnityEngine.Object obj, bool immediate)
    {
        if (obj == null) return;
        if (immediate) UnityEngine.Object.DestroyImmediate(obj);
        else UnityEngine.Object.Destroy(obj);
    }
}
