using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Бункер в мире: deterministic spot (угол/дистанция от сида, 20–40м), y по рельефу,
/// лицом к центру карты. Стены/пилоны/дверь/обломки — BoxCollider'ы (блокеры в бейк),
/// ниша/мох/веха — без. Материалы shared (бетон/тёмный/ниша/мох/ржавчина).
/// Лес, слайс s9.
/// </summary>
public class BunkerPlacer : MonoBehaviour
{
    public WorldGenConfigSO world;
    public long seed = 1337;

    public Vector3 BunkerCenter { get; private set; }
    public Vector3 PortalPos { get; private set; }
    public GameObject BunkerRoot { get; private set; }

    readonly List<Material> mats = new List<Material>();

    static Material Flat(Color color)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        mat.enableInstancing = true;
        return mat;
    }

    public void Build()
    {
        if (world == null) throw new System.ArgumentNullException(nameof(world));
        Clear();
        var concrete = Flat(new Color(0.43f, 0.45f, 0.47f));
        var dark = Flat(new Color(0.33f, 0.35f, 0.37f));
        var void_ = Flat(new Color(0.14f, 0.15f, 0.16f));
        var moss = Flat(new Color(0.35f, 0.42f, 0.23f));
        var rust = Flat(new Color(0.54f, 0.29f, 0.17f));
        mats.AddRange(new[] { concrete, dark, void_, moss, rust });

        float angle = SeededHash.ToFloat01(SeededHash.Hash(seed, 200, 1)) * Mathf.PI * 2f;
        float dist = 20f + SeededHash.ToFloat01(SeededHash.Hash(seed, 200, 2)) * 20f;
        Vector3 center = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
        center.y = WorldHeightField.SampleHeight(world, center.x, center.z);
        float yaw = Mathf.Atan2(-center.x, -center.z) * Mathf.Rad2Deg;
        BunkerCenter = center;
        PortalPos = center + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, 2f);

        BunkerRoot = new GameObject("Bunker");
        BunkerRoot.transform.SetParent(transform, false);
        BunkerRoot.transform.position = center;
        BunkerRoot.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        var cache = new Dictionary<string, Mesh>();
        foreach (var part in BunkerKit.Complex(seed))
        {
            string key = part.mesh + "|" + part.size;
            if (!cache.TryGetValue(key, out var mesh))
            {
                mesh = part.mesh == BunkerKit.PartMesh.Trunk
                    ? FloraMeshKit.Trunk(part.size.x, part.size.y, part.size.z)
                    : FloraMeshKit.Slab(part.size.x, part.size.y, part.size.z);
                cache[key] = mesh;
            }
            var go = new GameObject("BunkerPart");
            go.transform.SetParent(BunkerRoot.transform, false);
            go.transform.localPosition = part.pos;
            go.transform.localRotation = Quaternion.Euler(part.euler);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = part.mat == BunkerKit.PartMat.Dark ? dark
                : part.mat == BunkerKit.PartMat.Void ? void_
                : part.mat == BunkerKit.PartMat.Moss ? moss
                : part.mat == BunkerKit.PartMat.Rust ? rust : concrete;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            if (part.collider)
            {
                var box = go.AddComponent<BoxCollider>();
                box.size = part.size;
                box.center = new Vector3(0f, part.size.y * 0.5f, 0f);
            }
        }
    }

    public void Clear()
    {
        if (BunkerRoot != null)
        {
            if (Application.isPlaying) Destroy(BunkerRoot);
            else DestroyImmediate(BunkerRoot);
            BunkerRoot = null;
        }
        foreach (var mat in mats)
            if (mat != null)
            {
                if (Application.isPlaying) Destroy(mat);
                else DestroyImmediate(mat);
            }
        mats.Clear();
    }

    // Энтрипойнты витрины: построить/снести бункер в песочнице.
    public static void BuildPreview(string seedText)
    {
        long seed = 1337;
        long.TryParse(seedText, out seed);
        var ground = GameObject.Find("~ForestTerrain");
        if (ground == null) return;
        var placer = ground.GetComponent<BunkerPlacer>();
        if (placer == null) placer = ground.AddComponent<BunkerPlacer>();
        var world = ScriptableObject.CreateInstance<WorldGenConfigSO>();
        world.seed = seed;
        world.amplitude = 7f;
        world.baseFrequency = 0.03f;
        placer.world = world;
        placer.seed = seed;
        placer.Build();
        Debug.Log($"[Forest] бункер построен (сид {seed}, центр {placer.BunkerCenter})");
    }

    public static void WipePreview()
    {
        var ground = GameObject.Find("~ForestTerrain");
        if (ground == null) return;
        var placer = ground.GetComponent<BunkerPlacer>();
        if (placer != null) placer.Clear();
    }
}
