using System;
using Unity.AI.Navigation;
using UnityEngine;

/// <summary>
/// Точка подключения павильона — 1:1 как арена (ArenaWalls): висит на пустом объекте
/// в центре мира, сид — сериализованное поле (+ randomSeed-флаг как у арены),
/// размер — поле mapDiameter (аналог arenaSide). s10a: скелет — поля, разрешение сида
/// и лог-фраза; полный Awake-путь (чанки → флора → постройки → бейк) — следующими
/// заходами. Лес, слайс s10a.
/// </summary>
[RequireComponent(typeof(NavMeshSurface))]
public class ForestBuilder : MonoBehaviour
{
    [Header("Сид (виден в Inspector, пишется в лог с инструкцией — как ArenaWalls)")]
    public long seed = 1337;
    public bool randomSeed = true;

    [Header("Размер (аналог arenaSide: один параметр масштабирует всё)")]
    public float mapDiameter = 2000f;

    [Header("Конфиг (необязательно: иначе дефолты DomeGenConfigSO)")]
    public DomeGenConfigSO config;

    public int ResolvedSeed { get; private set; }

    void Awake()
    {
        if (randomSeed)
        {
            seed = Environment.TickCount;
            Debug.Log($"ForestBuilder: случайный сид {seed} (для воспроизведения — выключи randomSeed и впиши его)");
        }
        var cfg = ConfigOrDefault();
        ResolvedSeed = DomeGenRules.ResolveSeed(cfg, (int)seed);
        if (config == null) Destroy(cfg);
    }

    DomeGenConfigSO ConfigOrDefault()
    {
        if (config != null) return config;
        var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
        cfg.seed = seed;
        cfg.randomSeed = false;
        cfg.mapDiameter = mapDiameter;
        return cfg;
    }
}
