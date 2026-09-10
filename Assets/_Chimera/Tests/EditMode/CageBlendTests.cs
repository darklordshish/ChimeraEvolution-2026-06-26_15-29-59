using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Ф1 клетка: SameTopology + Blend Σ=1 + Ratio без метров (SPEC-kletka-tela.md §2-§4, BodyRules).
    /// </summary>
    public class CageBlendTests
    {
        CageTable MakeCage(string slot, int m, int n, float fill, string[] landmarks = null)
        {
            int len = m * n;
            var radii = new float[len];
            for (int i = 0; i < len; i++) radii[i] = fill;
            return new CageTable { slot = slot, M = m, N = n, landmarks = landmarks ?? new[] { "a", "b", "c" }, radii = radii };
        }

        [Test]
        public void Cage_SameTopology_Equal()
        {
            var a = MakeCage("Ноги", 7, 6, 0.3f);
            var b = MakeCage("Ноги", 7, 6, 0.4f);
            Assert.IsTrue(a.SameTopology(b), "одинаковые M×N + landmarks должны быть SameTopology");
            Assert.IsTrue(BodyRules.SameTopology(a, b));
        }

        [Test]
        public void Cage_SameTopology_DifferentM_False()
        {
            var a = MakeCage("Ноги", 7, 6, 0.3f);
            var b = MakeCage("Ноги", 6, 6, 0.3f);
            Assert.IsFalse(a.SameTopology(b));
            Assert.IsFalse(BodyRules.SameTopology(a, b));
        }

        [Test]
        public void Cage_SameTopology_DifferentN_False()
        {
            var a = MakeCage("голова", 6, 8, 0.2f);
            var b = MakeCage("голова", 6, 6, 0.2f);
            Assert.IsFalse(a.SameTopology(b));
        }

        [Test]
        public void Cage_SameTopology_DifferentLandmarks_False()
        {
            var a = MakeCage("Ноги", 7, 6, 0.3f, new[] { "a", "b", "c", "d", "e" });
            var b = MakeCage("Ноги", 7, 6, 0.3f, new[] { "a", "b", "c" });
            Assert.IsFalse(a.SameTopology(b));
        }

        [Test]
        public void Cage_Blend_SumOne_IsAverage()
        {
            // два донора, веса Σ=1 → blended = 0.5*0.2 + 0.5*0.4 = 0.3
            var a = MakeCage("Ноги", 4, 6, 0.2f);
            var b = MakeCage("Ноги", 4, 6, 0.4f);
            var tables = new List<CageTable> { a, b };
            var weights = new List<float> { 0.5f, 0.5f };
            float sum = weights[0] + weights[1];
            Assert.AreEqual(1f, sum, 1e-5f, "веса должны суммироваться в 1 (выпуклость И6)");

            var blended = CageTable.Blend(tables, weights);
            Assert.IsNotNull(blended, "Blend при Σ=1 и SameTopology должен вернуть массив");
            Assert.AreEqual(a.M * a.N, blended.Length);
            foreach (var v in blended)
                Assert.AreEqual(0.3f, v, 1e-5f, "0.5*0.2+0.5*0.4=0.3");
        }

        [Test]
        public void Cage_Blend_WeightsSumNotOne_ReturnsNull()
        {
            var a = MakeCage("Ноги", 4, 6, 0.2f);
            var b = MakeCage("Ноги", 4, 6, 0.4f);
            var tables = new List<CageTable> { a, b };
            var weights = new List<float> { 0.6f, 0.6f }; // Σ=1.2 ≠1
            var blended = CageTable.Blend(tables, weights);
            Assert.IsNull(blended, "при Σ≠1 Blend должен отказать (И6 выпуклость)");
        }

        [Test]
        public void Cage_Blend_DifferentTopology_ReturnsNull()
        {
            var a = MakeCage("Ноги", 7, 6, 0.2f);
            var b = MakeCage("Ноги", 6, 6, 0.2f);
            var blended = CageTable.Blend(new[] { a, b }, new[] { 0.5f, 0.5f });
            Assert.IsNull(blended, "разная топология → смешение невыразимо (SPEC §2)");
        }

        [Test]
        public void Cage_Blend_SingleDonor_Identity()
        {
            // И5 тождественность: вес 1 на себе → своя таблица
            var a = MakeCage("голова", 6, 8, 0.35f);
            var blended = CageTable.Blend(new[] { a }, new[] { 1f });
            Assert.IsNotNull(blended);
            for (int i = 0; i < blended.Length; i++)
                Assert.AreEqual(a.radii[i], blended[i], 1e-5f);
        }

        [Test]
        public void Cage_NoMeters_RadiiAreRatio()
        {
            // И4: в таблицах только доли, метры добавляются на выходе ×калибр
            var cage = MakeCage("Ноги", 4, 6, 0.3f);
            // Ratio 0.3 × калибр 0.2м = 0.06м радиус — правдоподобно
            var tables = new List<CageTable> { cage };
            var weights = new List<float> { 1f };
            float caliber = 0.2f;
            var meters = CageTable.BlendWithCaliber(tables, weights, caliber);
            Assert.IsNotNull(meters);
            Assert.AreEqual(0.06f, meters[0], 1e-5f);
            // ЗАЩИТА И4 СТРУКТУРНАЯ, А НЕ ПОРОГОВАЯ (переписано 11.09). Здесь стояла проверка эвристики
            // BodyRules «radii вне [0..5] — похоже, метры». Эвристику сняли намеренно и с разбором: метры
            // у наших зверей лежат в 0.02…1.5, то есть ЦЕЛИКОМ внутри коридора [0..5], и сработать правило
            // могло лишь на радиусе от пяти метров — таких в игре нет. Оно не способно поймать собственную
            // мишень ни на одном мыслимом входе. Сторож остался, правило ушло, и тест с тех пор горел.
            //     Настоящая защита — в том, что радиус ВООБЩЕ не может быть метром: он умножается на калибр
            // носителя на выходе. Её и сторожим: один и тот же радиус на разных калибрах обязан дать разные
            // метры, пропорционально. Если кто-то вернёт в таблицу метры, это сломает пропорцию.
            var far = CageTable.BlendWithCaliber(tables, weights, caliber * 3f);
            Assert.AreEqual(0.18f, far[0], 1e-5f, "радиус — ДОЛЯ: втрое больший калибр даёт втрое больший метр");
            Assert.AreEqual(meters[0] * 3f, far[0], 1e-5f, "пропорция строгая — метру в таблице взяться неоткуда");
        }

        [Test]
        public void Cage_ZeroIsDefault_Fallback()
        {
            // 0→дефолт: старый ассет с cages==null или M=N=0 не ломается
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            try
            {
                so.speciesName = "СтарыйАссет";
                so.sockets = new BodySocket[] { new BodySocket { name = "голова", baseSize = Vector3.one } };
                so.organs = new Organ[0];
                so.bones = new Bone[0];
                so.cages = null; // старый ассет
                Assert.IsNull(so.GetCage("голова"), "null cages → фолбэк на кубы");
                Assert.IsFalse(so.HasCage("голова"));

                so.cages = new[] { new CageTable { slot = "голова", M = 0, N = 0, radii = null } };
                Assert.IsNull(so.GetCage("голова"), "M=N=0 → 0→дефолт, фолбэк");

                var issues = BodyRules.CheckCages(so);
                Assert.AreEqual(0, issues.Count, "пустая заглушка не должна ругаться");
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void Cage_CheckCages_CrossSpecies_SameSlot_DifferentTopology_Error()
        {
            var a = ScriptableObject.CreateInstance<SpeciesSO>();
            var b = ScriptableObject.CreateInstance<SpeciesSO>();
            try
            {
                a.speciesName = "Человек"; b.speciesName = "Волк";
                a.sockets = b.sockets = new BodySocket[0];
                a.organs = b.organs = new Organ[0];
                a.bones = b.bones = new Bone[0];
                a.cages = new[] { MakeCage("Ноги", 7, 6, 0.3f) };
                b.cages = new[] { MakeCage("Ноги", 6, 6, 0.3f) };
                var issues = BodyRules.CheckCages(a, b);
                Assert.AreEqual(1, issues.Count, "разные M×N у одного слота → ошибка SameTopology");
                Assert.IsTrue(issues[0].error);
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }
    }
}
