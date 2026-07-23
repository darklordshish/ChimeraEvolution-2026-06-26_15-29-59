using UnityEngine;

/// <summary>
/// ЯД — накопительный статус-эффект (эскалирующий по стакам; тот же паттерн, что будущий обхват).
/// Стаки набегают от укусов (`HitEffect.Venom`), выцветают за stackDuration без нового укуса.
/// Пороги: 1 стак — стоп реген; 2 — уязвимость (входящий урон ↑, читает `Health.TakeDamage`); 3 — тик урона (DoT).
/// До-создаётся на цели в рантайме при первом укусе (параметры — свойство ЯДА, не жертвы; дефолты ок).
/// </summary>
public class Venom : MonoBehaviour
{
    [SerializeField] int maxStacks = 3;
    [SerializeField] float stackDuration = 4f;       // сколько живёт стак без нового укуса
    [SerializeField] float vulnerabilityMult = 1.4f; // 2 стака: входящий урон ×
    [SerializeField] int dotDamage = 3;              // 3 стака: урон за тик
    [SerializeField] float dotInterval = 0.6f;

    int stacks;
    float expireAt, nextDot;
    Health health;
    Health source; // кто отравил: смерть от DoT — на счету отравителя (родство убийце)

    public int Stacks => Time.time < expireAt ? stacks : 0;
    public float IncomingMult => Stacks >= 2 ? vulnerabilityMult : 1f; // читает Health.TakeDamage

    void Awake() => TryGetComponent(out health);

    VenomResist resist; // ЯДОУПОРНОСТЬ (сердце ежа): не блокирует стак, а укорачивает его жизнь

    public void AddStack()
    {
        if (resist == null) TryGetComponent(out resist); // ленивая привязка: маркер вешает тело в Recompute
        stacks = Mathf.Min(maxStacks, Stacks + 1);
        // у ядоупорного токсин разлагается почти сразу: укус проходит, но яд НЕ НАКАПЛИВАЕТСЯ —
        // до уязвимости и DoT нужно попасть несколько раз за доли секунды
        expireAt = Time.time + stackDuration * (resist != null ? resist.DurationMult : 1f);
    }

    public void SetSource(Health s) => source = s;

    void Update()
    {
        int s = Stacks;
        if (s <= 0 || health == null) return;
        health.SuppressRegen(0f, 0.3f);       // стадия 1: реген стоит, пока отравлен (пуш через существующий API)
        if (s >= 3 && Time.time >= nextDot)   // стадия 3: DoT
        {
            nextDot = Time.time + dotInterval;
            if (source != null) health.LastAttacker = source; // смерть от яда — убийство отравителя
            health.TakeDamage(dotDamage, true); // яд минует i-frames — от того, что уже внутри, не увернёшься
        }
    }
}
