using UnityEngine;

[CreateAssetMenu(menuName = "Chimera/Forest/DomeGenConfig", fileName = "DomeGenConfig")]
public class DomeGenConfigSO : ScriptableObject
{
    [Header("Сид: виден в Inspector, пишется в лог с инструкцией (как ArenaWalls)")]
    public long seed = 1337;
    public bool randomSeed = true;

    [Header("Версия (смена алгоритма = смена version)")]
    public int version = 1;

    [Header("Размер: один параметр масштабирует всё (формулы — DomeGenRules)")]
    public float mapDiameter = 2000f;
    public float chunkSize = 250f;

    [Header("Рельеф: FBM + ridged с маской гор + domain warp")]
    public int octaves = 5;
    public int ridgedOctaves = 5;
    public int warpOctaves = 4;
    public float baseFrequency = 0.02f;
    public float amplitude = 6f;
    public float maxSlopeDegrees = 35f;

    [Header("Вода (s10b): пока ЗАГЛУШКА числом, как в s8")]
    public float waterLevel = -1.5f;

    [Header("Плотности (s10d): цели выводятся формулой, не числом")]
    public float treeDensityPerM2 = 0.03f;
    public float forestFraction = 0.55f;

    [Header("NavMesh: ТОЛЬКО тайлами (моно-бейк на Ø2000 запрещён)")]
    public float navVoxelSize = 0.35f;
    public float navTileSize = 112f;

    [Header("Купол: пик константа, радиусы — от диаметра")]
    public float ringPeakHeight = 100f;
    public float vaultRadiusFactor = 1.4f;

    [Header("Fast-preview (20–45с вместо 70–140с полного бейка)")]
    public bool fastPreview = false;
    public int previewHeightmapSize = 2048;

    void OnValidate()
    {
        mapDiameter = Mathf.Clamp(mapDiameter, 100f, 4000f);
        chunkSize = Mathf.Clamp(chunkSize, 50f, 500f);
        octaves = Mathf.Clamp(octaves, 1, 8);
        ridgedOctaves = Mathf.Clamp(ridgedOctaves, 1, 8);
        warpOctaves = Mathf.Clamp(warpOctaves, 0, 8);
        baseFrequency = Mathf.Max(baseFrequency, 0.0001f);
        amplitude = Mathf.Max(amplitude, 0f);
        maxSlopeDegrees = Mathf.Clamp(maxSlopeDegrees, 1f, 60f);
        treeDensityPerM2 = Mathf.Max(treeDensityPerM2, 0f);
        forestFraction = Mathf.Clamp01(forestFraction);
        navVoxelSize = Mathf.Clamp(navVoxelSize, 0.05f, 1f);
        navTileSize = Mathf.Max(navTileSize, 16f);
        ringPeakHeight = Mathf.Max(ringPeakHeight, 0f);
        vaultRadiusFactor = Mathf.Max(vaultRadiusFactor, 1.01f);
    }
}
