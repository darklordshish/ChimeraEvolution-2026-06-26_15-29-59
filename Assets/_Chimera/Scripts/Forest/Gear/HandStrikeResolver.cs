/// <summary>
/// Чистый резолвер удара: предмет (+сломан?) + засада снаружи + мощь → HandStrike.
/// Сломанный или отсутствующий предмет — откат к голым. Лес, слайс s4.
/// </summary>
public static class HandStrikeResolver
{
    public static HandStrike Resolve(HandItemDef def, bool broken, bool isBackstab, float power)
    {
        HandItemDef d = (def == null || broken) ? HandItemDef.Bare() : def;
        float mult = power * (isBackstab ? d.backstabMult : 1f);
        return new HandStrike
        {
            damage = d.damage,
            damageMult = mult,
            knockForce = d.knockForce,
            bleedStacks = d.bleedStacks,
            range = d.range,
            halfAngle = d.halfAngle,
            staminaCost = d.staminaCost,
            cooldown = d.cooldown,
            windupTime = d.windupTime
        };
    }
}
