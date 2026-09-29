using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>АУГМЕНТ ПОДСТАВЛЯЕТ СВОЮ ЦЕПЬ СКЕЛЕТА (спека двух слоёв §3, решение геймдизайнера 13 от 29.09: «ради этого мы и
/// делали скелет графом и унифицировали слоты — чтобы их заменять»).
///
/// Граф шасси режется по разметке цепей: у конечности — по верхнему сегменту (плечо → локоть, бедро → колено), всё выше
/// (лопатка, пояс) остаётся шасси. Вынутый участок заменяется цепью донора — её узлами, топологией, буграми, — сведённой
/// к калибру носителя: цепь масштабируется к длине заменённой, внутри остаются донорские соотношения. Корень цепи встаёт
/// в сустав носителя и смотрит в мире так же, как у донора. Оболочка поля строится по собранному скелету — шов на
/// разрезе сваривает поле. Гнёзда на вынутых костях заменяются донорскими (детали конца конечности едут с цепью).
///
/// Результат — копия шасси в памяти (`meshKey` = состав), её и строит `MorphBuilder`; кэш — по составу, двадцать
/// одинаковых химер делят одну копию и одну оболочку.</summary>
public static class ChainSwap
{
    /// <summary>Слот → цепь и метка на конце верхнего сегмента (он — корень подставляемой цепи). Голова, хвост и
    /// грудь — следующими срезами.</summary>
    static readonly (string slot, string limb, string upperEnd)[] Map =
    {
        (BodySlots.Arms, "перед", "локоть"),
        (BodySlots.Legs, "зад", "колено"),
    };

    static readonly Dictionary<string, SpeciesSO> cache = new();
    static readonly Dictionary<Organ, SpeciesSO> owners = new();

    /// <summary>Тело для сборки: шасси, если прививок с цепями нет, иначе составная копия.</summary>
    public static SpeciesSO Compose(SpeciesSO chassis, IReadOnlyList<Organ> worn)
    {
        if (chassis == null || worn == null || chassis.bones == null || chassis.bones.Length == 0) return chassis;

        var grafts = new List<(string slot, string limb, string upperEnd, SpeciesSO donor)>();
        foreach (var (slot, limb, upperEnd) in Map)
        {
            var organ = worn.FirstOrDefault(o => o != null && o.slot == slot);   // видимый орган слота (порядок — `WornInDrawOrder`)
            var donor = OwnerOf(organ);
            if (donor == null || donor.speciesName == chassis.speciesName || donor.bones == null || donor.bones.Length == 0) continue;
            grafts.Add((slot, limb, upperEnd, donor));
        }
        if (grafts.Count == 0) return chassis;

        // КЛЮЧ — СОСТАВ И ОТПЕЧАТОК СОДЕРЖИМОГО: по одним именам кэш отдавал бы старое тело после пересоздания видов
        // (та же мина, что у `BoneMesher` по имени вида: ассет тот же, кости другие, ошибки нет)
        string key = chassis.speciesName + "#" + Stamp(chassis) + "|" +
                     string.Join(",", grafts.Select(g => g.slot + ":" + g.donor.speciesName + "#" + Stamp(g.donor)));
        if (cache.TryGetValue(key, out var hit) && hit != null) return hit;

        var body = ScriptableObject.CreateInstance<SpeciesSO>();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(chassis), body);
        body.hideFlags = HideFlags.HideAndDontSave;
        body.name = key;
        body.meshKey = key;
        body.organs = chassis.organs;   // ОРГАНЫ — ТЕ ЖЕ ОБЪЕКТЫ: копировать их незачем, а тождество нужно поиску владельца
        foreach (var g in grafts) Swap(body, g.donor, g.limb, g.upperEnd);
        cache[key] = body;
        return body;
    }

    /// <summary>Чей орган: орган живёт в записи вида и своего вида не знает. Ищем среди загруженных видов по ТОЖДЕСТВУ
    /// объекта; составные копии пропускаем — они делят органы шасси.</summary>
    public static SpeciesSO OwnerOf(Organ organ)
    {
        if (organ == null) return null;
        if (owners.TryGetValue(organ, out var sp) && sp != null) return sp;
        foreach (var s in Resources.FindObjectsOfTypeAll<SpeciesSO>())
            if (s != null && string.IsNullOrEmpty(s.meshKey) && s.organs != null && System.Array.IndexOf(s.organs, organ) >= 0)
                return owners[organ] = s;
        return null;
    }

    /// <summary>Вынуть у `body` цепь `limb` от верхнего сегмента и вставить донорскую.</summary>
    static void Swap(SpeciesSO body, SpeciesSO donor, string limb, string upperEnd)
    {
        var carrier = body.bones.ToList();
        var cRoot = carrier.FirstOrDefault(b => b.limb == limb && b.mark != null && b.mark.b == upperEnd);
        var dRoot = donor.bones.FirstOrDefault(b => b.limb == limb && b.mark != null && b.mark.b == upperEnd);
        if (cRoot == null || dRoot == null)
        {
            Debug.LogWarning($"[цепь] {body.speciesName} ← {donor.speciesName}: нет сегмента «…→{upperEnd}» цепи «{limb}» — не подставляю");
            return;
        }

        var cut = Subtree(carrier, cRoot.name);
        var graft = Subtree(donor.bones, dRoot.name);
        // ДЛИНА — к длине заменённой цепи (рука дотягивается туда же). ТОЛЩИНА — меньший из двух множителей: длины (так
        // сохраняется отношение толщины к длине, то есть форма донорской конечности) и стыка (радиус сустава носителя к
        // радиусу корня донора). По отдельности оба ломаются одинаково — раздувом, на разных парах (кадры 29–30.09):
        // по стыку человеческая нога с сечениями толще корня надувалась шарами на волке (у волка бедро — масса крупа),
        // по длине та же масса крупа становилась гигантской ляжкой человека. С меньшим множителем цепь не толще ни стыка
        // носителя, ни своей формы — раздуваться нечему; тоньше — допустимо, разницу на стыке сваривает поле
        float s = PathLength(carrier, cRoot) / Mathf.Max(1e-4f, PathLength(donor.bones, dRoot));
        float sr = Mathf.Min(s, cRoot.r0 / Mathf.Max(1e-4f, dRoot.r0));
        // МЯГКОСТЬ СЛИЯНИЯ — РЕЖИМ ПОЛЯ НОСИТЕЛЯ: у волка граф грубый и слияние в 5–7 раз мягче человеческого, в тех же
        // метрах; перенесённое как есть, оно растекает чужую цепь по телу. Берём мягкость сустава носителя, а
        // распределение по узлам цепи — донорское
        float sb = dRoot.blend > 1e-4f ? cRoot.blend / dRoot.blend : 0f;

        // КОРЕНЬ: ОСЬ ЦЕПИ — НОСИТЕЛЯ, ЗИГЗАГ — ДОНОРА. Стойка за шасси (решение 8): конечность приходит туда же, куда
        // приходила конечность носителя. Выравнивается ОСЬ ВСЕЙ ЦЕПИ (сустав корня → конец последнего сегмента), а не
        // первого сегмента: у волка бедро наклонено вперёд на 20°, и угол колена рассчитан на этот наклон — поставь
        // бедро отвесно, и голень ляжет горизонтально (поймано кадром 29.09). Поворот КРАТЧАЙШИЙ: взять кадр носителя
        // целиком нельзя, он бывает развёрнут вокруг оси (человеческое бедро — на 180°) и зеркалит плоскость сгиба
        var cBy = carrier.Where(b => b != null && !string.IsNullOrEmpty(b.name)).ToDictionary(b => b.name);
        var cPlaced = new Dictionary<string, (Vector3, Quaternion)>();
        var cParentRot = cBy.TryGetValue(cRoot.parent ?? "", out var cp) ? SkeletonBuilder.Place(cp, cBy, cPlaced).rot : Quaternion.identity;
        var dBy = donor.bones.Where(b => b != null && !string.IsNullOrEmpty(b.name)).ToDictionary(b => b.name);
        var dPlaced = new Dictionary<string, (Vector3, Quaternion)>();
        var dRootRot = SkeletonBuilder.Place(dRoot, dBy, dPlaced).rot;
        var rootRot = Quaternion.FromToRotation(ChainAxis(donor.bones, dRoot, dBy, dPlaced), ChainAxis(carrier, cRoot, cBy, cPlaced)) * dRootRot;

        var kept = new HashSet<string>(carrier.Where(b => !cut.Contains(b.name)).Select(b => b.name));
        var rename = new Dictionary<string, string>();
        foreach (var n in graft) rename[n] = kept.Contains(n) ? n + "~" + donor.speciesName : n;

        var added = new List<Bone>();
        foreach (var src in donor.bones.Where(b => graft.Contains(b.name)))
        {
            var b = JsonUtility.FromJson<Bone>(JsonUtility.ToJson(src));
            b.name = rename[src.name];
            b.length *= s; b.r0 *= sr; b.r1 *= sr; b.blend *= sb;
            if (src == dRoot)
            {
                // корень встаёт в сустав носителя; углы суставов ВНУТРИ цепи — донорские (колено волка гнётся по-волчьи)
                b.parent = cRoot.parent; b.attach = cRoot.attach; b.freeOrigin = cRoot.freeOrigin;
                b.origin = cRoot.origin; b.mirrorX = cRoot.mirrorX;
                b.dir = (Quaternion.Inverse(cParentRot) * rootRot).eulerAngles;
            }
            else
            {
                b.parent = rename.TryGetValue(src.parent, out var p) ? p : src.parent;
                // начало бугра — в кадре родителя: вдоль оси (Y) — длиной, поперёк — толщиной
                b.origin = new Vector3(b.origin.x * sr, b.origin.y * s, b.origin.z * sr);
            }
            added.Add(b);
        }
        carrier.RemoveAll(b => cut.Contains(b.name));
        carrier.AddRange(added);
        body.bones = carrier.ToArray();

        // ГНЁЗДА НА ВЫНУТЫХ КОСТЯХ → ДОНОРСКИЕ того же места: деталь конца конечности садится на свою цепь
        if (body.nests == null) return;
        var nests = new List<PlaceNest>();
        foreach (var n in body.nests)
        {
            if (n == null || !cut.Contains(n.host)) { nests.Add(n); continue; }
            var dn = donor.nests?.FirstOrDefault(x => x != null && x.name == n.name && graft.Contains(x.host));
            if (dn == null) { nests.Add(n); continue; }   // гнезда нет у донора — старое останется без хозяина, его покажет `MissingNests`
            nests.Add(new PlaceNest
            {
                name = dn.name, host = rename[dn.host],
                localPos = new Vector3(dn.localPos.x * sr, dn.localPos.y * s, dn.localPos.z * sr), localRot = dn.localRot,
                unit = dn.unit * sr, mirror = dn.mirror, proposed = dn.proposed, span = dn.span * s,
            });
        }
        body.nests = nests.ToArray();
    }

    /// <summary>Ось цепи в мире: от начала корня до конца последнего сегмента с меткой.</summary>
    static Vector3 ChainAxis(IList<Bone> bones, Bone root, Dictionary<string, Bone> by, Dictionary<string, (Vector3, Quaternion)> placed)
    {
        var start = SkeletonBuilder.Place(root, by, placed).pos;
        var last = root;
        for (var b = root; b != null; b = bones.FirstOrDefault(c => c != null && c.parent == b.name && c.mark != null && !string.IsNullOrEmpty(c.mark.b)))
            last = b;
        var (p, r) = SkeletonBuilder.Place(last, by, placed);
        return SkeletonBuilder.Tip(last, p, r) - start;
    }

    static HashSet<string> Subtree(IEnumerable<Bone> bones, string root)
    {
        var set = new HashSet<string> { root };
        bool grew = true;
        while (grew)
        {
            grew = false;
            foreach (var b in bones)
                if (b != null && !set.Contains(b.name) && b.parent != null && set.Contains(b.parent)) { set.Add(b.name); grew = true; }
        }
        return set;
    }

    /// <summary>Длина цепи по суставам: корень и дальше вниз по детям с меткой на конце (локоть → запястье).
    /// Бугры и сечения меток не несут и в длину не входят.</summary>
    static float PathLength(IList<Bone> bones, Bone root)
    {
        float len = 0f;
        for (var b = root; b != null; b = bones.FirstOrDefault(c => c != null && c.parent == b.name && c.mark != null && !string.IsNullOrEmpty(c.mark.b)))
            len += b.length;
        return len;
    }

    [System.Serializable] class Print { public Bone[] bones; public PlaceNest[] nests; }
    static string Stamp(SpeciesSO s) =>
        JsonUtility.ToJson(new Print { bones = s.bones, nests = s.nests }).GetHashCode().ToString("x8");

    /// <summary>Для тестов: сбросить кэш составов (статика переживает тест).</summary>
    public static void ResetCache() { cache.Clear(); owners.Clear(); }
}
