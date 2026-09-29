using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>РАЗМЕТКА ЦЕПЕЙ (спека двух слоёв §4.1, поставка 30) — сторож двух вещей: вывод узла «в долях родителя» в
    /// метры и то, что разметка ДОЕХАЛА до ассетов видов. Второе не тавтология: `JsonUtility` молча выбрасывает поля,
    /// которых у `Bone` нет, — поставка 30 пришла с разметкой, а до этого сторожа код её не читал.</summary>
    public class BodyChainsTests
    {
        static readonly string[] Species = { "Волк", "Лось", "Ёж", "Змея", "Человек" };

        [Test]
        public void ResolveRel_DerivesMetresFromParent()
        {
            var parent = new Bone { name = "p", length = 2f, r0 = 0.1f, r1 = 0.3f, section = 2f, depth = 0.5f };
            var child = new Bone { name = "c", parent = "p", rel = new BoneRel { at = 0.5f, offX = 1f, offZ = -1f, len = 0.25f, r0 = 0.5f, r1 = 1f } };
            Assert.AreEqual(1, BodyChains.ResolveRel(new[] { child, parent }), "ребёнок раньше родителя в списке — всё равно выводится");
            // радиус родителя в середине — 0.2
            Assert.IsTrue(child.freeOrigin);
            Assert.AreEqual(new Vector3(0.2f * 2f, 1f, -0.2f * 0.5f).ToString("F4"), child.origin.ToString("F4"));
            Assert.AreEqual(0.5f, child.length, 1e-5f);
            Assert.AreEqual(0.1f, child.r0, 1e-5f);
            Assert.AreEqual(0.2f, child.r1, 1e-5f);
        }

        [Test]
        public void ResolveRel_ClampsRadiusOutsideParent()
        {
            var parent = new Bone { name = "p", length = 1f, r0 = 0.1f, r1 = 0.3f, section = 1f, depth = 1f };
            var child = new Bone { name = "c", parent = "p", rel = new BoneRel { at = -0.63f, len = 1f, r0 = 1f, r1 = 1f } };
            BodyChains.ResolveRel(new[] { parent, child });
            Assert.AreEqual(-0.63f, child.origin.y, 1e-5f, "at < 0 — норма (пояс1 человека ниже начала хребта)");
            Assert.AreEqual(0.1f, child.r0, 1e-5f, "за началом радиус — по краю");
        }

        [Test]
        public void EverySpecies_EveryNodeHasLimb_MarksFromVocabularyOnce()
        {
            foreach (var name in Species)
            {
                var sp = AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{name}.asset");
                Assert.IsNotNull(sp, name);
                Assert.IsNotEmpty(sp.bones, $"{name}: графа нет");
                var marks = new HashSet<string>();
                foreach (var b in sp.bones)
                {
                    Assert.IsTrue(BodyChains.IsLimb(b.limb), $"{name}/{b.name}: цепь «{b.limb}» — не из словаря (разметка не доехала до ассета?)");
                    foreach (var m in new[] { b.mark?.a, b.mark?.b })
                    {
                        if (string.IsNullOrEmpty(m)) continue;
                        Assert.IsTrue(BodyChains.IsMark(m), $"{name}/{b.name}: метка «{m}» не из словаря");
                        Assert.IsTrue(marks.Add(m), $"{name}: метка «{m}» дважды");
                    }
                }
                // четвероногие — все девять; человек — без корня хвоста; змея — одна голова
                int expect = name == "Змея" ? 1 : name == "Человек" ? 8 : 9;
                Assert.AreEqual(expect, marks.Count, $"{name}: меток {marks.Count}, ждали {expect}");
            }
        }
    }
}
