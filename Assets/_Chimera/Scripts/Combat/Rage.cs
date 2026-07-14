using UnityEngine;

/// <summary>
/// ЯРОСТЬ — статус-бафф (второй житель канала статусов после Stagger): урон и скорость выше,
/// но входящий урон больше (защита проседает). Включает бесстрашие — яростные не бегут (мораль).
/// Носители: вервольф — вечная (permanent), вой волка — ближним сородичам, вой вервольфа — всей стае.
/// Потребители сами спрашивают множители: психики — скорость, доставки — урон, Health — входящий.
/// Спецэффекты (VFX/тинт) — потом, хук IsEnraged.
/// </summary>
public class Rage : MonoBehaviour
{
    [SerializeField] bool permanent;               // вервольф: вечная ярость
    [SerializeField] float damageMult = 1.25f;     // исходящий урон
    [SerializeField] float speedMult = 1.15f;      // скорость движения
    [SerializeField] float incomingMult = 1.5f;    // входящий урон — плата за ярость

    float until;

    public bool IsEnraged => permanent || Time.time < until;
    public float DamageMult => IsEnraged ? damageMult : 1f;
    public float SpeedMult => IsEnraged ? speedMult : 1f;
    public float IncomingMult => IsEnraged ? incomingMult : 1f;

    /// <summary>Взбесить на duration (вой). Не укорачивает уже идущую. Холоднокровный ИММУНЕН к ВНЕШНЕЙ ярости
    /// (эмоционально неподвижен — рациональный расчёт). Врождённую вечную (permanent, вервольф) это не трогает.</summary>
    public void Enrage(float duration)
    {
        if (GetComponent<ColdBlooded>() != null) return; // холоднокровный не раскачивается чужим воем
        until = Mathf.Max(until, Time.time + duration);
    }
}
