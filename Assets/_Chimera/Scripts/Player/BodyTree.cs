using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>УЗЕЛ ТЕЛА В ДЕРЕВЕ: кость и гнёзда, которые на ней живут. Гнездо — часть узла, а не ссылка на него по имени:
/// заменил поддерево — его гнёзда ушли вместе с ним, донорские пришли со своими костями. `Graft` — вид-донор, если узел
/// пришёл прививкой (по нему при развёртке решается, чьё имя уступает и чьё гнездо главнее).</summary>
public sealed class BodyNode
{
    public readonly Bone Bone;
    public readonly PlaceNest[] Nests;
    public readonly string Graft;

    public BodyNode(Bone bone, PlaceNest[] nests, string graft = null)
    {
        Bone = bone;
        Nests = nests ?? new PlaceNest[0];
        Graft = graft;
    }
}

/// <summary>ТЕЛО КАК РЕКУРСИВНЫЙ ТИП (отчёт `Docs/reports/Тело как рекурсивный тип.md` §5). Хранится тело плоским
/// списком (сериализация Unity и поставка модельной линии), а ПРАВИТСЯ деревом: `From` строит дерево из плоского, все
/// операции композиции идут чистыми функциями над `Tree&lt;BodyNode&gt;`, `Flatten` разворачивает обратно — родитель
/// раньше детей, имена уникальны, хозяева гнёзд выводятся из структуры. Исполнение (кости, меш, анимация) остаётся
/// плоским, как было.</summary>
public static class BodyTree
{
    /// <summary>Дерево из вида. Узлы и гнёзда КОПИРУЮТСЯ: ассет вида не правится никогда. Гнёзда, чей хозяин не узел
    /// графа (звенья цепи змеи `звено:N`), — в `loose`, развёртка вернёт их как были. Корней не один — null: граф
    /// обязан быть связным (`Graph.Check`), чинить его здесь нечем.</summary>
    public static Tree<BodyNode> From(SpeciesSO s, out List<PlaceNest> loose, string graft = null)
    {
        loose = new List<PlaceNest>();
        var bones = (s.bones ?? new Bone[0]).Where(b => b != null && !string.IsNullOrEmpty(b.name)).ToList();
        var names = new HashSet<string>(bones.Select(b => b.name));
        var nestsBy = new Dictionary<string, List<PlaceNest>>();
        foreach (var n in s.nests ?? new PlaceNest[0])
        {
            if (n == null) continue;
            var c = Clone(n);
            if (!names.Contains(n.host ?? "")) { loose.Add(c); continue; }
            if (!nestsBy.TryGetValue(n.host, out var l)) nestsBy[n.host] = l = new List<PlaceNest>();
            l.Add(c);
        }
        var kidsOf = bones.GroupBy(b => b.parent ?? "").ToDictionary(g => g.Key, g => g.ToList());
        var roots = bones.Where(b => string.IsNullOrEmpty(b.parent) || !names.Contains(b.parent)).ToList();
        if (roots.Count != 1) return null;

        Tree<BodyNode> Build(Bone b, int depth) =>
            new(new BodyNode(Clone(b), nestsBy.TryGetValue(b.name, out var ns) ? ns.ToArray() : null, graft),
                depth > 64 || !kidsOf.TryGetValue(b.name, out var ks) ? null : ks.Select(k => Build(k, depth + 1)));
        return Build(roots[0], 0);
    }

    /// <summary>Развернуть в плоское: прямой порядок (родитель раньше детей — так строит `BoneMesher`), родитель — из
    /// структуры. Имя привитого узла, занятое узлом носителя, получает суффикс донора; гнездо привитого узла главнее
    /// одноимённого гнезда носителя (деталь едет со своей частью).</summary>
    public static (Bone[] bones, PlaceNest[] nests) Flatten(Tree<BodyNode> tree, IEnumerable<PlaceNest> loose)
    {
        var all = tree.Subtrees().Select(t => t.Value).ToList();
        var used = new HashSet<string>(all.Where(n => n.Graft == null).Select(n => n.Bone.name));
        var name = new Dictionary<BodyNode, string>();
        foreach (var n in all)
        {
            string nm = n.Bone.name;
            if (n.Graft != null)
            {
                if (used.Contains(nm)) nm += "~" + n.Graft;
                for (int k = 2; name.ContainsValue(nm); k++) nm = n.Bone.name + "~" + n.Graft + k;
            }
            name[n] = nm;
        }
        var grafted = new HashSet<string>(all.Where(n => n.Graft != null).SelectMany(n => n.Nests).Select(x => x.name));

        var bones = new List<Bone>();
        var nests = new List<PlaceNest>();
        foreach (var (node, parent) in tree.WithParents())
        {
            var b = Clone(node.Value.Bone);
            b.name = name[node.Value];
            b.parent = parent == null ? node.Value.Bone.parent : name[parent.Value];
            bones.Add(b);
            foreach (var n in node.Value.Nests)
            {
                if (node.Value.Graft == null && grafted.Contains(n.name)) continue;   // уступает привитому
                var c = Clone(n);
                c.host = b.name;
                nests.Add(c);
            }
        }
        nests.AddRange(loose.Where(n => !grafted.Contains(n.name)).Select(Clone));
        return (bones.ToArray(), nests.ToArray());
    }

    /// <summary>ПОЗА — наследуемый атрибут: кадр каждого узла из кадра родителя по единой формуле `SkeletonBuilder.Child`.
    /// Ключ — сам узел (по ссылке): имена в дереве вторичны.</summary>
    public static Dictionary<BodyNode, (Vector3 pos, Quaternion rot)> Pose(Tree<BodyNode> tree)
    {
        var pose = new Dictionary<BodyNode, (Vector3, Quaternion)>();
        tree.Scan<(Bone bone, Vector3 pos, Quaternion rot)>((null, default, Quaternion.identity), (p, n) =>
        {
            var (pos, rot) = p.bone == null ? SkeletonBuilder.Root(n.Bone) : SkeletonBuilder.Child(p.bone, p.pos, p.rot, n.Bone);
            pose[n] = (pos, rot);
            return (n.Bone, pos, rot);
        });
        return pose;
    }

    public static Bone Clone(Bone b) => JsonUtility.FromJson<Bone>(JsonUtility.ToJson(b));

    public static PlaceNest Clone(PlaceNest n) => new()
    {
        name = n.name, host = n.host, localPos = n.localPos, localRot = n.localRot,
        unit = n.unit, mirror = n.mirror, proposed = n.proposed, span = n.span,
    };
}
