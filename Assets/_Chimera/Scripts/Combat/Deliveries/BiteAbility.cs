using UnityEngine;

/// <summary>
/// Доставка «укус»: фронтальный конус с замахом; отменяется уворотом из зоны/конуса (и стаггером).
/// Эффекты на попадании — урон + опц. вампиризм + опц. сбив регена (словарь Hit).
/// Дефолты = волк; вервольф ставит свои числа на префабе (вампиризм). Одна доставка — разные носители.
/// </summary>
public class BiteAbility : WindupAbility
{
    [Header("Укус")]
    [SerializeField] float range = 2.0f;
    [SerializeField] float halfAngle = 55f;
    [SerializeField] int damage = 8;
    [SerializeField] int lifeSteal = 0;                      // вервольф лечится укусом (может уйти в temp HP)
    [SerializeField, Range(0f, 1f)] float regenDebuff = 1f;  // <1 — сбивает реген цели
    [SerializeField] float regenDebuffTime = 0f;
    [SerializeField] int venomStacks = 0;                   // >0 — укус впрыскивает яд (змея)
    [SerializeField] int bleedStacks = 0;                   // >0 — укус пускает кровь (волчьи клыки)

    public float Range => range;         // психика читает для решений (дистанция атаки/удержания)
    public float HalfAngle => halfAngle; // и для прицельного конуса

    protected override float GizmoRange => range;         // хитбокс = реальный конус укуса
    protected override float GizmoHalfAngle => halfAngle;

    // тело-на-шасси (CreatureBody, NPC-режим) кормит урон И ЭФФЕКТЫ укуса из органов (data-driven, как у игрока)
    public void SetDamage(int v) => damage = v;
    public void SetVenom(int v) => venomStacks = v;
    public void SetBleed(int v) => bleedStacks = v;

    protected override Color TelegraphColor => TelegraphColors.Bite;

    protected override AbilityRun OnTick()
    {
        float dist = DistToTarget();
        bool inCone = Vector3.Angle(transform.forward, DirToTarget()) <= halfAngle;
        if (!(dist <= range && inCone)) return AbilityRun.Cancelled; // увернулся — замах сорван

        if (Time.time >= windupEnd)
        {
            Payload().Deliver(new Hit(ownHealth, transform.position), targetHealth, DamageMult);
            return AbilityRun.Done;
        }

        SettleInPlace();
        return AbilityRun.Running;
    }

    /// <summary>Паёк укуса — ЧИСЛА ИЗ ОРГАНА (яд/кровь/вампиризм тело кормит через SetVenom/SetBleed/SetDamage).
    /// Один источник правды: и замаховый укус, и укус без замаха (BiteNow) льют одно и то же.</summary>
    public MeleeBlow Payload() => new()
    {
        Damage = damage, LifeSteal = lifeSteal, VenomStacks = venomStacks, BleedStacks = bleedStacks,
        RegenDebuffFactor = regenDebuff, RegenDebuffTime = regenDebuffTime,
    };

    /// <summary>Укусить цель ПРЯМО СЕЙЧАС, без замаха и конуса: змея в хвате грызёт то, что держит
    /// (реальные констрикторы держат зубами). Яд/кровь — из органа, как в обычном укусе.</summary>
    public void BiteNow(Health victim)
    {
        if (victim == null) return;
        Payload().Deliver(new Hit(ownHealth, transform.position), victim, DamageMult);
    }
}
