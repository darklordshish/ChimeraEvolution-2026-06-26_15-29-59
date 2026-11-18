using Chimera.Laws;
using UnityEngine;

/// <summary>
/// КРОВОТЕЧЕНИЕ — накопительный статус-эффект (эталон — Venom). Стаки набегают от порезов (`HitEffect.Bleed`,
/// сигнатура волчьих клыков), выцветают за stackDuration без нового пореза. Перевалил ПОРОГ (bleedThreshold) →
/// кровопотеря: урон = % от МАКС HP за тик (масштаб-независимо — и на игроке, и на лосе, и на боссе одинаково
/// больно; «чем больше режут — тем сильнее истекаешь»). Держи кровотечение свежими порезами — иначе стаки спадут.
/// До-создаётся на цели при первом порезе. Числа — `LawsConfig.bleed` (свойство КЛЫКОВ, не жертвы), шаги — `StatusLaw`.
/// </summary>
public class Bleed : MonoBehaviour
{
    StackState state;
    Health health;
    Health source; // кто порезал: смерть от кровопотери — на счету источника (родство убийце)

    public int Stacks => StatusLaw.Stacks(state, Time.time);

    void Awake() => TryGetComponent(out health);

    BleedResist resist; // КРОВЕУПОРНОСТЬ (сердце лося): не блокирует порез, а укорачивает жизнь стака

    public void AddStack()
    {
        if (resist == null) TryGetComponent(out resist); // ленивая привязка (маркер вешает тело в Recompute)
        var p = LawsConfig.Current.bleed;
        // у кровеупорного рана затягивается быстро: порез проходит, но кровь НЕ КОПИТСЯ до порога кровопотери
        StatusLaw.AddStack(ref state, Time.time, p.MaxStacks, p.StackDuration, resist != null ? resist.DurationMult : 1f);
    }

    public void SetSource(Health s) => source = s;

    void Update()
    {
        if (health == null) return;
        int tick = StatusLaw.BleedStep(ref state, Time.time, LawsConfig.Current.bleed, health.Max); // кровопотеря — только за порогом
        if (tick > 0)
        {
            if (source != null) health.LastAttacker = source; // смерть от кровопотери — убийство источника
            health.TakeDamage(tick, true);                    // % HP, минует i-frames
        }
    }
}
