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
/// Смешение одной группы: положение вдоль сегмента и смещения поперёк — линейно; длина, радиусы и сечение — в логарифмах
/// (они мультипликативны: спека двух слоёв §4.3, «смешивать всё сложением — системная ошибка», 11.09). Сравниваются доли
/// МАСШТАБА ЦЕПИ (см. `Blend`). Группа шасси без пары у цели гаснет (радиус × (1 − g)); группа цели без пары у шасси
/// растёт из нуля (радиус × g) на том же сегменте шасси.</summary>
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

    /// <summary>Кости шасси, сдвинутые к цели на `g`: числа групп смешаны, метры узлов в долях пересчитаны.
    ///
    /// СРАВНИВАЮТСЯ ДОЛИ МАСШТАБА ЦЕПИ, А НЕ ДОЛИ КОСТИ-ХОЗЯИНА. Узел `rel` записан в долях радиуса хозяина, а радиус
    /// хозяина — авторское число вида: хребет человека тонкий (r 0.07), волка толстый (0.18). Грудная клетка 0.13 и 0.15 м
    /// записана как 1.99 и 0.91 — смешение по долям хозяина сдувало торс человека к волку вдвое (кадр ступеней 07.10).
    /// Поэтому положение, длина, радиусы и смещения переводятся в метры и делятся на масштаб цепи `S`: сегмент между
    /// метками у конечностей и шеи, длина торса между корнями задних и передних конечностей у хребта.</summary>
    public static Bone[] Blend(SpeciesSO chassis, SpeciesSO target, float g)
    {
        var bones = BodyTree.Clone(chassis.bones).ToList();
        if (target?.bones == null || g <= 0f) return bones.ToArray();
        var cBy = bones.ToDictionary(b => b.name);
        var tBy = target.bones.ToDictionary(b => b.name);
        var cPose = Poses(cBy); var tPose = Poses(tBy);
        var tGroups = target.bones.Where(b => !string.IsNullOrEmpty(b.group) && b.rel != null && b.rel.On).ToList();

        foreach (var c in bones.Where(b => !string.IsNullOrEmpty(b.group) && b.rel != null && b.rel.On).ToList())
        {
            var t = tGroups.FirstOrDefault(x => x.group == c.group);
            if (t == null || !cBy.TryGetValue(c.parent ?? "", out var cHost) || !tBy.TryGetValue(t.parent ?? "", out var tHost))
            { c.rel.r0 *= 1f - g; c.rel.r1 *= 1f - g; continue; }   // у цели такой массы нет — гаснет
            var cf = Frame(cBy, cPose, cHost); var tf = Frame(tBy, tPose, tHost);
            var cm = Metric(c, cHost, cf); var tm = Metric(t, tHost, tf);
            Apply(c, cHost, cf, new Metrics
            {
                u = Mathf.Lerp(cm.u, tm.u, g), len = LogLerp(cm.len, tm.len, g),
                r0 = LogLerp(cm.r0, tm.r0, g), r1 = LogLerp(cm.r1, tm.r1, g),
                x = Mathf.Lerp(cm.x, tm.x, g), z = Mathf.Lerp(cm.z, tm.z, g),
            });
            c.section = LogLerp(c.section, t.section, g);   // сечение массы — множитель, тоже в логарифмах
            c.depth = LogLerp(c.depth, t.depth, g);
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
            var m = Metric(t, tHost, Frame(tBy, tPose, tHost));
            m.r0 *= g; m.r1 *= g;
            Apply(n, host, Frame(cBy, cPose, host), m);
            bones.Add(n);
            cBy[n.name] = n;
        }

        var arr = bones.ToArray();
        BodyChains.ResolveRel(arr);   // доли → метры: узлы в долях считаются заново
        return arr;
    }

    static float LogLerp(float a, float b, float g) => a > 1e-6f && b > 1e-6f ? Mathf.Exp(Mathf.Lerp(Mathf.Log(a), Mathf.Log(b), g)) : Mathf.Lerp(a, b, g);

    /// <summary>Группа в долях масштаба цепи: `u` — начало вдоль сегмента, остальное — метры / `S`.</summary>
    struct Metrics { public float u, len, r0, r1, x, z; }

    /// <summary>Сегмент хозяина: начало хозяина на оси сегмента `c0` (м), проекция оси хозяина на ось сегмента `k`,
    /// масштаб цепи `S` (м).</summary>
    struct SegFrame { public float c0, k, S; }

    static Metrics Metric(Bone n, Bone host, SegFrame f)
    {
        var q = n.rel; float R = BodyChains.RadiusAt(host, q.at), L = Mathf.Max(1e-4f, host.length);
        return new Metrics
        {
            u = (f.c0 + q.at * L * f.k) / f.S, len = q.len * L / f.S,
            r0 = q.r0 * R / f.S, r1 = q.r1 * R / f.S,
            x = q.offX * R * host.section / f.S, z = q.offZ * R * host.depth / f.S,
        };
    }

    static void Apply(Bone n, Bone host, SegFrame f, Metrics m)
    {
        var q = n.rel; float L = Mathf.Max(1e-4f, host.length);
        q.at = (m.u * f.S - f.c0) / (L * (Mathf.Abs(f.k) > 0.2f ? f.k : 1f));
        float R = Mathf.Max(1e-5f, BodyChains.RadiusAt(host, q.at));
        q.len = m.len * f.S / L;
        q.r0 = m.r0 * f.S / R; q.r1 = m.r1 * f.S / R;
        q.offX = m.x * f.S / (R * Mathf.Max(1e-4f, host.section));
        q.offZ = m.z * f.S / (R * Mathf.Max(1e-4f, host.depth));
    }

    /// <summary>Сегмент кости `host`. У хребта меток нет — сегмент торса между корнями задних и передних конечностей
    /// (тазобедренный сустав → плечевой пояс), ось хозяина проецируется на него. У прочих цепей — кости между метками:
    /// вверх до кости, кончающейся меткой, вниз до своей метки конца (предплечье волка — две кости, человека — одна).</summary>
    static SegFrame Frame(Dictionary<string, Bone> by, Dictionary<string, (Vector3 pos, Quaternion rot)> pose, Bone host)
    {
        if (host.limb == "хребет" && pose.TryGetValue(host.name, out var hp)
            && LimbRoot(by, pose, "зад") is Vector3 hip && LimbRoot(by, pose, "перед") is Vector3 shoulder)
        {
            var d = shoulder - hip; float S = d.magnitude;
            if (S > 1e-3f)
            {
                d /= S;
                return new SegFrame { c0 = Vector3.Dot(hp.pos - hip, d), k = Vector3.Dot(hp.rot * Vector3.up, d), S = S };
            }
        }
        var (before, seg, _) = Segment(by, host.name);
        return new SegFrame { c0 = before, k = 1f, S = seg };
    }

    /// <summary>Корень цепи конечности на средней линии: первая кость цепи, чей родитель из другой цепи.</summary>
    static Vector3? LimbRoot(Dictionary<string, Bone> by, Dictionary<string, (Vector3 pos, Quaternion rot)> pose, string limb)
    {
        var root = by.Values.FirstOrDefault(b => b.limb == limb && string.IsNullOrEmpty(b.group)
                                                 && (b.parent == null || !by.TryGetValue(b.parent, out var p) || p.limb != limb));
        if (root == null || !pose.TryGetValue(root.name, out var rp)) return null;
        var v = rp.pos; v.x = 0f;
        return v;
    }

    /// <summary>Позы костей по именам — та же формула, что у сборки (`SkeletonBuilder.Root`/`Child`).</summary>
    static Dictionary<string, (Vector3 pos, Quaternion rot)> Poses(Dictionary<string, Bone> by)
    {
        var pose = new Dictionary<string, (Vector3 pos, Quaternion rot)>();
        bool Get(Bone b, int depth)
        {
            if (pose.ContainsKey(b.name)) return true;
            if (depth > 64) return false;
            if (string.IsNullOrEmpty(b.parent) || !by.TryGetValue(b.parent, out var p)) { pose[b.name] = SkeletonBuilder.Root(b); return true; }
            if (!Get(p, depth + 1)) return false;
            var pp = pose[p.name];
            pose[b.name] = SkeletonBuilder.Child(p, pp.pos, pp.rot, b);
            return true;
        }
        foreach (var b in by.Values) Get(b, 0);
        return pose;
    }

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
