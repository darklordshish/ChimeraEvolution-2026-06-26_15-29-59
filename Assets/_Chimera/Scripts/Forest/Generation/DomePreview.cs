using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Превью рельефа павильона (s10a): строит чанковые меши командой, сносит Wipe.
/// Паттерн — как BuildPreview флоры/бункера: корень ~DomePreview, shared-материал,
/// в сцену ничего не сохраняется. Полный диаметр по формуле чанков
/// (D=2000 → 8×8); quads — параметр (превью 16–24, кадры рельефа).
/// </summary>
public static class DomePreview
{
    public static GameObject PreviewRoot { get; private set; }
    public static DomeHydro.State Hydro { get; private set; }
    public static DomeAshSite.AshSpot Ash { get; private set; }
    public static bool HasAsh { get; private set; }
    public static DomeFacilityLayout.Facility Facility { get; private set; }
    public static List<DomeSlope.MouthSpot> Mouths { get; private set; }
    public static List<DomeSlope.NestSpot> Nests { get; private set; }

    public const string RootPrefix = "~DomePreview";

    /// <summary>
    /// Перепривязка после перезагрузки домена: статики умирают, объекты сцены живут.
    /// Сид — в имени корня, диаметр — из чанков; данные перестраиваются теми же
    /// семплерами, что билд (кольцо для устьев!). Без неё любой Frame* после
    /// перекомпиляции падает с «нет превью».
    /// </summary>
    public static bool Reattach()
    {
        if (PreviewRoot != null && Hydro != null && Mouths != null && Facility != null && HasAsh) return true;
        GameObject root = null;
        long seed = 1337;
        foreach (var go in GameObject.FindObjectsByType<GameObject>())
        {
            if (go == null || !go.name.StartsWith(RootPrefix)) continue;
            root = go;
            var tail = go.name.Substring(RootPrefix.Length).TrimStart('_');
            if (!long.TryParse(tail, out seed)) seed = 1337;
            break;
        }
        if (root == null) return false;
        PreviewRoot = root;
        if (Hydro == null || Mouths == null)
        {
            int chunks = 0;
            float chunk = 250f;
            foreach (Transform c in root.transform)
            {
                if (!c.name.StartsWith("Chunk_")) continue;
                chunks++;
                var mf = c.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) chunk = mf.sharedMesh.bounds.size.x;
            }
            int n = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(chunks)));
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.seed = seed;
            cfg.randomSeed = false;
            cfg.mapDiameter = n * chunk;
            if (Hydro == null) Hydro = DomeHydro.Build(cfg, 256);
            if (!HasAsh && Hydro != null)
            {
                Ash = DomeAshSite.FindSpot(cfg, seed, Vector2.zero, Hydro, 96);
                HasAsh = true;
            }
            if (Facility == null) Facility = DomeFacilityLayout.Build(cfg, seed, Vector2.zero);
            if (Mouths == null)
            {
                System.Func<float, float, float> ground = (x, z) => DomeSlope.RingField(cfg, x, z);
                Mouths = DomeSlope.FindMouths(cfg, 3, 96, ground);
                Nests = DomeSlope.FindNests(cfg, seed, Mouths, 2, ground);
            }
            Object.DestroyImmediate(cfg);
        }
        return true;
    }
    static Material skyMat;
    static Material starMat;
    static Material previewMat;
    static Material rockMat;
    static Material waterMat;

    static Material Flat(Color color)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        mat.SetFloat("_Smoothness", 0f); // лоу-поли: шершаво, без блика-засвета (кадр s10a)
        return mat;
    }

    public static void BuildPreview(string diameterText, string quadsText, string seedText = "1337")
    {
        float diameter = 2000f;
        float.TryParse(diameterText, out diameter);
        int quads = 16;
        int.TryParse(quadsText, out quads);
        long seed = 1337;
        long.TryParse(seedText, out seed);
        if (seed == 0) seed = 1337;
        WipePreview();

        var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
        cfg.seed = seed;
        cfg.randomSeed = false;
        cfg.mapDiameter = Mathf.Clamp(diameter, 100f, 4000f);
        diameter = cfg.mapDiameter;
        int n = DomeGenRules.ChunkCount(cfg);

        previewMat = Flat(DomePalette.Terrain);
        rockMat = Flat(DomePalette.Rock);
        waterMat = Flat(DomePalette.Water);
        PreviewRoot = new GameObject($"{RootPrefix}_{seed}");
        var hydro = DomeHydro.Build(cfg, 256);
        Hydro = hydro;
        System.Func<float, float, float> sampler = (x, z) => DomeHydro.SampleHydro(cfg, hydro, x, z);
        float half = cfg.mapDiameter * 0.5f;
        int built = 0;
        for (int cz = 0; cz < n; cz++)
            for (int cx = 0; cx < n; cx++)
            {
                // Круглый павильон: квадратные углы сетки за кольцом не строим.
                float centerX = -half + (cx + 0.5f) * cfg.chunkSize;
                float centerZ = -half + (cz + 0.5f) * cfg.chunkSize;
                if (new Vector2(centerX, centerZ).magnitude > half) continue;
                var mesh = DomeChunkMesh.BuildChunkMesh(cfg, cx, cz, quads, sampler);
                Part($"Chunk_{cx}_{cz}", mesh, previewMat);
                built++;
            }
        Part("RockRing", DomeRockRing.BuildRingMesh(cfg, 256, 6), rockMat);
        Part("Lake", DomeWaterMesh.LakeDisc(hydro), waterMat);
        Part("River", DomeWaterMesh.RiverRibbon(cfg, hydro), waterMat);
        BuildMouths(cfg, hydro);
        BuildAsh(cfg, hydro, seed);
        BuildFacility(cfg, hydro, seed);
        DomeFlora.Build(PreviewRoot, cfg, hydro, seed);
        float rv = DomeGenRules.VaultRadius(cfg);
        skyMat = new Material(Shader.Find("Chimera/DomeSky"));
        skyMat.SetVector("_Center", new Vector3(0f, DomeVault.CenterY(rv), 0f));
        Part("Vault", DomeVault.BuildVaultMesh(rv, 128, 24), skyMat);
        starMat = new Material(Shader.Find("Chimera/DomeFlat"));
        starMat.SetFloat("_Level", 0f);
        var vaultCenter = new Vector3(0f, DomeVault.CenterY(rv), 0f);
        Part("Stars", DomeStars.BuildStarMesh(seed, 400, vaultCenter, rv * 0.985f, rv * 0.004f), starMat);
        var sun = new GameObject("DomeSun");
        sun.transform.SetParent(PreviewRoot.transform, false);
        var sunLight = sun.AddComponent<Light>();
        sunLight.type = LightType.Directional;
        sunLight.shadows = LightShadows.None;
        DomeSkyRig.Reset();
        DomeSkyRig.ApplyPhase(PreviewRoot, 10.5f);
        Object.DestroyImmediate(cfg);
        Debug.Log($"[Forest] превью купола построено (сид {seed}, Ø{diameter} м, чанков {built}/{n * n} + кольцо + озеро r={hydro.lakeRadius:F0}м + река {hydro.riverPts.Count} т.)");
    }

    static GameObject Part(string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(PreviewRoot.transform, false);
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        return go;
    }

    public static void WipePreview()
    {
        // Снос ВСЕХ корней по имени (Find отдаёт первый — дубли копились, 5 корней s10h!).
        PreviewRoot = null;
        foreach (var go in GameObject.FindObjectsByType<GameObject>())
        {
            if (go == null || !go.name.StartsWith(RootPrefix)) continue;
            PreviewRoot = go;
            WipeOne();
        }
        PreviewRoot = null;
        Hydro = null;
    }

    static void WipeOne()
    {
        if (PreviewRoot == null)
        {
            foreach (var go in GameObject.FindObjectsByType<GameObject>())
                if (go != null && go.name.StartsWith(RootPrefix)) { PreviewRoot = go; break; }
        }
        Hydro = null;
        // Дождь — ДО сноса корня (иначе статики streak висят, дубли копятся).
        if (PreviewRoot != null) DomeRain.WipePreview(PreviewRoot);
        if (PreviewRoot != null)
        {
            // Снос мешей кита (создаются свежими каждый билд — иначе утечка):
            // уникальные sharedMesh всех фильтров корня.
            var seen = new System.Collections.Generic.HashSet<Mesh>();
            foreach (var filter in PreviewRoot.GetComponentsInChildren<MeshFilter>())
                if (filter != null && filter.sharedMesh != null && seen.Add(filter.sharedMesh))
                    Object.DestroyImmediate(filter.sharedMesh);
            Object.DestroyImmediate(PreviewRoot);
            PreviewRoot = null;
        }
        if (previewMat != null)
        {
            Object.DestroyImmediate(previewMat);
            previewMat = null;
        }
        if (rockMat != null)
        {
            Object.DestroyImmediate(rockMat);
            rockMat = null;
        }
        if (waterMat != null)
        {
            Object.DestroyImmediate(waterMat);
            waterMat = null;
        }
        if (skyMat != null)
        {
            Object.DestroyImmediate(skyMat);
            skyMat = null;
        }
        if (starMat != null)
        {
            Object.DestroyImmediate(starMat);
            starMat = null;
        }
        if (darkMat != null)
        {
            Object.DestroyImmediate(darkMat);
            darkMat = null;
        }
        if (nestMat != null)
        {
            Object.DestroyImmediate(nestMat);
            nestMat = null;
        }
        foreach (var m in new[] { barkMat, crownMat, owlMat })
            if (m != null) Object.DestroyImmediate(m);
        barkMat = crownMat = owlMat = null;
        HasAsh = false;
        Mouths = null;
        Nests = null;
        Facility = null;
        DomeFacilityPlacer.ClearMats();
        DomeFlora.ClearMats();
        DomeWeather.ResetWet();
        DomeSkyRig.Reset();
    }

    /// <summary>
    /// Погода в превью (s10h): дождь над центром + мокрая земля (ступень ×0.7).
    /// </summary>
    public static void BuildWeather()
    {
        if (PreviewRoot == null) throw new System.InvalidOperationException("сначала BuildDomePreview");
        bool hasRain = false;
        foreach (Transform c in PreviewRoot.transform)
            if (c.name == "~DomeRain") { hasRain = true; break; }
        if (!hasRain)
            DomeRain.BuildPreview(PreviewRoot, 0f, 0f);
        SetWet(1f);
        Debug.Log("[Forest] погода: дождь + мокро");
    }

    public static void SetWet(float wet)
    {
        var mats = new List<Material>
        {
            previewMat, rockMat, barkMat, crownMat, owlMat, darkMat, nestMat,
        };
        mats.AddRange(DomeFacilityPlacer.LiveMats());
        DomeWeather.ApplyWet(mats, wet);
    }

    public static GameObject NavRoot { get; private set; }
    public static NavMeshSurface NavSurface { get; private set; }
    static Material navMat;
    static Material darkMat;
    static Material nestMat;
    static Material barkMat;
    static Material crownMat;
    static Material owlMat;

    /// <summary>
    /// Ясень-исполин в превью (s10g-2): 85 м / крона 140 м / ствол 16 м / корни 50 м
    /// по принятым числам 14.10 + присада-гнездо и болванка совы. Геометрия — общая
    /// с рантаймом (DomeAshSite.BuildGeometry), иначе у превью и игры разойдутся числа.
    /// </summary>
    public static void BuildAsh(DomeGenConfigSO cfg, DomeHydro.State hydro, long seed)
    {
        float ashScale = DomeAshSite.ScaleFor(cfg);
        var spot = DomeAshSite.FindSpot(cfg, seed, Vector2.zero, hydro, 96, ashScale, false);
        float gy = DomeHydro.SampleHydro(cfg, hydro, spot.pos.x, spot.pos.y);
        spot.groundY = gy;
        Ash = spot;
        HasAsh = true;
        barkMat = Flat(DomePalette.Bark);
        crownMat = Flat(DomePalette.Crown);
        owlMat = Flat(DomePalette.Owl);
        DomeAshSite.BuildGeometry(PreviewRoot.transform, new Vector3(spot.pos.x, gy, spot.pos.y),
            barkMat, crownMat, owlMat, null, ashScale);
        Debug.Log($"[Forest] ясень: ({Ash.pos.x:F0},{Ash.pos.y:F0}), {DomeAshSite.Height:F0}м, гнездо на {DomeAshSite.NestHeight:F0}м");
    }

    public static void BuildFacility(DomeGenConfigSO cfg, DomeHydro.State hydro, long seed)
    {
        Facility = DomeFacilityLayout.Build(cfg, seed, Vector2.zero);
        System.Func<float, float, float> ground = (x, z) => Mathf.Max(
            DomeHydro.SampleHydro(cfg, hydro, x, z),
            DomeSlope.RingField(cfg, x, z));
        DomeFacilityPlacer.Build(PreviewRoot, Facility, ground);
        Debug.Log($"[Forest] корпуса: деталей {Facility.parts.Count}, POI {Facility.pois.Count}");
    }

    /// <summary>
    /// Устья и гнёзда в превью (s10e-2): портал из плит (косяки + перемычка + тёмная
    /// карта ниши), скалы вокруг, каирны-маркеры гнёзд. Геометрия — общая с рантаймом
    /// (DomeMouthBuilder), y — по кольцу, а не по площадке лаборатории.
    /// </summary>
    public static void BuildMouths(DomeGenConfigSO cfg, DomeHydro.State hydro)
    {
        System.Func<float, float, float> ground = (x, z) => DomeSlope.RingField(cfg, x, z);
        Mouths = DomeSlope.FindMouths(cfg, 3, 96, ground);
        Nests = DomeSlope.FindNests(cfg, cfg.seed, Mouths, 2, ground);
        if (Mouths.Count == 0)
        {
            Debug.Log("[Forest] устьев нет на этом рельефе (смени сид)");
            return;
        }
        darkMat = Flat(DomePalette.Dark);
        nestMat = Flat(DomePalette.Nest);
        DomeMouthBuilder.Build(PreviewRoot.transform, cfg, Mouths, Nests, ground,
            rockMat, darkMat, nestMat);
        Debug.Log($"[Forest] устья: {Mouths.Count}, гнёзда: {Nests.Count}");
    }

    static float navAmplitude;

    /// <summary>
    /// NavMesh-доказательство тайлования (s10a): чанки с MeshCollider + поверхность
    /// мелкими тайлами (tileSize=32 вокселя) + синхронный бейк. Моно-бейк на Ø2000
    /// запрещён спекой; здесь малый диаметр проверяет plumbing, полный — витрина s10i.
    /// </summary>
    public static void BuildNavPreview(string diameterText, string quadsText)
    {
        float diameter = 500f;
        float.TryParse(diameterText, out diameter);
        int quads = 8;
        int.TryParse(quadsText, out quads);
        WipeNavPreview();

        var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
        cfg.seed = 1337;
        cfg.randomSeed = false;
        cfg.amplitude = 2f;
        cfg.mapDiameter = Mathf.Clamp(diameter, 100f, 4000f);
        diameter = cfg.mapDiameter;
        int n = DomeGenRules.ChunkCount(cfg);

        navMat = Flat(DomePalette.Terrain);
        NavRoot = new GameObject("~DomeNav");
        for (int cz = 0; cz < n; cz++)
            for (int cx = 0; cx < n; cx++)
            {
                var mesh = DomeChunkMesh.BuildChunkMesh(cfg, cx, cz, quads);
                var go = new GameObject($"NavChunk_{cx}_{cz}");
                go.transform.SetParent(NavRoot.transform, false);
                var filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = navMat;
                var collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
            }
        NavSurface = NavRoot.AddComponent<NavMeshSurface>();
        NavSurface.collectObjects = CollectObjects.Children;
        NavSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        NavSurface.overrideVoxelSize = true;
        NavSurface.voxelSize = cfg.navVoxelSize;
        NavSurface.overrideTileSize = true;
        NavSurface.tileSize = 32;
        NavSurface.center = Vector3.zero;
        NavSurface.size = new Vector3(diameter + 400f, 200f, diameter + 400f);
        NavSurface.BuildNavMesh();
        navAmplitude = cfg.amplitude;
        Object.DestroyImmediate(cfg);
        Debug.Log($"[Forest] nav-превью запечено (Ø{diameter} м, чанков {n}×{n}, тайл 32)");
    }

    public static bool SampleNav(float x, float z, out Vector3 pos, float maxDistance = 5f)
    {
        var probe = new Vector3(x, navAmplitude + 6f, z);
        NavMeshHit hit;
        bool ok = NavMesh.SamplePosition(probe, out hit, Mathf.Max(maxDistance, navAmplitude + 8f), NavMesh.AllAreas);
        pos = hit.position;
        return ok;
    }

    public static void WipeNavPreview()
    {
        if (NavRoot == null)
        {
            var nav = GameObject.Find("~DomeNav");
            if (nav != null) NavRoot = nav;
        }
        if (NavSurface != null && NavSurface.navMeshData != null)
            NavSurface.RemoveData();
        NavSurface = null;
        if (NavRoot != null)
        {
            Object.DestroyImmediate(NavRoot);
            NavRoot = null;
        }
        if (navMat != null)
        {
            Object.DestroyImmediate(navMat);
            navMat = null;
        }
    }
}
