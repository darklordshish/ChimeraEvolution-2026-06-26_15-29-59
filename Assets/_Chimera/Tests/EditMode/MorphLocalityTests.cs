using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>Ф6 — локальность из графа: Пасть→голова, Руки/Ноги→хребет исключён.
    /// Проверяет что графт ног не тянет голову (психика не дёргается на пропорциях).
    /// </summary>
    public class MorphLocalityTests
    {
        SpeciesSO MakeSpecies(string name, Vector3 headSizeRel, Vector3 legSizeRel)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = name;
            so.tint = Color.gray;
            so.mutagenPool = 16;
            so.baseHp = 75;
            so.baseStamina = 100;
            so.baseStaminaRegen = 10f;
            so.sockets = new[]
            {
                new BodySocket { name = "хребет", localPos = Vector3.zero, baseSize = new Vector3(0.33f, 0.48f, 1.29f) },
                new BodySocket { name = "голова", parent = "хребет", attach = 1f, baseSize = new Vector3(0.26f, 0.22f, 0.46f), sizeRel = headSizeRel },
                new BodySocket { name = "Пасть", parent = "голова", attach = 1f, baseSize = new Vector3(0.13f, 0.09f, 0.22f) },
                new BodySocket { name = "Ноги", parent = "хребет", attach = 0.2f, baseSize = new Vector3(0.14f, 0.74f, 0.19f), sizeRel = legSizeRel, mirrorX = true },
                new BodySocket { name = "Руки", parent = "хребет", attach = 0.78f, baseSize = new Vector3(0.12f, 0.72f, 0.17f), mirrorX = true },
                new BodySocket { name = "Сердце", parent = "хребет", attach = 0.5f, baseSize = new Vector3(0.27f, 0.56f, 0.56f), inner = true },
                new BodySocket { name = "Чутьё", parent = "голова", attach = 0.5f, baseSize = Vector3.one * 0.1f, sizeRel = new Vector3(0.5f,0.5f,0.5f), inner = true },
            };
            so.bones = new Bone[0];
            so.organs = new[]
            {
                new Organ { organName = "Хребет", slot = "хребет", chassisOnly = true },
                new Organ { organName = "Пасть", slot = "Пасть", cost = 3 },
                new Organ { organName = "Сердце", slot = "Сердце", cost = 3 },
                new Organ { organName = "Ноги", slot = "Ноги", cost = 4 },
                new Organ { organName = "Руки", slot = "Руки", cost = 4 },
                new Organ { organName = "Нюх", slot = "Чутьё", cost = 3 },
                new Organ { organName = "Шкура", slot = "Шкура", cost = 3 },
            };
            so.cages = null;
            return so;
        }

        [Test]
        public void Locality_MawAffectsHead_LegsDoNot()
        {
            // голова человека 0.5, волка 1.0 по sizeRel — легко различить
            var human = MakeSpecies("Человек", new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0.418f, 1.544f, 0.154f));
            var wolf  = MakeSpecies("Волк",    new Vector3(1.0f, 1.0f, 1.0f), new Vector3(0.800f, 2.0f, 0.400f));
            var go = new GameObject("LocalityBody");
            go.AddComponent<Health>();
            var body = go.AddComponent<CreatureBody>();
            try
            {
                body.Configure(human, new[] { wolf });
                body.SetAffinity(human.speciesName, CreatureBody.AffinityCap);
                // найти слот Ноги и поставить волчий вариант
                int legSlot = -1;
                for (int i = 0; i < body.SlotCount; i++) if (body.GetSlot(i).slot == "Ноги") legSlot = i;
                Assert.AreNotEqual(-1, legSlot, "Слот Ноги не найден");
                var vars = body.GetVariants(legSlot);
                int wIdx = -1;
                for (int i = 0; i < vars.Count; i++) if (vars[i].species == "Волк") wIdx = i;
                Assert.AreNotEqual(-1, wIdx, "Волчий вариант Ноги не найден");
                Assert.IsTrue(body.Install(legSlot, wIdx), "Install волчьих ног должен пройти");

                // психика не должна дёрнуться: доминанта остаётся человек (Identity человека 0.85 > Medium 0.85? гранично)
                var dom = body.MostKin(out var tier);
                Assert.IsNotNull(dom, "Доминанта должна быть");
                Assert.AreEqual("Человек", dom.speciesName, "Человек с волчьими лапами — доминанта человек (локальность, морда не волчеет)");
                // пропорции головы не должны сместиться из-за ног (хребет исключён)
                var plan = body.GetBlendedPlan();
                // ноги меняют свой слот (Ноги), но голову — нет
                Assert.IsNotNull(plan, "Графт ног меняет свой слот, план не null");
                BodySocket headAfter = null;
                foreach (var s in plan) if (s != null && s.name == "голова") headAfter = s;
                Assert.IsNotNull(headAfter);
                Assert.AreEqual(0.5f, headAfter.sizeRel.x, 1e-5f, "Голова не должна тянуться из-за ног (локальность, хребет исключён)");
                // убедимся что ноги стали волчьи
                BodySocket legsAfter = null;
                foreach (var s in plan) if (s != null && s.name == "Ноги") legsAfter = s;
                Assert.IsNotNull(legsAfter);
                Assert.AreEqual(0.8f, legsAfter.sizeRel.x, 1e-5f, "Ноги должны стать волчьими (прямая форма слота)");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(human); Object.DestroyImmediate(wolf); }
        }

        [Test]
        public void Locality_MawBlendsHead()
        {
            var human = MakeSpecies("Человек", new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0.418f, 1.544f, 0.154f));
            var wolf  = MakeSpecies("Волк",    new Vector3(1.0f, 1.0f, 1.0f), new Vector3(0.800f, 2.0f, 0.400f));
            var go = new GameObject("LocalityMawBody");
            go.AddComponent<Health>();
            var body = go.AddComponent<CreatureBody>();
            try
            {
                body.Configure(human, new[] { wolf });
                body.SetAffinity(human.speciesName, CreatureBody.AffinityCap);
                int mawSlot = -1;
                for (int i = 0; i < body.SlotCount; i++) if (body.GetSlot(i).slot == "Пасть") mawSlot = i;
                Assert.AreNotEqual(-1, mawSlot);
                var vars = body.GetVariants(mawSlot);
                int wIdx = -1;
                for (int i = 0; i < vars.Count; i++) if (vars[i].species == "Волк") wIdx = i;
                Assert.AreNotEqual(-1, wIdx);
                Assert.IsTrue(body.Install(mawSlot, wIdx));

                var plan = body.GetBlendedPlan();
                Assert.IsNotNull(plan, "Пасть→голова — план должен смешать голову");
                BodySocket head = null;
                foreach (var s in plan) if (s != null && s.name == "голова") head = s;
                Assert.IsNotNull(head, "голова в плане должна быть");
                // голова должна быть между 0.5 и 1.0 (выпуклость И6)
                Assert.Greater(head.sizeRel.x, 0.5f, "Голова должна потянуться к волчьей (>0.5)");
                Assert.Less(head.sizeRel.x, 1.0f, "Голова не должна перепрыгнуть волчью (<1.0)");
                // психика: доминанта остаётся человек, но Identity волка вырос
                Assert.Greater(body.Identity(wolf), 0f);
                // MostKin не должен прыгнуть на волка из-за одной пасти (0.15 < Medium)
                var dom = body.MostKin(out var tier);
                Assert.AreEqual("Человек", dom.speciesName);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(human); Object.DestroyImmediate(wolf); }
        }

        [Test]
        public void Budget_800PerCreature_20kFor25()
        {
            Assert.AreEqual(830, CreatureBody.BudgetTrisPerCreature, "Бюджет на существо ≈830 трис (ADR-1 хребет 8×10)");
            Assert.AreEqual(20750, CreatureBody.BudgetTris25, "25 в кадре ≈20.7k трис");
            Assert.AreEqual(324, BodyRules.BudgetQuads, "SPEC §6: 324 квада (было 310; хребет 70)");
            // 324 квада = 648 трис + сферы/шапки ≈830 — бюджет в трис больше квадов*2
            Assert.GreaterOrEqual(BodyRules.BudgetTrisPerCreature, BodyRules.BudgetQuads * 2);
        }

        [Test]
        public void Identity_NativeComposition_IsOne()
        {
            var human = MakeSpecies("Человек", new Vector3(0.5f,0.5f,0.5f), new Vector3(0.4f,1.5f,0.15f));
            var wolf = MakeSpecies("Волк", new Vector3(1.0f,1.0f,1.0f), new Vector3(0.8f,2f,0.4f));
            var go = new GameObject("IdentityNative");
            go.AddComponent<Health>();
            var body = go.AddComponent<CreatureBody>();
            try
            {
                body.Configure(human, new[] { wolf });
                body.SetAffinity(human.speciesName, CreatureBody.AffinityCap);
                Assert.AreEqual(1f, body.Identity(human), 1e-4f, "Родной состав → Identity 1");
                Assert.AreEqual(0f, body.Identity(wolf), 1e-4f);
                Assert.IsNull(body.GetBlendedPlan(), "Родной состав → тождественность, план null");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(human); Object.DestroyImmediate(wolf); }
        }
    }
}
