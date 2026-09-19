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
            var kind = (FloraKind)(k % 3);
            k++;
            if (kind == FloraKind.PikeTree) Tree(p, 0.14f, 0.09f, 2.6f, 1.1f);
            else if (kind == FloraKind.UmbrellaTree) Tree(p, 0.16f, 0.10f, 1.8f, 1.5f);
            else DeadHook(p);
        }
        var rocks = FloraScatter.Scatter(world.seed, 22, flora.rockCount, world.mapHalfExtent, flora.minDistance, world);
        k = 0;
        foreach (var p in rocks)
        {
            if ((k++ % 2) == 0) Rock(p, true);
            else Rock(p, false);
        }
        foreach (var p in FloraScatter.Scatter(world.seed, 33, flora.bushCount, world.mapHalfExtent, flora.minDistance, world))
            Part(FloraMeshKit.Bush(0.7f), leafMat, p, false, null);
        Mesh blade = FloraMeshKit.GrassBlade(0.12f, 0.7f);
        foreach (var p in FloraScatter.Scatter(world.seed, 44, flora.grassCount, world.mapHalfExtent, flora.minDistance, world))
            Part(blade, grassMat, p, false, null);
    }

    void Tree(FloraScatter.FloraPoint p, float r0, float r1, float h, float crownR)
    {
        var trunk = Part(FloraMeshKit.Trunk(r0, r1, h), trunkMat, p, true,
            go => { var cap = go.AddComponent<CapsuleCollider>(); cap.center = new Vector3(0, h * 0.5f, 0); cap.height = h; cap.radius = r0; });
        var crown = Part(FloraMeshKit.Crown(crownR), leafMat, p, false, null);
        crown.transform.localPosition += new Vector3(0, h, 0);
        TrunkColliders++;
    }

    void DeadHook(FloraScatter.FloraPoint p)
    {
        Part(FloraMeshKit.Trunk(0.10f, 0.03f, 2.2f), trunkMat, p, false, null);
    }

    void Rock(FloraScatter.FloraPoint p, bool slab)
    {
        if (slab) Part(FloraMeshKit.Slab(0.9f, 0.5f, 0.7f), rockMat, p, true,
            go => { var box = go.AddComponent<BoxCollider>(); box.size = new Vector3(1.8f, 0.5f, 1.4f); box.center = new Vector3(0, 0.25f, 0); });
        else Part(FloraMeshKit.Spike(0.5f, 2.4f), rockMat, p, true,
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
