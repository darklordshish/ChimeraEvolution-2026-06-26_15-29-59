using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>ГЛОБАЛЬНЫЙ СЛОЙ ТЕЛА (спека `2026-10-07-globalnyj-sloj-shablon-plana.md`, шаг 3): идентичность по составу тянет
/// мышечные массы шасси к массам вида-цели. Массы — группы шаблона плана (`Bone.group`, узлы в долях `rel`); смешиваются
/// ЧИСЛА одноимённых групп — подход 1 ресёрча, сотни операций. Порядок `body = local ∘ global`: `ChainSwap.Compose`
/// сначала сдвигает шасси здесь, затем ставит аугменты по швам сдвинутого тела.
///
/// Ступени — пороги признания спеки двух слоёв §2.2 (0.65 / 0.85; ступень 3 — только родное шасси). Сила ступеней
/// `G1`, `G2` выбирается кадром «человек · ст.1 · ст.2 · волк» (развилка геймдизайнера).
///
/// Смешение одной группы: положение вдоль сегмента и смещения поперёк — линейно; длина и радиусы — в логарифмах (они
/// мультипликативны: спека двух слоёв §4.3, «смешивать всё сложением — системная ошибка», 11.09). Положение считается в
/// долях СЕГМЕНТА между метками, а не кости: предплечье волка — две кости, человека — одна. Группа шасси без пары у цели
/// гаснет (радиус × (1 − g)); группа цели без пары у шасси растёт из нуля (радиус × g) на том же сегменте шасси.</summary>
public static class GlobalLayer
{
    public static float G1 = 0.35f;   // ступень 1 — небольшие общие изменения (по кадру среза 4)
    public static float G2 = 0.7f;    // ступень 2 — серьёзные (по кадру среза 4)
    public const float WeakAt = 0.65f, MediumAt = 0.85f;   // пороги признания, общие для облика и узнавания (§2.2)
    const float ChassisWeight = 0.1f;                      // вес шасси в составе — как `CreatureBody.chassisIdentityWeight`

    /// <summary>Идентичность по составу — та же формула, что `CreatureBody.Identity`: шасси весит 0.1, каждый орган —
    /// 0.9 / (аугмент-пул его вида); `chassisOnly` — плоть шасси, живёт в его весе. Σ = 1.</summary>
    public static Dictionary<SpeciesSO, float> Identity(SpeciesSO chassis, IReadOnlyList<Organ> worn)
    {
        var units = new Dictionary<SpeciesSO, float> { [chassis] = ChassisWeight };
        float mass = ChassisWeight;
        foreach (var o in worn ?? new List<Organ>())
        {
            if (o == null || o.chassisOnly) continue;
            var owner = chassis.organs != null && System.Array.IndexOf(chassis.organs, o) >= 0 ? chassis : ChainSwap.OwnerOf(o);
            if (owner == null) continue;
            float unit = (1f - ChassisWeight) / AugPool(owner);
            units[owner] = (units.TryGetValue(owner, out var u) ? u : 0f) + unit;
            mass += unit;
        }
        return units.ToDictionary(kv => kv.Key, kv => kv.Value / mass);
    }

    static int AugPool(SpeciesSO s) => Mathf.Max(1, s.organs?.Count(o => o != null && !o.chassisOnly) ?? 0);

    /// <summary>Вид-цель и ступень: цель — вид с наибольшей долей; если это само шасси — ступени нет.</summary>
    public static (SpeciesSO target, int step) Step(SpeciesSO chassis, IReadOnlyList<Organ> worn)
    {
        var id = Identity(chassis, worn);
        var top = id.OrderByDescending(kv => kv.Value).First();
        if (top.Key == chassis) return (null, 0);
        int step = top.Value >= MediumAt ? 2 : top.Value >= WeakAt ? 1 : 0;
        return (step > 0 ? top.Key : null, step);
    }

    public static float G(int step) => step == 1 ? G1 : step == 2 ? G2 : 0f;

    /// <summary>Есть ли у тела массы шаблона — без них глобальному слою нечего смешивать (пока виды не переведены).</summary>
    public static bool HasGroups(SpeciesSO sp) => sp?.bones != null && sp.bones.Any(b => !string.IsNullOrEmpty(b.group) && b.rel != null && b.rel.On);

    /// <summary>Кости шасси, сдвинутые к цели на `g`: числа групп смешаны, метры узлов в долях пересчитаны.</summary>
    public static Bone[] Blend(SpeciesSO chassis, SpeciesSO target, float g)
    {
        var bones = BodyTree.Clone(chassis.bones).ToList();
        if (target?.bones == null || g <= 0f) return bones.ToArray();
        var cBy = bones.ToDictionary(b => b.name);
        var tBy = target.bones.ToDictionary(b => b.name);
        var tGroups = target.bones.Where(b => !string.IsNullOrEmpty(b.group) && b.rel != null && b.rel.On).ToList();

        foreach (var c in bones.Where(b => !string.IsNullOrEmpty(b.group) && b.rel != null && b.rel.On).ToList())
        {
            var t = tGroups.FirstOrDefault(x => x.group == c.group);
            if (t == null) { c.rel.r0 *= 1f - g; c.rel.r1 *= 1f - g; continue; }   // у цели такой массы нет — гаснет
            var (cOff, cSeg, cLen) = Segment(cBy, c.parent);
            var (tOff, tSeg, tLen) = Segment(tBy, t.parent);
            float cAt = (cOff + c.rel.at * cLen) / cSeg, tAt = (tOff + t.rel.at * tLen) / tSeg;
            c.rel.at = (Mathf.Lerp(cAt, tAt, g) * cSeg - cOff) / cLen;
            c.rel.len = LogLerp(c.rel.len * cLen / cSeg, t.rel.len * tLen / tSeg, g) * cSeg / cLen;
            c.rel.r0 = LogLerp(c.rel.r0, t.rel.r0, g);
            c.rel.r1 = LogLerp(c.rel.r1, t.rel.r1, g);
            c.rel.offX = Mathf.Lerp(c.rel.offX, t.rel.offX, g);
            c.rel.offZ = Mathf.Lerp(c.rel.offZ, t.rel.offZ, g);
        }

        // у шасси такой массы нет — растёт из нуля на том же сегменте шасси
        var have = new HashSet<string>(bones.Where(b => !string.IsNullOrEmpty(b.group)).Select(b => b.group));
        foreach (var t in tGroups.Where(t => !have.Contains(t.group)))
        {
            if (t.parent == null || !tBy.TryGetValue(t.parent, out var tHost)) continue;
            var end = PlanTemplate.SegmentEnd(target, tBy, tHost);
            var host = bones.FirstOrDefault(b => b.limb == tHost.limb && string.IsNullOrEmpty(b.group) && PlanTemplate.SegmentEnd(chassis, cBy, b) == end);
            if (host == null) continue;
            var n = BodyTree.Clone(t);
            n.name = t.name + "~" + target.speciesName;
            n.parent = host.name;
            var (tOff, tSeg, tLen) = Segment(tBy, t.parent);
            var (cOff, cSeg, cLen) = Segment(cBy, host.name);
            n.rel.at = ((tOff + t.rel.at * tLen) / tSeg * cSeg - cOff) / cLen;
            n.rel.len = t.rel.len * tLen / tSeg * cSeg / cLen;
            n.rel.r0 *= g; n.rel.r1 *= g;
            bones.Add(n);
            cBy[n.name] = n;
        }

        var arr = bones.ToArray();
        BodyChains.ResolveRel(arr);   // доли → метры: узлы в долях считаются заново
        return arr;
    }

    static float LogLerp(float a, float b, float g) => a > 1e-6f && b > 1e-6f ? Mathf.Exp(Mathf.Lerp(Mathf.Log(a), Mathf.Log(b), g)) : Mathf.Lerp(a, b, g);

    /// <summary>Сегмент кости `host` между метками: (смещение начала кости от начала сегмента, длина сегмента, длина кости).
    /// Путь — по костям той же цепи без групп; вверх до кости, кончающейся меткой, вниз до своей метки конца.</summary>
    static (float offset, float seg, float len) Segment(Dictionary<string, Bone> by, string hostName)
    {
        if (hostName == null || !by.TryGetValue(hostName, out var host)) return (0f, 1f, 1f);
        float len = Mathf.Max(1e-4f, host.length);
        float before = 0f;
        for (var p = host; p.parent != null && by.TryGetValue(p.parent, out var pp)
                           && pp.limb == host.limb && string.IsNullOrEmpty(pp.group) && string.IsNullOrEmpty(pp.mark?.b); p = pp)
            before += pp.length;
        float after = 0f;
        for (var c = host; string.IsNullOrEmpty(c.mark?.b); )
        {
            var cur = c;
            var next = by.Values.FirstOrDefault(k => k.parent == cur.name && k.limb == cur.limb && string.IsNullOrEmpty(k.group)
                                                      && (!string.IsNullOrEmpty(k.mark?.b) || by.Values.Any(y => y.parent == k.name)));
            if (next == null) break;
            after += next.length; c = next;
        }
        return (before, Mathf.Max(1e-4f, before + len + after), len);
    }
}
