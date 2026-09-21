using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Расстановка флоры: scatter по видам → блокеры (ствол-капсула, шип, плита>1м;
/// кроны/куст/трава без коллайдеров) + декор на 4 shared-материалах с инстансингом
/// (авто-батчинг Unity вместо ручного Indirect — тот же GPU-инстансинг, в 10 раз меньше
/// кода; дистанционный cull — позже). Корень Flora — под GO аппликатора (тест),
/// бейк разом собирает стволы. Лес, слайс s8.
/// </summary>
public class FloraPlacer : MonoBehaviour
{
    public WorldGenConfigSO world;
    public FloraConfigSO flora;

    public Material trunkMat;
    public Material leafMat;
    public Material rockMat;
    public Material grassMat;

    public GameObject FloraRoot { get; private set; }
    public int TrunkColliders { get; private set; }

    /// <summary>Центр запретной зоны (бункер) + радиус: точки внутри не сеем.</summary>
    public Vector3 excludeCenter;
    public float excludeRadius;

    readonly System.Collections.Generic.Dictionary<string, Mesh> meshCache =
        new System.Collections.Generic.Dictionary<string, Mesh>();

    Mesh Kit(string key, System.Func<Mesh> build)
    {
        if (!meshCache.TryGetValue(key, out var mesh)) meshCache[key] = mesh = build();
        return mesh;
    }

    bool Excluded(Vector3 pos)
    {
        if (excludeRadius <= 0f) return false;
        float dx = pos.x - excludeCenter.x, dz = pos.z - excludeCenter.z;
        return dx * dx + dz * dz < excludeRadius * excludeRadius;
    }

    static Shader LitOrSimple()
    {
        var s = Shader.Find("Universal Render Pipeline/Simple Lit");
        return s != null ? s : Shader.Find("Universal Render Pipeline/Lit");
    }

    static Material Flat(Color color, bool doubleSided)
    {
        var mat = new Material(LitOrSimple());
        mat.color = color;
        mat.enableInstancing = true;
        if (doubleSided) mat.doubleSidedGI = true;
        return mat;
    }

    /// <summary>
    /// Пояс деревьев (s10d, чистая функция): хвойные выше/суше — вес растёт от
    /// +1м над водой до полного к +4м; низина идёт старым k%3 без сдвига
    /// (раскладки на тех же сидах не плывут). Детерминировано хэшем (seed,500,k).
    /// </summary>
    public static FloraKind TreeKindFor(long seed, int k, float heightAboveWater)
    {
        float spruceW = Mathf.Clamp01((heightAboveWater - 1f) / 3f);
        float roll = SeededHash.ToFloat01(SeededHash.Hash(seed, 500, k));
        if (roll < spruceW) return FloraKind.SpruceTree;
        return (FloraKind)(k % 3);
    }

    public void Build()
    {
        if (world == null || flora == null) throw new System.ArgumentNullException("world/flora конфиги");
        Clear();
        trunkMat = Flat(new Color(0.35f, 0.25f, 0.18f), false);
        leafMat = Flat(new Color(0.30f, 0.45f, 0.25f), false);
        rockMat = Flat(new Color(0.50f, 0.50f, 0.50f), false);
        grassMat = Flat(new Color(0.55f, 0.60f, 0.30f), true);
        FloraRoot = new GameObject("Flora");
        FloraRoot.transform.SetParent(transform, false);
        TrunkColliders = 0;

        var trees = FloraScatter.Scatter(world.seed, 11, flora.treeCount, world.mapHalfExtent, flora.minDistance, world);
        int k = 0;
        foreach (var p in trees)
        {
            if (Excluded(p.pos)) continue;
            var kind = TreeKindFor(world.seed, k, p.pos.y - world.waterLevel);
            k++;
            if (kind == FloraKind.PikeTree) Tree(p, 0.14f, 0.09f, 2.6f, 1.1f);
            else if (kind == FloraKind.UmbrellaTree) Tree(p, 0.16f, 0.10f, 1.8f, 1.5f);
            else if (kind == FloraKind.SpruceTree) Spruce(p);
            else DeadHook(p);
        }
        var rocks = FloraScatter.Scatter(world.seed, 22, flora.rockCount, world.mapHalfExtent, flora.minDistance, world);
        k = 0;
        foreach (var p in rocks)
        {
            if (Excluded(p.pos)) continue;
            if ((k++ % 2) == 0) Rock(p, true);
            else Rock(p, false);
        }
        Mesh bushMesh = Kit("bush", () => FloraMeshKit.Bush(0.7f));
        foreach (var p in FloraScatter.Scatter(world.seed, 33, flora.bushCount, world.mapHalfExtent, flora.minDistance, world))
        {
            if (Excluded(p.pos)) continue;
            Part(bushMesh, leafMat, p, false, null);
        }
        Mesh blade = Kit("blade", () => FloraMeshKit.GrassBlade(0.12f, 0.7f));
        foreach (var p in FloraScatter.Scatter(world.seed, 44, flora.grassCount, world.mapHalfExtent, flora.minDistance, world))
        {
            if (Excluded(p.pos)) continue;
            Part(blade, grassMat, p, false, null);
        }
        Mesh fern = Kit("fern", () => FloraMeshKit.Fern(0.8f, 0.5f, 0.08f));
        foreach (var p in FloraScatter.ScatterBelt(world.seed, 55, flora.fernCount, world.mapHalfExtent, flora.minDistance, world, 1f))
        {
            if (Excluded(p.pos)) continue;
            Part(fern, grassMat, p, false, null); // двусторонний лист держит односторонние вайи
        }
    }

    void Spruce(FloraScatter.FloraPoint p)
    {
        const float h = 4.5f;
        Mesh trunk = Kit("spruceTrunk", () => FloraMeshKit.Trunk(0.11f, 0.05f, h));
        Mesh crownMesh = Kit("spruceCrown", () => FloraMeshKit.SpruceCrown(1f, 3f));
        Part(trunk, trunkMat, p, true,
            go => { var cap = go.AddComponent<CapsuleCollider>(); cap.center = new Vector3(0, h * 0.5f, 0); cap.height = h; cap.radius = 0.11f; });
        var crown = Part(crownMesh, leafMat, p, false, null);
        crown.transform.localPosition += new Vector3(0, h * 0.55f, 0);
        TrunkColliders++;
    }

    void Tree(FloraScatter.FloraPoint p, float r0, float r1, float h, float crownR)
    {
        Mesh trunk = Kit($"trunk{r0}/{r1}/{h}", () => FloraMeshKit.Trunk(r0, r1, h));
        Mesh crownMesh = Kit($"crown{crownR}", () => FloraMeshKit.Crown(crownR));
        var trunkGo = Part(trunk, trunkMat, p, true,
            go => { var cap = go.AddComponent<CapsuleCollider>(); cap.center = new Vector3(0, h * 0.5f, 0); cap.height = h; cap.radius = r0; });
        var crown = Part(crownMesh, leafMat, p, false, null);
        crown.transform.localPosition += new Vector3(0, h, 0);
        TrunkColliders++;
    }

    void DeadHook(FloraScatter.FloraPoint p)
    {
        Part(Kit("hook", () => FloraMeshKit.Trunk(0.10f, 0.03f, 2.2f)), trunkMat, p, false, null);
    }

    void Rock(FloraScatter.FloraPoint p, bool slab)
    {
        if (slab) Part(Kit("slab", () => FloraMeshKit.Slab(0.9f, 0.5f, 0.7f)), rockMat, p, true,
            go => { var box = go.AddComponent<BoxCollider>(); box.size = new Vector3(1.8f, 0.5f, 1.4f); box.center = new Vector3(0, 0.25f, 0); });
        else Part(Kit("spike", () => FloraMeshKit.Spike(0.5f, 2.4f)), rockMat, p, true,
            go => { go.AddComponent<MeshCollider>(); });
    }

    GameObject Part(Mesh mesh, Material mat, FloraScatter.FloraPoint p, bool shadows, System.Action<GameObject> tune)
    {
        var go = new GameObject("FloraPart");
        go.transform.SetParent(FloraRoot.transform, false);
        go.transform.position = p.pos;
        go.transform.rotation = Quaternion.Euler(0f, p.rotY, 0f);
        go.transform.localScale = Vector3.one * p.scale;
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = shadows
            ? UnityEngine.Rendering.ShadowCastingMode.On
            : UnityEngine.Rendering.ShadowCastingMode.Off;
        tune?.Invoke(go);
        return go;
    }

    public void Clear()
    {
        if (FloraRoot != null)
        {
            if (Application.isPlaying) Destroy(FloraRoot);
            else DestroyImmediate(FloraRoot);
            FloraRoot = null;
        }
        foreach (var mat in new[] { trunkMat, leafMat, rockMat, grassMat })
            if (mat != null)
            {
                if (Application.isPlaying) Destroy(mat);
                else DestroyImmediate(mat);
            }
        trunkMat = leafMat = rockMat = grassMat = null;
        foreach (var mesh in meshCache.Values)
            if (mesh != null)
            {
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
            }
        meshCache.Clear();
        TrunkColliders = 0;
    }

    // Энтрипойнты превью для песочницы (террейн + флора командой, снос — тоже).
    public static void BuildPreview(string seedText, string halfExtentText)
    {
        long seed = 1337;
        float half = 35f;
        long.TryParse(seedText, out seed);
        float.TryParse(halfExtentText, out half);
        if (half <= 0f) half = 35f;
        var ground = GameObject.Find("~ForestTerrain");
        if (ground == null) return;
        var placer = ground.GetComponent<FloraPlacer>();
        if (placer == null) placer = ground.AddComponent<FloraPlacer>();
        var world = ScriptableObject.CreateInstance<WorldGenConfigSO>();
        world.seed = seed;
        world.mapHalfExtent = half;
        var flora = ScriptableObject.CreateInstance<FloraConfigSO>();
        placer.world = world;
        placer.flora = flora;
        placer.Build();
        Debug.Log($"[Forest] флора построена (сид {seed})");
    }

    public static void WipePreview()
    {
        var ground = GameObject.Find("~ForestTerrain");
        if (ground == null) return;
        var placer = ground.GetComponent<FloraPlacer>();
        if (placer != null) placer.Clear();
    }
}
