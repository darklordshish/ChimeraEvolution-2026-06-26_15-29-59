using UnityEngine;

/// <summary>
/// СЫТОСТЬ — ЕДИНАЯ механика «поел → бонус-реген HP» (объединила бывшее змеиное «переваривание»: лечение
/// в ядро, поведение — на психику). Делает экосистему самоподдерживающейся: зверь, выигравший тяжёлый бой,
/// восстанавливается и не остаётся сразу лёгкой добычей (проблема ежа: добил змею на крохах → следующая
/// доедала его). Начисляет `CreatureBody.CreditKiller` на смерть жертвы, только ХИЩНИКУ (`eatsMeat`):
/// убийце полный бонус, стае рядом (делили добычу) — половина, глотающему целиком (змея) — двойной.
///
/// Уроном НЕ прерывается: это «сыт и восстанавливаюсь», устойчивое лечение (реакцию на удар ведёт психика).
/// До-создаётся в рантайме на убийце любого хищного вида. Что ДЕЛАТЬ сытым — решает психика (змея прячется
/// на насест, читая IsSated; ёж/волк — ничего особого): тело = физиология, психика = поведение.
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
