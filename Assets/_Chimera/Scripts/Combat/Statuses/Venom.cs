using Chimera.Laws;
using UnityEngine;

/// <summary>
/// ЯД — накопительный статус-эффект (эскалирующий по стакам; тот же паттерн, что будущий обхват).
/// Стаки набегают от укусов (`HitEffect.Venom`), выцветают за stackDuration без нового укуса.
/// Пороги: 1 стак — стоп реген; 2 — уязвимость (входящий урон ↑, читает `Health.TakeDamage`); 3 — тик урона (DoT).
/// До-создаётся на цели в рантайме при первом укусе. Числа — `LawsConfig.venom` (свойство ЯДА, не жертвы), шаги — `StatusLaw`.
/// </summary>
public class Venom : MonoBehaviour
{
    StackState state;
    Health health;
    Health source; // кто отравил: смерть от DoT — на счету отравителя (родство убийце)

    public int Stacks => StatusLaw.Stacks(state, Time.time);
    public float IncomingMult => StatusLaw.VenomIncoming(Stacks, LawsConfig.Current.venom); // читает Health.TakeDamage

    void Awake() => TryGetComponent(out health);

    VenomResist resist; // ЯДОУПОРНОСТЬ (сердце ежа): не блокирует стак, а укорачивает его жизнь

    public void AddStack()
    {
        if (resist == null) TryGetComponent(out resist); // ленивая привязка: маркер вешает тело в Recompute
        var p = LawsConfig.Current.venom;
        // у ядоупорного токсин разлагается почти сразу: укус проходит, но яд НЕ НАКАПЛИВАЕТСЯ —
        // до уязвимости и DoT нужно попасть несколько раз за доли секунды
        StatusLaw.AddStack(ref state, Time.time, p.MaxStacks, p.StackDuration, resist != null ? resist.DurationMult : 1f);
    }

    public void SetSource(Health s) => source = s;

    void Update()
    {
        if (Stacks <= 0 || health == null) return;
        health.SuppressRegen(0f, 0.3f);       // стадия 1: реген стоит, пока отравлен (пуш через существующий API)
        int dot = StatusLaw.VenomStep(ref state, Time.time, LawsConfig.Current.venom); // стадия 3: DoT
        if (dot > 0)
        {
            if (source != null) health.LastAttacker = source; // смерть от яда — убийство отравителя
            health.TakeDamage(dot, true); // яд минует i-frames — от того, что уже внутри, не увернёшься
        }
    }
}
