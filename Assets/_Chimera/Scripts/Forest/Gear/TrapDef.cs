using System;
using System.Collections.Generic;

/// <summary>
/// Силок: данные отложенного пайка. Рвёт/калечит (урон + кровь + замедление),
/// не душит и не держит — это НЕ захват (машина Constrict здесь не участвует).
/// Лес, слайс s4c.
/// </summary>
[Serializable]
public class TrapDef
{
    public string id = "snare";
    public int damage;
    public int bleedStacks;
    public int slowStacks;
    public float radius = 2f;
    public int uses = 1;

    public static List<string> Validate(TrapDef def)
    {
        var issues = new List<string>();
        if (def == null)
        {
            issues.Add("деф ловушки — null");
            return issues;
        }
        if (def.damage < 0) issues.Add($"урон={def.damage} обязан быть >= 0");
        if (def.bleedStacks < 0) issues.Add("кровотечение обязано быть >= 0");
        if (def.slowStacks < 0) issues.Add("замедление обязано быть >= 0");
        if (def.radius <= 0f) issues.Add($"радиус={def.radius} обязан быть > 0");
        if (def.uses <= 0) issues.Add($"заряды={def.uses} обязаны быть > 0");
        return issues;
    }
}
