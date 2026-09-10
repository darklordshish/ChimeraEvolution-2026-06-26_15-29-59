using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Ф1 клетка + Ф6 идентичность: химерный CageBlend — Identity как веса → CageTable.Blend.
    /// Ловит: невыпуклость Σ≠1, метры в Ratio, нарушение SameTopology, размазывание на ноге,
    /// потерю тождественности на чистом составе, перебор бюджета.
    /// SPEC-kletka-tela.md §2-§4, 2026-08-14-morfologiya-po-identichnosti.md И4-И6.
    /// </summary>
    public class ChimeraBlendTests
    {
        readonly List<Object> sos = new List<Object>();
        readonly List<GameObject> gos = new List<GameObject>();

        CageTable MakeCage(string slot, int m, int n, float fill)
        {
            int len = m * n;
            var radii = new float[len];
            for (int i = 0; i < len; i++) radii[i] = fill;
            // ландмарки общие для видов — иначе SameTopology false (решение 28.08)
            return new CageTable { slot = slot, M = m, N = n, landmarks = new[] { "a", "b", "c" }, radii = radii };
        }

        SpeciesSO MakeSpecies(string name, CageTable[] cages, BodySocket[] sockets, Organ[] organs)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            sos.Add(so);
            so.speciesName = name;
            so.tint = Color.gray;
            so.mutagenPool = 16;
            so.baseHp = 75;
            so.baseStamina = 100;
            so.baseStaminaRegen = 10f;
            so.sockets = sockets;
            so.bones = new Bone[0];
            so.organs = organs;
            so.cages = cages;
            return so;
        }

        BodySocket[] HumanWolfSockets()
        {
            return new[]
            {
                new BodySocket { name = "хребет", localPos = Vector3.zero, baseSize = new Vector3(0.33f, 0.48f, 1.29f) },
                new BodySocket { name = "голова", parent = "хребет", attach = 1f, baseSize = new Vector3(0.26f, 0.22f, 0.46f), sizeRel = new Vector3(0.5f, 0.5f, 0.5f) },
                new BodySocket { name = "Пасть", parent = "голова", attach = 1f, baseSize = new Vector3(0.13f, 0.09f, 0.22f) },
                new BodySocket { name = "Ноги", parent = "хребет", attach = 0.2f, baseSize = new Vector3(0.14f, 0.74f, 0.19f), sizeRel = new Vector3(0.4f, 1.5f, 0.15f), mirrorX = true },
                new BodySocket { name = "Чутьё", parent = "голова", attach = 0.5f, baseSize = Vector3.one * 0.1f, inner = true },
            };
        }

        Organ[] OrgansFor(string name)
        {
            return new[]
            {
                new Organ { organName = name + "-Хребет", slot = "хребет", chassisOnly = true },
                new Organ { organName = name + "-Пасть", slot = "Пасть", cost = 3 },
                new Organ { organName = name + "-Ноги", slot = "Ноги", cost = 4 },
                new Organ { organName = name + "-Нюх", slot = "Чутьё", cost = 3 },
            };
        }

        CreatureBody MakeBody(SpeciesSO chassis, SpeciesSO[] donors)
        {
            var go = new GameObject("ChimeraBlend_" + chassis.speciesName);
            gos.Add(go);
            go.AddComponent<Health>();
            var body = go.AddComponent<CreatureBody>();
            body.Configure(chassis, donors);
            body.SetAffinity(chassis.speciesName, CreatureBody.AffinityCap);
            return body;
        }

        int FindSlot(CreatureBody body, string slot)
        {
            for (int i = 0; i < body.SlotCount; i++) if (body.GetSlot(i).slot == slot) return i;
            return -1;
        }

        int FindVariant(CreatureBody body, int slotIdx, string species)
        {
            var vars = body.GetVariants(slotIdx);
            for (int i = 0; i < vars.Count; i++) if (vars[i].species == species) return i;
            return -1;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in gos) Object.DestroyImmediate(go);
            gos.Clear();
            foreach (var o in sos) Object.DestroyImmediate(o);
            sos.Clear();
        }

        [Test]
        public void CageChimera_BlendViaIdentity_ConvexAndBetween()
        {
            // Человек Ratios 0.30, Волк 0.50 для одного слота "голова" 6×8 — SameTopology
            var humanCage = MakeCage("голова", 6, 8, 0.30f);
            var wolfCage  = MakeCage("голова", 6, 8, 0.50f);
            // также Пасть 4×6 — чтобы локальность не смешала ногу
            var humanMaw = MakeCage("Пасть", 4, 6, 0.25f);
            var wolfMaw  = MakeCage("Пасть", 4, 6, 0.45f);

            var sockets = HumanWolfSockets();
            var human = MakeSpecies("Человек", new[] { humanCage, humanMaw }, sockets, OrgansFor("Человек"));
            var wolf  = MakeSpecies("Волк",    new[] { wolfCage, wolfMaw }, sockets, OrgansFor("Волк"));

            var body = MakeBody(human, new[] { wolf });

            // ставим волчью Пасть → Identity волка ≈ 0.15 (1 - chassis0.1)/6
            int mawSlot = FindSlot(body, "Пасть");
            Assert.AreNotEqual(-1, mawSlot, "Слот Пасть не найден");
            int wIdx = FindVariant(body, mawSlot, "Волк");
            Assert.AreNotEqual(-1, wIdx);
            Assert.IsTrue(body.Install(mawSlot, wIdx), "Install волчьей Пасти должен пройти");

            // веса = Identity(вид) — выпуклость Σ=1 неотрицательны по построению (проверено в IdentitySumTests)
            float hW = body.Identity(human);
            float wW = body.Identity(wolf);
            Assert.Greater(hW, 0f);
            Assert.Greater(wW, 0f);
            Assert.AreEqual(1f, hW + wW, 1e-4f, "Σ Identity =1 (выпуклость И6)");

            // SameTopology обязательна для химеры — иначе Blend вернёт null (невыразимость)
            Assert.IsTrue(humanCage.SameTopology(wolfCage), "голова M×N+landmarks должны совпасть");
            Assert.IsTrue(BodyRules.SameTopology(humanCage, wolfCage));

            // Blend голов через Identity-веса — химера головы
            var blendedHead = CageTable.Blend(new[] { humanCage, wolfCage }, new[] { hW, wW });
            Assert.IsNotNull(blendedHead, "Blend при SameTopology и Σ=1 должен вернуть массив");
            Assert.AreEqual(humanCage.M * humanCage.N, blendedHead.Length);
            // выпуклость: каждый радиус между исходными (И6)
            float expected = hW * 0.30f + wW * 0.50f;
            foreach (var v in blendedHead)
                Assert.AreEqual(expected, v, 1e-5f, "выпуклое среднее 0.30/0.50");
            Assert.Greater(blendedHead[0], 0.30f, "химера головы потянулась к волку");
            Assert.Less(blendedHead[0], 0.50f);

            // И4 калибр от шасси — Ratio × калибр(носитель) = метры на выходе
            float caliber = 0.40f; // условный калибр головы носителя
            var meters = CageTable.BlendWithCaliber(new[] { humanCage, wolfCage }, new[] { hW, wW }, caliber);
            Assert.AreEqual(expected * caliber, meters[0], 1e-5f, "И4: метры = Ratio × калибр носителя");

            // локальность: Пасть→голова — голова смешалась, ноги не трогаем здесь
            var blendedMaw = CageTable.Blend(new[] { humanMaw, wolfMaw }, new[] { hW, wW });
            Assert.IsNotNull(blendedMaw);
            float expMaw = hW * 0.25f + wW * 0.45f;
            Assert.AreEqual(expMaw, blendedMaw[0], 1e-5f);

            // бюджет: голова 6×8=48 + Пасть 4×6=24 =72 квада на эти 2 слота, всего <324
            int quads = humanCage.M * humanCage.N + humanMaw.M * humanMaw.N;
            Assert.LessOrEqual(quads, BodyRules.BudgetQuads, "часть бюджета <324");
            Assert.AreEqual(324, BodyRules.BudgetQuads);
            Assert.AreEqual(830, BodyRules.BudgetTrisPerCreature);
            Assert.AreEqual(20750, BodyRules.BudgetTris25, "25×≈20k проверка бюджета");
        }

        [Test]
        public void CageChimera_PureComposition_Tождественность()
        {
            // И5: на родном составе (чистый человек) вес 1 на себе → своя таблица без изменений
            var humanCage = MakeCage("голова", 6, 8, 0.33f);
            var wolfCage  = MakeCage("голова", 6, 8, 0.55f);
            var sockets = HumanWolfSockets();
            var human = MakeSpecies("Человек", new[] { humanCage }, sockets, OrgansFor("Человек"));
            var wolf  = MakeSpecies("Волк",    new[] { wolfCage }, sockets, OrgansFor("Волк"));

            var body = MakeBody(human, new[] { wolf });
            // чистый — ничего не ставим
            Assert.AreEqual(1f, body.Identity(human), 1e-4f, "чистый человек Identity=1");
            Assert.AreEqual(0f, body.Identity(wolf), 1e-4f);
            Assert.IsNull(body.GetBlendedPlan(), "родной состав → план null (тождественность до микрона)");

            // Blend с весом 1 на себе даёт исходную таблицу
            var blended = CageTable.Blend(new[] { humanCage }, new[] { 1f });
            Assert.IsNotNull(blended);
            for (int i = 0; i < blended.Length; i++)
                Assert.AreEqual(humanCage.radii[i], blended[i], 1e-5f, "И5 тождественность: вес1 → своя таблица");

            // калибр всё равно шасси
            var meters = CageTable.BlendWithCaliber(new[] { humanCage }, new[] { 1f }, 0.40f);
            Assert.AreEqual(humanCage.radii[0] * 0.40f, meters[0], 1e-5f);
        }

        [Test]
        public void CageChimera_SameTopology_Required_And_RatioGuard()
        {
            // разная топология → химера невыразима покомпонентным средним (SPEC §2)
            var a = MakeCage("голова", 6, 8, 0.30f);
            var b = MakeCage("голова", 6, 6, 0.30f); // N разное
            Assert.IsFalse(a.SameTopology(b));
            Assert.IsNull(CageTable.Blend(new[] { a, b }, new[] { 0.5f, 0.5f }), "разная топология → Blend null");

            // И4 СТОРОЖИТСЯ СТРУКТУРНО (переписано 11.09; подробный разбор — в `CageBlendTests`). Порог
            // «радиус >5 — это метры» снят намеренно: метры зверей лежат в 0.02…1.5, внутри самого
            // коридора, и правило не могло сработать ни на одном реальном входе. Сторожим то, что
            // действует: радиус — ДОЛЯ, метр появляется только умножением на калибр носителя
            var ratio = MakeCage("Ноги", 4, 6, 0.30f);
            var m1 = CageTable.BlendWithCaliber(new[] { ratio }, new[] { 1f }, 0.20f);
            var m2 = CageTable.BlendWithCaliber(new[] { ratio }, new[] { 1f }, 0.60f);
            Assert.AreEqual(0.06f, m1[0], 1e-5f);
            Assert.AreEqual(m1[0] * 3f, m2[0], 1e-5f, "втрое больший калибр — втрое больший метр: доля, а не метр");

            // отрицательный радиус остаётся ЖЁСТКОЙ ошибкой: наружу от оси на минус нельзя
            var neg = MakeCage("Ноги", 4, 6, -0.20f);
            var soNeg = MakeSpecies("ТестМинус", new[] { neg }, HumanWolfSockets(), OrgansFor("ТестМинус"));
            bool foundNeg = false;
            foreach (var iss in BodyRules.CheckCages(soNeg)) if (iss.error) foundNeg = true;
            Assert.IsTrue(foundNeg, "отрицательный радиус — ошибка знака, должна быть error");

            // 0→дефолт фолбэк — старый ассет с cages==null не ломается
            var old = MakeSpecies("Старый", null, HumanWolfSockets(), OrgansFor("Старый"));
            Assert.IsNull(old.GetCage("голова"));
            Assert.IsFalse(old.HasCage("голова"));
            Assert.AreEqual(0, BodyRules.CheckCages(old).Count, "пустая заглушка не должна ругаться");
        }

        [Test]
        public void CageChimera_Locality_MawAffectsHead_LegsExcluded()
        {
            // Проверка локальности из графа: Пасть.parent=голова, Ноги.parent=хребет(исключён)
            var sockets = HumanWolfSockets();
            var human = MakeSpecies("Человек", new CageTable[0], sockets, OrgansFor("Человек"));
            var wolf  = MakeSpecies("Волк",    new CageTable[0], sockets, OrgansFor("Волк"));
            var body = MakeBody(human, new[] { wolf });

            // ставим волчьи Ноги — голова не должна потянуться (хребет исключён, И3)
            int legSlot = FindSlot(body, "Ноги");
            int wLeg = FindVariant(body, legSlot, "Волк");
            Assert.IsTrue(body.Install(legSlot, wLeg));
            var plan = body.GetBlendedPlan();
            // Ноги→хребет исключён — план либо null (нет затронутых родителей), либо без изменения головы
            BodySocket headAfterLegs = null;
            if (plan != null) foreach (var s in plan) if (s != null && s.name == "голова") headAfterLegs = s;
            // если план null — голова осталась как в шасси (sizeRel 0.5), что и означает локальность
            float headSizeX = headAfterLegs != null ? headAfterLegs.sizeRel.x : 0.5f;
            Assert.AreEqual(0.5f, headSizeX, 1e-5f, "голова не тянется от ног (локальность, хребет исключён)");

            // теперь Пасть — голова должна потянуться (Пасть→голова) — нужен разный размер голов у видов
            var humanSockets2 = new[] {
                new BodySocket { name = "хребет", localPos = Vector3.zero, baseSize = new Vector3(0.33f, 0.48f, 1.29f) },
                new BodySocket { name = "голова", parent = "хребет", attach = 1f, baseSize = new Vector3(0.26f, 0.22f, 0.46f), sizeRel = new Vector3(0.5f, 0.5f, 0.5f) },
                new BodySocket { name = "Пасть", parent = "голова", attach = 1f, baseSize = new Vector3(0.13f, 0.09f, 0.22f) },
                new BodySocket { name = "Ноги", parent = "хребет", attach = 0.2f, baseSize = new Vector3(0.14f, 0.74f, 0.19f), sizeRel = new Vector3(0.4f, 1.5f, 0.15f), mirrorX = true },
                new BodySocket { name = "Чутьё", parent = "голова", attach = 0.5f, baseSize = Vector3.one * 0.1f, inner = true },
            };
            var wolfSockets2 = new[] {
                new BodySocket { name = "хребет", localPos = Vector3.zero, baseSize = new Vector3(0.33f, 0.48f, 1.29f) },
                new BodySocket { name = "голова", parent = "хребет", attach = 1f, baseSize = new Vector3(0.26f, 0.22f, 0.46f), sizeRel = new Vector3(1.0f, 1.0f, 1.0f) },
                new BodySocket { name = "Пасть", parent = "голова", attach = 1f, baseSize = new Vector3(0.13f, 0.09f, 0.22f) },
                new BodySocket { name = "Ноги", parent = "хребет", attach = 0.2f, baseSize = new Vector3(0.14f, 0.74f, 0.19f), sizeRel = new Vector3(0.4f, 1.5f, 0.15f), mirrorX = true },
                new BodySocket { name = "Чутьё", parent = "голова", attach = 0.5f, baseSize = Vector3.one * 0.1f, inner = true },
            };
            var human2 = MakeSpecies("Человек", new CageTable[0], humanSockets2, OrgansFor("Человек"));
            var wolf2  = MakeSpecies("Волк",    new CageTable[0], wolfSockets2, OrgansFor("Волк"));
            body = MakeBody(human2, new[] { wolf2 });
            int mawSlot2 = FindSlot(body, "Пасть");
            int wMaw2 = FindVariant(body, mawSlot2, "Волк");
            Assert.IsTrue(body.Install(mawSlot2, wMaw2));
            var plan2b = body.GetBlendedPlan();
            Assert.IsNotNull(plan2b, "Пасть→голова — план должен смешать голову");
            BodySocket headAfterMaw2 = null;
            foreach (var s in plan2b) if (s != null && s.name == "голова") headAfterMaw2 = s;
            Assert.IsNotNull(headAfterMaw2);
            Assert.Greater(headAfterMaw2.sizeRel.x, 0.5f, "голова потянулась к волку из-за Пасти");
        }
    }
}
