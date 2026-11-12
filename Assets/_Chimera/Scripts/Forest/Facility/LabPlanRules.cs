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
        ValidateDoorGeometry(errors, plan, ids);
        ValidateDepth(errors, plan);
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
        if (plan.version >= 2)
        {
            RequireRange(errors, id, r, w, d);
            return;
        }
        if (Mathf.Abs(r.width - w) > 0.01f || Mathf.Abs(r.depth - d) > 0.01f)
            errors.Add($"комната {id}: {r.width}×{r.depth}, ожидалось {w}×{d}");
    }

    static void RequireRange(List<string> errors, string id, LabRoom r, float w, float d)
    {
        float minW, maxW, minD, maxD;
        switch (id)
        {
            case "hall": minW = 12f; maxW = 16f; minD = 12f; maxD = 16f; break;
            case "arena": minW = 40f; maxW = 46f; minD = 6f; maxD = 10f; break;
            case "surgery":
            case "cells": minW = 10f; maxW = 16f; minD = 14f; maxD = 22f; break;
            case "rest":
            case "lobby": minW = 16f; maxW = 30f; minD = 6f; maxD = 10f; break;
            default: minW = w; maxW = w; minD = d; maxD = d; break;
        }
        if (r.width < minW - 0.01f || r.width > maxW + 0.01f ||
            r.depth < minD - 0.01f || r.depth > maxD + 0.01f)
            errors.Add($"комната {id}: {r.width}×{r.depth} вне диапазона v2");
    }

    static void Connected(List<string> errors, LabPlan plan, string a, string b)
    {
        foreach (var d in plan.doors)
            if ((d.from == a && d.to == b) || (d.from == b && d.to == a)) return;
        errors.Add($"нет двери {a}-{b}");
    }

    const float DoorWallGap = 3.2f;

    static void ValidateDoorGeometry(List<string> errors, LabPlan plan, HashSet<string> ids)
    {
        foreach (var d in plan.doors)
        {
            if (!ids.Contains(d.from) || !ids.Contains(d.to)) continue;
            var a = plan.Room(d.from);
            var b = plan.Room(d.to);

            if (d.alongX)
            {
                float overlap = Mathf.Min(a.x + a.width * 0.5f, b.x + b.width * 0.5f) -
                                Mathf.Max(a.x - a.width * 0.5f, b.x - b.width * 0.5f);
                if (overlap + 0.01f < d.width)
                    errors.Add($"дверь {d.from}-{d.to}: проём шире общей стены");
                float gap = Min4(
                    Mathf.Abs(d.z - (a.z - a.depth * 0.5f)),
                    Mathf.Abs(d.z - (a.z + a.depth * 0.5f)),
                    Mathf.Abs(d.z - (b.z - b.depth * 0.5f)),
                    Mathf.Abs(d.z - (b.z + b.depth * 0.5f)));
                if (gap > DoorWallGap)
                    errors.Add($"дверь {d.from}-{d.to}: не на общей стене (зазор {gap:F1} м)");
            }
            else
            {
                float overlap = Mathf.Min(a.z + a.depth * 0.5f, b.z + b.depth * 0.5f) -
                                Mathf.Max(a.z - a.depth * 0.5f, b.z - b.depth * 0.5f);
                if (overlap + 0.01f < d.width)
                    errors.Add($"дверь {d.from}-{d.to}: проём шире общей стены");
                float gap = Min4(
                    Mathf.Abs(d.x - (a.x - a.width * 0.5f)),
                    Mathf.Abs(d.x - (a.x + a.width * 0.5f)),
                    Mathf.Abs(d.x - (b.x - b.width * 0.5f)),
                    Mathf.Abs(d.x - (b.x + b.width * 0.5f)));
                if (gap > DoorWallGap)
                    errors.Add($"дверь {d.from}-{d.to}: не на общей стене (зазор {gap:F1} м)");
            }
        }
    }

    static void ValidateDepth(List<string> errors, LabPlan plan)
    {
        var depth = new Dictionary<string, int>();
        depth["lobby"] = 0;
        var queue = new Queue<string>();
        queue.Enqueue("lobby");
        while (queue.Count > 0)
        {
            string id = queue.Dequeue();
            foreach (var d in plan.doors)
            {
                string next = d.from == id ? d.to : d.to == id ? d.from : null;
                if (next == null || depth.ContainsKey(next)) continue;
                depth[next] = depth[id] + 1;
                queue.Enqueue(next);
            }
        }

        foreach (var id in new[] { "hall", "arena", "surgery", "cells", "rest" })
            if (!depth.ContainsKey(id))
                errors.Add($"нет пути от лобби до {id}");

        if (depth.ContainsKey("arena") && depth["arena"] > 2)
            errors.Add("арена глубже двух дверей от лобби");
        if (depth.ContainsKey("surgery") && depth["surgery"] > 2)
            errors.Add("хирургия глубже двух дверей от лобби");
        if (depth.ContainsKey("cells") && depth["cells"] > 2)
            errors.Add("клетки глубже двух дверей от лобби");

        if (!depth.ContainsKey("hall")) return;
        var hallDepth = DepthsFrom(plan, "hall");
        float sum = 0f;
        int n = 0;
        foreach (var r in plan.rooms)
        {
            if (r.id == "hall" || !hallDepth.ContainsKey(r.id)) continue;
            sum += hallDepth[r.id];
            n++;
        }
        if (n > 0 && sum / n > 1.2f + 0.01f)
            errors.Add($"зал не центр: средняя глубина {sum / n:F2} > 1.2");
    }

    static Dictionary<string, int> DepthsFrom(LabPlan plan, string root)
    {
        var depth = new Dictionary<string, int> { [root] = 0 };
        var queue = new Queue<string>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            string id = queue.Dequeue();
            foreach (var d in plan.doors)
            {
                string next = d.from == id ? d.to : d.to == id ? d.from : null;
                if (next == null || depth.ContainsKey(next)) continue;
                depth[next] = depth[id] + 1;
                queue.Enqueue(next);
            }
        }
        return depth;
    }

    static float Min4(float a, float b, float c, float d) =>
        Mathf.Min(Mathf.Min(a, b), Mathf.Min(c, d));
}
