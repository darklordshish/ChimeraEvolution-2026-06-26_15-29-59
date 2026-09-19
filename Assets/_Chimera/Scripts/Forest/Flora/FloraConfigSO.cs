using UnityEngine;

/// <summary>
/// Плотность растительности. Только данные — семплирует FloraScatter.
/// Ассет — в Data/Forest/Flora (создаётся в Unity). Лес, слайс s8.
/// </summary>
[CreateAssetMenu(menuName = "Chimera/Forest/FloraConfig", fileName = "FloraConfig")]
public class FloraConfigSO : ScriptableObject
{
    [Header("Штук на карту (может выйти меньше — теснота)")]
    public int treeCount = 40;
    public int rockCount = 12;
    public int bushCount = 24;
    public int grassCount = 200;

    [Header("Мин-дистанция между стволами/камнями, м")]
    public float minDistance = 3f;

    void OnValidate()
    {
        treeCount = Mathf.Max(0, treeCount);
        rockCount = Mathf.Max(0, rockCount);
        bushCount = Mathf.Max(0, bushCount);
        grassCount = Mathf.Max(0, grassCount);
        minDistance = Mathf.Max(0.5f, minDistance);
    }
}
