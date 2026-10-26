using System.Collections.Generic;

/// <summary>
/// B0.3: выбор допустимого плана. Пока два проверенных варианта: fixed и mirror
/// (хирургия и клетки меняются сторонами). Всё, что не проходит правила, — fixed.
/// </summary>
public static class LabPlanGenerator
{
    public static LabPlan Generate(long seed, int version, LabEntrance entrance, LabStyle style)
    {
        var plan = LabPlanFixed.Create();
        plan.version = version;
        plan.template = "fixed";

        int roll = (int)(SeededHash.Hash(seed, 8301, version) % 2);
        if (roll == 1)
        {
            plan.template = "mirror";
            Mirror(plan);
        }

        var errors = LabPlanRules.Validate(plan);
        if (errors.Count > 0)
        {
            var fallback = LabPlanFixed.Create();
            fallback.version = version;
            fallback.fallback = true;
            fallback.note = "fallback: " + string.Join("; ", errors);
            return fallback;
        }
        return plan;
    }

    static void Mirror(LabPlan plan)
    {
        for (int i = 0; i < plan.rooms.Count; i++)
        {
            var r = plan.rooms[i];
            if (r.function == LabFunction.Surgery || r.function == LabFunction.Cells)
            {
                r.x = -r.x;
                plan.rooms[i] = r;
            }
        }
        for (int i = 0; i < plan.doors.Count; i++)
        {
            var d = plan.doors[i];
            if (d.from == "hall" && (d.to == "surgery" || d.to == "cells"))
            {
                d.x = -d.x;
                plan.doors[i] = d;
            }
        }
    }
}
