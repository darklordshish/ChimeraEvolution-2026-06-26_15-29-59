using UnityEngine;

/// <summary>
/// Витрина s9 одной командой: террейн 120м (amp 7, freq 0.03) + густая флора
/// (150/30/60/800, exclusion 8м вокруг бункера) + бункер + бейк. В сцену ничего
/// не сохраняется — Wipe сносит всё. Лес, слайс s9.
/// </summary>
public static class Showcase
{
    public static void Build(string seedText)
    {
        long seed = 1337;
        long.TryParse(seedText, out seed);

        var world = ScriptableObject.CreateInstance<WorldGenConfigSO>();
        world.seed = seed;
        world.amplitude = 7f;
        world.baseFrequency = 0.03f;
        world.mapHalfExtent = 60f;

        var ground = GameObject.Find("~ForestTerrain");
        if (ground == null) ground = new GameObject("~ForestTerrain");
        var applier = ground.GetComponent<TerrainApplier>();
        if (applier == null) applier = ground.AddComponent<TerrainApplier>();
        applier.Configure(world, 64, 120f);
        applier.Build();

        var bunker = ground.GetComponent<BunkerPlacer>();
        if (bunker == null) bunker = ground.AddComponent<BunkerPlacer>();
        bunker.world = world;
        bunker.seed = seed;
        bunker.Build();

        var floraCfg = ScriptableObject.CreateInstance<FloraConfigSO>();
        floraCfg.treeCount = 150;
        floraCfg.rockCount = 30;
        floraCfg.bushCount = 60;
        floraCfg.grassCount = 800;
        floraCfg.minDistance = 2f;
        var placer = ground.GetComponent<FloraPlacer>();
        if (placer == null) placer = ground.AddComponent<FloraPlacer>();
        placer.world = world;
        placer.flora = floraCfg;
        placer.excludeCenter = bunker.BunkerCenter;
        placer.excludeRadius = 8f;
        placer.Build();

        applier.BakeNavMesh();
        Debug.Log($"[Forest] витрина построена (сид {seed}): террейн + флора + бункер {bunker.BunkerCenter}");
    }

    public static void Wipe()
    {
        var ground = GameObject.Find("~ForestTerrain");
        if (ground == null) return;
        var placer = ground.GetComponent<FloraPlacer>();
        if (placer != null) placer.Clear();
        var bunker = ground.GetComponent<BunkerPlacer>();
        if (bunker != null) bunker.Clear();
        var applier = ground.GetComponent<TerrainApplier>();
        if (applier != null) applier.Clear();
        Object.DestroyImmediate(ground);
        Debug.Log("[Forest] витрина снесена");
    }
}
