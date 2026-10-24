using System;
using Unity.AI.Navigation;
using UnityEngine;

/// <summary>
/// Точка подключения павильона — 1:1 как арена (ArenaWalls): висит на пустом объекте
/// в центре мира, сид — сериализованное поле (+ randomSeed-флаг как у арены),
/// размер — поле mapDiameter (аналог arenaSide). Срез A: полная сборка мира в Awake
/// через DomeWorld (чанки, вода, кольцо, устья/гнездо, ясень, корпуса, флора, свод),
/// NavMesh тайлами и постановка игрока во дворе лаборатории.
/// Лес, срез A (24.10).
/// </summary>
[RequireComponent(typeof(NavMeshSurface))]
public class ForestBuilder : MonoBehaviour
{
    [Header("Сид (виден в Inspector, пишется в лог с инструкцией — как ArenaWalls)")]
    public long seed = 1337;
    public bool randomSeed = false;

    [Header("Размер (аналог arenaSide: один параметр масштабирует всё)")]
    public float mapDiameter = 2000f;

    [Header("Рельеф: квадов на чанк (16 — превью-редколесье)")]
    public int terrainQuads = 16;

    [Header("Сборка при старте (выключить — соберёшь Build() вручную)")]
    public bool buildOnAwake = true;

    [Header("Конфиг (необязательно: иначе дефолты DomeGenConfigSO)")]
    public DomeGenConfigSO config;

    [Header("Игрок (необязательно: иначе двор лаборатории)")]
    public Transform playerSpawn;

    public int ResolvedSeed { get; private set; }
    public GameObject BuiltRoot { get; private set; }
    public DomeHydro.State Hydro => world != null ? world.Hydro : null;
    public DomeFacilityLayout.Facility Facility => world != null ? world.Facility : null;
    public DomeAshSite.AshSpot Ash => world != null ? world.Ash : default;
    public LairSite NestSite => world != null ? world.NestSite : null;
    public int Chunks => world != null ? world.Chunks : 0;
    public int Trees => world != null ? world.Trees : 0;
    public Vector3 SpawnPoint { get; private set; }
    public float SampleGround(float x, float z) => world != null ? world.SampleGround(x, z) : 0f;

    DomeWorld world;
    DomeGenConfigSO ownedConfig;
    LairRegistry lairRegistry;
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
        world.Build(BuiltRoot, ownedConfig, terrainQuads, chosen);

        var surface = GetComponent<NavMeshSurface>();
        world.BuildNavMesh(surface);

        SpawnPlayer();

        lairRegistry = GetComponent<LairRegistry>();
        if (lairRegistry == null) lairRegistry = gameObject.AddComponent<LairRegistry>();
        if (world.NestSite != null) lairRegistry.Register(world.NestSite);

        int parts = world.Facility != null ? world.Facility.parts.Count : 0;
        Debug.Log($"[Forest] мир {chosen}: чанков {world.Chunks}, деревьев {world.Trees}, " +
                  $"устий {world.Mouths.Count}, гнёзд {world.Nests.Count}, корпусов {parts}, " +
                  $"гнездо совы {(world.NestSite != null ? "есть" : "нет")}");
    }

    /// <summary>
    /// Игрок — в точку playerSpawn (если задан) или в центр двора лаборатории.
    /// CharacterController гасим на время телепорта: иначе он вернёт позицию.
    /// </summary>
    void SpawnPlayer()
    {
        if (world == null) return;
        Vector3 basePos = playerSpawn != null ? playerSpawn.position : world.SpawnPoint;
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
    /// Снести мир и всё, что он создал. Сериализованные поля (сид, диаметр, конфиг)
    /// не трогаются — иначе повторный Build соберёт не тот мир.
    /// </summary>
    public void Clear()
    {
        var nest = world != null ? world.NestSite : null;
        if (lairRegistry != null && nest != null) lairRegistry.Unregister(nest);

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
        DomeWeather.ResetWet();
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
