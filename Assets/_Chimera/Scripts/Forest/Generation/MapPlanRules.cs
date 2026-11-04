using System;
using System.Collections.Generic;

/// <summary>
/// G7 шаг 1: правила плана, а не его геометрия. Один родитель у ветви,
/// стабильный порядок вариантов, named fallback, requires/forbids не спорят.
/// </summary>
public static class MapPlanRules
{
    /// <summary>Узел с одним вариантом — параметр, а не ветвь. Это законно.</summary>
    public static bool IsParameter(DecisionNode node) =>
        node != null && node.variants != null && node.variants.Count == 1;

    /// <summary>Порядок не зависит от порядка массива: только ordinal по id.</summary>
    public static List<Variant> StableOrder(DecisionNode node)
    {
        var list = new List<Variant>();
        if (node != null && node.variants != null) list.AddRange(node.variants);
        list.Sort((a, b) => string.CompareOrdinal(a != null ? a.id : "", b != null ? b.id : ""));
        return list;
    }

    public static bool Applies(Variant v, IReadOnlyCollection<string> facts)
    {
        if (v == null) return false;
        var set = new HashSet<string>();
        if (facts != null) foreach (string f in facts) if (!string.IsNullOrEmpty(f)) set.Add(f);
        if (v.requires != null)
            foreach (string r in v.requires) if (!set.Contains(r)) return false;
        if (v.forbids != null)
            foreach (string f in v.forbids) if (set.Contains(f)) return false;
        return true;
    }

    /// <summary>null — план законен; иначе первая причина.</summary>
    public static string Validate(MapPlan plan)
    {
        if (plan == null) return "plan=null";
        if (string.IsNullOrEmpty(plan.path)) return "path пуст";
        if (string.IsNullOrEmpty(plan.identity)) return "identity пуст";
        if (plan.nodes == null) return "nodes=null";
        var byId = new Dictionary<string, DecisionNode>();
        foreach (var n in plan.nodes)
        {
            if (n == null) return "node=null";
            if (string.IsNullOrEmpty(n.id)) return "node.id пуст";
            if (byId.ContainsKey(n.id)) return "дубль узла " + n.id;
            byId[n.id] = n;
        }
        foreach (var n in plan.nodes)
        {
            if (!string.IsNullOrEmpty(n.parent) && !byId.ContainsKey(n.parent))
                return $"у узла {n.id} нет родителя {n.parent}";
            if (n.variants == null || n.variants.Count == 0)
                return $"у узла {n.id} нет вариантов";
            var vids = new HashSet<string>();
            foreach (var v in n.variants)
            {
                if (v == null) return $"у узла {n.id} variant=null";
                if (string.IsNullOrEmpty(v.id)) return $"у узла {n.id} variant.id пуст";
                if (!vids.Add(v.id)) return $"у узла {n.id} дубль варианта {v.id}";
                if (Overlaps(v.requires, v.forbids))
                    return $"вариант {n.id}/{v.id}: requires и forbids спорят";
                if (!string.IsNullOrEmpty(v.fallback) && v.fallback != "none" &&
                    v.fallback != "stop" && !vids.Contains(v.fallback) &&
                    !HasVariant(n, v.fallback))
                    return $"вариант {n.id}/{v.id}: fallback {v.fallback} не назван";
            }
            // Один родитель не запрещает несколько детей; цикл — запрещён.
            string cursor = n.parent;
            int guard = 0;
            while (!string.IsNullOrEmpty(cursor) && guard++ <= plan.nodes.Count)
            {
                if (cursor == n.id) return $"цикл родителя у {n.id}";
                cursor = byId.TryGetValue(cursor, out var p) ? p.parent : "";
            }
            if (guard > plan.nodes.Count) return $"цикл родителя у {n.id}";
        }
        return null;
    }

    static bool HasVariant(DecisionNode n, string id)
    {
        if (n == null || n.variants == null) return false;
        foreach (var v in n.variants) if (v != null && v.id == id) return true;
        return false;
    }

    static bool Overlaps(List<string> a, List<string> b)
    {
        if (a == null || b == null || a.Count == 0 || b.Count == 0) return false;
        var set = new HashSet<string>(a);
        foreach (string s in b) if (set.Contains(s)) return true;
        return false;
    }
}
