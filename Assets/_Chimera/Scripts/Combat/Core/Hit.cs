using UnityEngine;

/// <summary>
/// Тип эффекта удара. Маленький закрытый набор — то, что УЖЕ есть в бою.
/// Новый (яд/DoT) добавляется одной веткой в Hit.Apply — лакмус здоровья абстракции.
/// </summary>
public enum EffectKind { Damage, LifeSteal, Knockback, RegenDebuff, Stun, Venom, Rage, Bleed, Slow }

/// <summary>
/// Эффект удара как value-тип (ноль аллокаций на удар). Собирается фабриками, применяется через Hit.
/// </summary>
public readonly struct HitEffect
{
    public readonly EffectKind Kind;
    public readonly int Amount;     // Damage / LifeSteal
    public readonly float Force;    // Knockback
    public readonly float Factor;   // RegenDebuff — множитель регена цели
    public readonly float Duration; // RegenDebuff — на сколько

    HitEffect(EffectKind kind, int amount, float force, float factor, float duration)
    { Kind = kind; Amount = amount; Force = force; Factor = factor; Duration = duration; }

    public static HitEffect Damage(int amount) => new(EffectKind.Damage, amount, 0f, 0f, 0f);
    public static HitEffect LifeSteal(int amount) => new(EffectKind.LifeSteal, amount, 0f, 0f, 0f);
    public static HitEffect Knockback(float force) => new(EffectKind.Knockback, 0, force, 0f, 0f);
    public static HitEffect RegenDebuff(float factor, float duration) => new(EffectKind.RegenDebuff, 0, 0f, factor, duration);
    public static HitEffect Stun(float duration) => new(EffectKind.Stun, 0, 0f, 0f, duration);
    public static HitEffect Venom() => new(EffectKind.Venom, 0, 0f, 0f, 0f); // добавляет стак яда цели
    public static HitEffect Rage(float duration) => new(EffectKind.Rage, 0, 0f, 0f, duration); // взбесить извне (холоднокровный иммунен)
    public static HitEffect Bleed() => new(EffectKind.Bleed, 0, 0f, 0f, 0f);                   // добавляет стак кровотечения (клыки)
    public static HitEffect Slow() => new(EffectKind.Slow, 0, 0f, 0f, 0f);                     // добавляет стак замедления (иглы ежа)
}

/// <summary>
/// Контекст удара: кто бьёт (Source лечит LifeSteal) и откуда (Origin даёт направление Knockback).
/// Собери Hit один раз за замах, применяй эффекты к каждой цели через Apply. Общий для игрока и ИИ —
/// в Фазе 2 способности будут нести список HitEffect как данные.
/// </summary>
public readonly struct Hit
{
    public readonly Health Source;
    public readonly Vector3 Origin;

    public Hit(Health source, Vector3 origin) { Source = source; Origin = origin; }

    public void Apply(Health target, HitEffect e)
    {
        if (target == null) return;
        bool byPlayer = Source != null && Source.GetComponent<PlayerController>() != null;
        // dev-призрак: раскрывает НАНЕСЁННОЕ воздействие (не замах в воздух) — так можно выцепить
        // единичную цель; централизовано здесь, чтобы будущие способности игрока подхватили правило сами
        if (Perception.PlayerGhost && byPlayer) Perception.BreakGhost();
        switch (e.Kind)
        {
            case EffectKind.Damage:
                if (Source != null) target.LastAttacker = Source; // атрибуция убийства (родство — убийце)
                // ЭРОЗИЯ ПРИЗНАНИЯ — централизована, как призрак: удар ИГРОКА по кину подтачивает признание
                // сам; драйверам своя обвязка не нужна, будущие приёмы подхватят правило автоматически
                if (byPlayer && CreatureBody.PlayerBody != null) CreatureBody.PlayerBody.NoteHit(target);
                target.TakeDamage(e.Amount);
                break;
            case EffectKind.LifeSteal:
                if (Source != null) Source.Heal(e.Amount);
                break;
            case EffectKind.Knockback:
                if (target.TryGetComponent<Knockback>(out var kb))
                {
                    Vector3 away = target.transform.position - Origin; away.y = 0f;
                    if (away.sqrMagnitude > 0.0001f) kb.Push(away.normalized * e.Force);
                }
                break;
            case EffectKind.RegenDebuff:
                target.SuppressRegen(e.Factor, e.Duration);
                break;
            case EffectKind.Stun: // СТАН — контроль ≥1с (вой и т.п.); короткий стаггер цель даёт себе сама от урона
                if (target.TryGetComponent<Stagger>(out var st)) st.Stun(e.Duration);
                break;
            case EffectKind.Venom: // ЯД — накопительный статус; стак (компонент до-создаётся при первом укусе)
                var venom = target.GetComponent<Venom>() ?? target.gameObject.AddComponent<Venom>();
                venom.AddStack();
                if (Source != null) venom.SetSource(Source); // смерть от DoT — на счету отравителя
                break;
            case EffectKind.Rage: // ЯРОСТЬ извне (вой/феромон); Enrage сам гейтит холоднокровных (иммунны к внешней)
                (target.GetComponent<Rage>() ?? target.gameObject.AddComponent<Rage>()).Enrage(e.Duration);
                break;
            case EffectKind.Bleed: // КРОВОТЕЧЕНИЕ — накопительный статус; стак (компонент до-создаётся при первом порезе)
                var bleed = target.GetComponent<Bleed>() ?? target.gameObject.AddComponent<Bleed>();
                bleed.AddStack();
                if (Source != null) bleed.SetSource(Source); // смерть от кровопотери — на счету источника
                break;
            case EffectKind.Slow: // ЗАМЕДЛЕНИЕ — накопительный статус (иглы ежа тянут вниз); компонент до-создаётся
                (target.GetComponent<Slow>() ?? target.gameObject.AddComponent<Slow>()).AddStack();
                break;
        }
    }

    // применить набор эффектов к одной цели (для Фазы 2 — список из данных способности)
    public void Apply(Health target, HitEffect[] effects)
    {
        if (effects == null) return;
        for (int i = 0; i < effects.Length; i++) Apply(target, effects[i]);
    }
}
