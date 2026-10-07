using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>ШАБЛОН ПЛАНА ТЕЛА (спека `2026-10-07-globalnyj-sloj-shablon-plana.md` §2–§3): общий набор мышечных групп у всех
/// видов одного плана. Состав групп и кости, на которых они сидят, — правило плана; доли группы (узел `rel` с полем
/// `group`) — числа вида. Глобальный слой смешивает числа одноимённых групп, а между планами — по таблице `PlanTable`.
///
/// Файлы — поставка модельной линии, `Docs/models/handoff/`:
/// <code>
/// plan-dvunogij.json   { "plan": "двуногий", "groups": [ { "name": "дельта.перед", "limb": "перед", "endMark": "локоть" }, … ] }
/// plan-dvunogij__chetveronogij.json
///                      { "a": "двуногий", "b": "четвероногий", "pairs": [ { "a": "дельта.перед", "b": "плечевая.перед" },
///                                                                        { "a": "ягодица", "b": "" } ] }
/// </code>
/// `endMark` — метка конца кости, на которой сидит группа (`локоть` — плечо, `запястье` — предплечье); пусто — любая кость
/// цепи `limb` (хребет без меток). Пустая сторона пары — группа без пары: растёт из нуля или гаснет по ступени.</summary>
[System.Serializable]
public class PlanTemplate
{
    public string plan;
    public PlanGroup[] groups;

    public static PlanTemplate Parse(string json) => JsonUtility.FromJson<PlanTemplate>(json);

    /// <summary>Нарушения шаблона у вида: каждая группа — ровно один узел, узел — в долях (`rel`), на кости своей цепи
    /// и своего сегмента; групп вне шаблона нет (отличительное вида — черты, без `group`).</summary>
    public List<string> Check(SpeciesSO sp)
    {
        var bad = new List<string>();
        if (sp?.bones == null || groups == null) return bad;
        var by = new Dictionary<string, Bone>();
        foreach (var b in sp.bones) if (b != null && !by.ContainsKey(b.name)) by[b.name] = b;
        var known = new HashSet<string>(groups.Select(g => g.name));
        foreach (var g in groups)
        {
            var nodes = sp.bones.Where(b => b.group == g.name).ToList();
            if (nodes.Count != 1) { bad.Add($"{sp.speciesName}: группа «{g.name}» — узлов {nodes.Count}, нужен один"); continue; }
            var n = nodes[0];
            if (n.rel == null) bad.Add($"{sp.speciesName}: группа «{g.name}» ({n.name}) не в долях — нет `rel`");
            if (n.parent == null || !by.TryGetValue(n.parent, out var host)) { bad.Add($"{sp.speciesName}: группа «{g.name}» ({n.name}) без кости"); continue; }
            if (host.limb != g.limb) bad.Add($"{sp.speciesName}: группа «{g.name}» на цепи «{host.limb}», по шаблону «{g.limb}»");
            if (!string.IsNullOrEmpty(g.endMark) && host.mark?.b != g.endMark)
                bad.Add($"{sp.speciesName}: группа «{g.name}» на кости «{host.name}» (конец «{host.mark?.b}»), по шаблону — кончающейся «{g.endMark}»");
        }
        foreach (var b in sp.bones.Where(b => !string.IsNullOrEmpty(b.group) && !known.Contains(b.group)))
            bad.Add($"{sp.speciesName}: узел «{b.name}» — группа «{b.group}», которой нет в шаблоне «{plan}»");
        return bad;
    }
}

[System.Serializable]
public class PlanGroup
{
    public string name, limb, endMark;
}

/// <summary>Таблица соответствия групп двух планов (оборотень — «двуногий ↔ четвероногий»). Конечная и авторская.</summary>
[System.Serializable]
public class PlanTable
{
    public string a, b;
    public GroupPair[] pairs;

    public static PlanTable Parse(string json) => JsonUtility.FromJson<PlanTable>(json);

    /// <summary>Нарушения таблицы: планы те, имена из своих шаблонов, каждая группа не больше одной пары; пары
    /// «пусто — пусто» нет. Группа шаблона без строки — тоже нарушение: пропуск должен быть явным.</summary>
    public List<string> Check(PlanTemplate ta, PlanTemplate tb)
    {
        var bad = new List<string>();
        if (ta?.plan != a || tb?.plan != b) { bad.Add($"таблица «{a} ↔ {b}» сверена не с теми шаблонами"); return bad; }
        var na = new HashSet<string>(ta.groups.Select(g => g.name));
        var nb = new HashSet<string>(tb.groups.Select(g => g.name));
        var seenA = new HashSet<string>(); var seenB = new HashSet<string>();
        foreach (var p in pairs ?? new GroupPair[0])
        {
            bool ea = string.IsNullOrEmpty(p.a), eb = string.IsNullOrEmpty(p.b);
            if (ea && eb) { bad.Add("пара «пусто — пусто»"); continue; }
            if (!ea && !na.Contains(p.a)) bad.Add($"«{p.a}» нет в шаблоне «{a}»");
            if (!eb && !nb.Contains(p.b)) bad.Add($"«{p.b}» нет в шаблоне «{b}»");
            if (!ea && !seenA.Add(p.a)) bad.Add($"«{p.a}» в таблице дважды");
            if (!eb && !seenB.Add(p.b)) bad.Add($"«{p.b}» в таблице дважды");
        }
        foreach (var g in na.Except(seenA)) bad.Add($"группа «{g}» плана «{a}» не упомянута в таблице");
        foreach (var g in nb.Except(seenB)) bad.Add($"группа «{g}» плана «{b}» не упомянута в таблице");
        return bad;
    }
}

[System.Serializable]
public class GroupPair
{
    public string a, b;
}
