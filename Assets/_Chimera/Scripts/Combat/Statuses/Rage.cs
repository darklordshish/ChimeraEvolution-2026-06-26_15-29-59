using UnityEngine;

/// <summary>
/// ЯРОСТЬ — статус-бафф (второй житель канала статусов после Stagger): урон и скорость выше,
/// но входящий урон больше (защита проседает). ВЕЧНОСТЬ = САМОПОДДЕРЖКА (M2): флага permanent нет —
/// вервольф продлевает себе каждый кадр, волк — пока дух над порогом (коммит шкалы), лось — берсерком.
/// Потребители сами спрашивают множители: психики — скорость, доставки — урон, Health — входящий.
/// Спецэффекты: морда красится (EmotionTint; у стайных — градусником шкалы).
/// </summary>
public class Rage : MonoBehaviour
{
    [SerializeField] float damageMult = 1.25f;     // исходящий урон
    [SerializeField] float speedMult = 1.15f;      // скорость движения
    [SerializeField] float incomingMult = 1.5f;    // входящий урон — плата за ярость
    [SerializeField] float staminaRegenMult = 1.5f; // ВТОРОЕ ДЫХАНИЕ: разъярённый восстанавливается быстрее

    float until;

    public bool IsEnraged => Time.time < until;
    public float DamageMult => IsEnraged ? damageMult : 1f;
    public float SpeedMult => IsEnraged ? speedMult : 1f;
    public float IncomingMult => IsEnraged ? incomingMult : 1f;
    // дыхалка на ярости (спека витальности): «загнан → ярость → дольше держишься». У ежа это станет
    // прямой связкой с его ПРЕДЕЛОМ — берсерк на грани и есть то, чем он оплачивает клубок и катание
    public float StaminaRegenMult => IsEnraged ? staminaRegenMult : 1f;

    /// <summary>Взбесить на duration. Не укорачивает уже идущую. Холоднокровный ИММУНЕН к ярости
    /// (эмоционально неподвижен — рациональный расчёт).</summary>
    public void Enrage(float duration)
    {
        if (GetComponent<ColdBlooded>() != null) return; // холоднокровный не раскачивается чужим воем
        until = Mathf.Max(until, Time.time + duration);
    }
}
