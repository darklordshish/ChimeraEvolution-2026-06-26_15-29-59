using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>ТЕЛО КАК РЕКУРСИВНЫЙ ТИП (отчёт `Docs/reports/Тело как рекурсивный тип.md`). Сторожит законы дерева и
    /// главное свойство представления: развёртка свёрнутого — тождество (`Flatten ∘ From = id`) на всех пяти видах.
    /// Без него рефактор композиции на дерево мог бы молча терять кости и гнёзда.</summary>
    public class BodyTreeTests
    {
        static readonly string[] Species = { "Волк", "Лось", "Ёж", "Змея", "Человек" };

        static Tree<int> Sample() =>
            new(1, new[] { new Tree<int>(2, new[] { new Tree<int>(4) }), new Tree<int>(3) });

        static int[] Pre(Tree<int> t) => t.Subtrees().Select(x => x.Value).ToArray();

        [Test]
        public void Map_ObeysFunctorLaws()
        {
            var t = Sample();
            CollectionAssert.AreEqual(Pre(t), Pre(t.Map(x => x)), "fmap id ≠ id");
            System.Func<int, int> f = x => x + 1, g = x => x * 10;
            CollectionAssert.AreEqual(Pre(t.Map(x => g(f(x)))), Pre(t.Map(f).Map(g)), "fmap (g∘f) ≠ fmap g ∘ fmap f");
        }

        [Test]
        public void Fold_IsCatamorphism_SumAndDepth()
        {
            var t = Sample();
            Assert.AreEqual(10, t.Fold<int>((v, kids) => v + kids.Sum()));
            Assert.AreEqual(3, t.Fold<int>((v, kids) => 1 + (kids.Count == 0 ? 0 : kids.Max())));
        }

        [Test]
        public void Scan_PassesAttributeDown()
        {
            // наследуемый атрибут: сумма значений на пути от корня
            var s = Sample().Scan(0, (acc, v) => acc + v);
            CollectionAssert.AreEqual(new[] { 1, 3, 7, 4 }, s.Subtrees().Select(x => x.Value.attr).ToArray());
        }

        [Test]
        public void Replace_SharesUntouchedBranches()
        {
            var t = Sample();
            var r = t.Replace(x => x.Value == 4, _ => new Tree<int>(40));
            CollectionAssert.AreEqual(new[] { 1, 2, 40, 3 }, Pre(r));
            Assert.AreSame(t.Kids[1], r.Kids[1], "нетронутая ветвь скопирована, а должна делиться");
            Assert.AreSame(t, t.Replace(x => x.Value == 99, _ => null), "без совпадения дерево должно вернуться тем же объектом");
        }

        [Test]
        public void Prune_DropsSubtreesWithDescendants()
        {
            CollectionAssert.AreEqual(new[] { 1, 3 }, Pre(Sample().Prune(v => v != 2)));
        }

        [Test]
        public void FlattenFrom_IsIdentity_OnEverySpecies()
        {
            foreach (var name in Species)
            {
                var sp = AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{name}.asset");
                var tree = BodyTree.From(sp, out var loose);
                Assert.IsNotNull(tree, $"{name}: граф не дерево");
                var (bones, nests) = BodyTree.Flatten(tree, loose);

                string B(Bone b) => JsonUtility.ToJson(b);
                CollectionAssert.AreEquivalent(sp.bones.Select(B).ToArray(), bones.Select(B).ToArray(), $"{name}: кости не вернулись теми же");
                string N(PlaceNest n) => $"{n.name}@{n.host} {n.localPos} {n.localRot} {n.unit} {n.mirror} {n.proposed} {n.span}";
                CollectionAssert.AreEquivalent(sp.nests.Select(N).ToArray(), nests.Select(N).ToArray(), $"{name}: гнёзда не вернулись теми же");
                for (int i = 0; i < bones.Length; i++)
                    if (!string.IsNullOrEmpty(bones[i].parent))
                        Assert.Less(System.Array.FindIndex(bones, b => b.name == bones[i].parent), i, $"{name}: родитель «{bones[i].parent}» после ребёнка");
            }
        }
    }
}
