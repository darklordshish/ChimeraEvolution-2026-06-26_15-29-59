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
    /// <summary>
    /// S3: единственный пол мира. Порядок слоёв — в <see cref="DomeTerrain"/>:
    /// <c>Hydro0 → Shape0 → [erosion] → ShapeGround → pad → SampleGround</c>.
    /// Наружу торчат только эти функции, а не <c>BaseGround</c>/<c>TerrainGround</c>.
    /// </summary>
    public DomeTerrain Terrain { get; private set; }
    /// <summary>S3: маски габаритов (null у v1 — там центровые проверки).</summary>
    public DomeMasks Masks => masks;
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
    public Vector3 LabAtrium { get; private set; }
    public SamplingContract Contract { get; private set; }
    public GenTrace Trace { get; private set; }
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
    public DomeHill.State Hill { get; private set; }
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
    public int TreeColliders { get; private set; }
    public float ForestAreaKm2 { get; private set; }
    /// <summary>Лежащие стволы сухостоя: у них нет коллайдера (S5 §5.3).</summary>
    public int DeadLogs { get; private set; }
    public int CoverPodsBuilt { get; private set; }
    public int ArenaEdges { get; private set; }
    public DomeRelief.State Relief { get; private set; }
    /// <summary>S2: композиция v2 (семь ролей; плато станции — шаг 5).</summary>
    public DomeComposition.State Composition { get; private set; }
    /// <summary>G7 шаг 1: текущий мир, выраженный планом; null у v1.</summary>
    public MapPlan Plan { get; private set; }
    /// <summary>S4: ������ ������ (null � ����� ���������).</summary>
    public DomeErosion.State Erosion { get; private set; }
    public int ReliefHills => Relief != null ? Relief.hills.Count : 0;
    public IReadOnlyList<Vector3> TreeColliderPositions { get; private set; } = new List<Vector3>();
    public float CrownCover { get; private set; }
    /// <summary>Открытые зоны (S3): прогалины экосистемы, не арены.</summary>
    public IReadOnlyList<ForestSpace.Zone> Zones { get; private set; } = new List<ForestSpace.Zone>();
    /// <summary>Связи 16 м: лесные проходы между зонами.</summary>
    public IReadOnlyList<ForestSpace.Link> Links { get; private set; } = new List<ForestSpace.Link>();
    public IReadOnlyList<ForestSpace.Zone> Commons { get; private set; } = new List<ForestSpace.Zone>();
    /// <summary>Водопои (2 из общих зон) — адреса, а не отдельный счёт.</summary>
    public IReadOnlyList<ForestSpace.Zone> Waterholes { get; private set; } = new List<ForestSpace.Zone>();
    public IReadOnlyList<DomeShell.Spot> Shells { get; private set; } = new List<DomeShell.Spot>();
    public IReadOnlyList<DomeGate.Spot> Gates { get; private set; } = new List<DomeGate.Spot>();
    public ForestPoiRegistry Pois { get; } = new ForestPoiRegistry();
    /// <summary>S4 P6: ���� ����� v2 (null � ���������/��� POI).</summary>
    public ForestRoadNetwork Roads { get; private set; }
    public IReadOnlyList<ForestSpace.CoverPod> CoverPods { get; private set; } = new List<ForestSpace.CoverPod>();
    public float PlatformY => platformY;
    /// <summary>Поворот площадки станции (градусы); у старого лабиринта A/C — 0.</summary>
    public float PadYaw => padYaw;
    public float MapDiameter => cfg != null ? cfg.mapDiameter : 0f;

    DomeGenConfigSO cfg;
    /// <summary>S3: маски габаритов формы. null у v1 — там остаются центровые проверки.</summary>
    DomeMasks masks;
    /// <summary>Квадов на чанк в собранной сетке: дорожка садится на ТУ ЖЕ поверхность.</summary>
    int chunkQuads;
    float platformY;
    /// <summary>Кадр площадки под комплекс: поворот плиты (у старого лабиринта не работает).</summary>
    float padYaw;
    /// <summary>Площадка прямоугольная (комплекс) или круглая (старый лабиринт/двор A).</summary>
    bool padRect;
    bool northCrevice;
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
        bool buildTerrace = false, bool buildCanyon = false, bool buildStationLoop = false,
        bool buildStationHill = false)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        if (Root != null) Clear();

        Root = root;
        this.cfg = cfg;
        chunkQuads = Mathf.Clamp(terrainQuads, 4, 128);
        northCrevice = buildStationLoop && labEntrance == LabEntrance.Buried;
        Contract = SamplingContract.From(cfg, terrainQuads);
        // S1 §12 (P0-6): v2 объявлен только на профиле Ø2000/48. На прочих разрешениях
        // поле не запрещено математически, но не объявлено — говорим вслух, а не молчим.
        if (cfg.WorldVersion >= 2 && !SamplingContract.BandProfileSupported(cfg.mapDiameter, terrainQuads))
            Debug.LogWarning(
                $"[Forest] поле v2 при Ø{cfg.mapDiameter} и terrainQuads={terrainQuads}: " +
                $"объявленный профиль — Ø{SamplingContract.BandProfileDiameter}, " +
                $"≥{SamplingContract.BandProfileMinQuads} квадов; λ_min={Contract.lambdaMin:F1} " +
                $"против 4·meshStep={4f * Contract.meshStep:F1}");
        Trace = new GenTrace();
        Trace.Add("height", 1, 1,
            $"worldVersion={cfg.WorldVersion} field={(cfg.WorldVersion >= 2 ? "bands" : "v1")} " +
            $"lambdaMin={Contract.lambdaMin:F1} meshStep={Contract.meshStep:F2}");
        Trace.Add("classes", 0, 0,
            $"walk<={Contract.walkMaxDegrees:F0} climb<={Contract.wallMinDegrees:F0} eps={Contract.classEpsilon:F1}");
        Hydro = DomeHydro.Build(cfg, 256);
        Trace.Add("hydro", 1, Hydro != null ? 1 : 0);
        Terrain = new DomeTerrain
        {
            cfg = cfg,
            hydro = Hydro,
            worldVersion = cfg.WorldVersion,
            terrace = default,
            canyon = default,
            ravines = new DomeRavines.Ravine[0],
            relief = null,
            hill = default,
            entrance = labEntrance == LabEntrance.Auto ? LabEntrance.Rock : labEntrance,
            labCenter = Vector2.zero,
        };
        Trace.Add("order", 1, 1, Terrain.TargetOrder
            ? "v2: hydro→terrace→canyon→ravines→landform→hill→ShapeGround→pad→SampleGround"
            : "v1: исторический порядок (pad до landform) — A/C/B0/Pavilion бит-в-бит");

        // 1) ВХОД — ДО лаборатории. Ангар (его угол задан сидом) показывает, с какой стороны
        //    света вход; сама точка — у подножия кольца внутри чаши. Ни площадки, ни корпусов
        //    для этого не нужно, поэтому лабораторию можно ставить «от входа», а не наоборот.
        Vector2 entryXZ = EntryPointXZ(seed);
        ResolvedLabEntrance = labEntrance == LabEntrance.Auto ? LabEntrance.Rock : labEntrance;

        // 2) Место лаборатории: B0.1 — поиск по сиду; A/C — двор в центре мира.
        //    Ищем на Hydro0 = H0 + hydro (DomeHydro.SampleHydro), а НЕ на финальном
        //    ShapeGround: финальный пол зависит от маски станции. Terrace/canyon/ravines
        //    получают уже найденный центр как keep-out и в её пятно не входят — цикла нет.
        float minDistance = minLabDistance > 0f ? minLabDistance : DefaultMinLabDistance;
        var site = default(DomeLabSite.Spot);
        if (buildLabComplex)
        {
            site = DomeLabSite.Find(cfg, seed, entryXZ, Hydro, labStyle, minDistance,
                ResolvedLabEntrance, preferBest: buildStationLoop, lattice: buildStationLoop);
            if (!site.found)
            {
                Debug.LogError($"[Forest] лаборатории нет места: {site.note} — остаётся старый лабиринт");
                buildLabComplex = false;
            }
        }

        if (buildLabComplex)
        {
            Trace.Add("site", 1, 1, $"Hydro0: {site.note}");
            LabCenter = site.center;
            ResolvedLabStyle = site.style;
            LabWetApproach = site.wetApproach;
            padYaw = site.yawDeg;
            padRect = true;
            // Плита стоит на верхней точке пятна (поиск её и вернул): не висит и не режет склон.
            platformY = Mathf.Max(site.groundY, Hydro.lakeLevel + 0.5f);
            // Подземный вход: пол ниже подхода на перепад рампы; земля над ним — позже.
            if (ResolvedLabEntrance == LabEntrance.Underground ||
                ResolvedLabEntrance == LabEntrance.Buried)
                platformY = Mathf.Max(site.groundY - DomeLabComplex.DepthFor(ResolvedLabEntrance),
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

        // Маски габаритов (S3 §3): известны ДО формы — их читает сама форма.
        // Только для v2: v1 обязан остаться бит-в-бит (центровые проверки как были).
        masks = Terrain.TargetOrder
            ? BuildMasks(cfg, seed, Hydro, LabCenter, padYaw, padRect, ResolvedLabEntrance,
                entryXZ, site.found ? site.door : Vector3.zero, northCrevice)
            : null;
        if (buildTerrace)
            Terrace = DomeTerrace.Build(cfg, seed, LabCenter, entryXZ, Hydro,
                DomeRockRing.InnerRadius(cfg), masks);
        if (buildCanyon)
            Canyon = DomeCanyon.Build(cfg, seed, Hydro, entryXZ, LabCenter,
                DomeRockRing.InnerRadius(cfg), Terrace, masks);
        if (buildRavines)
            Ravines = DomeRavines.Build(cfg, seed, LabCenter, entryXZ, Hydro,
                DomeRockRing.InnerRadius(cfg), 3, masks);
        Trace.Add("terrace", 1, Terrace.valid ? 1 : 0);
        Trace.Add("canyon", 1, Canyon.valid ? 1 : 0);
        Trace.Add("ravines", Ravines.Count, Ravines.Count);

        // Слои формы — в одном месте (DomeTerrain). Сначала заполняем то, что уже есть.
        Terrain.terrace = Terrace;
        Terrain.canyon = Canyon;
        Terrain.ravines = Ravines.ToArray();
        Terrain.labCenter = LabCenter;
        Terrain.padYaw = padYaw;
        Terrain.padRect = padRect;
        Terrain.entrance = ResolvedLabEntrance;
        Terrain.northCrevice = northCrevice;

        if (Terrain.TargetOrder)
        {
            // Построенные формы — тоже keep-out для зон: иначе зона встанет на террасу.
            masks = AddFormsToMasks(masks, Terrace, Canyon, Ravines);

            // S2 §7: landform НЕ получает открытые зоны на вход — он знает только
            // станцию/вход/воду/кольцо/маршрут/ворота. Поэтому композиция строится
            // ДО предварительных зон, а не после них.
            if (cfg.ReliefEnabled)
            {
                Vector3 door = site.found ? site.door
                    : new Vector3(LabCenter.x, 0f, LabCenter.y);
                // Реальные якоря S2 §1.1: предварительные ясень/логова считаются
                // ДО формы (на том же предварительном поле), иначе форма их накроет.
                Vector2? ashAnchor = null;
                try
                {
                    bool standLab = LabCenter.sqrMagnitude > 0.01f;
                    float ashScale = standLab ? DomeAshSite.ScaleFor(cfg) : 1f;
                    var ashSpot = DomeAshSite.FindSpot(cfg, seed, LabCenter, Hydro, 96,
                        ashScale, standLab, null, (x, z) => Terrain.Shape0(x, z), masks);
                    ashAnchor = ashSpot.pos;
                }
                catch { /* ясеня нет — форма его не ждёт */ }
                var lairAnchors = new List<Vector2>();
                var preMouths = DomeSlope.FindMouths(cfg, 3, 96, RingGround);
                foreach (var m in preMouths) lairAnchors.Add(m.pos);
                foreach (var n in DomeSlope.FindNests(cfg, seed, preMouths, 2, RingGround))
                    lairAnchors.Add(n.pos);

                Composition = DomeComposition.Build(cfg, seed, entryXZ, door, LabCenter,
                    Hydro, masks, (x, z) => Terrain.Shape0(x, z), ashAnchor, lairAnchors);
                string compSkips = "";
                foreach (var p in Composition.declared)
                    if (!p.applied) compSkips += $"{p.name}: {p.note}; ";
                Trace.Add("composition", DomeComposition.DeclaredRoles,
                    Composition.forms.Count, Composition.note + " | " + compSkips);
                Plan = DomeComposition.ToPlan(Composition, cfg, terrainQuads,
                    buildTerrace, buildCanyon, buildAsh, buildLabComplex);
                Trace.Add("plan", Plan.nodes.Count, Plan.forms.Count,
                    $"{Plan.path}; identity={Plan.identity}; signature={Plan.signature}");
            }
            Terrain.composition = Composition;
            Terrain.relief = Relief; // v2: композиция вместо P5-lite; Relief остаётся v1

            // Формы композиции — keep-out для зон/связей/общих (две стороны §3.2):
            // место выбирается на ShapeGround и не встаёт на форму.
            if (Composition != null)
                foreach (var f in Composition.forms)
                    masks.Add("landform", DomeMasks.ToFootprint(f));

            // S3 §3.2, фаза 1: предварительные якоря зон/связей известны ДО hill —
            // из них строятся маски `zone`/`corridor`/`common`, и форма в них не входит.
            // ФИНАЛЬНЫЕ зоны считаются позже, на ShapeGround (после hill и слота эрозии):
            // приёмка места не имеет права смотреть на поле, которого не будет.
            if (cfg.patchyFlora)
            {
                Func<float, float, float> preForm = (x, z) => Terrain.Shape0(x, z);
                var preZones = ForestSpace.BuildZones(cfg, seed, preForm, Entry, LabCenter,
                    Hydro, masks, 5);
                var preLinks = ForestSpace.BuildLinks(cfg, preZones, preForm, masks, 16f);
                var preWater = ForestSpace.BuildWaterholes(cfg, Hydro, preForm, masks, seed, 2);
                var preCommons = ForestSpace.BuildCommons(cfg, preLinks, preZones, preForm,
                    Hydro, masks, seed, 4);
                foreach (var z in preZones)
                    masks.Add("zone", DomeFootprint.Circle(z.center, z.radius + 20f));
                foreach (var l in preLinks)
                    masks.Add("corridor", DomeFootprint.Capsule(l.a, l.b, 14f));
                foreach (var c in preCommons)
                    masks.Add("common", DomeFootprint.Circle(c.center, c.radius + 2f));
                foreach (var w in preWater)
                    masks.Add("common", DomeFootprint.Circle(w.center, w.radius + 2f));
                Trace.Add("zones.pre", preZones.Count, preZones.Count,
                    $"связей {preLinks.Count}, общих {preCommons.Count}, водопоев {preWater.Count} " +
                    "(маски для hill; финальные — на ShapeGround)");
            }

            if (buildStationHill)
            {
                float yaw = padYaw * Mathf.Deg2Rad;
                Vector2 north = LabCenter + new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw)) *
                    DomeLabComplex.HalfDepth;
                // S3 §3.2: холм уважает проходы маски станции (рампа/щель/подход),
                // но НЕ плиту: плато обязано поднять пятно, иначе platformY — яма.
                Func<float, float, bool> hillHole = masks == null
                    ? (Func<float, float, bool>)null
                    : (x, z) => masks.ContainsPoint(new Vector2(x, z),
                            "ramp", "crevice", "approach", "zone", "common",
                            "water", "corridor", "gate") ||
                        masks.OverlapsRing(DomeFootprint.Circle(new Vector2(x, z), 0f));
                Hill = DomeHill.Build(cfg, seed, LabCenter, north, 9f, hillHole);
            }
            Terrain.hill = Hill;

            // S4: слот [erosion] — гидро(Shape0) → тепловая(hydro). Вход строго Shape0,
            // маска — только формы ДО эрозии (DomeMasks.ErosionKeeps) + кольцо как домен.
            if (cfg.buildThermalErosion || cfg.buildHydraulicErosion)
            {
                Func<float, float, bool> erosionMask = (x, z) =>
                {
                    if (new Vector2(x, z).magnitude > DomeRockRing.InnerRadius(cfg)) return true;
                    if (masks != null)
                        foreach (var name in DomeMasks.ErosionKeeps)
                            if (masks.ContainsPoint(new Vector2(x, z), name)) return true;
                    return DomeHill.Contains(Hill, x, z, 0f);
                };
                Erosion = DomeErosion.Build(cfg, (x, z) => Terrain.Shape0(x, z),
                    erosionMask, seed);
                Terrain.erosion = Erosion;
                Trace.Add("erosion", 1, 1,
                    $"hydro={Erosion.hydraulicStatus} t={Erosion.hydraulicSeconds:F2}s " +
                    $"thermal={cfg.buildThermalErosion} res={Erosion.resolution} " +
                    $"sum={Erosion.deltaSum:x16}");
            }

            // platformY — ВЫХОД формы, а не вход: читаем финальный ShapeGround в центре
            // уже найденного пятна. Площадка ниже применится последним blend'ом.
            if (buildLabComplex)
            {
                float shape = Terrain.ShapeGround(LabCenter.x, LabCenter.y);
                platformY = Mathf.Max(
                    shape - (ResolvedLabEntrance == LabEntrance.Underground ||
                             ResolvedLabEntrance == LabEntrance.Buried
                        ? DomeLabComplex.DepthFor(ResolvedLabEntrance)
                        : 0f),
                    Hydro.lakeLevel + 0.5f);
                Trace.Add("platform", 1, 1,
                    $"ShapeGround({LabCenter.x:F0};{LabCenter.y:F0})={shape:F2} → platformY={platformY:F2}");
            }
        }
        else
        {
            // v1: историческая сверка — без холма и без P5-lite, плита поднимается
            // до TerrainGround. Это НЕ цель, а замороженное поведение A/C/B0/Pavilion.
            if (buildLabComplex)
            {
                float finalGround = TerrainGround(LabCenter.x, LabCenter.y);
                if (Mathf.Abs(finalGround - site.groundY) > 0.5f)
                    Debug.LogWarning($"[Forest] место станции: сырое поле {site.groundY:F1} м, " +
                                     $"финальное {finalGround:F1} м — площадка скорректирована");
                if (ResolvedLabEntrance == LabEntrance.Underground ||
                    ResolvedLabEntrance == LabEntrance.Buried)
                    platformY = Mathf.Max(finalGround - DomeLabComplex.DepthFor(ResolvedLabEntrance),
                        Hydro.lakeLevel + 0.5f);
                else
                    platformY = Mathf.Max(finalGround, Hydro.lakeLevel + 0.5f);
            }

            if (buildStationHill)
            {
                float yaw = padYaw * Mathf.Deg2Rad;
                Vector2 north = LabCenter + new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw)) *
                    DomeLabComplex.HalfDepth;
                Hill = DomeHill.Build(cfg, seed, LabCenter, north, 9f);
            }
            Terrain.hill = Hill;

            if (cfg.ReliefEnabled)
                Relief = DomeRelief.Build(cfg, seed, LabCenter, entryXZ, Hydro, Terrace, Canyon, Ravines);
            Terrain.relief = Relief;
        }

        Terrain.platformY = platformY;
        Terrain.padEnabled = true;
        if (!Terrain.TargetOrder) EnsureChunks();
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
        if (buildStationHill) BuildStationHill();

        // SpawnPoint — двор старого лабиринта или закрытый зал комплекса. У комплекса
        // верх плиты на Plinth выше площадки: игрок встаёт на плиту, а не в её толщу.
        float spawnGround = SampleGround(LabCenter.x, LabCenter.y);
        if (LabComplex != null)
            spawnGround = Mathf.Max(spawnGround, platformY + DomeLabComplex.Plinth);
        SpawnPoint = new Vector3(LabCenter.x, spawnGround + 1f, LabCenter.y);

        BuildEntry(buildEntry, entryXZ);
        // B-lite — только при patchyFlora. Раньше скобки отсутствовали, и коридоры с
        // подами строились ВСЕГДА, даже когда разметка полян выключена (A/C).
        bool target = Terrain.TargetOrder;
        // v2: пады коридоров добавляются ПОСЛЕ зон/связей (S2 §1.1), то есть меняют
        // ShapeGround/SampleGround. Меши чанков и NavMesh обязаны читать уже финальный
        // пол, иначе G0 видит на 1.4 м ниже поле до пада. v1 строит чанки на прежнем
        // месте — A/C/B0/Pavilion остаются бит-в-бит.
        void EnsureChunks()
        {
            if (Chunks != 0) return;
            BuildChunks(terrainQuads);
            Trace.Add("chunks", Chunks, Chunks);
        }
        // P0.2: зоны, связи, поды и POI читают ОДИН пол прохода — ShapeGround.
        // v1 (A/C/B0/Pavilion) остаётся на историческом BaseGround/SampleGround
        // бит-в-бит; второй правды для v2 нет.
        Func<float, float, float> zoneGround = target
            ? (Func<float, float, float>)ShapeGround
            : Terrain.BaseGround;
        if (cfg.patchyFlora)
        {
            if (!target)
            {
                Zones = ForestSpace.BuildZonesLegacy(cfg, seed, zoneGround, Entry, LabCenter, Hydro);
                Links = ForestSpace.BuildLinksLegacy(Zones, 16f);
            }
            else
            {
                // ФАЗА 2 (P0.1): финальные зоны/связи/общие/водопои считаются на
                // ShapeGround — после landform, hill и слота эрозии S4. Предварительные
                // якоря (фаза 1) уже вырезали эти места из формы, поэтому приёмка видит
                // тот пол, который будет построен, и эродированный пол не меняет её.
                Func<float, float, float> finalGround = ShapeGround;
                Zones = ForestSpace.BuildZones(cfg, seed, finalGround, Entry, LabCenter,
                    Hydro, masks, 5);
                Links = ForestSpace.BuildLinks(cfg, Zones, finalGround, masks, 16f);

                // S2 §1.1: места нет — S3 кладёт локальный пад коридора (не второй пол:
                // полоса входит в ShapeGround). Пад только для пар, заблокированных
                // УКЛОНОМ; водa/кольцо/станция — по-прежнему «нельзя».
                var padded = new HashSet<long>();
                for (int pass = 0; pass < 6; pass++)
                {
                    if (Links.Count >= 6 && MinExits(Zones, Links) >= 2) break;
                    int bi = -1, bj = -1; float bd = float.MaxValue;
                    for (int i = 0; i < Zones.Count; i++)
                        for (int j = i + 1; j < Zones.Count; j++)
                        {
                            if (HasLink(Links, Zones[i].center, Zones[j].center)) continue;
                            long key = (long)i * 1000 + j;
                            if (padded.Contains(key)) continue;
                            string why = ForestSpace.LinkReason(cfg, finalGround, masks,
                                Zones[i].center, Zones[j].center, 16f,
                                Zones[i].radius + 40f, Zones[j].radius + 40f);
                            if (why == null || why.StartsWith("mask")) continue;
                            float d = Vector2.Distance(Zones[i].center, Zones[j].center);
                            if (d < bd) { bd = d; bi = i; bj = j; }
                        }
                    if (bi < 0) break;
                    Vector2 pa = Zones[bi].center, pb = Zones[bj].center;
                    Vector2 dir = (pb - pa).normalized;
                    Vector2 ea = pa + dir * (Zones[bi].radius + 40f);
                    Vector2 eb = pb - dir * (Zones[bj].radius + 40f);
                    if (Vector2.Distance(ea, eb) < 20f) { padded.Add((long)bi * 1000 + bj); continue; }
                    if (!PadClear(Zones, bi, bj, ea, eb, masks))
                    { padded.Add((long)bi * 1000 + bj); continue; }
                    float y = 0.5f * (ShapeGround(ea.x, ea.y) + ShapeGround(eb.x, eb.y));
                    Terrain.AddCorridorPad(ea, eb, 20f, y, 10f);
                    padded.Add((long)bi * 1000 + bj);
                    Links = ForestSpace.BuildLinks(cfg, Zones, finalGround, masks, 16f);
                    Trace.Add("link.pad", 1, 1,
                        $"{bi}-{bj}: насыпь коридора 16 м, y={y:F1}");
                }

                EnsureChunks();
                Waterholes = ForestSpace.BuildWaterholes(cfg, Hydro, finalGround, masks, seed, 2);
                Commons = ForestSpace.BuildCommons(cfg, Links, Zones, finalGround, Hydro,
                    masks, seed, 4);

                // «2 водопоя — из общих зон» (S3 §4): если полок у воды нашлось меньше,
                // роль водопоя берут ближайшие общие зоны. Это адрес, а не второй счёт.
                if (Waterholes.Count < 2 && Commons.Count > 0)
                {
                    var commons = new List<ForestSpace.Zone>(Commons);
                    var water = new List<ForestSpace.Zone>(Waterholes);
                    while (water.Count < 2 && commons.Count > 0)
                    {
                        int best = 0; float bestD = float.MaxValue;
                        for (int ci = 0; ci < commons.Count; ci++)
                        {
                            float d = DistanceToWater(Hydro, commons[ci].center);
                            if (d < bestD) { bestD = d; best = ci; }
                        }
                        water.Add(commons[best]);
                        commons.RemoveAt(best);
                    }
                    Waterholes = water; // ������� � ���� ����� ����, ���� ���� ������� � Commons
                }
                Trace.Add("zones", Zones.Count, Zones.Count,
                    $"ShapeGround: связей {Links.Count}, общих {Commons.Count}, " +
                    $"водопоев {Waterholes.Count}; план 5 (4–6) — на S1-поле без " +
                    "композиции S2 меньше, долг S2");

                Gates = DomeGate.Anchors(cfg, seed);
                DomeGate.Build(Root, Gates, zoneGround,
                    Flat(DomePalette.ConcreteDark), Flat(DomePalette.Bark), meshes);

                // Остовы вместо кромок-арен: 2 пятна у кромки зон (S3 §4.2).
                Shells = DomeShell.Build(Root, Zones, Links, zoneGround,
                    Flat(DomePalette.ConcreteDark), Flat(DomePalette.Rock), seed, meshes, 2);
                BuildPoisV2(zoneGround);
                Roads = ForestRoads.Build(cfg, Pois.All, ShapeGround, Hydro, masks, seed);
                var roadMesh = DomeRoadMesh.Build(Roads, RenderedGround);
                Track(roadMesh);
                Part("ForestRoads", roadMesh, Flat(DomePalette.Path));
                Trace.Add("roads", Pois.All.Count, Roads.roads.Count,
                    $"{Roads.note}; длина {Roads.length:F0} м, петель {Roads.loops}");
            }

            if (target) EnsureChunks();
            Func<float, float, float> podGround = target
                ? (Func<float, float, float>)ShapeGround
                : SampleGround;
            Func<float, float, bool> podExclude = (x, z) =>
                (LabComplex != null && InsideLabPad(x, z, 3.5f)) ||
                InsideRamp(x, z) ||
                Vector2.Distance(new Vector2(x, z), new Vector2(Entry.x, Entry.z)) < 12f ||
                (LabComplex != null &&
                 Vector2.Distance(new Vector2(x, z), new Vector2(LabDoor.x, LabDoor.z)) < 14f);
            if (target && masks != null)
            {
                var baseExclude = podExclude;
                podExclude = (x, z) => baseExclude(x, z) ||
                    masks.OverlapsRing(DomeFootprint.Circle(new Vector2(x, z), 0f)) ||
                    masks.ContainsPoint(new Vector2(x, z), "station", "gate");
            }
            // Поды не ставятся на плиту станции, у входа и у двери: укрытие в стене
            // бункера — не укрытие, а дырка в геометрии.
            CoverPods = ForestSpace.BuildCoverPods(Zones, Links, seed, podGround,
                Hydro.lakeLevel, 24, podExclude);
        }

        // Вход — не чаща: вокруг Entry оставляем поляну R12, иначе игрок рождается
        // в стволе и не видит ни входа, ни леса. Пятно комплекса тоже не засаживаем:
        // дерево в стене или на крыше — не «густой лес», а дырка в бункере.
        Func<float, float, bool> labPad = (x, z) => LabComplex != null && InsideLabPad(x, z, 0f);
        Func<float, float, bool> clearing = (x, z) =>
        {
            if (Vector2.Distance(new Vector2(x, z), new Vector2(Entry.x, Entry.z)) < 12f)
                return true;
            if (DomeRavines.Contains(Ravines, x, z, 1.2f)) return true;
            // v2: 0 стволов в R+20 от зоны (S2 З2); v1 — исторический запас 1 м,
            // иначе поехали бы деревья B-lite и замороженный эталон Pavilion.
            if (target ? ForestSpace.Contains(Zones, x, z, 20f)
                       : ForestSpace.Contains(Zones, x, z, 1f)) return true;
            // v2: полоса связи 28 м (16 + 2×6), значит запас 6 м к полуширине 8.
            if (ForestSpace.ContainsLink(Links, x, z, target ? 6f : 1f)) return true;
            if (target && ForestRoads.Near(Roads, x, z, 1.5f)) return true;
            if (target && Hydro != null)
            {
                // M14: 0 стволов на воде; Patches режет R+2, значит флора тоже.
                if (Vector2.Distance(new Vector2(x, z), Hydro.lakeCenter) <
                    Hydro.lakeRadius + 2f) return true;
                for (int i = 1; i < Hydro.riverPts.Count; i++)
                    if (DomeFootprint.DistanceToSegment(new Vector2(x, z),
                            Hydro.riverPts[i - 1], Hydro.riverPts[i]) <
                        Hydro.riverHalfWidth + 2f) return true;
            }
            if (ForestSpace.NearPods(CoverPods, x, z, 3f)) return true;
            if (target)
            {
                if (NearPoi(Gates, x, z, 8f)) return true;
                if (NearShells(Shells, x, z, 6f)) return true;
            }
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
        Trees = DomeFlora.Build(Root, cfg, Hydro, seed, SampleGround, clearing, undergrowthExclude, out var flora,
            (x, z) => ForestSpace.Near(Zones, Links, x, z, 12f), CoverPods,
            (x, z) => ForestSpace.Near(Zones, Links, x, z, 20f));
        TreeColliders = flora.treeColliders;
        ForestAreaKm2 = flora.forestAreaKm2;
        DeadLogs = flora.deadLogs;
        CoverPodsBuilt = flora.coverPods;
        // P0.4: лента дорожек v1 обязана читать SampleGround (как исторически),
        // а не BaseGround: BaseGround — внутренний шаг формы, наружу он не выходит.
        BuildSpacePaths(target ? (Func<float, float, float>)ShapeGround : SampleGround);
        if (!target)
        {
            // v1: исторические кромки-арены. На v2 их нет — вместо них остовы.
            ArenaEdges = DomeArenaEdge.Build(Root, Zones, Links, SampleGround,
                Flat(DomePalette.Rock), Flat(DomePalette.Bark), seed);
        }
        TreeColliderPositions = flora.treeColliderPositions ?? new List<Vector3>();
        CrownCover = flora.crownCover;
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
    /// Сырое поле (Hydro0 = H0 + hydro) — только диагностика. Единственный пол мира —
    /// <see cref="SampleGround"/> (S3 §2.4): отдельной правды «BaseGround» наружу нет.
    /// </summary>
    public float RawGround(float x, float z) => Terrain != null ? Terrain.Hydro0(x, z) : 0f;

    /// <summary>
    /// ТОЛЬКО v1: историческая сверка места станции — поле без холма и без landform.
    /// В целевом порядке (v2) её нет: <c>platformY</c> читается из
    /// <see cref="DomeTerrain.ShapeGround"/> (S3 §4.1), цикла не остаётся.
    /// </summary>
    float TerrainGround(float x, float z)
    {
        float raw = RawGround(x, z);
        raw = DomeTerrace.Apply(raw, Terrace, x, z);
        raw = DomeCanyon.Apply(raw, Canyon, x, z, Hydro.lakeLevel);
        raw = DomeRavines.Apply(raw, Ravines, x, z, Hydro.lakeLevel);
        return raw;
    }

    public float RingGround(float x, float z) =>
        DomeSlope.RingField(cfg, x, z, SampleGround);

    /// <summary>Единственная публичная высота для потребителей (S3 §2.4).</summary>
    public float SampleGround(float x, float z) =>
        Terrain != null ? Terrain.SampleGround(x, z) : 0f;

    /// <summary><c>ShapeGround</c> — форма без площадки; вход POI и эрозии S4.</summary>
    public float ShapeGround(float x, float z) =>
        Terrain != null ? Terrain.ShapeGround(x, z) : 0f;

    /// <summary><c>Shape0 = PreErosion</c> — форма до эрозии (сейчас тождество ShapeGround).</summary>
    public float Shape0(float x, float z) =>
        Terrain != null ? Terrain.Shape0(x, z) : 0f;

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
        if (!padRect || (ResolvedLabEntrance != LabEntrance.Underground &&
                         ResolvedLabEntrance != LabEntrance.Buried)) return false;
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

    /// <summary>
    /// S3 §3.2: маски, известные ДО формы. Станция — OBB плиты + PadBlend, вход — R12 и
    /// портал, рампа — OBB спуска, вода — озеро и капсулы русла с берегом 2 м, главный
    /// маршрут — R30 Entry→LabDoor, кольцо — полоса [rin−30, rout]. Только v2: v1 обязан
    /// остаться на центровых проверках, иначе A/C/B0/Pavilion сдвинут высоты.
    /// </summary>
    DomeMasks BuildMasks(DomeGenConfigSO cfg, long seed, DomeHydro.State hydro,
        Vector2 labCenter, float padYawDeg, bool padRect, LabEntrance entrance,
        Vector2 entry, Vector3 door, bool northCrevice)
    {
        var set = new DomeMasks();
        float rin = DomeRockRing.InnerRadius(cfg);
        set.RingBand(rin - 30f, DomeRockRing.OuterRadius(cfg));

        // Маска станции = плита + PadBlend + рампа + расщелина + подход 16/24 м
        // (S3 §3.2). Все части — отдельные записи с одним именем «station»:
        // форма обязана уважать каждую, а hill найдёт по ним свои вырезы.
        if (padRect)
            set.Add("station", DomeFootprint.Obb(labCenter, padYawDeg,
                DomeLabComplex.PadHalfWidth + DomeLabComplex.PadBlend,
                DomeLabComplex.PadHalfDepth + DomeLabComplex.PadBlend));
        else
            set.Add("station", DomeFootprint.Circle(labCenter, LabRadius + DomeLabComplex.PadBlend));

        float inwardYaw = entry.sqrMagnitude > 1e-6f
            ? Mathf.Atan2(-entry.x, -entry.y) * Mathf.Rad2Deg
            : 0f;
        set.Add("entry", DomeFootprint.Circle(entry, 12f));
        set.Add("entry", DomeFootprint.Obb(entry, inwardYaw, 4.2f, 1.1f));

        if (padRect && (entrance == LabEntrance.Underground || entrance == LabEntrance.Buried))
        {
            var rampCenter = PadLocal(labCenter, padYawDeg,
                new Vector2(0f, -DomeLabComplex.HalfDepth - DomeLabComplex.RampLength * 0.5f));
            var ramp = DomeFootprint.Obb(rampCenter, padYawDeg,
                DomeLabComplex.RampWidth * 0.5f + 1.5f, DomeLabComplex.RampLength * 0.5f + 1.5f);
            set.Add("ramp", ramp);
            set.Add("station", ramp);
        }

        if (padRect && northCrevice)
        {
            var creviceCenter = PadLocal(labCenter, padYawDeg,
                new Vector2(0f, DomeLabComplex.HalfDepth + DomeLabComplex.CreviceLength * 0.5f));
            var crevice = DomeFootprint.Obb(creviceCenter, padYawDeg,
                DomeLabComplex.CreviceWidth * 0.5f + 2f,
                DomeLabComplex.CreviceLength * 0.5f + 1.5f);
            set.Add("crevice", crevice);
            set.Add("station", crevice);
        }

        // Подход перед дверью: 16 м у скального входа, 24 м у подземного.
        if (door.sqrMagnitude > 0.01f)
        {
            Vector2 doorXZ = new Vector2(door.x, door.z);
            Vector2 outward = doorXZ - labCenter;
            if (outward.sqrMagnitude > 1e-6f)
            {
                outward.Normalize();
                float len = (entrance == LabEntrance.Underground ||
                             entrance == LabEntrance.Buried) ? 24f : 16f;
                var approach = DomeFootprint.Obb(doorXZ + outward * (len * 0.5f), padYawDeg,
                    4f, len * 0.5f);
                set.Add("approach", approach);
                set.Add("station", approach);
            }
            set.Add("mainRoute", DomeFootprint.Capsule(entry, doorXZ, 30f));
        }

        // Ворота уровня — 2 якоря ДО формы (S3 §3.2): landform их не задевает.
        foreach (var gate in DomeGate.Anchors(cfg, seed))
            set.Add("gate", DomeFootprint.Circle(gate.center, 8f));

        // Коридоры связей — 28 м полоса (16 + 2×6 плеч): якоря зон известны заранее,
        // поэтому landform уважает и их, а не только финальную раскладку.
        var anchors = ForestSpace.AnchorZones(cfg, seed, 5);
        foreach (var link in ForestSpace.BuildLinksLegacy(anchors, 28f))
            set.Add("corridor", DomeFootprint.Capsule(link.a, link.b, 14f));

        if (hydro != null)
        {
            if (hydro.lakeRadius > 0f)
                set.Add("water", DomeFootprint.Circle(hydro.lakeCenter, hydro.lakeRadius + 2f));
            for (int i = 1; i < hydro.riverPts.Count; i++)
                set.Add("water", DomeFootprint.Capsule(hydro.riverPts[i - 1], hydro.riverPts[i],
                    hydro.riverHalfWidth + 2f));
        }

        return set;
    }

    /// <summary>Добавить уже построенные формы как keep-out для landform (терраса/каньон/овраги).</summary>
    static DomeMasks AddFormsToMasks(DomeMasks set, DomeTerrace.State terrace,
        DomeCanyon.State canyon, IReadOnlyList<DomeRavines.Ravine> ravines)
    {
        if (set == null) return null;
        if (terrace.valid)
            set.Add("terrace", DomeFootprint.Circle(terrace.center, terrace.radius + terrace.blend));
        if (canyon.valid && canyon.axis != null && canyon.axis.Count >= 2)
            set.Add("canyon", DomeFootprint.Capsule(canyon.axis[0],
                canyon.axis[canyon.axis.Count - 1], canyon.halfWidth + canyon.rim));
        if (ravines != null)
            foreach (var r in ravines)
            {
                float yaw = r.angleDeg * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(yaw), Mathf.Sin(yaw));
                float half = Mathf.Max(0f, r.length * 0.5f - r.width);
                set.Add("ravine", DomeFootprint.Capsule(r.center - dir * half,
                    r.center + dir * half, r.width + r.rim));
            }
        return set;
    }

    /// <summary>
    /// S3 §4: реестр адресов v0. Все места — на финальном ShapeGround; исключения
    /// названы в спеке: base — Hydro0/LabDoor, entry — высота SampleGround,
    /// ясень/логова — свои полы (RingGround/крона).
    /// </summary>
    void BuildPoisV2(Func<float, float, float> ground)
    {
        Pois.Clear();
        Roads = null;
        if (LabComplex != null)
            Pois.Add(ForestPoiKind.Base, "base", new Vector2(LabDoor.x, LabDoor.z),
                LabRadius, padYaw, LabDoor.y);
        else
            Pois.Add(ForestPoiKind.Base, "base", LabCenter, LabRadius, 0f,
                ground(LabCenter.x, LabCenter.y));

        Pois.Add(ForestPoiKind.Entry, "entry", new Vector2(Entry.x, Entry.z), 12f, 0f,
            SampleGround(Entry.x, Entry.z));

        foreach (var gate in Gates)
            Pois.Add(ForestPoiKind.Gate, "gate_" + gate.index, gate.center, 8f, gate.yawDeg,
                ground(gate.center.x, gate.center.y), gate.index);

        foreach (var zone in Zones)
            Pois.Add(ForestPoiKind.Zone, "zone", zone.center, zone.radius, zone.yawDeg,
                ground(zone.center.x, zone.center.y));

        foreach (var shell in Shells)
            Pois.Add(ForestPoiKind.Shell, "shell", shell.center,
                Mathf.Max(shell.halfWidth, shell.halfDepth), shell.yawDeg, shell.groundY,
                shell.zone);

        foreach (var c in Commons)
        {
            bool water = false;
            foreach (var w in Waterholes)
                if (Vector2.Distance(c.center, w.center) < 0.5f) { water = true; break; }
            Pois.Add(water ? ForestPoiKind.Waterhole : ForestPoiKind.Common,
                water ? "waterhole" : "common", c.center, c.radius, c.yawDeg,
                ground(c.center.x, c.center.y));
        }

        foreach (var link in Links)
        {
            Vector2 mid = (link.a + link.b) * 0.5f;
            float yaw = Mathf.Atan2(link.b.x - link.a.x, link.b.y - link.a.y) * Mathf.Rad2Deg;
            Pois.Add(ForestPoiKind.Link, "link", mid,
                Vector2.Distance(link.a, link.b) * 0.5f, yaw, ground(mid.x, mid.y));
        }

        if (HasAsh)
            Pois.Add(ForestPoiKind.Ash, "ash", Ash.pos,
                DomeAshSite.CrownDiameter * 0.5f * DomeAshSite.ScaleFor(cfg) + 5f, 0f,
                Ash.groundY);

        foreach (var mouth in Mouths)
            Pois.Add(ForestPoiKind.Lair, "lair_mouth", mouth.pos, 25f, mouth.facing,
                RingGround(mouth.pos.x, mouth.pos.y));
        foreach (var nest in Nests)
            Pois.Add(ForestPoiKind.Lair, "lair_nest", nest.pos, 25f, 0f,
                RingGround(nest.pos.x, nest.pos.y));
    }

    static int MinExits(IReadOnlyList<ForestSpace.Zone> zones,
        IReadOnlyList<ForestSpace.Link> links)
    {
        int min = int.MaxValue;
        for (int i = 0; i < zones.Count; i++)
            min = Mathf.Min(min, ForestSpace.Exits(zones, links, i));
        return zones.Count == 0 ? 0 : min;
    }

    /// <summary>Пад коридора не идёт через чужую зону и не лезет в воду/кольцо/станцию.</summary>
    static bool PadClear(IReadOnlyList<ForestSpace.Zone> zones, int i, int j,
        Vector2 a, Vector2 b, DomeMasks masks)
    {
        for (int k = 0; k < zones.Count; k++)
        {
            if (k == i || k == j) continue;
            float d = DomeFootprint.DistanceToSegment(zones[k].center, a, b);
            if (d < zones[k].radius + 20f + 10f) return false;
        }
        if (masks != null &&
            masks.BlocksForm(DomeFootprint.Capsule(a, b, 10f),
                "corridor", "zone", "common", "landform", "mainRoute", "gate"))
            return false;
        return true;
    }

    static bool HasLink(IReadOnlyList<ForestSpace.Link> links, Vector2 a, Vector2 b)
    {
        foreach (var l in links)
            if ((l.a - a).sqrMagnitude < 0.25f && (l.b - b).sqrMagnitude < 0.25f) return true;
        return false;
    }

    /// <summary>Расстояние до ближайшего уреза воды: озеро/русло. Для роли водопоя.</summary>
    static float DistanceToWater(DomeHydro.State hydro, Vector2 p)
    {
        if (hydro == null) return float.MaxValue;
        float d = Vector2.Distance(p, hydro.lakeCenter) - hydro.lakeRadius;
        foreach (var rp in hydro.riverPts)
            d = Mathf.Min(d, Vector2.Distance(p, rp) - hydro.riverHalfWidth);
        return d;
    }

    static bool NearPoi(IReadOnlyList<DomeGate.Spot> gates, float x, float z, float margin)
    {
        if (gates == null) return false;
        foreach (var g in gates)
            if (Vector2.Distance(g.center, new Vector2(x, z)) < 8f + margin) return true;
        return false;
    }

    static bool NearShells(IReadOnlyList<DomeShell.Spot> shells, float x, float z, float margin)
    {
        if (shells == null) return false;
        foreach (var s in shells)
            if (Vector2.Distance(s.center, new Vector2(x, z)) <
                Mathf.Max(s.halfWidth, s.halfDepth) + margin) return true;
        return false;
    }

    /// <summary>Локальный кадр плиты → координаты поля (совпадает с PadPoint в DomeLabSite).</summary>
    static Vector2 PadLocal(Vector2 center, float yawDeg, Vector2 local)
    {
        float yaw = yawDeg * Mathf.Deg2Rad;
        float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
        return new Vector2(center.x + local.x * c + local.y * s,
            center.y - local.x * s + local.y * c);
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
        surface.voxelSize = cfg.navVoxelSize > 0f ? cfg.navVoxelSize : 0.35f;
        surface.overrideTileSize = true;
        surface.tileSize = Mathf.RoundToInt(cfg.navTileSize > 0f ? cfg.navTileSize : 112f);
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
        Terrain = null;
        masks = null;
        Ash = default;
        HasAsh = false;
        Facility = null;
        LabComplex = null;
        LabPlan = null;
        LabCenter = Vector2.zero;
        LabDoor = default;
        LabAtrium = default;
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
        TreeColliders = 0;
        ForestAreaKm2 = 0f;
        DeadLogs = 0;
        CoverPodsBuilt = 0;
        ArenaEdges = 0;
        Relief = null;
        Composition = null;
        Plan = null;
        Erosion = null;
        TreeColliderPositions = new List<Vector3>();
        CrownCover = 0f;
        Zones = new List<ForestSpace.Zone>();
        Links = new List<ForestSpace.Link>();
        Commons = new List<ForestSpace.Zone>();
        Waterholes = new List<ForestSpace.Zone>();
        Shells = new List<DomeShell.Spot>();
        Gates = new List<DomeGate.Spot>();
        Pois.Clear();
        Roads = null;
        CoverPods = new List<ForestSpace.CoverPod>();
        Ravines = new List<DomeRavines.Ravine>();
        Terrace = default;
        Canyon = default;
        Hill = default;
        FallenTrees = 0;
        VisualNests = 0;
        platformY = 0f;
        chunkQuads = 0;
        Contract = default;
        Trace = null;
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

    void BuildSpacePaths(Func<float, float, float> ground)
    {
        if (Links == null || Links.Count == 0) return;
        // Дорожка не заходит в центр зоны, не ложится на плиту станции, не идёт через
        // вход и дверь: лента обрезается по этим адресам. Высота — нарисованный чанк.
        Func<float, float, bool> exclude = (x, z) =>
            (LabComplex != null && InsideLabPad(x, z, 3.5f)) ||
            InsideRamp(x, z) ||
            Vector2.Distance(new Vector2(x, z), new Vector2(Entry.x, Entry.z)) < 12f ||
            (LabComplex != null &&
             Vector2.Distance(new Vector2(x, z), new Vector2(LabDoor.x, LabDoor.z)) < 14f);
        var mesh = DomeSpacePaths.Build(Links, ground, 6f, Zones, exclude, RenderedGround);
        Track(mesh);
        Part("SpacePaths", mesh, Flat(DomePalette.Path));
    }

    /// <summary>
    /// Высота НАРИСОВАННОГО чанка, а не аналитического поля: чанк — крупная сетка,
    /// и между её вершинами плоскость выше/ниже <see cref="SampleGround"/>. Дорожка,
    /// посаженная на аналитику, «клюётся» террейном. Повторяем треугольники чанка
    /// (p00,p11,p10) и (p00,p01,p11) с той же диагональю — лента ложится ровно.
    /// </summary>
    float RenderedGround(float x, float z)
    {
        if (cfg == null) return 0f;
        float chunk = cfg.chunkSize;
        if (chunk <= 0f) return SampleGround(x, z);
        float half = cfg.mapDiameter * 0.5f;
        int cx = Mathf.FloorToInt((x + half) / chunk);
        int cz = Mathf.FloorToInt((z + half) / chunk);
        // Квадратные углы сетки за кольцом не строятся — там поверхности нет.
        float centerX = -half + (cx + 0.5f) * chunk;
        float centerZ = -half + (cz + 0.5f) * chunk;
        if (new Vector2(centerX, centerZ).magnitude > half) return SampleGround(x, z);

        float x0 = -half + cx * chunk;
        float z0 = -half + cz * chunk;
        int res = Mathf.Clamp(chunkQuads, 4, 128);
        float step = chunk / res;
        float fx = Mathf.Clamp((x - x0) / step, 0f, res - 1e-4f);
        float fz = Mathf.Clamp((z - z0) / step, 0f, res - 1e-4f);
        int ix = Mathf.FloorToInt(fx);
        int iz = Mathf.FloorToInt(fz);
        float u = fx - ix;
        float v = fz - iz;
        float xa = x0 + ix * step, xb = xa + step;
        float za = z0 + iz * step, zb = za + step;
        float h00 = SampleGround(xa, za);
        float h10 = SampleGround(xb, za);
        float h01 = SampleGround(xa, zb);
        float h11 = SampleGround(xb, zb);
        // Диагональ p00–p11: v ≤ u — нижний треугольник, иначе верхний.
        if (v <= u)
            return h00 + (h10 - h00) * u + (h11 - h10) * v;
        return h00 + (h01 - h00) * v + (h11 - h01) * u;
    }
    void BuildChunks(int terrainQuads)
    {
        var mat = Flat(DomePalette.Terrain);
        // S2 §4: материал по полосе уклона — только v2; v1 остаётся одним Terrain.
        Material[] slopeMats = Terrain.TargetOrder
            ? new[]
            {
                mat,
                Flat(DomePalette.Turf),
                Flat(DomePalette.Scree),
                Flat(DomePalette.Rock),
                Flat(DomePalette.Rock * 0.85f),
            }
            : null;
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
                var mesh = DomeChunkMesh.BuildChunkMesh(cfg, cx, cz, terrainQuads,
                    SampleGround, Terrain.TargetOrder, cfg.seed);
                Track(mesh);
                var go = Part($"Chunk_{cx}_{cz}", mesh, mat);
                if (slopeMats != null)
                    go.GetComponent<MeshRenderer>().sharedMaterials = slopeMats;
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
        if (masks != null)
        {
            // Логова — адреса (S3 §3.2): R25 вокруг устья/гнезда не может быть формой.
            foreach (var m in Mouths) masks.Add("lair", DomeFootprint.Circle(m.pos, 25f));
            foreach (var n in Nests) masks.Add("lair", DomeFootprint.Circle(n.pos, 25f));
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
                (x, z) => DomeRavines.Contains(Ravines, x, z, 1.4f), SampleGround, masks);
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
    void BuildStationHill()
    {
        // Пятно станции повёрнуто: габарит меша считаем по AABB повёрнутой плиты,
        // иначе углы выходят за лист и дают отвесную кромку. Запас 4 м выводит
        // кромку за площадку — там она садится на сырой рельеф, а не висит над полом.
        float yaw = padYaw * Mathf.Deg2Rad;
        float c = Mathf.Abs(Mathf.Cos(yaw));
        float s = Mathf.Abs(Mathf.Sin(yaw));
        float halfX = DomeLabComplex.HalfWidth * c + DomeLabComplex.HalfDepth * s + 4f;
        float halfZ = DomeLabComplex.HalfWidth * s + DomeLabComplex.HalfDepth * c + 4f;
        // Кромка должна дойти туда, где подъём холма уже ноль, иначе лист
        // обрывается вертикальной стеной на середине склона.
        float reach = Hill.radius + Hill.blend + 1f;
        halfX = Mathf.Max(halfX, reach);
        halfZ = Mathf.Max(halfZ, reach);
        // Южный спуск — вырез в холме, иначе лист закрывает портал и рампа
        // выглядит чёрной рамой, торчащей из земли.
        Func<float, float, bool> rampCut = null;
        Func<float, float, float> rampWeight = null;
        if (ResolvedLabEntrance == LabEntrance.Buried ||
            ResolvedLabEntrance == LabEntrance.Underground)
        {
            rampCut = (x, z) =>
            {
                float dx = x - LabCenter.x, dz = z - LabCenter.y;
                float lx = dx * Mathf.Cos(padYaw * Mathf.Deg2Rad) -
                           dz * Mathf.Sin(padYaw * Mathf.Deg2Rad);
                float lz = dx * Mathf.Sin(padYaw * Mathf.Deg2Rad) +
                           dz * Mathf.Cos(padYaw * Mathf.Deg2Rad);
                float zDoor = -DomeLabComplex.HalfDepth;
                float zOuter = zDoor - DomeLabComplex.RampLength;
                return Mathf.Abs(lx) <= DomeLabComplex.RampWidth * 0.5f + 1.2f &&
                       lz <= zDoor + 1f && lz >= zOuter - 1f;
            };
            rampWeight = (x, z) =>
            {
                float dx = x - LabCenter.x, dz = z - LabCenter.y;
                float lx = dx * Mathf.Cos(padYaw * Mathf.Deg2Rad) -
                           dz * Mathf.Sin(padYaw * Mathf.Deg2Rad);
                float lz = dx * Mathf.Sin(padYaw * Mathf.Deg2Rad) +
                           dz * Mathf.Cos(padYaw * Mathf.Deg2Rad);
                float zDoor = -DomeLabComplex.HalfDepth;
                float zOuter = zDoor - DomeLabComplex.RampLength;
                float wx = 1f - Mathf.InverseLerp(DomeLabComplex.RampWidth * 0.5f + 0.6f,
                    DomeLabComplex.RampWidth * 0.5f + 3.2f, Mathf.Abs(lx));
                float wz = Mathf.Min(Mathf.InverseLerp(zDoor + 2f, zDoor, lz),
                    Mathf.InverseLerp(zOuter - 2f, zOuter, lz));
                return Mathf.SmoothStep(0f, 1f, wx * wz);
            };
        }
        // Северная расщелина режется тем же способом, что и южный спуск;
        // диск вокруг портала больше не нужен — он открывал арену сверху.
        Func<float, float, bool> cut = rampCut;
        Func<float, float, float> cutWeight = rampWeight;
        if (northCrevice)
        {
            Func<float, float, bool> creviceCut = (x, z) =>
            {
                float dx = x - LabCenter.x, dz = z - LabCenter.y;
                float lx = dx * Mathf.Cos(padYaw * Mathf.Deg2Rad) -
                           dz * Mathf.Sin(padYaw * Mathf.Deg2Rad);
                float lz = dx * Mathf.Sin(padYaw * Mathf.Deg2Rad) +
                           dz * Mathf.Cos(padYaw * Mathf.Deg2Rad);
                float z0 = DomeLabComplex.HalfDepth;
                float z1 = z0 + DomeLabComplex.CreviceLength;
                return Mathf.Abs(lx) <= DomeLabComplex.CreviceWidth * 0.5f + 4f &&
                       lz >= z0 && lz <= z1;
            };
            Func<float, float, float> creviceWeight = (x, z) =>
            {
                float dx = x - LabCenter.x, dz = z - LabCenter.y;
                float lx = dx * Mathf.Cos(padYaw * Mathf.Deg2Rad) -
                           dz * Mathf.Sin(padYaw * Mathf.Deg2Rad);
                float lz = dx * Mathf.Sin(padYaw * Mathf.Deg2Rad) +
                           dz * Mathf.Cos(padYaw * Mathf.Deg2Rad);
                float z0 = DomeLabComplex.HalfDepth;
                float z1 = z0 + DomeLabComplex.CreviceLength;
                float half = DomeLabComplex.CreviceWidth * 0.5f;
                float wx = 1f - Mathf.InverseLerp(half, half + 4f, Mathf.Abs(lx));
                float wz = Mathf.Min(Mathf.InverseLerp(z0 - 2f, z0, lz),
                    Mathf.InverseLerp(z1 + 2f, z1, lz));
                return Mathf.SmoothStep(0f, 1f, wx * wz);
            };
            cut = (x, z) => (rampCut != null && rampCut(x, z)) || creviceCut(x, z);
            cutWeight = (x, z) => Mathf.Max(
                rampWeight != null ? rampWeight(x, z) : 0f,
                creviceWeight(x, z));
        }
        // v2: меш холма строится от финального ShapeGround (холм уже в нём);
        // v1 остаётся RawGround + Hill.Apply — бит-в-бит, как было.
        var mesh = Terrain.TargetOrder
            ? DomeHillMesh.Build(Hill, ShapeGround, 48, 0f, halfX, halfZ,
                cut, SampleGround, cutWeight, hillInGround: true)
            : DomeHillMesh.Build(Hill, RawGround, 48, 0f, halfX, halfZ,
                cut, SampleGround, cutWeight);
        if (mesh == null) return;
        Track(mesh);
        // Холм — земля, а не бетон: камень остаётся акцентами у расщелины.
        // Двусторонний: из зала видно земляной потолок, а не небо через backface.
        var hillMat = Flat(DomePalette.Terrain);
        hillMat.SetFloat("_Cull", 0f);
        Part("StationHill", mesh, hillMat);
    }

    void BuildFacility(long seed, bool buildLabComplex, bool buildStationLoop = false)
    {
        if (buildLabComplex)
        {
            LabComplex = DomeLabComplex.Build(Root, LabCenter, platformY, padYaw,
                ResolvedLabStyle, ResolvedLabEntrance, LabWetApproach, LabPlan, buildStationLoop);
            DomeLabDamage.Apply(LabComplex, seed, 1, LabPlan);
            Trace.Add("lab", LabComplex.parts, LabComplex.parts, LabComplex.planTemplate);
            LabDoor = LabComplex.door;
            LabAtrium = LabComplex.atrium;
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
        LabAtrium = default;
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
        // v2: свод/звёзды чуть грубее — это фон, а сборка G8 упиралась в 12 с.
        // v1 остаётся 128×24, чтобы A/C/B0/Pavilion были прежними.
        int vaultRings = Terrain.TargetOrder ? 64 : 128;
        int vaultSegments = Terrain.TargetOrder ? 12 : 24;
        var vault = DomeVault.BuildVaultMesh(rv, vaultRings, vaultSegments);
        Track(vault);
        Part("Vault", vault, skyMat);

        var starMat = new Material(starShader != null ? starShader : Shader.Find("Universal Render Pipeline/Unlit"));
        if (starMat.HasProperty("_Level")) starMat.SetFloat("_Level", 0f);
        mats.Add(starMat);
        var stars = DomeStars.BuildStarMesh(seed, Terrain.TargetOrder ? 200 : 400,
            vaultCenter, rv * 0.985f, rv * 0.004f);
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
