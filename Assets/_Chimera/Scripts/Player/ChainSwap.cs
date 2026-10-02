using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>АУГМЕНТ ПОДСТАВЛЯЕТ СВОЮ ЧАСТЬ СКЕЛЕТА (спека двух слоёв §3, решение геймдизайнера 13 от 29.09: «ради этого мы и
/// делали скелет графом и унифицировали слоты — чтобы их заменять»).
///
/// ДЕРЕВОМ (01.10, отчёт `Docs/reports/Тело как рекурсивный тип.md`): тело шасси читается в `Tree&lt;BodyNode&gt;`, каждая
/// прививка — чистая функция дерево → дерево, в конце дерево разворачивается в плоский список для билдера. Гнёзда живут
/// в узлах: уходят и приходят вместе со своими костями.
///
/// ЦЕПЬ (Руки, Ноги, Пасть). Фокус — корень цепи по разметке: у конечности — верхний сегмент (плечо → локоть, бедро →
/// колено), у головы — первый узел цепи. Донорское поддерево отображается в калибр носителя (`Map`) и встаёт на место
/// фокуса (`Replace`). Нет цепи у носителя — донорская растёт из гнезда этого места на хребте. Нет цепи у донора —
/// подставлять нечего, детали органа встают в гнездо, как прежде.
///
/// ГРУППА (Сердце). Грудь — не поддерево, а узлы места `Сердце` на хребте: группа носителя срезается (`Prune`), узлы,
/// висевшие на ней (шея и лопатки человека), перевешиваются на хребет без сдвига, донорская группа встаёт по центру,
/// объёму и оси хребта. Что грудь не поддерево — находка отчёта §3.3, развилка §6 у геймдизайнера.
///
/// Результат — копия шасси в памяти (`meshKey` = состав), её и строит `MorphBuilder`; кэш — по составу.</summary>
public static class ChainSwap
{
    enum Kind { Chain, Group }

    /// <summary>Слот → что он подставляет. У цепи `upperEnd` — метка на конце её корневого сегмента; пусто — корень это
    /// первый узел цепи. `calibre` — гнездо, чья единица меряет часть (пусто — длина цепи); `gaze` — часть смотрит, а не
    /// стоит: её поворот донорский в мире, а не по оси носителя.
    ///     Пасть несёт голову целиком (череп, морда, челюсть); признаки чувств рисует Чутьё по гнёздам головы — они
    /// приезжают с донорской головой.
    ///     ХВОСТА ЗДЕСЬ НЕТ — и это не пропуск (01.10). Орган на слоте `Хвост` есть только у змеи, а её хвост — звенья, не
    /// граф: подставлять нечего. Хвост волка, лося и ежа — черта шасси (спека двух слоёв, решение 4), проступает
    /// глобальным слоем. Путь «цепь растёт из гнезда, если у носителя её нет» оставлен: он нужен первому органу с цепью
    /// в графе, которого у носителя нет, — тогда хвост встанет сюда одной строкой.</summary>
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

    // ПОТОЛОК (01.10): составная копия — скрытый ScriptableObject, сам он не выгрузится; вытесненную уничтожаем
    /// <summary>Калибр цепи с деталью: по суставу (радиус сустава носителя к донору — калибр носителя), по досягаемости
    /// (длина цепи носителя к донору — рука достаёт туда же), донорский (деталь своего размера).</summary>
    public enum PartScale { Joint, Reach, Donor }
    public static PartScale PartCalibre = PartScale.Joint;

    static readonly BoundedCache<string, SpeciesSO> cache = new(256, so => { if (so == null) return; if (Application.isPlaying) Object.Destroy(so); else Object.DestroyImmediate(so); });
    static readonly Dictionary<Organ, SpeciesSO> owners = new();

    /// <summary>Тело для сборки: шасси, если прививок нет, иначе составная копия.</summary>
    public static SpeciesSO Compose(SpeciesSO chassis, IReadOnlyList<Organ> worn)
    {
        if (chassis == null || worn == null || chassis.bones == null || chassis.bones.Length == 0) return chassis;

        var grafts = Grafts(chassis, worn);
        // ДЕТАЛИ (спека конструктора): у органа, чей вид принёс деталь своего места, часть рисует деталь, а не поле. Это
        // тоже составное тело — даже на родном шасси без прививок (волк со своей ногой-деталью)
        var parts = PartAssembly.Choose(chassis, worn);
        if (grafts.Count == 0 && parts.Count == 0) return chassis;

        // КЛЮЧ — СОСТАВ И ОТПЕЧАТОК СОДЕРЖИМОГО: по одним именам кэш отдавал бы старое тело после пересоздания видов
        // (та же мина, что у `BoneMesher` по имени вида: ассет тот же, кости другие, ошибки нет)
        string key = chassis.speciesName + "#" + Stamp(chassis) + "|" +
                     string.Join(",", grafts.Select(g => g.e.slot + ":" + g.donor.speciesName + "#" + Stamp(g.donor))) +
                     (parts.Count == 0 ? "" : "|калибр:" + PartCalibre + "|детали:" + string.Join(",", parts.Select(p => p.part.slot + ":" + p.donor.speciesName + "#" +
                                                                                               (p.part.mesh != null ? p.part.mesh.GetInstanceID() : 0))));
        if (cache.TryGetValue(key, out var hit) && hit != null) return hit;

        (Bone[] bones, PlaceNest[] nests) assembled = grafts.Count == 0 ? (BodyTree.Clone(chassis.bones), chassis.nests) : Assemble(chassis, grafts, worn, null);
        if (assembled.bones == null) return chassis;

        var body = ScriptableObject.CreateInstance<SpeciesSO>();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(chassis), body);
        body.hideFlags = HideFlags.HideAndDontSave;
        body.name = key;
        body.meshKey = key;
        body.organs = chassis.organs;   // ОРГАНЫ — ТЕ ЖЕ ОБЪЕКТЫ: копировать их незачем, а тождество нужно поиску владельца
        (body.bones, body.nests) = assembled;
        body.placedParts = parts.ToArray();
        body.fieldSkip = PartAssembly.FieldSkip(body, parts);
        cache[key] = body;
        return body;
    }

    /// <summary>СБОРКА ТЕЛА: `body = local ∘ global` (спека конструктора §5, находка математика консилиума). Глобальный слой —
    /// `zipWith` по форме дерева ШАССИ, а локальный `Replace` эту форму меняет, поэтому определён только этот порядок:
    /// сначала шасси идёт к виду-цели, затем по швам уже смещённого носителя вставляются части-аугменты. Глобального
    /// слоя в коде ещё нет — его место ровно здесь, ПЕРЕД циклом подстановок. Порядок самих подстановок на тело не влияет
    /// (сторож `ChainSwapTests.SlotOrder_DoesNotChangeBody`); `slotOrder` — только для этого сторожа.</summary>
    static (Bone[] bones, PlaceNest[] nests) Assemble(SpeciesSO chassis,
        List<((string slot, string limb, string upperEnd, Kind kind, string calibre, bool gaze) e, SpeciesSO donor)> grafts,
        IReadOnlyList<Organ> worn, IReadOnlyList<string> slotOrder)
    {
        var tree = BodyTree.From(chassis, out var loose);
        if (tree == null) { Debug.LogWarning($"[тело] {chassis.speciesName}: граф не дерево (корней не один) — не подставляю"); return (null, null); }

        // ГЛОБАЛЬНЫЙ СЛОЙ (идентичность → вид-цель) — сюда, когда появится

        var order = slotOrder == null ? grafts : grafts.OrderBy(g => IndexOf(slotOrder, g.e.slot)).ToList();
        foreach (var (e, donor) in order)
        {
            var dTree = BodyTree.From(donor, out _, graft: donor.speciesName);
            if (dTree == null) continue;
            bool hasPart = donor.parts != null && donor.parts.Any(p => p != null && p.slot == e.slot && p.mesh != null);
            tree = e.kind == Kind.Chain ? SwapChain(tree, chassis, dTree, e.limb, e.upperEnd, e.slot, e.calibre, e.gaze, chassis.organs, worn, hasPart)
                                        : SwapGroup(tree, dTree, e.slot, chassis.speciesName, donor.speciesName);
        }
        return BodyTree.Flatten(tree, loose);
    }

    static int IndexOf(IReadOnlyList<string> list, string x) { for (int i = 0; i < list.Count; i++) if (list[i] == x) return i; return int.MaxValue; }

    /// <summary>Для сторожа порядка: собрать тело, подставляя слоты в заданном порядке (кэш не участвует).</summary>
    public static (Bone[] bones, PlaceNest[] nests) AssembleInOrder(SpeciesSO chassis, IReadOnlyList<Organ> worn, IReadOnlyList<string> slotOrder)
    {
        var grafts = Grafts(chassis, worn);
        return grafts.Count == 0 ? (chassis.bones, chassis.nests) : Assemble(chassis, grafts, worn, slotOrder);
    }

    static List<((string slot, string limb, string upperEnd, Kind kind, string calibre, bool gaze) e, SpeciesSO donor)> Grafts(SpeciesSO chassis, IReadOnlyList<Organ> worn)
    {
        var grafts = new List<((string slot, string limb, string upperEnd, Kind kind, string calibre, bool gaze) e, SpeciesSO donor)>();
        foreach (var e in Map)
        {
            var organ = worn.FirstOrDefault(o => o != null && o.slot == e.slot);   // видимый орган слота (порядок — `WornInDrawOrder`)
            var donor = OwnerOf(organ);
            if (donor == null || donor.speciesName == chassis.speciesName || donor.bones == null || donor.bones.Length == 0) continue;
            grafts.Add((e, donor));
        }
        return grafts;
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

    static Tree<BodyNode> SwapChain(Tree<BodyNode> carrier, SpeciesSO chassis, Tree<BodyNode> donor, string limb, string upperEnd,
                                    string slot, string calibre, bool gaze, IReadOnlyList<Organ> own, IReadOnlyList<Organ> worn,
                                    bool hasPart = false)
    {
        var dRoot = ChainRoot(donor, limb, upperEnd);
        if (dRoot == null) return carrier;   // донору нечего дать (хвост змеи — звенья): детали органа встанут в гнездо, как прежде
        var cFocus = ChainRoot(carrier, limb, upperEnd);
        var cPose = BodyTree.Pose(carrier);
        var dPose = BodyTree.Pose(donor);

        if (cFocus == null) return GrowFromNest(carrier, donor, dRoot, slot, cPose, dPose);

        var cParent = carrier.WithParents().First(p => ReferenceEquals(p.node, cFocus)).parent;
        var c = cFocus.Value.Bone;
        var d = dRoot.Value.Bone;
        // ДЛИНА — к длине заменённой цепи (рука дотягивается туда же). ТОЛЩИНА — меньший из двух множителей: длины (так
        // сохраняется отношение толщины к длине, то есть форма донорской конечности) и стыка (радиус сустава носителя к
        // радиусу корня донора). По отдельности оба ломаются одинаково — раздувом, на разных парах (кадры 29–30.09):
        // по стыку человеческая нога с сечениями толще корня надувалась шарами на волке (у волка бедро — масса крупа),
        // по длине та же масса крупа становилась гигантской ляжкой человека. С меньшим множителем цепь не толще ни стыка
        // носителя, ни своей формы — раздуваться нечему; тоньше — допустимо, разницу на стыке сваривает поле
        float s = PathLength(cFocus) / Mathf.Max(1e-4f, PathLength(dRoot));
        float sr = Mathf.Min(s, c.r0 / Mathf.Max(1e-4f, d.r0));
        // КАЛИБР ПО ГНЕЗДУ (голова — по ширине): мера сама гомологична, ею меряется и длина, и толщина
        var cCal = calibre == null ? null : FindNest(carrier, calibre);
        var dCal = calibre == null ? null : FindNest(donor, calibre);
        if (cCal != null && dCal != null && dCal.unit > 1e-4f) s = sr = cCal.unit / dCal.unit;
        // ЦЕПЬ С ДЕТАЛЬЮ — ОДИН МАСШТАБ (спека конструктора §4: калибр — один масштаб на шов). Деталь — авторская форма, и
        // разный масштаб вдоль и поперёк превращает её в жердь (пилот 02.10: длина ×1.7, толщина ×0.6 на человеке). Какой
        // именно масштаб — решение формы, пока выбирается кадром (`PartCalibre`)
        if (hasPart) s = sr = PartCalibre == PartScale.Joint ? c.r0 / Mathf.Max(1e-4f, d.r0)
                            : PartCalibre == PartScale.Reach ? s : 1f;
        // МЯГКОСТЬ СЛИЯНИЯ — РЕЖИМ ПОЛЯ НОСИТЕЛЯ: у волка граф грубый и слияние в 5–7 раз мягче человеческого, в тех же
        // метрах; перенесённое как есть, оно растекает чужую цепь по телу. Берём мягкость сустава носителя, а
        // распределение по узлам цепи — донорское
        float sb = d.blend > 1e-4f ? c.blend / d.blend : 1f;

        // КОРЕНЬ: ОСЬ ЦЕПИ — НОСИТЕЛЯ, ЗИГЗАГ — ДОНОРА. Стойка за шасси (решение 8): конечность приходит туда же, куда
        // приходила конечность носителя. Выравнивается ОСЬ ВСЕЙ ЦЕПИ (сустав корня → конец последнего сегмента), а не
        // первого сегмента: у волка бедро наклонено вперёд на 20°, и угол колена рассчитан на этот наклон — поставь
        // бедро отвесно, и голень ляжет горизонтально (поймано кадром 29.09). Поворот КРАТЧАЙШИЙ: взять кадр носителя
        // целиком нельзя, он бывает развёрнут вокруг оси (человеческое бедро — на 180°) и зеркалит плоскость сгиба.
        // У части-взгляда (голова) поворот донорский в мире
        var cParentRot = cParent != null ? cPose[cParent.Value].rot : Quaternion.identity;
        var dRootRot = dPose[dRoot.Value].rot;
        var rootRot = gaze ? dRootRot
                           : Quaternion.FromToRotation(ChainAxis(dRoot, dPose), ChainAxis(cFocus, cPose)) * dRootRot;
        var graft = Scaled(dRoot, s, sr, sb, b =>
        {
            // корень встаёт в сустав носителя; углы суставов ВНУТРИ цепи — донорские (колено волка гнётся по-волчьи)
            b.parent = c.parent; b.attach = c.attach; b.freeOrigin = c.freeOrigin;
            b.origin = c.origin; b.mirrorX = c.mirrorX;
            b.dir = (Quaternion.Inverse(cParentRot) * rootRot).eulerAngles;
        });

        // КОРЕНЬ ГРАФА ЛЕЖИТ НА ЗЕМЛЕ — это стойка, а стойка за шасси (решение 8). У змеи голова и есть корень: Модельный
        // поставил её так, что нижняя точка на земле. Чужая голова, сев в ту же точку, свешивает челюсть, глаза и нос ниже
        // своей оси и уходит под землю (матрица 01.10: 10 поломок «ниже земли» на 5–10 см у змеи с любой чужой пастью).
        // Поэтому нижняя точка новой части ставится на землю. СРАВНИВАЕТСЯ ОДНО И ТО ЖЕ: до и после — только гнёзда,
        // которые едут с этой частью (у донора сидят на ней). У змеи на голове-корне висят почти все гнёзда, включая
        // предложенные гнёзда ходовой: они с головой не меняются, а в отметку «до» попадали и утягивали её под землю
        if (cParent == null)
        {
            var riding = new HashSet<string>(dRoot.Subtrees().SelectMany(t => t.Value.Nests).Select(n => n.name));
            float before = Lowest(cFocus, cPose, chassis, own, riding);
            float after = Lowest(graft, BodyTree.Pose(graft), chassis, worn, riding);
            // цель — не «как было», а «на земле»: у самой змеи кусок головы на −2 см в допуске детектора, и выравнивание по
            // нему клало чужую голову ровно на порог. Высоты в данных — от земли, земля — y = 0
            graft.Value.Bone.origin.y += Mathf.Max(before, 0f) - after;
        }

        return carrier.Replace(t => ReferenceEquals(t, cFocus), _ => graft);
    }

    /// <summary>ЦЕПИ У НОСИТЕЛЯ НЕТ (хвост человека): донорская растёт из гнезда этого места — оно и есть «корень хвоста ✎»
    /// разметки (письмо Модельного 29b §2). Гнездо обязано сидеть на хребте: цепь растёт из тела, а не из головы (у змеи
    /// все гнёзда на голове — туда не растим). Калибр — единица гнезда носителя к единице того же гнезда донора;
    /// направление — донорское в мире (хвост висит, как висел).</summary>
    static Tree<BodyNode> GrowFromNest(Tree<BodyNode> carrier, Tree<BodyNode> donor, Tree<BodyNode> dRoot, string slot,
                                       Dictionary<BodyNode, (Vector3 pos, Quaternion rot)> cPose,
                                       Dictionary<BodyNode, (Vector3 pos, Quaternion rot)> dPose)
    {
        var host = carrier.Subtrees().FirstOrDefault(t => t.Value.Nests.Any(n => n.name == slot));
        var dNest = FindNest(donor, slot);
        if (host == null || dNest == null || host.Value.Bone.limb != "хребет") return carrier;
        var cNest = host.Value.Nests.First(n => n.name == slot);
        float s = cNest.unit / Mathf.Max(1e-4f, dNest.unit);
        var dParent = donor.WithParents().First(p => ReferenceEquals(p.node, dRoot)).parent;
        float pb = dParent != null ? dParent.Value.Bone.blend : 0f;
        float sb = pb > 1e-4f ? host.Value.Bone.blend / pb : 1f;
        var hostRot = cPose[host.Value].rot;
        var dRootRot = dPose[dRoot.Value].rot;
        var graft = Scaled(dRoot, s, s, sb, b =>
        {
            b.attach = 1f; b.freeOrigin = true;
            b.origin = cNest.localPos;
            b.dir = (Quaternion.Inverse(hostRot) * dRootRot).eulerAngles;
        });
        return carrier.Replace(t => ReferenceEquals(t, host), h => h.WithKids(h.Kids.Append(graft)));
    }

    /// <summary>Донорское поддерево в калибре носителя — функтор по дереву: длина × `s`, толщина × `sr`, мягкость × `sb`;
    /// начало бугра — в кадре родителя (вдоль оси длиной, поперёк толщиной); гнёзда тем же калибром. Корень садится
    /// через `seat`.</summary>
    static Tree<BodyNode> Scaled(Tree<BodyNode> root, float s, float sr, float sb, System.Action<Bone> seat) =>
        root.Map(n =>
        {
            var b = BodyTree.Clone(n.Bone);
            b.length *= s; b.r0 *= sr; b.r1 *= sr; b.blend *= sb;
            if (ReferenceEquals(n, root.Value)) seat(b);
            else b.origin = new Vector3(b.origin.x * sr, b.origin.y * s, b.origin.z * sr);
            var nests = n.Nests.Select(x =>
            {
                var c = BodyTree.Clone(x);
                c.localPos = new Vector3(x.localPos.x * sr, x.localPos.y * s, x.localPos.z * sr);
                c.unit = x.unit * sr; c.span = x.span * s;
                return c;
            }).ToArray();
            return new BodyNode(b, nests, n.Graft);
        });

    /// <summary>Корень цепи: сегмент, кончающийся меткой `upperEnd`, или — без метки — первый узел цепи (родитель из
    /// другой цепи или корень графа).</summary>
    static Tree<BodyNode> ChainRoot(Tree<BodyNode> tree, string limb, string upperEnd) =>
        upperEnd != null
            ? tree.Subtrees().FirstOrDefault(t => t.Value.Bone.limb == limb && t.Value.Bone.mark != null && t.Value.Bone.mark.b == upperEnd)
            : tree.WithParents().Where(p => p.node.Value.Bone.limb == limb && (p.parent == null || p.parent.Value.Bone.limb != limb))
                                .Select(p => p.node).FirstOrDefault();

    /// <summary>НИЖНЯЯ ТОЧКА ЧАСТИ ТЕЛА В МИРЕ — по тому, что действительно рисуется: концы костей минус радиус и куски
    /// органов в гнёздах части `nestNames` (повёрнутой коробкой). Какие куски в каком гнезде — правило билдера: в гнезде
    /// места — куски органа этого места без роли; в месте с `formFrom` — куски своей роли от органа-источника (глаза,
    /// нос — от Чутья). Запасов «на всякий случай» нет: запас в полъединицы гнезда 01.10 утянул под землю саму отметку.</summary>
    static float Lowest(Tree<BodyNode> part, Dictionary<BodyNode, (Vector3 pos, Quaternion rot)> pose, SpeciesSO chassis,
                        IReadOnlyList<Organ> organs, HashSet<string> nestNames)
    {
        float low = float.MaxValue;
        foreach (var n in part.Subtrees().Select(t => t.Value))
        {
            var (p, r) = pose[n];
            var t = SkeletonBuilder.Tip(n.Bone, p, r);
            low = Mathf.Min(low, Mathf.Min(p.y - n.Bone.r0, t.y - n.Bone.r1));
            foreach (var nest in n.Nests.Where(x => nestNames.Contains(x.name)))
            {
                var np = p + r * nest.localPos;
                var nr = r * nest.localRot;
                foreach (var pt in PartsIn(chassis, nest.name, organs))
                {
                    var pr = nr * Quaternion.Euler(pt.euler);
                    var half = pt.scale * nest.unit * 0.5f;
                    float ext = Mathf.Abs((pr * Vector3.right).y) * half.x + Mathf.Abs((pr * Vector3.up).y) * half.y +
                                Mathf.Abs((pr * Vector3.forward).y) * half.z;
                    low = Mathf.Min(low, (np + nr * (pt.offset * nest.unit)).y - ext);
                }
            }
        }
        return low == float.MaxValue ? 0f : low;
    }

    /// <summary>Куски, которые билдер поставит в гнездо места `place`.</summary>
    static IEnumerable<OrganPart> PartsIn(SpeciesSO chassis, string place, IReadOnlyList<Organ> organs)
    {
        if (organs == null) yield break;
        var socket = chassis.sockets?.FirstOrDefault(x => x != null && x.name == place);
        var own = organs.FirstOrDefault(o => o != null && o.slot == place);
        if (own?.visualParts != null)
            foreach (var pt in own.visualParts) if (pt != null && pt.nest && pt.role == PartRole.None) yield return pt;
        if (socket == null || string.IsNullOrEmpty(socket.formFrom)) yield break;
        var src = organs.FirstOrDefault(o => o != null && o.slot == socket.formFrom);
        if (src?.visualParts != null)
            foreach (var pt in src.visualParts) if (pt != null && pt.nest && pt.role == socket.formRole) yield return pt;
    }

    // ── ГРУППА ───────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Срезать у носителя узлы места `socket` и поставить донорские на то же место: по центру, по объёму, по оси
    /// хребта. Грудь — не цепь: у волка один бугор `грудь` на грудном отделе, у человека три сечения и грудные мышцы на
    /// пояснице, — сопоставлять узел к узлу нечего, сопоставляется место целиком.</summary>
    static Tree<BodyNode> SwapGroup(Tree<BodyNode> carrier, Tree<BodyNode> donor, string socket, string carrierName, string donorName)
    {
        bool In(BodyNode n) => n.Bone.socket == socket;
        var cGroup = carrier.WithParents().Where(p => In(p.node.Value)).ToList();
        var dGroup = donor.WithParents().Where(p => In(p.node.Value)).ToList();
        if (cGroup.Count == 0 || dGroup.Count == 0) return carrier;
        var cAnchor = cGroup.FirstOrDefault(p => p.parent != null && !In(p.parent.Value));
        var dAnchor = dGroup.FirstOrDefault(p => p.parent != null && !In(p.parent.Value));
        if (cAnchor.node == null || dAnchor.node == null)
        {
            Debug.LogWarning($"[группа] {carrierName} ← {donorName}: место «{socket}» не висит на хребте — не подставляю");
            return carrier;
        }
        var cHost = cAnchor.parent.Value;
        var cPose = BodyTree.Pose(carrier);
        var dPose = BodyTree.Pose(donor);

        var (cCenter, cSize) = Box(cGroup.Select(p => p.node.Value), cPose);
        var (dCenter, dSize) = Box(dGroup.Select(p => p.node.Value), dPose);
        // РАЗМЕР — ПО ОБЪЁМУ МЕСТА (кубический корень отношения коробок): длина бугра здесь ничего не значит, у человека
        // грудь — три коротких сечения поперёк, у волка — один длинный бугор вдоль
        float vc = cSize.x * cSize.y * cSize.z, vd = dSize.x * dSize.y * dSize.z;
        float s = vd > 1e-9f ? Mathf.Pow(vc / vd, 1f / 3f) : 1f;
        var ca = cAnchor.node.Value.Bone; var da = dAnchor.node.Value.Bone;
        float sb = da.blend > 1e-4f ? ca.blend / da.blend : 1f;
        // ОСЬ — ХРЕБТА НОСИТЕЛЯ: грудь волка лежит вдоль горизонтального хребта, у человека встаёт вертикально
        var (cHostPos, cHostRot) = cPose[cHost];
        var dHostRot = dPose[dAnchor.parent.Value].rot;
        var rg = Quaternion.FromToRotation(dHostRot * Vector3.up, cHostRot * Vector3.up);
        var inv = Quaternion.Inverse(cHostRot);

        // СИРОТЫ: узлы носителя, висевшие на груди (шея и лопатки человека висят на `грудь2`), перевешиваются на хребет
        // и остаются на своих местах в мире — грудь меняется локально, остальное тело не едет. Своё поддерево несут с собой
        var orphans = carrier.WithParents()
            .Where(p => !In(p.node.Value) && p.parent != null && In(p.parent.Value))
            .Select(p =>
            {
                var n = p.node.Value;
                var (op, or) = cPose[n];
                var b = BodyTree.Clone(n.Bone);
                b.freeOrigin = true; b.attach = 1f;
                b.origin = inv * (op - cHostPos);
                b.dir = (inv * or).eulerAngles;
                return new Tree<BodyNode>(new BodyNode(b, n.Nests, n.Graft), p.node.Kids);
            }).ToList();

        // ДОНОРСКАЯ ГРУППА: каждый узел — ребёнок хребта носителя, поза — донорская, перенесённая центром, объёмом и осью
        var placed = dGroup.Select(p =>
        {
            var n = p.node.Value;
            var (dp, dr) = dPose[n];
            var b = BodyTree.Clone(n.Bone);
            b.freeOrigin = true; b.attach = 1f; b.endBone = null;
            b.origin = inv * (cCenter + rg * ((dp - dCenter) * s) - cHostPos);
            b.dir = (inv * (rg * dr)).eulerAngles;
            b.length *= s; b.r0 *= s; b.r1 *= s; b.blend *= sb;
            var nests = n.Nests.Select(x =>
            {
                var c = BodyTree.Clone(x);
                c.localPos = x.localPos * s; c.unit = x.unit * s; c.span = x.span * s;
                return c;
            }).ToArray();
            return new Tree<BodyNode>(new BodyNode(b, nests, donorName));
        }).ToList();

        return carrier.Prune(n => !In(n))
                      .Replace(t => ReferenceEquals(t.Value, cHost), h => h.WithKids(h.Kids.Concat(orphans).Concat(placed)));
    }

    /// <summary>Коробка места в мире: концы узлов с запасом на радиус.</summary>
    static (Vector3 center, Vector3 size) Box(IEnumerable<BodyNode> nodes, Dictionary<BodyNode, (Vector3 pos, Quaternion rot)> pose)
    {
        Vector3 lo = Vector3.one * 1e9f, hi = -lo;
        foreach (var n in nodes)
        {
            var (p, r) = pose[n];
            var t = SkeletonBuilder.Tip(n.Bone, p, r);
            float rad = Mathf.Max(n.Bone.r0, n.Bone.r1);
            lo = Vector3.Min(lo, Vector3.Min(p, t) - Vector3.one * rad);
            hi = Vector3.Max(hi, Vector3.Max(p, t) + Vector3.one * rad);
        }
        return ((lo + hi) * 0.5f, hi - lo);
    }

    // ── ОБЩЕЕ ────────────────────────────────────────────────────────────────────────────────────────

    static PlaceNest FindNest(Tree<BodyNode> tree, string name) =>
        tree.Subtrees().SelectMany(t => t.Value.Nests).FirstOrDefault(n => n.name == name);

    /// <summary>Следующий сустав цепи: ребёнок той же цепи с меткой на конце или сидящий на конце родителя. Бугры и
    /// сечения (свободное начало, без метки) — не суставы.</summary>
    static Tree<BodyNode> MainChild(Tree<BodyNode> t) =>
        t.Kids.FirstOrDefault(k => k.Value.Bone.limb == t.Value.Bone.limb &&
                                   ((k.Value.Bone.mark != null && !string.IsNullOrEmpty(k.Value.Bone.mark.b)) || !k.Value.Bone.freeOrigin));

    /// <summary>Ось цепи в мире: от начала корня до конца последнего сустава.</summary>
    static Vector3 ChainAxis(Tree<BodyNode> root, Dictionary<BodyNode, (Vector3 pos, Quaternion rot)> pose)
    {
        var last = root;
        for (var t = root; t != null; t = MainChild(t)) last = t;
        var (p, r) = pose[last.Value];
        return SkeletonBuilder.Tip(last.Value.Bone, p, r) - pose[root.Value].pos;
    }

    /// <summary>Длина цепи по суставам: корень и дальше вниз. Бугры и сечения в длину не входят.</summary>
    static float PathLength(Tree<BodyNode> root)
    {
        float len = 0f;
        for (var t = root; t != null; t = MainChild(t)) len += t.Value.Bone.length;
        return len;
    }

    [System.Serializable] class Print { public Bone[] bones; public PlaceNest[] nests; }
    static string Stamp(SpeciesSO s) =>
        JsonUtility.ToJson(new Print { bones = s.bones, nests = s.nests }).GetHashCode().ToString("x8");

    /// <summary>Для тестов: сбросить кэш составов (статика переживает тест).</summary>
    public static void ResetCache() { cache.Clear(); owners.Clear(); }
}
