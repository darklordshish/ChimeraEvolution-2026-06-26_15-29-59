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

        // ДЛИНЫ ОТРЕЗКОВ — ПЕРВЫМИ: массы ниже смешиваются в долях масштаба цепи, и при g = 1 совпасть с целью и по длине,
        // и по толщине они могут только на уже перемеренных отрезках
        var ts = SegmentsOn(target, chassis.Plan);
        if (ts != null) BlendSegments(chassis, cBy, ts, g);

        var cPose = Poses(cBy); var tPose = Poses(tBy);
        var tGroups = target.bones.Where(b => !string.IsNullOrEmpty(b.group) && b.rel != null && b.rel.On).ToList();

        // ЦЕЛЬ — ТЕЛО ВИДА НА ПЛАНЕ ШАССИ (спека 08.10): волк на двуногом, а не четвероногий волк. Нет поставки — числа
        // родного плана цели (как до 08.10) и строка долга в `MissingBodies`, а не молча
        var body = BodyOn(target, chassis.Plan);
        bool native = target.Plan == chassis.Plan;
        if (body == null)
        {
            MissingBodies.Add(target.speciesName + " на плане «" + chassis.Plan + "»");
            body = BodyOn(target, target.Plan);
            native = true;
        }

        // ЦЕЛЬ НЕ НА ШАБЛОНЕ (лось и ёж до шага 6 спеки 07.10): групп у неё нет вовсе — это «чисел нет», а не «масс нет».
        // Иначе все массы шасси гасли бы как «группы без пары»: волк с органами ежа терял бы треть мышц на ступени 1
        bool masses = body.Count > 0;
        foreach (var c in masses ? bones.Where(b => !string.IsNullOrEmpty(b.group) && b.rel != null && b.rel.On).ToList() : new List<Bone>())
        {
            if (!body.TryGetValue(c.group, out var t) || !cBy.TryGetValue(c.parent ?? "", out var cHost))
            { c.rel.r0 *= 1f - g; c.rel.r1 *= 1f - g; continue; }   // у цели такой массы нет — гаснет
            var cf = Frame(cBy, cPose, cHost);
            var cm = Metric(c, cHost, cf);
            Apply(c, cHost, cf, new Metrics
            {
                u = Mathf.Lerp(cm.u, t.u, g), len = LogLerp(cm.len, t.len, g),
                r0 = LogLerp(cm.r0, t.r0, g), r1 = LogLerp(cm.r1, t.r1, g),
                x = Mathf.Lerp(cm.x, t.x, g), z = Mathf.Lerp(cm.z, t.z, g),
            });
            c.section = LogLerp(c.section, t.section, g);   // сечение массы — множитель, тоже в логарифмах
            c.depth = LogLerp(c.depth, t.depth, g);
        }

        // у шасси такой массы нет — растёт из нуля на том же сегменте шасси. Только когда цель на своём плане: тело на
        // чужом плане — группы ЕГО шаблона, а шасси этого плана полно по сторожу шаблона
        var have = new HashSet<string>(bones.Where(b => !string.IsNullOrEmpty(b.group)).Select(b => b.group));
        foreach (var t in native ? tGroups.Where(t => !have.Contains(t.group)) : Enumerable.Empty<Bone>())
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

        // ШЕЯ И ГОЛОВА — частью силы ступени (`KHead`): пропорции, наклон, посадка; блоки головы едут за узлами
        float gh = g * Mathf.Clamp01(KHead);
        var th = HeadOn(target, chassis.Plan);
        if (gh > 0f && th != null && th.On) BlendHead(bones, cBy, th, gh);

        var arr = bones.ToArray();
        BodyChains.ResolveRel(arr);   // доли → метры: узлы в долях считаются заново
        return arr;
    }

    /// <summary>ОТРЕЗКИ КОНЕЧНОСТЕЙ вида на плане: длина кости между суставными метками в долях торса, ключ — «цепь→метка
    /// конца». Свой план — из графа, чужой — `segments` поставки (нет — null: длины не смешиваются).</summary>
    public static Dictionary<string, float> SegmentsOn(SpeciesSO sp, string plan)
    {
        if (sp?.bones == null) return null;
        if (sp.Plan != plan)
        {
            var s = sp.planBodies?.FirstOrDefault(b => b != null && b.plan == plan)?.segments;
            return s == null || s.Length == 0 ? null
                 : s.Where(x => x != null && x.len > 0f).GroupBy(x => x.limb + "→" + x.end).ToDictionary(x => x.Key, x => x.First().len);
        }
        var by = new Dictionary<string, Bone>();
        foreach (var b in sp.bones) if (b != null && !by.ContainsKey(b.name)) by[b.name] = b;
        if (!TorsoAxes(by, Poses(by), out _, out _, out var S)) return null;
        return LimbSegments(by).ToDictionary(kv => kv.Key, kv => kv.Value.Sum(b => b.length) / S);
    }

    /// <summary>Отрезки конечностей `перед` и `зад`: кости от кончающейся меткой вверх по цепи до предыдущей метки.
    /// Только скелет графа — без групп и узлов в долях (те едут за отрезком сами).</summary>
    static Dictionary<string, List<Bone>> LimbSegments(Dictionary<string, Bone> by)
    {
        var segs = new Dictionary<string, List<Bone>>();
        foreach (var end in by.Values.Where(b => (b.limb == "перед" || b.limb == "зад") && Plain(b) && !string.IsNullOrEmpty(b.mark?.b)))
        {
            var list = new List<Bone> { end };
            // вверх — до предыдущей метки конца или до метки НАЧАЛА у самой кости (бедро начинается меткой «бедро»: таз
            // над ним — та же цепь, но не отрезок «до колена»)
            for (var p = end; string.IsNullOrEmpty(p.mark?.a) && p.parent != null && by.TryGetValue(p.parent, out var pp)
                              && pp.limb == end.limb && Plain(pp) && string.IsNullOrEmpty(pp.mark?.b) && list.Count < 16; p = pp)
                list.Insert(0, pp);
            segs[end.limb + "→" + end.mark.b] = list;
        }
        return segs;
    }

    static bool Plain(Bone b) => string.IsNullOrEmpty(b.group) && (b.rel == null || !b.rel.On);

    /// <summary>Длины отрезков шасси к долям цели: отрезок масштабируется целиком (все его кости и начала детей со своим
    /// началом вдоль оси), торс — калибр шасси — не трогается. Потом корень опускается или поднимается так, чтобы низ
    /// опорных конечностей остался на прежней высоте: короткие ноги не вешают тело в воздухе.</summary>
    static void BlendSegments(SpeciesSO chassis, Dictionary<string, Bone> by, Dictionary<string, float> target, float g)
    {
        if (!TorsoAxes(by, Poses(by), out _, out _, out var S)) return;
        float before = Lowest(by, chassis);
        foreach (var kv in LimbSegments(by))
        {
            if (!target.TryGetValue(kv.Key, out var t)) continue;   // у цели такого отрезка нет — длина шасси
            float cur = kv.Value.Sum(b => b.length) / S;
            if (cur <= 1e-5f || t <= 1e-5f) continue;
            float k = LogLerp(cur, t, g) / cur;
            if (Mathf.Abs(k - 1f) < 1e-6f) continue;
            foreach (var b in kv.Value)
            {
                b.length *= k;
                foreach (var child in by.Values.Where(x => x.parent == b.name && x.freeOrigin && Plain(x)))
                    child.origin = new Vector3(child.origin.x, child.origin.y * k, child.origin.z);
            }
        }
        float after = Lowest(by, chassis);
        var root = by.Values.FirstOrDefault(b => string.IsNullOrEmpty(b.parent) || !by.ContainsKey(b.parent));
        if (root != null && !float.IsInfinity(before) && !float.IsInfinity(after)) root.origin += Vector3.up * (before - after);
    }

    /// <summary>Нижняя точка опорных конечностей (концы костей цепей опоры шасси), метры.</summary>
    static float Lowest(Dictionary<string, Bone> by, SpeciesSO chassis)
    {
        var limbs = new List<string>();
        if (chassis.stanceLimbs != null && System.Array.IndexOf(chassis.stanceLimbs, BodySlots.Legs) >= 0) limbs.Add("зад");
        if (chassis.stanceLimbs != null && System.Array.IndexOf(chassis.stanceLimbs, BodySlots.Arms) >= 0) limbs.Add("перед");
        var pose = Poses(by);
        float y = float.PositiveInfinity;
        foreach (var b in by.Values.Where(b => limbs.Contains(b.limb) && Plain(b) && pose.ContainsKey(b.name)))
        {
            var (p, r) = pose[b.name];
            y = Mathf.Min(y, Mathf.Min(p.y, (p + r * Vector3.up * b.length).y));
        }
        return y;
    }

    /// <summary>ГНЁЗДА ЕДУТ ЗА СВОЕЙ КОСТЬЮ. Гнездо записано в метрах кадра кости-хозяина, а его единица — мера кости (ширина
    /// головы, диаметр конца конечности). Глобальный слой меняет кость — гнездо обязано меняться так же: поперёк — по
    /// толщине кости, вдоль — по длине, единица — по толщине. Иначе калибр аугмента застревает в шасси: «Пасть» садилась
    /// в ширину голой человеческой головы, сколько ни раздувай голову ступенью (кадр оборотня 08.10).</summary>
    public static PlaceNest[] FollowNests(SpeciesSO chassis, Bone[] blended)
    {
        if (chassis?.nests == null) return null;
        var before = new Dictionary<string, Bone>();
        foreach (var b in chassis.bones ?? new Bone[0]) if (b != null && !before.ContainsKey(b.name)) before[b.name] = b;
        var after = new Dictionary<string, Bone>();
        foreach (var b in blended ?? new Bone[0]) if (b != null && !after.ContainsKey(b.name)) after[b.name] = b;
        return chassis.nests.Select(n =>
        {
            var c = BodyTree.Clone(n);
            if (n?.host == null || !before.TryGetValue(n.host, out var o) || !after.TryGetValue(n.host, out var m)) return c;
            float radial = (o.r0 + o.r1) > 1e-6f ? (m.r0 + m.r1) / (o.r0 + o.r1) : 1f;
            float axial = o.length > 1e-6f ? m.length / o.length : 1f;
            c.localPos = new Vector3(n.localPos.x * radial, n.localPos.y * axial, n.localPos.z * radial);
            c.unit = n.unit * radial;
            return c;
        }).ToArray();
    }

    /// <summary>Доля силы ступени, которая достаётся шее и голове (спека 08.10 §2 п.3: «частично должна меняться»).
    /// Подбирается кадром ступеней с аугментами и без.</summary>
    public static float KHead = 0.5f;

    /// <summary>Шея и голова вида на плане: свой план — из графа, чужой — блок `head` поставки (нет — null).</summary>
    public static HeadNumbers HeadOn(SpeciesSO sp, string plan)
    {
        if (sp?.bones == null) return null;
        if (sp.Plan != plan)
        {
            var h = sp.planBodies?.FirstOrDefault(b => b != null && b.plan == plan)?.head;
            return h != null && h.On ? h : null;
        }
        var by = new Dictionary<string, Bone>();
        foreach (var b in sp.bones) if (b != null && !by.ContainsKey(b.name)) by[b.name] = b;
        return MeasureHead(by, Poses(by));
    }

    static HeadNumbers MeasureHead(Dictionary<string, Bone> by, Dictionary<string, (Vector3 pos, Quaternion rot)> pose)
    {
        var neck = ChainRoot(by, "шея"); var head = ChainRoot(by, "голова");
        if (neck == null || head == null || !TorsoAxes(by, pose, out var d, out var v, out var S)) return null;
        float pn = Pitch(pose[neck.name].rot * Vector3.up, d, v), ph = Pitch(pose[head.name].rot * Vector3.up, d, v);
        return new HeadNumbers
        {
            neckLen = neck.length / S, neckR0 = neck.r0 / S, neckR1 = neck.r1 / S, neckPitch = pn,
            headLen = head.length / S, headR0 = head.r0 / S, headR1 = head.r1 / S, headPitch = Mathf.DeltaAngle(pn, ph),
        };
    }

    static void BlendHead(List<Bone> bones, Dictionary<string, Bone> by, HeadNumbers t, float g)
    {
        var pose = Poses(by);
        var c = MeasureHead(by, pose);
        if (c == null || !TorsoAxes(by, pose, out var d, out var v, out var S)) return;
        var neck = ChainRoot(by, "шея"); var head = ChainRoot(by, "голова");

        float oldNeck = neck.length;
        neck.length = LogLerp(c.neckLen, t.neckLen, g) * S;
        // голова со своим началом (`freeOrigin`) сидит смещением ОТ НАЧАЛА шеи: длинная шея иначе проткнула бы её,
        // короткая — оторвала. Сдвиг вдоль оси шеи на прирост длины держит зазор шеи и головы прежним
        if (head.parent == neck.name && head.freeOrigin) head.origin += Vector3.up * (neck.length - oldNeck);
        neck.r0 = LogLerp(c.neckR0, t.neckR0, g) * S; neck.r1 = LogLerp(c.neckR1, t.neckR1, g) * S;
        head.length = LogLerp(c.headLen, t.headLen, g) * S;
        head.r0 = LogLerp(c.headR0, t.headR0, g) * S; head.r1 = LogLerp(c.headR1, t.headR1, g) * S;

        float pn = c.neckPitch + Mathf.DeltaAngle(c.neckPitch, t.neckPitch) * g;
        float ph = c.headPitch + Mathf.DeltaAngle(c.headPitch, t.headPitch) * g;
        Turn(by, neck, pn, d, v);
        Turn(by, head, pn + ph, d, v);   // поза шеи уже новая: голова считается от неё
    }

    /// <summary>Повернуть кость так, чтобы её ось легла под наклоном `pitch` в сагиттальной плоскости торса; боковая
    /// составляющая оси не трогается. Поворот наследуется детьми (`dir` — относительно родителя).</summary>
    static void Turn(Dictionary<string, Bone> by, Bone b, float pitch, Vector3 d, Vector3 v)
    {
        var pose = Poses(by);
        var (_, rot) = pose[b.name];
        var w = rot * Vector3.up;
        float side = Vector3.Dot(w, Vector3.Cross(d, v));
        float plane = Mathf.Sqrt(Mathf.Max(0f, 1f - side * side));
        var w2 = (d * Mathf.Cos(pitch * Mathf.Deg2Rad) + v * Mathf.Sin(pitch * Mathf.Deg2Rad)) * plane + Vector3.Cross(d, v) * side;
        if (Vector3.Angle(w, w2) < 1e-3f) return;
        var world = Quaternion.FromToRotation(w, w2) * rot;
        var parentRot = b.parent != null && pose.TryGetValue(b.parent, out var pp) ? pp.rot : Quaternion.identity;
        b.dir = (Quaternion.Inverse(parentRot) * world).eulerAngles;
    }

    /// <summary>Наклон оси `w` в сагиттальной плоскости торса: от оси торса `d` к брюху `v`, градусы.</summary>
    static float Pitch(Vector3 w, Vector3 d, Vector3 v) => Mathf.Atan2(Vector3.Dot(w, v), Vector3.Dot(w, d)) * Mathf.Rad2Deg;

    /// <summary>Оси торса: `d` — от тазобедренного к плечевому поясу, `v` — к брюху (составляющая «вперёд» мира, +Z, поперёк
    /// `d`: у двуногого это грудь, у четвероногого — низ), `S` — длина торса.</summary>
    static bool TorsoAxes(Dictionary<string, Bone> by, Dictionary<string, (Vector3 pos, Quaternion rot)> pose, out Vector3 d, out Vector3 v, out float S)
    {
        d = v = Vector3.zero; S = 0f;
        if (!(LimbRoot(by, pose, "зад") is Vector3 hip) || !(LimbRoot(by, pose, "перед") is Vector3 shoulder)) return false;
        d = shoulder - hip; S = d.magnitude;
        if (S < 1e-3f) return false;
        d /= S;
        v = Vector3.forward - Vector3.Dot(Vector3.forward, d) * d;
        if (v.sqrMagnitude < 1e-6f) return false;
        v.Normalize();
        return true;
    }

    /// <summary>Корень цепи: кость цепи без группы и не в долях, чей родитель из другой цепи.</summary>
    static Bone ChainRoot(Dictionary<string, Bone> by, string limb) =>
        by.Values.FirstOrDefault(b => b.limb == limb && string.IsNullOrEmpty(b.group) && (b.rel == null || !b.rel.On)
                                      && (b.parent == null || !by.TryGetValue(b.parent, out var p) || p.limb != limb));

    /// <summary>Пары «вид на плане», которых не нашлось в поставке, — долг модельной линии (спека 08.10 §4.5).</summary>
    public static readonly HashSet<string> MissingBodies = new();

    /// <summary>ТЕЛО ВИДА НА ПЛАНЕ — числа групп в долях масштаба цепи. Свой план — из графа вида; чужой — поставка
    /// `planBodies`; нет поставки — null. Функция «план → (группа → числа)» тотальна там, где тело задано.</summary>
    public static Dictionary<string, GroupNumbers> BodyOn(SpeciesSO sp, string plan)
    {
        if (sp?.bones == null) return null;
        if (sp.Plan != plan)
            return sp.planBodies?.FirstOrDefault(b => b != null && b.plan == plan)?.groups?
                     .Where(n => n != null && !string.IsNullOrEmpty(n.name)).GroupBy(n => n.name).ToDictionary(x => x.Key, x => x.First());
        var by = new Dictionary<string, Bone>();
        foreach (var b in sp.bones) if (b != null && !by.ContainsKey(b.name)) by[b.name] = b;
        var pose = Poses(by);
        var body = new Dictionary<string, GroupNumbers>();
        foreach (var n in sp.bones.Where(b => !string.IsNullOrEmpty(b.group) && b.rel != null && b.rel.On))
        {
            if (body.ContainsKey(n.group) || !by.TryGetValue(n.parent ?? "", out var host)) continue;
            var m = Metric(n, host, Frame(by, pose, host));
            body[n.group] = new GroupNumbers { name = n.group, u = m.u, len = m.len, r0 = m.r0, r1 = m.r1, x = m.x, z = m.z, section = n.section, depth = n.depth };
        }
        return body;
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
