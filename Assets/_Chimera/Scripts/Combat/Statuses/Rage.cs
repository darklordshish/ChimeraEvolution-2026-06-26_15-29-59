using Chimera.Laws;
using UnityEngine;

/// <summary>
/// ЯРОСТЬ — статус-бафф (второй житель канала статусов после Stagger): урон и скорость выше,
/// но входящий урон больше (защита проседает). ВЕЧНОСТЬ = САМОПОДДЕРЖКА (M2): флага permanent нет —
/// босс с вечной яростью получает её бессрочно, волк — пока дух над порогом (коммит шкалы), лось — берсерком.
/// Потребители сами спрашивают множители: психики — скорость, доставки — урон, Health — входящий.
/// Спецэффекты: морда красится (EmotionTint; у стайных — градусником шкалы).
/// </summary>
public class Rage : MonoBehaviour
{
    static RageParams P => LawsConfig.Current.rage; // числа — `LawsConfig.rage`, не поля компонента

    float until;
    // УСИЛЕНИЕ ИЗВНЕ: ярость бывает разной злости. Человечий клич качает её по своей крови (чем больше
    // стаков — тем злее), поэтому сила приходит параметром, а не живёт константой у каждого носителя.
    // Живёт ровно столько же, сколько сама ярость: отгорела — вернулись к паспортным множителям
    float boost = 1f;

    public bool IsEnraged => Time.time < until;
    public float DamageMult => StatusLaw.RageDamage(P, IsEnraged, boost);
    public float SpeedMult => StatusLaw.RageSpeed(P, IsEnraged);
    public float IncomingMult => StatusLaw.RageIncoming(P, IsEnraged);
    // дыхалка на ярости (спека витальности): «загнан → ярость → дольше держишься». У ежа это станет
    // прямой связкой с его ПРЕДЕЛОМ — берсерк на грани и есть то, чем он оплачивает клубок и катание
    public float StaminaRegenMult => StatusLaw.RageStamina(P, IsEnraged);

    /// <summary>Взбесить на duration. Не укорачивает уже идущую. Холоднокровный ИММУНЕН к ярости
    /// (эмоционально неподвижен — рациональный расчёт).</summary>
    public void Enrage(float duration, float strength = 1f)
    {
        if (GetComponent<ColdBlooded>() != null) return; // холоднокровный не раскачивается чужим воем
        // ЗАТУХШАЯ ЯРОСТЬ НЕ ПОМНИТ ПРОШЛОЙ СИЛЫ: иначе один сильный клич навсегда поднимал бы планку
        // всем последующим — включая ярость от воя, к кличу отношения не имеющую
        if (!IsEnraged) boost = 1f;
        boost = Mathf.Max(boost, strength);
        until = Mathf.Max(until, Time.time + duration);
    }
}
