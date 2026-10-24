using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Рантайм-мир павильона: один экземпляр = один собранный мир под переданным корнем.
/// Порядок — как в спеке среза A: гидрология → чанки с коллайдерами → скальное кольцо →
/// вода → устья/гнёзда → ясень и гнездо → корпуса → вход → флора → свод/звёзды/солнце.
/// Никакой статики превью и никакого Find: корень и все ссылки — поля экземпляра.
/// Чанки, флора, корпуса и спавн игрока сидят на SampleGround — ровной площадке
/// лаборатории R25; вода, устья и ясень — на сыром поле.
/// Срез B (B0): ясень можно не строить, а вход — точку у подножия кольца — строить
/// с каменным порталом-маркером (без коллайдеров: NavMesh он не задевает).
/// Лес, срез A + B0.
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

    public GameObject Root { get; private set; }
    public DomeHydro.State Hydro { get; private set; }
    public DomeAshSite.AshSpot Ash { get; private set; }
    public bool HasAsh { get; private set; }
    public DomeFacilityLayout.Facility Facility { get; private set; }
    public List<DomeSlope.MouthSpot> Mouths { get; private set; } = new List<DomeSlope.MouthSpot>();
    public List<DomeSlope.NestSpot> Nests { get; private set; } = new List<DomeSlope.NestSpot>();
    public LairSite NestSite { get; private set; }
    /// <summary>Точка постановки игрока: центр двора лаборатории, земля + 1 м.</summary>
    public Vector3 SpawnPoint { get; private set; }
    /// <summary>
    /// Вход мира (срез B): у подножия скального кольца внутри чаши, на земле. Готов
    /// до постановки игрока, поэтому ForestBuilder со spawnAtEntry может на него встать.
    /// Мир без корпусов входа не имеет — тогда это двор.
    /// </summary>
    public Vector3 Entry { get; private set; }
    public int Chunks { get; private set; }
    public int Trees { get; private set; }
    public float PlatformY => platformY;
    public float MapDiameter => cfg != null ? cfg.mapDiameter : 0f;

    DomeGenConfigSO cfg;
    float platformY;
    NavMeshSurface navSurface;
    readonly List<Material> mats = new List<Material>();
    readonly List<Mesh> meshes = new List<Mesh>();

    /// <summary>
    /// Собрать мир под root. root должен быть свежим (ForestBuilder создаёт "<seed>_ForestWorld");
    /// cfg остаётся собственностью вызывающего и здесь не уничтожается.
    /// <para>
    /// buildAsh=false — не строить ясень и совиное гнездо (HasAsh=false, Ash=default,
    /// NestSite=null): на Ø400 дерево встаёт в скальное кольцо, а срез B его ещё не проверяет.
    /// buildEntry=true — вычислить Entry и поставить каменный портал-маркер у подножия кольца.
    /// </para>
    /// </summary>
    public void Build(GameObject root, DomeGenConfigSO cfg, int terrainQuads, long seed,
        bool buildAsh = true, bool buildEntry = false)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        if (Root != null) Clear();

        Root = root;
        this.cfg = cfg;
        Hydro = DomeHydro.Build(cfg, 256);
        // Цель площадки: не ниже уровня озера, иначе центр двора уйдёт под воду.
        platformY = Mathf.Max(
            DomeHydro.SampleHydro(cfg, Hydro, 0f, 0f),
            Hydro.lakeLevel + 0.5f);

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
        BuildFacility(seed);

        // SpawnPoint нужен ДО Entry: у мира без корпусов вход — это он же.
        Vector2 lab = Facility != null ? Facility.labCenter : Vector2.zero;
        SpawnPoint = new Vector3(lab.x, SampleGround(lab.x, lab.y) + 1f, lab.y);
        BuildEntry(buildEntry);

        // Вход — не чаща: вокруг Entry оставляем поляну R12, иначе игрок рождается
        // в стволе и не видит ни входа, ни леса. Двор лаборатории уже выровнен.
        Func<float, float, bool> entryClearing = (x, z) =>
            Vector2.Distance(new Vector2(x, z), new Vector2(Entry.x, Entry.z)) < 12f;
        Trees = DomeFlora.Build(Root, cfg, Hydro, seed, SampleGround, entryClearing);
        BuildSky(seed);
    }

    /// <summary>
    /// Пол мира: вырезанное гидрологией поле + плоская площадка лаборатории R25
    /// (плоско до R20, плавный блендинг к сырому полю на краю). Вода, устья и ясень
    /// эту функцию НЕ используют.
    /// </summary>
    public float SampleGround(float x, float z)
    {
        if (cfg == null || Hydro == null) return 0f;
        float raw = DomeHydro.SampleHydro(cfg, Hydro, x, z);
        float d = Mathf.Sqrt(x * x + z * z);
        if (d >= LabRadius) return raw;
        if (d <= LabFlatRadius) return platformY;
        float t = Mathf.InverseLerp(LabFlatRadius, LabRadius, d);
        t = t * t * (3f - 2f * t);
        return Mathf.Lerp(platformY, raw, t);
    }

    /// <summary>
    /// Запечь NavMesh тайлами по коллайдерам мира. Поверхность — компонент ForestBuilder;
    /// collectObjects=Children означает, что корень мира должен быть её ребёнком.
    /// </summary>
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
        foreach (var mat in mats)
            if (mat != null) DestroyObject(mat, !Application.isPlaying);
        mats.Clear();
        foreach (var mesh in meshes)
            if (mesh != null) DestroyObject(mesh, !Application.isPlaying);
        meshes.Clear();

        DomeFlora.ReleaseCaches();
        DomeFacilityPlacer.ReleaseCaches();
        DomeWeather.ResetWet();
        DomeSkyRig.Reset();

        Hydro = null;
        Ash = default;
        HasAsh = false;
        Facility = null;
        Mouths = new List<DomeSlope.MouthSpot>();
        Nests = new List<DomeSlope.NestSpot>();
        NestSite = null;
        SpawnPoint = default;
        Entry = default;
        Chunks = 0;
        Trees = 0;
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
        var lake = DomeWaterMesh.LakeDisc(Hydro);
        Track(lake);
        Part("Lake", lake, mat);
        var river = DomeWaterMesh.RiverRibbon(cfg, Hydro);
        Track(river);
        Part("River", river, mat);
    }

    void BuildMouthsAndNests(long seed)
    {
        // Устья стоят на скальном кольце: площадку лаборатории сюда не тянем.
        Func<float, float, float> ringGround = (x, z) => DomeSlope.RingField(cfg, x, z);
        Mouths = DomeSlope.FindMouths(cfg, 3, 96, ringGround);
        Nests = DomeSlope.FindNests(cfg, seed, Mouths, 2, ringGround);
        if (Mouths.Count == 0)
        {
            Debug.LogWarning("[Forest] устьев нет на этом рельефе (смени сид)");
            return;
        }
        var rock = Flat(DomePalette.Rock);
        var dark = Flat(DomePalette.Dark);
        var nest = Flat(DomePalette.Nest);
        DomeMouthBuilder.Build(Root.transform, cfg, Mouths, Nests, ringGround, rock, dark, nest, meshes);
    }

    void BuildAshAndNest(long seed)
    {
        try
        {
            var spot = DomeAshSite.FindSpot(cfg, seed, Vector2.zero, Hydro, 96);
            float gy = DomeHydro.SampleHydro(cfg, Hydro, spot.pos.x, spot.pos.y);
            spot.groundY = gy;
            Ash = spot;
            HasAsh = true;

            var bark = Flat(DomePalette.Bark);
            var crown = Flat(DomePalette.Crown);
            var owl = Flat(DomePalette.Owl);
            DomeAshSite.BuildGeometry(Root.transform, new Vector3(spot.pos.x, gy, spot.pos.y),
                bark, crown, owl, meshes);

            var nestGo = new GameObject("AshNest");
            nestGo.transform.SetParent(Root.transform, false);
            var site = nestGo.AddComponent<LairSite>();
            // Гнездо на 36 м по OTVET; вбок — потому что ствол 16 м в поперечнике
            // (на 36 м радиус ≈ 5 м, присада в 12 м от оси уже снаружи).
            var nestLocal = new Vector3(spot.pos.x + DomeAshSite.NestLateral,
                gy + DomeAshSite.NestHeight, spot.pos.y);
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
    /// </summary>
    void BuildEntry(bool buildMarker)
    {
        if (Facility == null)
        {
            // Нечего ориентировать — у мира без корпусов вход и есть двор.
            Entry = SpawnPoint;
            return;
        }

        Vector2 dir = -Facility.hangarPos; // от ангара к центру
        if (dir.sqrMagnitude < 1e-6f) dir = Vector2.down;
        dir.Normalize();
        float radius = Mathf.Max(0f, DomeRockRing.InnerRadius(cfg) - EntryInset);
        Vector2 p = dir * radius;
        Entry = new Vector3(p.x, SampleGround(p.x, p.y), p.y);

        if (buildMarker) BuildEntryMarker(dir);
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

    void BuildFacility(long seed)
    {
        Facility = DomeFacilityLayout.Build(cfg, seed, Vector2.zero);
        Vector2 lab = Facility.labCenter;
        // Во дворе — только площадка лаборатории (RingField там даёт сырое поле и
        // поднимает пол над выровненной землёй). В кольце (ангар) — высшее из
        // площадки и кольца: там детали уже стоят в мировых координатах.
        Func<float, float, float> ground = (x, z) =>
        {
            float d = Vector2.Distance(new Vector2(x, z), lab);
            if (d <= LabRadius + 10f) return SampleGround(x, z);
            return Mathf.Max(SampleGround(x, z), DomeSlope.RingField(cfg, x, z));
        };
        DomeFacilityPlacer.Build(Root, Facility, ground);
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
        light.shadows = LightShadows.None;

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
