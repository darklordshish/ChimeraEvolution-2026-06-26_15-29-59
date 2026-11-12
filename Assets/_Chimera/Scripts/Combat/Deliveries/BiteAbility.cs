using System;
using UnityEngine;

/// <summary>
/// Доставка «укус» NPC: фронтальный конус с замахом; отменяется уворотом из зоны/конуса (и стаггером).
/// ВСЕ ЧИСЛА — ИЗ ЗАПИСИ ОРГАНА (<see cref="BiteData"/>, спека «данные в органах»): тело раскрывает запись надетой
/// Пасти и кормит ею доставку через <see cref="Configure"/>. Своих чисел у доставки нет: нет записи — нет укуса
/// (компонент остаётся, чтобы не рвать ссылку психики, но приём недоступен). У индивида — только модификаторы:
/// ярость, мораль, сытость, разброс особи, боссовость (см. DamageMult).
/// </summary>
public class BiteAbility : WindupAbility, IOrganAbility
{
    BiteData data; // раскрытая запись надетой Пасти; null — укуса нет

    public Type DataType => typeof(BiteData);
    public void Configure(AbilityData d) => data = d as BiteData;
    public bool Available => data != null;
    protected override bool Ready => data != null;
    protected override float WindupTime => data.windupTime;
    protected override float Cooldown => data != null ? data.cooldown : 0f;            // перезарядка укуса — запись Пасти

    // психика читает для решений (дистанция атаки/удержания, прицельный конус); нет укуса — 0: в зону не заманит
    public override float WindowMax => Range;               // окно арсенала: вплотную — до досягаемости записи
    public float Range => data != null ? data.range : 0f;
    public float HalfAngle => data != null ? data.halfAngle : 0f;

    protected override float GizmoRange => Range;         // хитбокс = реальный конус укуса
    protected override float GizmoHalfAngle => HalfAngle;

    protected override Color TelegraphColor => TelegraphColors.Bite;

    protected override AbilityRun OnTick()
    {
        float dist = DistToTarget();
        bool inCone = Vector3.Angle(transform.forward, DirToTarget()) <= data.halfAngle;
        if (!(dist <= data.range && inCone)) return AbilityRun.Cancelled; // увернулся — замах сорван

        if (Time.time >= windupEnd)
        {
            Payload().Deliver(new Hit(ownHealth, transform.position), targetHealth, DamageMult);
            return AbilityRun.Done;
        }

        SettleInPlace();
        return AbilityRun.Running;
    }

    /// <summary>Паёк укуса из записи органа. Один источник: и замаховый укус, и укус без замаха (BiteNow).
    /// Вампиризм — не черта укуса, а модуля боссовости (BossLifeSteal).</summary>
    public MeleeBlow Payload() => new()
    {
        Damage = data.damage, LifeSteal = BossLifeSteal, VenomStacks = data.venomStacks, BleedStacks = data.bleedStacks,
        RegenDebuffFactor = data.regenDebuff, RegenDebuffTime = data.regenDebuffTime,
    };

    /// <summary>Укусить цель ПРЯМО СЕЙЧАС, без замаха и конуса: змея в хвате грызёт то, что держит
    /// (реальные констрикторы держат зубами). Числа — из записи Пасти, как в обычном укусе.</summary>
    public void BiteNow(Health victim)
    {
        if (victim == null || data == null) return; // нет Пасти с укусом — грызть нечем
        Payload().Deliver(new Hit(ownHealth, transform.position), victim, DamageMult);
    }
}
