using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>АУГМЕНТ ПОДСТАВЛЯЕТ СВОЮ ЧАСТЬ СКЕЛЕТА (спека двух слоёв §3, решение геймдизайнера 13 от 29.09: «ради этого мы и
/// делали скелет графом и унифицировали слоты — чтобы их заменять»).
///
/// ЦЕПЬ (Руки, Ноги, Хвост, Пасть). Граф шасси режется по разметке цепей: у конечности — по верхнему сегменту (плечо →
/// локоть, бедро → колено), у хвоста и головы — по первому узлу цепи; всё выше остаётся шасси. Вынутый участок
/// заменяется цепью донора — её узлами, топологией, буграми, — сведённой к калибру носителя. Нет цепи у носителя (хвост
/// человека) — донорская растёт из гнезда этого места на хребте. Нет цепи у донора (хвост змеи — звенья, не граф) —
/// подставлять нечего, детали органа встают в гнездо, как прежде.
///
/// ГРУППА (Сердце). Грудь — не цепь, а узлы места `Сердце` на хребте (бугры грудной клетки). Группа донора встаёт на
/// место группы носителя: по центру, по объёму и по оси хребта; узлы носителя, висевшие на груди (шея и лопатки
/// человека), перевешиваются на хребет, не сдвигаясь.
///
/// Оболочка поля строится по собранному скелету — шов на разрезе сваривает поле. Гнёзда на вынутых костях заменяются
/// донорскими (детали конца конечности и чувства головы едут со своей частью). Результат — копия шасси в памяти
/// (`meshKey` = состав), её и строит `MorphBuilder`; кэш — по составу.</summary>
public static class ChainSwap
{
    enum Kind { Chain, Group }

    /// <summary>Слот → что он подставляет. У цепи `upperEnd` — метка на конце её корневого сегмента; пусто — корень это
    /// первый узел цепи. Пасть несёт голову целиком (череп, морда, челюсть); признаки чувств рисует Чутьё по гнёздам
    /// головы — они приезжают с донорской головой.
    ///     ХВОСТА ЗДЕСЬ НЕТ — и это не пропуск (01.10). Орган на слоте `Хвост` есть только у змеи, а её хвост — звенья, не
    /// граф: подставлять нечего. Хвост волка, лося и ежа — черта шасси (спека двух слоёв, решение 4), проступает
    /// глобальным слоем. Путь «цепь растёт из гнезда, если у носителя её нет» оставлен: он нужен первому органу с цепью
    /// в графе, которого у носителя нет, — тогда хвост встанет сюда одной строкой.</summary>
    /// `calibre` — гнездо, чья единица меряет часть (пусто — длина цепи); `gaze` — часть смотрит, а не стоит: её поворот
    /// донорский в мире, а не по оси носителя.
    static readonly (string slot, string limb, string upperEnd, Kind kind, string calibre, bool gaze)[] Map =
    {
        (BodySlots.Arms, "перед", "локоть", Kind.Chain, null, false),
        (BodySlots.Legs, "зад", "колено", Kind.Chain, null, false),
        // ГОЛОВА — ВЗГЛЯД, А НЕ СТОЙКА (кадр 01.10: волчья голова на человеке смотрела в небо). Кость `голова` у видов
        // не гомологична: у человека она идёт от шеи к макушке, у волка — вдоль морды. Поэтому ни ось, ни длину кости
        // сравнивать нельзя: морда смотрит вперёд у любого носителя (поворот донорский в мире), а калибр — ширина головы,
        // единица гнезда `голова`
        (BodySlots.Maw, "голова", null, Kind.Chain, "голова", true),
        (BodySlots.Heart, null, null, Kind.Group, null, false),
    };

    static readonly Dictionary<string, SpeciesSO> cache = new();
    static readonly Dictionary<Organ, SpeciesSO> owners = new();

    /// <summary>Тело для сборки: шасси, если прививок нет, иначе составная копия.</summary>
    public static SpeciesSO Compose(SpeciesSO chassis, IReadOnlyList<Organ> worn)
    {
        if (chassis == null || worn == null || chassis.bones == null || chassis.bones.Length == 0) return chassis;

        var grafts = new List<(string slot, string limb, string upperEnd, Kind kind, SpeciesSO donor)>();
        foreach (var (slot, limb, upperEnd, kind, _, _) in Map)
        {
            var organ = worn.FirstOrDefault(o => o != null && o.slot == slot);   // видимый орган слота (порядок — `WornInDrawOrder`)
            var donor = OwnerOf(organ);
            if (donor == null || donor.speciesName == chassis.speciesName || donor.bones == null || donor.bones.Length == 0) continue;
            grafts.Add((slot, limb, upperEnd, kind, donor));
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
        foreach (var g in grafts)
            if (g.kind == Kind.Chain) SwapChain(body, g.donor, g.limb, g.upperEnd, own: chassis.organs, worn: worn);
            else SwapGroup(body, g.donor, g.slot);
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

    // ── ЦЕПЬ ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Вынуть у `body` цепь `limb` и вставить донорскую.</summary>
    static void SwapChain(SpeciesSO body, SpeciesSO donor, string limb, string upperEnd,
                          IReadOnlyList<Organ> own, IReadOnlyList<Organ> worn)
    {
        var carrier = body.bones.ToList();
        var entry = Map.First(m => m.limb == limb);
        string slot = entry.slot;
        var dRoot = ChainRoot(donor.bones, limb, upperEnd);
        if (dRoot == null) return;   // донору нечего дать (хвост змеи — звенья): детали органа встанут в гнездо, как прежде
        var cRoot = ChainRoot(carrier, limb, upperEnd);

        var cBy = ByName(carrier);
        var cPlaced = new Dictionary<string, (Vector3, Quaternion)>();
        var dBy = ByName(donor.bones);
        var dPlaced = new Dictionary<string, (Vector3, Quaternion)>();
        var dRootRot = SkeletonBuilder.Place(dRoot, dBy, dPlaced).rot;
        var graft = Subtree(donor.bones, dRoot.name);

        HashSet<string> cut;
        float s, sr, sb;
        System.Action<Bone> seat;
        if (cRoot != null)
        {
            cut = Subtree(carrier, cRoot.name);
            // ДЛИНА — к длине заменённой цепи (рука дотягивается туда же). ТОЛЩИНА — меньший из двух множителей: длины (так
            // сохраняется отношение толщины к длине, то есть форма донорской конечности) и стыка (радиус сустава носителя к
            // радиусу корня донора). По отдельности оба ломаются одинаково — раздувом, на разных парах (кадры 29–30.09):
            // по стыку человеческая нога с сечениями толще корня надувалась шарами на волке (у волка бедро — масса крупа),
            // по длине та же масса крупа становилась гигантской ляжкой человека. С меньшим множителем цепь не толще ни стыка
            // носителя, ни своей формы — раздуваться нечему; тоньше — допустимо, разницу на стыке сваривает поле
            s = PathLength(carrier, cRoot) / Mathf.Max(1e-4f, PathLength(donor.bones, dRoot));
            sr = Mathf.Min(s, cRoot.r0 / Mathf.Max(1e-4f, dRoot.r0));
            // КАЛИБР ПО ГНЕЗДУ (голова — по ширине): мера сама гомологична, ею меряется и длина, и толщина
            var cCal = entry.calibre == null ? null : body.nests?.FirstOrDefault(n => n != null && n.name == entry.calibre);
            var dCal = entry.calibre == null ? null : donor.nests?.FirstOrDefault(n => n != null && n.name == entry.calibre);
            if (cCal != null && dCal != null && dCal.unit > 1e-4f) s = sr = cCal.unit / dCal.unit;
            // МЯГКОСТЬ СЛИЯНИЯ — РЕЖИМ ПОЛЯ НОСИТЕЛЯ: у волка граф грубый и слияние в 5–7 раз мягче человеческого, в тех же
            // метрах; перенесённое как есть, оно растекает чужую цепь по телу. Берём мягкость сустава носителя, а
            // распределение по узлам цепи — донорское
            sb = dRoot.blend > 1e-4f ? cRoot.blend / dRoot.blend : 1f;

            // КОРЕНЬ: ОСЬ ЦЕПИ — НОСИТЕЛЯ, ЗИГЗАГ — ДОНОРА. Стойка за шасси (решение 8): конечность приходит туда же, куда
            // приходила конечность носителя. Выравнивается ОСЬ ВСЕЙ ЦЕПИ (сустав корня → конец последнего сегмента), а не
            // первого сегмента: у волка бедро наклонено вперёд на 20°, и угол колена рассчитан на этот наклон — поставь
            // бедро отвесно, и голень ляжет горизонтально (поймано кадром 29.09). Поворот КРАТЧАЙШИЙ: взять кадр носителя
            // целиком нельзя, он бывает развёрнут вокруг оси (человеческое бедро — на 180°) и зеркалит плоскость сгиба
            var cParentRot = cBy.TryGetValue(cRoot.parent ?? "", out var cp) ? SkeletonBuilder.Place(cp, cBy, cPlaced).rot : Quaternion.identity;
            var rootRot = entry.gaze ? dRootRot
                        : Quaternion.FromToRotation(ChainAxis(donor.bones, dRoot, dBy, dPlaced), ChainAxis(carrier, cRoot, cBy, cPlaced)) * dRootRot;
            var c = cRoot;
            seat = b =>
            {
                // корень встаёт в сустав носителя; углы суставов ВНУТРИ цепи — донорские (колено волка гнётся по-волчьи)
                b.parent = c.parent; b.attach = c.attach; b.freeOrigin = c.freeOrigin;
                b.origin = c.origin; b.mirrorX = c.mirrorX;
                b.dir = (Quaternion.Inverse(cParentRot) * rootRot).eulerAngles;
            };
        }
        else
        {
            // ЦЕПИ У НОСИТЕЛЯ НЕТ (хвост человека): донорская растёт из гнезда этого места — оно и есть «корень хвоста ✎»
            // разметки (письмо Модельного 29b §2). Гнездо обязано сидеть на хребте: цепь растёт из тела, а не из головы
            // (у змеи все гнёзда на голове — хвост звеньями, туда не растим). Калибр — единица гнезда носителя к единице
            // того же гнезда донора; направление — донорское в мире (хвост висит, как висел)
            var cNest = body.nests?.FirstOrDefault(n => n != null && n.name == slot);
            var dNest = donor.nests?.FirstOrDefault(n => n != null && n.name == slot);
            if (cNest == null || dNest == null || !cBy.TryGetValue(cNest.host ?? "", out var host) || host.limb != "хребет") return;
            cut = new HashSet<string>();
            s = sr = cNest.unit / Mathf.Max(1e-4f, dNest.unit);
            var dParent = dBy.TryGetValue(dRoot.parent ?? "", out var dp) ? dp : null;
            sb = dParent != null && dParent.blend > 1e-4f ? host.blend / dParent.blend : 1f;
            var hostRot = SkeletonBuilder.Place(host, cBy, cPlaced).rot;
            seat = b =>
            {
                b.parent = host.name; b.attach = 1f; b.freeOrigin = true;
                b.origin = cNest.localPos;
                b.dir = (Quaternion.Inverse(hostRot) * dRootRot).eulerAngles;
            };
        }

        // КОРЕНЬ ГРАФА ЛЕЖИТ НА ЗЕМЛЕ — это стойка, а стойка за шасси (решение 8). У змеи голова и есть корень: Модельный
        // поставил её так, что нижняя точка на земле. Чужая голова, сев в ту же точку, свешивает челюсть, глаза и нос ниже
        // своей оси и уходит под землю (матрица 01.10: 10 поломок «ниже земли» на 5–10 см у змеи с любой чужой пастью).
        // Поэтому нижняя точка новой части ставится туда же, где была нижняя точка старой
        bool root = cRoot != null && string.IsNullOrEmpty(cRoot.parent);
        // СРАВНИВАТЬ ОДНО И ТО ЖЕ: до и после — только гнёзда, которые едут с этой частью (у донора сидят на ней). У змеи
        // на голове-корне висят почти все гнёзда, включая предложенные гнёзда ходовой: они с головой не меняются, а в
        // отметку «до» попадали и утягивали её под землю (01.10, вторая итерация)
        var riding = new HashSet<string>((donor.nests ?? new PlaceNest[0]).Where(n => n != null && graft.Contains(n.host ?? "")).Select(n => n.name));
        float lowBefore = root ? Lowest(body, cut, carrier, cBy, own, riding) : 0f;   // до: органы шасси

        var rename = Insert(carrier, donor, graft, cut, dRoot, seat, s, sr, sb);
        body.bones = carrier.ToArray();
        SwapNests(body, donor, cut, graft, rename, s, sr);

        if (root)
        {
            var placedSet = new HashSet<string>(graft.Select(n => rename[n]));
            var newRoot = carrier.First(b => b.name == rename[dRoot.name]);
            // цель — не «как было», а «на земле»: у самой змеи кусок головы на −2 см в допуске детектора, и выравнивание по
            // нему клало чужую голову ровно на порог. Высоты в данных — от земли, земля — y = 0
            newRoot.origin.y += Mathf.Max(lowBefore, 0f) - Lowest(body, placedSet, carrier, ByName(carrier), worn, riding);   // после: надетые
        }
    }

    /// <summary>НИЖНЯЯ ТОЧКА ЧАСТИ ТЕЛА В МИРЕ — по тому, что действительно рисуется: концы костей минус радиус и куски
    /// органов в гнёздах части (повёрнутой коробкой). Какие куски в каком гнезде — правило билдера: в гнезде места —
    /// куски органа этого места без роли; в месте с `formFrom` — куски своей роли от органа-источника (глаза, нос —
    /// от Чутья). Запасов «на всякий случай» нет: запас в полъединицы гнезда 01.10 утянул под землю саму отметку, и
    /// новая голова честно встала на ложную высоту.</summary>
    static float Lowest(SpeciesSO body, HashSet<string> part, IList<Bone> bones, Dictionary<string, Bone> by,
                        IReadOnlyList<Organ> organs, HashSet<string> nestNames)
    {
        var placed = new Dictionary<string, (Vector3, Quaternion)>();
        float low = float.MaxValue;
        foreach (var b in bones.Where(x => x != null && part.Contains(x.name)))
        {
            var (p, r) = SkeletonBuilder.Place(b, by, placed);
            var t = SkeletonBuilder.Tip(b, p, r);
            low = Mathf.Min(low, Mathf.Min(p.y - b.r0, t.y - b.r1));
        }
        foreach (var n in body.nests ?? new PlaceNest[0])
        {
            if (n == null || !nestNames.Contains(n.name) || !part.Contains(n.host ?? "") || !by.TryGetValue(n.host, out var h)) continue;
            var (hp, hr) = SkeletonBuilder.Place(h, by, placed);
            var np = hp + hr * n.localPos;
            var nr = hr * n.localRot;
            foreach (var pt in PartsIn(body, n.name, organs))
            {
                var pr = nr * Quaternion.Euler(pt.euler);
                var half = pt.scale * n.unit * 0.5f;
                float ext = Mathf.Abs((pr * Vector3.right).y) * half.x + Mathf.Abs((pr * Vector3.up).y) * half.y +
                            Mathf.Abs((pr * Vector3.forward).y) * half.z;
                low = Mathf.Min(low, (np + nr * (pt.offset * n.unit)).y - ext);
            }
        }
        return low == float.MaxValue ? 0f : low;
    }

    /// <summary>Куски, которые билдер поставит в гнездо места `place`.</summary>
    static IEnumerable<OrganPart> PartsIn(SpeciesSO body, string place, IReadOnlyList<Organ> organs)
    {
        if (organs == null) yield break;
        var socket = body.sockets?.FirstOrDefault(x => x != null && x.name == place);
        var own = organs.FirstOrDefault(o => o != null && o.slot == place);
        if (own?.visualParts != null)
            foreach (var pt in own.visualParts) if (pt != null && pt.nest && pt.role == PartRole.None) yield return pt;
        if (socket == null || string.IsNullOrEmpty(socket.formFrom)) yield break;
        var src = organs.FirstOrDefault(o => o != null && o.slot == socket.formFrom);
        if (src?.visualParts != null)
            foreach (var pt in src.visualParts) if (pt != null && pt.nest && pt.role == socket.formRole) yield return pt;
    }

    /// <summary>Корень цепи: сегмент, кончающийся меткой `upperEnd`, или — без метки — первый узел цепи (родитель из
    /// другой цепи или корень графа).</summary>
    static Bone ChainRoot(IList<Bone> bones, string limb, string upperEnd)
    {
        if (upperEnd != null) return bones.FirstOrDefault(b => b != null && b.limb == limb && b.mark != null && b.mark.b == upperEnd);
        return bones.FirstOrDefault(b => b != null && b.limb == limb &&
                                         !bones.Any(p => p != null && p.name == b.parent && p.limb == limb));
    }

    /// <summary>Вставить узлы донора `graft` вместо `cut`: корень садится через `seat`, остальные масштабируются в кадре
    /// своего родителя (вдоль оси — длиной, поперёк — толщиной). Имена, занятые оставшимися узлами носителя, получают
    /// суффикс донора.</summary>
    static Dictionary<string, string> Insert(List<Bone> carrier, SpeciesSO donor, HashSet<string> graft, HashSet<string> cut,
                                             Bone dRoot, System.Action<Bone> seat, float s, float sr, float sb)
    {
        var kept = new HashSet<string>(carrier.Where(b => !cut.Contains(b.name)).Select(b => b.name));
        var rename = new Dictionary<string, string>();
        foreach (var n in graft) rename[n] = kept.Contains(n) ? n + "~" + donor.speciesName : n;

        var added = new List<Bone>();
        foreach (var src in donor.bones.Where(b => graft.Contains(b.name)))
        {
            var b = Clone(src);
            b.name = rename[src.name];
            b.length *= s; b.r0 *= sr; b.r1 *= sr; b.blend *= sb;
            if (src == dRoot) seat(b);
            else
            {
                b.parent = rename.TryGetValue(src.parent, out var p) ? p : src.parent;
                b.origin = new Vector3(b.origin.x * sr, b.origin.y * s, b.origin.z * sr);
            }
            added.Add(b);
        }
        carrier.RemoveAll(b => cut.Contains(b.name));
        carrier.AddRange(added);
        return rename;
    }

    // ── ГРУППА ───────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Вынуть у `body` узлы места `socket` и поставить донорские на то же место: по центру, по объёму, по оси
    /// хребта. Грудь — не цепь: у волка один бугор `грудь` на грудном отделе, у человека три сечения и грудные мышцы на
    /// пояснице, — сопоставлять узел к узлу нечего, сопоставляется место целиком.</summary>
    static void SwapGroup(SpeciesSO body, SpeciesSO donor, string socket)
    {
        var carrier = body.bones.ToList();
        var cSet = new HashSet<string>(carrier.Where(b => b != null && b.socket == socket).Select(b => b.name));
        var dSet = new HashSet<string>(donor.bones.Where(b => b != null && b.socket == socket).Select(b => b.name));
        if (cSet.Count == 0 || dSet.Count == 0) return;

        var cBy = ByName(carrier); var cPlaced = new Dictionary<string, (Vector3, Quaternion)>();
        var dBy = ByName(donor.bones); var dPlaced = new Dictionary<string, (Vector3, Quaternion)>();
        var cAnchor = carrier.First(b => cSet.Contains(b.name) && !cSet.Contains(b.parent ?? ""));
        var dAnchor = donor.bones.First(b => dSet.Contains(b.name) && !dSet.Contains(b.parent ?? ""));
        if (!cBy.TryGetValue(cAnchor.parent ?? "", out var cHost) || !dBy.TryGetValue(dAnchor.parent ?? "", out var dHost))
        {
            Debug.LogWarning($"[группа] {body.speciesName} ← {donor.speciesName}: место «{socket}» не висит на хребте — не подставляю");
            return;
        }

        var (cCenter, cSize) = Box(cSet, carrier, cBy, cPlaced);
        var (dCenter, dSize) = Box(dSet, donor.bones, dBy, dPlaced);
        // РАЗМЕР — ПО ОБЪЁМУ МЕСТА (кубический корень отношения коробок): длина бугра здесь ничего не значит, у человека
        // грудь — три коротких сечения поперёк, у волка — один длинный бугор вдоль
        float vc = cSize.x * cSize.y * cSize.z, vd = dSize.x * dSize.y * dSize.z;
        float s = vd > 1e-9f ? Mathf.Pow(vc / vd, 1f / 3f) : 1f;
        float sb = dAnchor.blend > 1e-4f ? cAnchor.blend / dAnchor.blend : 1f;
        // ОСЬ — ХРЕБТА НОСИТЕЛЯ: грудь волка лежит вдоль горизонтального хребта, у человека встаёт вертикально
        var (cHostPos, cHostRot) = SkeletonBuilder.Place(cHost, cBy, cPlaced);
        var dHostRot = SkeletonBuilder.Place(dHost, dBy, dPlaced).rot;
        var rg = Quaternion.FromToRotation(dHostRot * Vector3.up, cHostRot * Vector3.up);
        var inv = Quaternion.Inverse(cHostRot);

        // СИРОТЫ: узлы носителя, висевшие на груди (шея и лопатки человека висят на `грудь2`), перевешиваются на хребет
        // и остаются на своих местах в мире — грудь меняется локально, остальное тело не едет
        foreach (var o in carrier.Where(b => !cSet.Contains(b.name) && cSet.Contains(b.parent ?? "")).ToList())
        {
            var (op, or) = SkeletonBuilder.Place(o, cBy, cPlaced);
            o.parent = cHost.name; o.freeOrigin = true; o.attach = 1f;
            o.origin = inv * (op - cHostPos);
            o.dir = (inv * or).eulerAngles;
        }

        var kept = new HashSet<string>(carrier.Where(b => !cSet.Contains(b.name)).Select(b => b.name));
        var rename = new Dictionary<string, string>();
        foreach (var n in dSet) rename[n] = kept.Contains(n) ? n + "~" + donor.speciesName : n;
        var added = new List<Bone>();
        foreach (var src in donor.bones.Where(b => dSet.Contains(b.name)))
        {
            var (p, r) = SkeletonBuilder.Place(src, dBy, dPlaced);
            var b = Clone(src);
            b.name = rename[src.name];
            b.parent = cHost.name; b.freeOrigin = true; b.attach = 1f; b.endBone = null;
            b.origin = inv * (cCenter + rg * ((p - dCenter) * s) - cHostPos);
            b.dir = (inv * (rg * r)).eulerAngles;
            b.length *= s; b.r0 *= s; b.r1 *= s; b.blend *= sb;
            added.Add(b);
        }
        carrier.RemoveAll(b => cSet.Contains(b.name));
        carrier.AddRange(added);
        body.bones = carrier.ToArray();
        SwapNests(body, donor, cSet, dSet, rename, s, s);
    }

    /// <summary>Коробка места в мире: концы узлов с запасом на радиус.</summary>
    static (Vector3 center, Vector3 size) Box(HashSet<string> set, IList<Bone> bones, Dictionary<string, Bone> by,
                                              Dictionary<string, (Vector3, Quaternion)> placed)
    {
        Vector3 lo = Vector3.one * 1e9f, hi = -lo;
        foreach (var b in bones.Where(x => x != null && set.Contains(x.name)))
        {
            var (p, r) = SkeletonBuilder.Place(b, by, placed);
            var t = SkeletonBuilder.Tip(b, p, r);
            float rad = Mathf.Max(b.r0, b.r1);
            lo = Vector3.Min(lo, Vector3.Min(p, t) - Vector3.one * rad);
            hi = Vector3.Max(hi, Vector3.Max(p, t) + Vector3.one * rad);
        }
        return ((lo + hi) * 0.5f, hi - lo);
    }

    // ── ОБЩЕЕ ────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>ГНЁЗДА: место, чьё гнездо сидело на вынутой кости или у донора сидит на вставленной, берёт донорское —
    /// деталь конца конечности, чувства головы, кончик хвоста едут со своей частью.</summary>
    static void SwapNests(SpeciesSO body, SpeciesSO donor, HashSet<string> cut, HashSet<string> graft,
                          Dictionary<string, string> rename, float s, float sr)
    {
        if (body.nests == null) return;
        var nests = new List<PlaceNest>();
        foreach (var n in body.nests)
        {
            var dn = n == null ? null : donor.nests?.FirstOrDefault(x => x != null && x.name == n.name && graft.Contains(x.host));
            if (dn == null) { nests.Add(n); continue; }   // нет донорского — гнездо носителя; на вынутой кости его покажет `MissingNests`
            nests.Add(new PlaceNest
            {
                name = dn.name, host = rename[dn.host],
                localPos = new Vector3(dn.localPos.x * sr, dn.localPos.y * s, dn.localPos.z * sr), localRot = dn.localRot,
                unit = dn.unit * sr, mirror = dn.mirror, proposed = dn.proposed, span = dn.span * s,
            });
        }
        body.nests = nests.ToArray();
    }

    static Dictionary<string, Bone> ByName(IEnumerable<Bone> bones) =>
        bones.Where(b => b != null && !string.IsNullOrEmpty(b.name)).ToDictionary(b => b.name);

    static Bone Clone(Bone b) => JsonUtility.FromJson<Bone>(JsonUtility.ToJson(b));

    /// <summary>Следующий сустав цепи: ребёнок той же цепи с меткой на конце или сидящий на конце родителя. Бугры и
    /// сечения (свободное начало, без метки) — не суставы.</summary>
    static Bone MainChild(IList<Bone> bones, Bone b) =>
        bones.FirstOrDefault(c => c != null && c.parent == b.name && c.limb == b.limb &&
                                  ((c.mark != null && !string.IsNullOrEmpty(c.mark.b)) || !c.freeOrigin));

    /// <summary>Ось цепи в мире: от начала корня до конца последнего сустава.</summary>
    static Vector3 ChainAxis(IList<Bone> bones, Bone root, Dictionary<string, Bone> by, Dictionary<string, (Vector3, Quaternion)> placed)
    {
        var start = SkeletonBuilder.Place(root, by, placed).pos;
        var last = root;
        for (var b = root; b != null; b = MainChild(bones, b)) last = b;
        var (p, r) = SkeletonBuilder.Place(last, by, placed);
        return SkeletonBuilder.Tip(last, p, r) - start;
    }

    /// <summary>Длина цепи по суставам: корень и дальше вниз. Бугры и сечения в длину не входят.</summary>
    static float PathLength(IList<Bone> bones, Bone root)
    {
        float len = 0f;
        for (var b = root; b != null; b = MainChild(bones, b)) len += b.length;
        return len;
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

    [System.Serializable] class Print { public Bone[] bones; public PlaceNest[] nests; }
    static string Stamp(SpeciesSO s) =>
        JsonUtility.ToJson(new Print { bones = s.bones, nests = s.nests }).GetHashCode().ToString("x8");

    /// <summary>Для тестов: сбросить кэш составов (статика переживает тест).</summary>
    public static void ResetCache() { cache.Clear(); owners.Clear(); }
}
