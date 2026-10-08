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
/// `endMark` — метка конца СЕГМЕНТА, на котором сидит группа (`локоть` — плечо, `запястье` — предплечье); пусто — любая
/// кость цепи `limb` (хребет без меток). Сегмент — кости цепи между метками: у волка предплечье — `предплечье` и узел-риг
/// `пясть`, у человека — одна кость (вопрос модельной линии 07.10). Доли группы при смешении переводятся в доли сегмента. Пустая сторона пары — группа без пары: растёт из нуля или гаснет по ступени.</summary>
[System.Serializable]
public class PlanTemplate
{
    public string plan;
    public PlanGroup[] groups;
    // ПЕРЕВЕДЁННЫЕ ВИДЫ (просьба модельной линии 07.10): перевод на шаблон идёт по виду — волк раньше лося и ежа. Вид из
    // списка проверяется полностью, остальные виды плана — только «нет групп вне шаблона». Пусто — проверяются все
    public string[] species;

    public bool Covers(SpeciesSO sp) => species == null || species.Length == 0 || System.Array.IndexOf(species, sp.speciesName) >= 0;

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
        foreach (var g in Covers(sp) ? groups : new PlanGroup[0])
        {
            var nodes = sp.bones.Where(b => b.group == g.name).ToList();
            if (nodes.Count != 1) { bad.Add($"{sp.speciesName}: группа «{g.name}» — узлов {nodes.Count}, нужен один"); continue; }
            var n = nodes[0];
            if (n.rel == null) bad.Add($"{sp.speciesName}: группа «{g.name}» ({n.name}) не в долях — нет `rel`");
            if (n.parent == null || !by.TryGetValue(n.parent, out var host)) { bad.Add($"{sp.speciesName}: группа «{g.name}» ({n.name}) без кости"); continue; }
            if (host.limb != g.limb) bad.Add($"{sp.speciesName}: группа «{g.name}» на цепи «{host.limb}», по шаблону «{g.limb}»");
            if (!string.IsNullOrEmpty(g.endMark) && SegmentEnd(sp, by, host) != g.endMark)
                bad.Add($"{sp.speciesName}: группа «{g.name}» на кости «{host.name}» сегмента до «{SegmentEnd(sp, by, host)}», по шаблону — до «{g.endMark}»");
        }
        foreach (var b in sp.bones.Where(b => !string.IsNullOrEmpty(b.group) && !known.Contains(b.group)))
            bad.Add($"{sp.speciesName}: узел «{b.name}» — группа «{b.group}», которой нет в шаблоне «{plan}»");
        return bad;
    }

    /// <summary>Метка конца сегмента, которому принадлежит кость: своя метка конца, а без неё — первая метка вниз по цепи
    /// (через ребёнка той же цепи, не группу и не сечение лофта без меток впереди).</summary>
    public static string SegmentEnd(SpeciesSO sp, Dictionary<string, Bone> by, Bone b)
    {
        for (int guard = 0; b != null && guard < 64; guard++)
        {
            if (!string.IsNullOrEmpty(b.mark?.b)) return b.mark.b;
            var cur = b;
            b = sp.bones.FirstOrDefault(k => k.parent == cur.name && k.limb == cur.limb && string.IsNullOrEmpty(k.group)
                                             && sp.bones.Any(x => x == k && (!string.IsNullOrEmpty(x.mark?.b) || sp.bones.Any(y => y.parent == x.name))));
        }
        return null;
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

/// <summary>ТЕЛО ВИДА НА ПЛАНЕ (спека `2026-10-08-vid-na-chuzhom-plane.md`): числа групп шаблона плана. На своём плане вид их
/// не хранит — они вычисляются из графа (`GlobalLayer.BodyOn`); на чужом — поставка модельной линии
/// `Docs/models/handoff/<вид>-na-<план>.json`, снятая с последней колонки листа ступеней («волк на двуногом»).</summary>
[System.Serializable]
public class PlanBody
{
    public string species, plan;
    public GroupNumbers[] groups;
    public HeadNumbers head;   // посадка шеи и головы (спека 08.10 §2 п.3); `neckLen` = 0 — не задано, голова не смешивается
    public SegmentNumbers[] segments;   // длины отрезков конечностей; пусто — длины шасси не смешиваются
}

/// <summary>Отрезок конечности: цепь (`перед`/`зад`), метка его конца (`локоть`, `колено`…) и длина в долях торса.</summary>
[System.Serializable]
public class SegmentNumbers
{
    public string limb, end;
    public float len;
}

/// <summary>ШЕЯ И ГОЛОВА В ГЛОБАЛЬНОМ СЛОЕ (спека 08.10 §2 п.3). Длины и радиусы — в долях торса (между корнями задних и
/// передних конечностей); наклоны — градусы в сагиттальной плоскости: `neckPitch` — шея от оси торса к брюху (+) или к
/// спине (−), `headPitch` — голова от оси шеи, так же. Ось головы — соглашение ПЛАНА: у двуногого — вдоль черепа вверх,
/// у четвероногих — вдоль морды; поэтому тело на чужом плане задаётся в соглашении того плана.</summary>
[System.Serializable]
public class HeadNumbers
{
    public float neckLen, neckR0, neckR1, neckPitch;
    public float headLen, headR0, headR1, headPitch;
    public bool On => neckLen > 0f && headLen > 0f;
}

/// <summary>Числа одной группы в ДОЛЯХ МАСШТАБА ЦЕПИ (спека 08.10 §2 п.4): `u` — начало вдоль сегмента; `len`, `r0`, `r1`,
/// `x`, `z` — метры, делённые на масштаб цепи (сегмент между метками; у хребта — торс между корнями конечностей);
/// `section`, `depth` — множители сечения самой массы. От костей вида не зависят, метров не содержат.</summary>
[System.Serializable]
public class GroupNumbers
{
    public string name;
    public float u, len, r0, r1, x, z;
    public float section = 1f, depth = 1f;
}
