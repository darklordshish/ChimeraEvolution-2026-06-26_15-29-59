using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>РОСТ ХИМЕРЫ ПО МАССЕ (спека `2026-10-20-shkala-morfov.md`, шкала морфов принята геймдизайнером 20.10): на
    /// человеке со всеми органами вида тело до макушки — оборотень 1.92 м, человек-лось 3.10, человек-Хеджхалк 1.05. Сторож чисел из
    /// данных видов: правишь массу или ручку `GrowthP` — этот тест говорит, куда уехал рост.</summary>
    public class GrowthTests
    {
        const float HumanHeight = 1.85f;

        static SpeciesSO Sp(float mass) { var s = ScriptableObject.CreateInstance<SpeciesSO>(); s.mass = mass; return s; }

        [Test]
        public void PureSpecies_KeepsItsSize()
        {
            var h = Sp(80);
            Assert.AreEqual(1f, GlobalLayer.Growth(h, new Dictionary<SpeciesSO, float> { [h] = 1f }), 1e-5f);
        }

        [Test]
        public void NeverAboveHeaviest_AndGrowsWithShare()
        {
            var h = Sp(80); var m = Sp(400);
            float prev = 1f;
            foreach (var w in new[] { 0.2f, 0.5f, 0.85f, 1f })
            {
                float g = GlobalLayer.Growth(h, new Dictionary<SpeciesSO, float> { [h] = 1 - w, [m] = w });
                Assert.Greater(g, prev - 1e-5f, $"доля {w}");
                Assert.LessOrEqual(g, Mathf.Pow(400f / 80f, 1f / 3f) + 1e-4f);
                prev = g;
            }
        }

        [Test]
        public void SpeciesWithoutMass_DoesNotMoveHeight()
        {
            var h = Sp(80); var x = Sp(0);
            Assert.AreEqual(1f, GlobalLayer.Growth(h, new Dictionary<SpeciesSO, float> { [h] = 0.15f, [x] = 0.85f }), 1e-5f);
        }

        static SpeciesSO Load(string n) => AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{n}.asset");

        /// <summary>Человек, у которого каждый слот, что есть у вида, занят органом вида, — как в `Tools/Agent/GameSizes.cs`.</summary>
        static float HeightOnHuman(string species)
        {
            var human = Load("Человек"); var sp = Load(species);
            var worn = human.organs.Where(o => o != null)
                            .Select(o => o.chassisOnly ? o : sp.organs.FirstOrDefault(w => w != null && w.slot == o.slot && !w.chassisOnly) ?? o).ToList();
            return HumanHeight * GlobalLayer.Growth(human, worn);
        }

        [TestCase("Волк", 1.88f, 1.96f)]     // шкала морфов: 1.92
        [TestCase("Лось", 3.0f, 3.2f)]       // шкала морфов: 3.10
        [TestCase("Ёж", 1.0f, 1.1f)]         // шкала морфов: 1.05 (Хеджхалк ×2/3 по решению 20.10)
        [TestCase("Змея", 1.7f, 1.8f)]       // условная цель 1.75 (ст.2 к змее на человеке недостижима)
        public void Morph_OnHuman_HasDecidedHeight(string species, float lo, float hi)
        {
            float h = HeightOnHuman(species);
            Assert.That(h, Is.InRange(lo, hi), $"{species} на человеке: {h:0.00} м");
        }
    }
}
