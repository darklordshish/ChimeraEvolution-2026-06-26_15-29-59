using System.Collections.Generic;
using UnityEngine;

/// <summary>Правила допустимого плана B0.3. Возвращает ошибки, не бросает.</summary>
public static class LabPlanRules
{
    public static List<string> Validate(LabPlan plan)
    {
        var errors = new List<string>();
        if (plan == null)
        {
            errors.Add("план отсутствует");
            return errors;
        }
        if (Mathf.Abs(plan.footprintX - 46f) > 0.01f ||
            Mathf.Abs(plan.footprintZ - 36f) > 0.01f)
            errors.Add($"пятно {plan.footprintX}×{plan.footprintZ}, нужно 46×36");

        var ids = new HashSet<string>();
        foreach (var r in plan.rooms)
        {
            if (string.IsNullOrEmpty(r.id)) { errors.Add("комната без id"); continue; }
            if (!ids.Add(r.id)) errors.Add($"дубль комнаты {r.id}");
            float hx = r.width * 0.5f, hz = r.depth * 0.5f;
            if (Mathf.Abs(r.x) + hx > plan.footprintX * 0.5f + 0.01f ||
                Mathf.Abs(r.z) + hz > plan.footprintZ * 0.5f + 0.01f)
                errors.Add($"комната {r.id} выходит за пятно");
        }

        Require(errors, plan, "hall", 14f, 14f);
        Require(errors, plan, "arena", 46f, 8f);
        Require(errors, plan, "surgery", 13.5f, 19f);
        Require(errors, plan, "cells", 13.5f, 19f);
        Require(errors, plan, "rest", 20f, 8f);
        Require(errors, plan, "lobby", 26f, 8f);

        for (int i = 0; i < plan.rooms.Count; i++)
            for (int j = i + 1; j < plan.rooms.Count; j++)
            {
                var a = plan.rooms[i];
                var b = plan.rooms[j];
                float ox = (a.width + b.width) * 0.5f - Mathf.Abs(a.x - b.x);
                float oz = (a.depth + b.depth) * 0.5f - Mathf.Abs(a.z - b.z);
                if (ox > 0.2f && oz > 0.2f)
                    errors.Add($"комнаты {a.id} и {b.id} пересекаются");
            }

        foreach (var d in plan.doors)
        {
            if (d.width < 4f - 0.01f) errors.Add($"дверь {d.from}-{d.to}: ширина {d.width}, нужно ≥4");
            if (d.height < 4f - 0.01f) errors.Add($"дверь {d.from}-{d.to}: высота {d.height}, нужно ≥4");
            if (!ids.Contains(d.from) || !ids.Contains(d.to))
                errors.Add($"дверь {d.from}-{d.to}: нет комнаты");
        }

        Connected(errors, plan, "lobby", "hall");
        Connected(errors, plan, "hall", "arena");
        Connected(errors, plan, "hall", "surgery");
        Connected(errors, plan, "hall", "cells");
        Connected(errors, plan, "rest", "lobby");
        return errors;
    }

    static void Require(List<string> errors, LabPlan plan, string id, float w, float d)
    {
        var r = plan.Room(id);
        if (string.IsNullOrEmpty(r.id))
        {
            errors.Add($"нет комнаты {id}");
            return;
        }
        if (Mathf.Abs(r.width - w) > 0.01f || Mathf.Abs(r.depth - d) > 0.01f)
            errors.Add($"комната {id}: {r.width}×{r.depth}, нужно {w}×{d}");
    }

    static void Connected(List<string> errors, LabPlan plan, string a, string b)
    {
        foreach (var d in plan.doors)
            if ((d.from == a && d.to == b) || (d.from == b && d.to == a)) return;
        errors.Add($"нет двери {a}-{b}");
    }
}
