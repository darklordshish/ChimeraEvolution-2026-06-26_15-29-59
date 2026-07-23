using UnityEngine;

/// <summary>
/// СЫТОСТЬ — награда хищнику за убийство: временный бонус-реген HP. Делает экосистему самоподдерживающейся —
/// зверь, выигравший тяжёлый бой, восстанавливается и не остаётся сразу лёгкой добычей (проблема ежа:
/// добил змею на крохах HP → следующая змея доедала его). Начисляет `CreatureBody.CreditKiller` на смерть
/// жертвы: УБИЙЦЕ — полный бонус, стае рядом (делили добычу) — половина.
///
/// В отличие от `Digestion` (физиология змеиного шасси, прерывается уроном — змея уязвима, пока глотает),
/// сытость уроном НЕ прерывается: это «сыт и восстанавливаюсь», а не деликатная трапеза. До-создаётся
/// в рантайме на убийце любого вида (кроме змеи — у неё своё переваривание, чтобы не лечить дважды).
/// </summary>
[RequireComponent(typeof(Health))]
public class Satiety : MonoBehaviour
{
    [SerializeField] float bonusRegen = 4f; // HP/с на ПОЛНОЙ сытости (убийца); помощник — половина
    [SerializeField] float duration = 8f;   // сколько длится сытость после трапезы

    Health health;
    float until, scale, acc;

    public bool IsSated => Time.time < until;

    void Awake() => TryGetComponent(out health);

    /// <summary>Наелся: scale 1 — убийца, 0.5 — помогал в бою. Полный бонус перебивает половинный,
    /// свежая трапеза освежает таймер.</summary>
    public void Feed(float feedScale)
    {
        scale = Mathf.Max(IsSated ? scale : 0f, feedScale);
        until = Time.time + duration;
    }

    void Update()
    {
        if (!IsSated || health == null || health.Current >= health.Max) return;
        acc += bonusRegen * scale * Time.deltaTime;
        if (acc >= 1f) { int h = (int)acc; acc -= h; health.Heal(h); }
    }
}
