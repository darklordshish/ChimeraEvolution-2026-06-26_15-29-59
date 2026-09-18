using UnityEngine;

/// <summary>
/// Данные генерации мира леса. Только данные — логика в WorldHeightField/WorldRules
/// (тот же закон, что «тело = данные»: числа живут в ассете, а не в коде).
/// Ассет создаётся в Unity (Create → Chimera → Forest → WorldGenConfig) и лежит
/// в Data/Forest/Worldgen. Лес, слайс s1.
/// </summary>
[CreateAssetMenu(menuName = "Chimera/Forest/WorldGenConfig", fileName = "WorldGenConfig")]
public class WorldGenConfigSO : ScriptableObject
{
    [Header("Сид и версия (смена алгоритма = смена version, сид при этом даёт другую карту)")]
    public long seed = 1337;
    public int version = 1;

    [Header("Рельеф: FBM (частота 1/м, амплитуда ±м)")]
    public int octaves = 4;
    public float baseFrequency = 0.02f;
    public float amplitude = 4f;

    [Header("Карта и проходимость (лимит уклона — под CharacterController/NavMesh)")]
    public float mapHalfExtent = 95f;
    public float maxSlopeDegrees = 35f;

    void OnValidate()
    {
        octaves = Mathf.Clamp(octaves, 1, 8);
        baseFrequency = Mathf.Max(baseFrequency, 0.0001f);
        amplitude = Mathf.Max(amplitude, 0f);
        mapHalfExtent = Mathf.Max(mapHalfExtent, 10f);
        maxSlopeDegrees = Mathf.Clamp(maxSlopeDegrees, 1f, 60f);
    }
}
