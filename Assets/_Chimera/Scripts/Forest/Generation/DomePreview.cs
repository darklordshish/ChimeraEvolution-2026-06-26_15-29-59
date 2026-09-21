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

    /// <summary>
    /// Перепривязка после перезагрузки домена: статики умирают, объекты сцены живут.
    /// Диаметр выводится из чанков (n² штук × размер), Hydro перестраивается
    /// детерминированно (сид превью фиксирован). Без неё любой Frame* после
    /// перекомпиляции падает с «нет превью».
    /// </summary>
    public static bool Reattach()
    {
        if (PreviewRoot != null && Hydro != null && Mouths != null && Facility != null && HasAsh) return true;
        var root = GameObject.Find("~DomePreview");
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
            cfg.seed = 1337;
            cfg.randomSeed = false;
            cfg.mapDiameter = n * chunk;
            if (Hydro == null) Hydro = DomeHydro.Build(cfg, 256);
            if (!HasAsh && Hydro != null)
            {
                Ash = DomeAshSite.FindSpot(cfg, 1337, Vector2.zero, Hydro, 96);
                HasAsh = true;
            }
            if (Facility == null) Facility = DomeFacilityLayout.Build(cfg, 1337, Vector2.zero);
            if (Mouths == null)
            {
                Mouths = DomeSlope.FindMouths(cfg, 3, 96);
                Nests = DomeSlope.FindNests(cfg, cfg.seed, Mouths, 2);
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

    public static void BuildPreview(string diameterText, string quadsText)
    {
        float diameter = 2000f;
        float.TryParse(diameterText, out diameter);
        int quads = 16;
        int.TryParse(quadsText, out quads);
        WipePreview();

        var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
        cfg.seed = 1337;
        cfg.randomSeed = false;
        cfg.mapDiameter = Mathf.Clamp(diameter, 100f, 4000f);
        diameter = cfg.mapDiameter;
        int n = DomeGenRules.ChunkCount(cfg);

        previewMat = Flat(new Color(0.32f, 0.44f, 0.26f));
        rockMat = Flat(new Color(0.5f, 0.5f, 0.5f));
        waterMat = Flat(new Color(0.2f, 0.45f, 0.6f));
        PreviewRoot = new GameObject("~DomePreview");
        var hydro = DomeHydro.Build(cfg, 256);
        Hydro = hydro;
        System.Func<float, float, float> sampler = (x, z) => DomeHydro.SampleHydro(cfg, hydro, x, z);
        for (int cz = 0; cz < n; cz++)
            for (int cx = 0; cx < n; cx++)
            {
                var mesh = DomeChunkMesh.BuildChunkMesh(cfg, cx, cz, quads, sampler);
                Part($"Chunk_{cx}_{cz}", mesh, previewMat);
            }
        Part("RockRing", DomeRockRing.BuildRingMesh(cfg, 256, 6), rockMat);
        Part("Lake", LakeDisc(hydro), waterMat);
        Part("River", RiverRibbon(cfg, hydro), waterMat);
        BuildMouths(cfg, hydro);
        BuildAsh(cfg, hydro);
        BuildFacility(cfg, hydro);
        float rv = DomeGenRules.VaultRadius(cfg);
        skyMat = new Material(Shader.Find("Chimera/DomeSky"));
        skyMat.SetVector("_Center", new Vector3(0f, DomeVault.CenterY(rv), 0f));
        Part("Vault", DomeVault.BuildVaultMesh(rv, 128, 24), skyMat);
        starMat = new Material(Shader.Find("Chimera/DomeFlat"));
        starMat.SetFloat("_Level", 0f);
        var vaultCenter = new Vector3(0f, DomeVault.CenterY(rv), 0f);
        Part("Stars", DomeStars.BuildStarMesh(1337, 400, vaultCenter, rv * 0.985f, rv * 0.004f), starMat);
        var sun = new GameObject("DomeSun");
        sun.transform.SetParent(PreviewRoot.transform, false);
        var sunLight = sun.AddComponent<Light>();
        sunLight.type = LightType.Directional;
        sunLight.shadows = LightShadows.None;
        DomeSkyRig.Reset();
        DomeSkyRig.ApplyPhase(PreviewRoot, 10.5f);
        Object.DestroyImmediate(cfg);
        Debug.Log($"[Forest] превью купола построено (сид 1337, Ø{diameter} м, чанков {n}×{n} + кольцо + озеро r={hydro.lakeRadius:F0}м + река {hydro.riverPts.Count} т.)");
    }

    static Mesh LakeDisc(DomeHydro.State s)
    {
        const int segs = 48;
        var verts = new Vector3[segs * 3];
        var center = new Vector3(s.lakeCenter.x, s.lakeLevel, s.lakeCenter.y);
        float r = s.lakeRadius * 1.02f;
        for (int i = 0; i < segs; i++)
        {
            float a0 = i / (float)segs * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)segs * Mathf.PI * 2f;
            verts[i * 3] = center;
            verts[i * 3 + 1] = center + new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r);
            verts[i * 3 + 2] = center + new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r);
        }
        return FacetMesh("LakeDisc", verts);
    }

    /// <summary>
    /// Лента реки: сегменты дробятся ×4 с посадкой каждой точки на вырезанное поле
    /// (+0.25), поэтому вода непрерывна и повторяет русло, а не режет бугры напрямик.
    /// </summary>
    static Mesh RiverRibbon(DomeGenConfigSO cfg, DomeHydro.State s)
    {
        var pts = s.riverPts;
        const int sub = 4;
        var verts = new List<Vector3>((pts.Count - 1) * sub * 6);
        float w = s.riverHalfWidth;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector2 d = (pts[i + 1] - pts[i]).normalized;
            Vector2 p = new Vector2(-d.y, d.x) * w;
            Vector3 prevL = WaterEdge(cfg, s, pts[i].x - p.x, pts[i].y - p.y);
            Vector3 prevR = WaterEdge(cfg, s, pts[i].x + p.x, pts[i].y + p.y);
            for (int k = 1; k <= sub; k++)
            {
                float t = k / (float)sub;
                float px = Mathf.Lerp(pts[i].x, pts[i + 1].x, t);
                float pz = Mathf.Lerp(pts[i].y, pts[i + 1].y, t);
                Vector2 dd = (pts[i + 1] - pts[i]).normalized;
                Vector2 pp = new Vector2(-dd.y, dd.x) * w;
                Vector3 l = WaterEdge(cfg, s, px - pp.x, pz - pp.y);
                Vector3 r = WaterEdge(cfg, s, px + pp.x, pz + pp.y);
                verts.Add(prevL); verts.Add(prevR); verts.Add(r);
                verts.Add(prevL); verts.Add(r); verts.Add(l);
                prevL = l; prevR = r;
            }
        }
        return FacetMesh("RiverRibbon", verts.ToArray());
    }

    static Vector3 WaterEdge(DomeGenConfigSO cfg, DomeHydro.State s, float x, float z)
    {
        return new Vector3(x, DomeHydro.SampleHydro(cfg, s, x, z) + 0.25f, z);
    }

    static Mesh FacetMesh(string name, Vector3[] verts)
    {
        var tris = new int[verts.Length];
        for (int i = 0; i < tris.Length; i++) tris[i] = i;
        var mesh = new Mesh { name = name };
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
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
            if (go == null || go.name != "~DomePreview") continue;
            PreviewRoot = go;
            WipeOne();
        }
        PreviewRoot = null;
        Hydro = null;
    }

    static void WipeOne()
    {
        if (PreviewRoot == null) PreviewRoot = GameObject.Find("~DomePreview");
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
    /// Ясень-исполин в превью (s10g-2): ствол 30м + крона 12м + гнездо-платформа
    /// + болванка совы (2 икосаэдра — масса и масштаб, не модель).
    /// </summary>
    public static void BuildAsh(DomeGenConfigSO cfg, DomeHydro.State hydro)
    {
        Ash = DomeAshSite.FindSpot(cfg, 1337, Vector2.zero, hydro, 96);
        HasAsh = true;
        barkMat = Flat(new Color(0.35f, 0.25f, 0.18f));
        crownMat = Flat(new Color(0.24f, 0.38f, 0.20f));
        owlMat = Flat(new Color(0.55f, 0.52f, 0.47f));
        float gy = DomeHeightField.SampleHeight(cfg, Ash.pos.x, Ash.pos.y);
        var root = new GameObject("Ash");
        root.transform.SetParent(PreviewRoot.transform, false);
        root.transform.position = new Vector3(Ash.pos.x, gy, Ash.pos.y);
        TrunkPart(root.transform, FloraMeshKit.Trunk(0.8f, 0.3f, 30f), barkMat, Vector3.zero);
        TrunkPart(root.transform, FloraMeshKit.AshCrown(12f), crownMat, new Vector3(0f, 30f, 0f));
        TrunkPart(root.transform, FloraMeshKit.Slab(2.2f, 0.3f, 2.2f), barkMat, new Vector3(3f, 36f, 1f));
        TrunkPart(root.transform, FloraMeshKit.Icosahedron(0.55f), owlMat, new Vector3(3f, 37f, 1f));
        TrunkPart(root.transform, FloraMeshKit.Icosahedron(0.32f), owlMat, new Vector3(3f, 37.8f, 1.35f));
        Debug.Log($"[Forest] ясень: ({Ash.pos.x:F0},{Ash.pos.y:F0}), гнездо на 36м");
    }

    static void TrunkPart(Transform parent, Mesh mesh, Material mat, Vector3 localPos)
    {
        var go = new GameObject("AshPart");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
    }

    /// <summary>
    /// Устья и гнёзда в превью (s10e-2): портал из плит (косяки + перемычка + тёмная
    /// карта ниши), скалы вокруг, каирны-маркеры гнёзд. y — по carved-полю превью.
    /// </summary>
    public static void BuildFacility(DomeGenConfigSO cfg, DomeHydro.State hydro)
    {
        Facility = DomeFacilityLayout.Build(cfg, 1337, Vector2.zero);
        System.Func<float, float, float> ground = (x, z) => Mathf.Max(
            DomeHydro.SampleHydro(cfg, hydro, x, z),
            DomeSlope.RingField(cfg, x, z));
        DomeFacilityPlacer.Build(PreviewRoot, Facility, ground);
        Debug.Log($"[Forest] корпуса: деталей {Facility.parts.Count}, POI {Facility.pois.Count}");
    }

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
        darkMat = Flat(new Color(0.14f, 0.15f, 0.16f));
        nestMat = Flat(new Color(0.35f, 0.42f, 0.23f));
        // Посадка — по кольцу (портал стоит на скале, чанки кольца не дублируют).
        foreach (var m in Mouths)
        {
            float gy = ground(m.pos.x, m.pos.y);
            float yaw = Mathf.Atan2(Mathf.Cos(m.facing), Mathf.Sin(m.facing)) * Mathf.Rad2Deg;
            var root = new GameObject("Mouth");
            root.transform.SetParent(PreviewRoot.transform, false);
            root.transform.position = new Vector3(m.pos.x, gy - 0.3f, m.pos.y);
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Box(root.transform, FloraMeshKit.Slab(0.8f, 3f, 1.2f), rockMat, new Vector3(-1.6f, 0f, 0f));
            Box(root.transform, FloraMeshKit.Slab(0.8f, 3f, 1.2f), rockMat, new Vector3(1.6f, 0f, 0f));
            Box(root.transform, FloraMeshKit.Slab(4.2f, 0.8f, 1.4f), rockMat, new Vector3(0f, 3.2f, 0f));
            Box(root.transform, FloraMeshKit.Slab(3.2f, 3f, 0.1f), darkMat, new Vector3(0f, 1.5f, -0.8f));
        }
        int ri = 0;
        foreach (var m in Mouths)
        {
            for (int j = 0; j < 14; j++, ri++)
            {
                float a = SeededHash.ToFloat01(SeededHash.Hash(cfg.seed, 800, ri)) * Mathf.PI * 2f;
                float dist = 5f + SeededHash.ToFloat01(SeededHash.Hash(cfg.seed, 801, ri)) * 13f;
                float x = m.pos.x + Mathf.Cos(a) * dist;
                float z = m.pos.y + Mathf.Sin(a) * dist;
                var mesh = (ri % 2 == 0) ? FloraMeshKit.Slab(0.9f, 0.5f, 0.7f) : FloraMeshKit.Spike(0.5f, 2.4f);
                var go = Part("MouthRock", mesh, rockMat);
                go.transform.position = new Vector3(x, ground(x, z), z);
                float s = 1.2f + SeededHash.ToFloat01(SeededHash.Hash(cfg.seed, 802, ri)) * 1.6f;
                go.transform.localScale = Vector3.one * s;
                go.transform.rotation = Quaternion.Euler(0f, SeededHash.ToFloat01(SeededHash.Hash(cfg.seed, 803, ri)) * 360f, 0f);
            }
        }
        foreach (var n in Nests)
        {
            float gy = ground(n.pos.x, n.pos.y);
            Cairn(n.pos.x, gy, n.pos.y, 0.9f);
            Cairn(n.pos.x, gy + 0.55f, n.pos.y, 0.6f);
            Cairn(n.pos.x, gy + 0.95f, n.pos.y, 0.35f);
        }
        Debug.Log($"[Forest] устья: {Mouths.Count}, гнёзда: {Nests.Count}");
    }

    static void Box(Transform parent, Mesh mesh, Material mat, Vector3 localPos)
    {
        var go = new GameObject("MouthPart");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
    }

    static void Cairn(float x, float y, float z, float size)
    {
        var go = Part("NestCairn", FloraMeshKit.Slab(size, size * 0.6f, size), nestMat);
        go.transform.position = new Vector3(x, y, z);
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

        navMat = Flat(new Color(0.32f, 0.44f, 0.26f));
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
