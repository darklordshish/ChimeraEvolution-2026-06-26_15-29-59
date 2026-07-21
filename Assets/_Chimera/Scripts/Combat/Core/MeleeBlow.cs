using UnityEngine;

/// <summary>
/// Паёк ближнего удара органа: ЧТО делает удар (эффекты + числа), записанный ЕДИНОЖДЫ.
/// Драйверы (виндап-психика NPC / мгновенный ввод игрока) сами решают КОГДА и ПО КОМУ, затем
/// зовут Deliver — тело удара одно на всех. Числа — из данных органа: у NPC масштабируются мощью
/// (ярость/дух/разброс) в момент удара, у игрока впечены в бленд CreatureBody (мощь = 1).
/// Ноль аллокаций: Hit/HitEffect — value-структуры; собирай blow локально на замах.
///
/// U1-эталон единого приёма: доставка = этот паёк, драйвер = тонкий (см. спеку edinyj-priyom).
/// </summary>
public struct MeleeBlow
{
    public int Damage;
    public float KnockForce;         // 0 = без откидывания (Massive резистит внутри Knockback.Push)
    public int BleedStacks;          // протыкание — кровь стаками (рога/клыки)
    public int VenomStacks;          // яд стаками (клыки змеи)
    public int LifeSteal;            // вампиризм при попадании (слот «Пасть»)
    public float RegenDebuffFactor;  // в (0;1) — сбить реген цели (укус против сустейна босса); 0 = не трогать
    public float RegenDebuffTime;    // на сколько держится сбив регена
    public float StaggerTime;        // явный сбив (0 = урон сам даст короткий стаггер через onDamaged)

    /// <summary>Применить удар к одной цели. damageMult — динамическая мощь NPC (1 у игрока).</summary>
    public void Deliver(in Hit ctx, Health target, float damageMult = 1f)
    {
        if (target == null) return;
        if (Damage > 0) ctx.Apply(target, HitEffect.Damage(Mathf.RoundToInt(Damage * damageMult)));
        if (LifeSteal > 0) ctx.Apply(target, HitEffect.LifeSteal(LifeSteal));
        if (RegenDebuffFactor > 0f && RegenDebuffFactor < 1f) ctx.Apply(target, HitEffect.RegenDebuff(RegenDebuffFactor, RegenDebuffTime));
        if (KnockForce > 0f) ctx.Apply(target, HitEffect.Knockback(KnockForce));
        for (int i = 0; i < BleedStacks; i++) ctx.Apply(target, HitEffect.Bleed());
        for (int i = 0; i < VenomStacks; i++) ctx.Apply(target, HitEffect.Venom());
        if (StaggerTime > 0f && target.TryGetComponent<Stagger>(out var st)) st.Hitstun(StaggerTime);
    }
}
