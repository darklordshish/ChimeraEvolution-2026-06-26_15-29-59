using System.Collections.Generic;

/// <summary>
/// Валидация дефа предмета. Ловит silent-нули (нулевая досягаемость, множитель меньше 1),
/// факел без света/топлива и двуручный факел. Лес, слайс s4.
/// </summary>
public static class HandItemRules
{
    public static List<string> Validate(HandItemDef def)
    {
        var issues = new List<string>();
        if (def == null)
        {
            issues.Add("деф предмета — null");
            return issues;
        }
        if (def.damage < 0) issues.Add($"урон={def.damage} обязан быть >= 0");
        if (def.knockForce < 0f) issues.Add("толчок обязан быть >= 0");
        if (def.bleedStacks < 0) issues.Add("кровотечение обязано быть >= 0");
        if (def.range <= 0f) issues.Add($"досягаемость={def.range} обязана быть > 0");
        if (def.halfAngle <= 0f || def.halfAngle > 180f)
            issues.Add($"полуугол={def.halfAngle} обязан быть в (0, 180]");
        if (def.staminaCost < 0f) issues.Add("цена стамины обязана быть >= 0");
        if (def.cooldown < 0f) issues.Add("перезарядка обязана быть >= 0");
        if (def.windupTime < 0f) issues.Add("замах обязан быть >= 0");
        if (def.backstabMult < 1f) issues.Add($"множитель засады={def.backstabMult} обязан быть >= 1");
        if (def.durabilityMax < 0) issues.Add("прочность обязана быть >= 0");
        if (def.spoilSeconds < 0f) issues.Add("порча обязана быть >= 0");
        if (def.fuelSeconds < 0f) issues.Add("топливо обязано быть >= 0");
        if (def.kind == HandItemKind.Torch)
        {
            if (def.lightRadius <= 0f) issues.Add("факел обязан светить (lightRadius > 0)");
            if (def.fuelSeconds <= 0f) issues.Add("факел обязан гореть (fuelSeconds > 0)");
            if (def.twoHanded) issues.Add("факел не бывает двуручным");
        }
        return issues;
    }
}
