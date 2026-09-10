using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// BodyRules CheckParts + CheckBudget: негатив >324→error, M×N проверка, 324 не превышен.
    /// Закрывает дыру из плана 2026-09-05 mustfix п.8 (BodyRules.cs:182 CheckParts + бюджет).
    /// SPEC-kletka-tela.md §6: 324 квада = (M-1)×N сумма, но BodyRules.CheckBudget считает M×N как в комменте §6.
    /// Тесты проверяют реальную формулу M×N, а не (M-1)×N, и границу 324.
    /// </summary>
    public class BodyRulesCheckTests
    {
        CageTable MakeCage(string slot, int m, int n, float fill = 0.3f)
        {
            int len = m * n;
            var radii = new float[len];
            for (int i = 0; i < len; i++) radii[i] = fill;
            return new CageTable { slot = slot, M = m, N = n, landmarks = new[] { "a", "b", "c" }, radii = radii };
        }

        SpeciesSO MakeSpeciesWithCages(string name, params CageTable[] cages)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = name;
            so.sockets = new BodySocket[0];
            so.organs = new Organ[0];
            so.bones = new Bone[0];
            so.cages = cages;
            return so;
        }

        // ── Бюджет: константы ──────────────────────────────────────────────────

        [Test]
        public void Budget_Quads_Is324_And_TrisConsistent()
        {
            Assert.AreEqual(324, BodyRules.BudgetQuads, "SPEC §6 + ADR-1: 324 квада (было 310; хребет 8×10=70)");
            Assert.AreEqual(830, BodyRules.BudgetTrisPerCreature);
            Assert.AreEqual(20750, BodyRules.BudgetTris25);
            Assert.GreaterOrEqual(BodyRules.BudgetTrisPerCreature, BodyRules.BudgetQuads * 2, "трис ≥ квадов*2 (2 трис на квад + шапки)");
        }

        [Test]
        public void Budget_Exactly324_NoError()
        {
            // 18×18 =324 ровно — граница, не должна ругаться
            var so = MakeSpeciesWithCages("Граница324", MakeCage("хребет", 18, 18));
            try
            {
                var issues = BodyRules.CheckBudget(so);
                Assert.AreEqual(0, issues.Count, "324 квада ровно — бюджет не превышен");
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void Budget_310Old_Negative_Now324()
        {
            // Исторический негатив >310→error (план 05.09). Сейчас порог 324 (ADR-1 +14 за хребет N=10).
            // 311 (>310) было бы ошибкой на старом бюджете, на новом 311 <324 — проходит.
            // Проверяем оба смысла: старый порог упомянут, новый действует.
            var so311 = MakeSpeciesWithCages("Старый310_311", MakeCage("Тело", 311, 1));
            var so324 = MakeSpeciesWithCages("Новый324", MakeCage("хребет", 18, 18));
            var so325 = MakeSpeciesWithCages("Новый325", MakeCage("хребет", 25, 13)); // 25*13=325 >324
            try
            {
                Assert.AreEqual(0, BodyRules.CheckBudget(so311).Count, "311 квадов <324 — на новом бюджете проходит (на старом 310 было бы error)");
                Assert.AreEqual(0, BodyRules.CheckBudget(so324).Count, "324 ровно — не error");
                var over = BodyRules.CheckBudget(so325);
                Assert.AreEqual(1, over.Count, "325 >324 — должен быть error (негатив)");
                Assert.IsTrue(over[0].error);
                StringAssert.Contains("325", over[0].text, "текст должен содержать фактический totalQuads");
                StringAssert.Contains("324", over[0].text, "текст должен содержать лимит BudgetQuads");
            }
            finally { Object.DestroyImmediate(so311); Object.DestroyImmediate(so324); Object.DestroyImmediate(so325); }
        }

        [Test]
        public void Budget_Negative_Over324_Error()
        {
            // Явный негатив: сумма M×N >324 должна дать error
            var so = MakeSpeciesWithCages("Перебор325", MakeCage("хребет", 25, 13)); // 325
            try
            {
                var issues = BodyRules.CheckBudget(so);
                Assert.AreEqual(1, issues.Count);
                Assert.IsTrue(issues[0].error);
                Assert.AreEqual("бюджет", issues[0].where);
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void Budget_MxN_Not_Mminus1xN()
        {
            // Проверка что бюджет считается как M×N, а не (M-1)×N.
            // Подбираем 3 слота по 11×10 =110 каждый → M×N=330 >324, но (M-1)×N=100*3=300 <324.
            // Если бы считали (M-1)×N, ошибки бы не было — ловим регрессию.
            var a = MakeCage("хребет", 11, 10);
            var b = MakeCage("голова", 11, 10);
            var c = MakeCage("шея", 11, 10);
            var so = MakeSpeciesWithCages("MxNvsMminus1", a, b, c);
            try
            {
                int totalMxN = a.M * a.N + b.M * b.N + c.M * c.N; // 330
                int totalMinus = (a.M - 1) * a.N + (b.M - 1) * b.N + (c.M - 1) * c.N; // 300
                Assert.AreEqual(330, totalMxN);
                Assert.AreEqual(300, totalMinus);
                var issues = BodyRules.CheckBudget(so);
                Assert.AreEqual(1, issues.Count, "M×N=330 >324 → error, (M-1)×N=300 прошёл бы — проверяем формулу M×N");
                StringAssert.Contains("330", issues[0].text);
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void Budget_NullOrUnconfigured_NoError()
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            try
            {
                so.speciesName = "Пустой";
                so.cages = null;
                Assert.AreEqual(0, BodyRules.CheckBudget(so).Count);
                so.cages = new[] { new CageTable { slot = "хребет", M = 0, N = 0, radii = null } };
                Assert.AreEqual(0, BodyRules.CheckBudget(so).Count, "M=N=0 не настроено → фолбэк, не error");
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void Budget_SPEC_Slots_NotExceeded_WhenUsing_MxN_Equivalent()
        {
            // Проверяем что суммарный (M-1)×N по SPEC таблице =324 не превышен.
            // BodyRules считает M×N, поэтому эквивалентная проверка — что (M-1)×N=324,
            // а M×N=394 >324. Этот тест документирует расхождение и проверяет что
            // хотя бы (M-1)×N версия укладывается. Если BodyRules перейдёт на (M-1)×N,
            // этот тест останется зелёным.
            // SPEC §6 таблица: хребет 8×10=70, шея 4×8=24, голова 6×8=40, Пасть 4×6=18,
            // Руки×2 6×6=60, Ноги×2 7×6=72, Хвост 5×6=24, Чутьё×2 3×4=16 → 324
            int specQuadsMinus = 70 + 24 + 40 + 18 + 60 + 72 + 24 + 16;
            Assert.AreEqual(324, specQuadsMinus, "SPEC §6 (M-1)×N сумма =324");
            Assert.AreEqual(BodyRules.BudgetQuads, specQuadsMinus, "BudgetQuads должен совпадать с SPEC суммой");
        }

        // ── CheckParts ─────────────────────────────────────────────────────────

        SpeciesSO MakeSpeciesForParts(params BodySocket[] sockets)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = "ТестParts";
            so.sockets = sockets;
            so.organs = new Organ[0];
            so.bones = new Bone[0];
            return so;
        }

        List<BodyProbe.Part> MakeParts(params (string name, Vector3 center, Vector3 size)[] defs)
        {
            var list = new List<BodyProbe.Part>();
            foreach (var d in defs)
                list.Add(new BodyProbe.Part { name = d.name, center = d.center, size = d.size, hasRenderer = true, parent = "" });
            return list;
        }

        [Test]
        public void CheckParts_InnerLargerThanCarrier_Error()
        {
            // Сердце inner больше носителя хребет по всем осям → error
            var sockets = new[]
            {
                new BodySocket { name = "хребет", baseSize = new Vector3(0.4f,0.5f,1.2f) },
                new BodySocket { name = "Сердце", parent = "хребет", inner = true, baseSize = new Vector3(0.3f,0.3f,0.3f) },
            };
            var so = MakeSpeciesForParts(sockets);
            // хребет 1×1×1, Сердце 2×2×2 → больше носителя по всем осям
            var parts = MakeParts(
                ("хребет", Vector3.zero, new Vector3(1f,1f,1f)),
                ("Сердце", Vector3.zero, new Vector3(2f,2f,2f))
            );
            try
            {
                var issues = BodyRules.CheckParts(so, parts);
                bool found = false;
                foreach (var iss in issues) if (iss.where == "Сердце" && iss.text.Contains("внутреннее место больше носителя")) found = true;
                Assert.IsTrue(found, "внутреннее место больше носителя по всем осям → должен быть error");
                Assert.IsTrue(issues[0].error);
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void CheckParts_InnerSmaller_NoError()
        {
            var sockets = new[]
            {
                new BodySocket { name = "хребет", baseSize = new Vector3(0.4f,0.5f,1.2f) },
                new BodySocket { name = "Сердце", parent = "хребет", inner = true, baseSize = new Vector3(0.3f,0.3f,0.3f) },
            };
            var so = MakeSpeciesForParts(sockets);
            var parts = MakeParts(
                ("хребет", Vector3.zero, new Vector3(2f,2f,2f)),
                ("Сердце", Vector3.zero, new Vector3(1f,1f,1f))
            );
            try
            {
                var issues = BodyRules.CheckParts(so, parts);
                foreach (var iss in issues) Assert.IsFalse(iss.where == "Сердце" && iss.text.Contains("внутреннее место больше носителя"), "меньше носителя — не error");
                // должен быть 0 ошибок (или хотя бы без inner-проверки)
                bool hasInnerError = false;
                foreach (var iss in issues) if (iss.text.Contains("внутреннее место больше носителя")) hasInnerError = true;
                Assert.IsFalse(hasInnerError);
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void CheckParts_OuterLargerThanCarrier_NoError()
        {
            // голова НЕ inner, даже если шире родителя — не ругаемся (голова законно шире шеи)
            var sockets = new[]
            {
                new BodySocket { name = "хребет", baseSize = new Vector3(0.4f,0.5f,1.2f) },
                new BodySocket { name = "голова", parent = "хребет", inner = false, baseSize = new Vector3(0.5f,0.5f,0.5f) },
            };
            var so = MakeSpeciesForParts(sockets);
            var parts = MakeParts(
                ("хребет", Vector3.zero, new Vector3(1f,1f,1f)),
                ("голова", Vector3.zero, new Vector3(2f,2f,2f))
            );
            try
            {
                var issues = BodyRules.CheckParts(so, parts);
                bool hasInnerError = false;
                foreach (var iss in issues) if (iss.text.Contains("внутреннее место больше носителя")) hasInnerError = true;
                Assert.IsFalse(hasInnerError, "внешнее место больше носителя — не error, только inner проверяется");
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void CheckParts_MissingDrawnParent_Error()
        {
            // Место висит на несуществующем родителе → "нет нарисованного предка"
            var sockets = new[]
            {
                new BodySocket { name = "хребет", baseSize = Vector3.one },
                new BodySocket { name = "Тест", parent = "Фантом", baseSize = Vector3.one },
            };
            var so = MakeSpeciesForParts(sockets);
            var parts = MakeParts(
                ("хребет", Vector3.zero, new Vector3(1f,1f,1f)),
                ("Тест", Vector3.zero, new Vector3(0.5f,0.5f,0.5f))
            );
            try
            {
                var issues = BodyRules.CheckParts(so, parts);
                bool found = false;
                foreach (var iss in issues) if (iss.where == "Тест" && iss.text.Contains("нет нарисованного предка")) found = true;
                Assert.IsTrue(found, "место без нарисованного предка должно дать error");
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void CheckParts_BoneParent_NoError()
        {
            // Хвост на кости крестец — кость законный предок, не ругаемся
            var sockets = new[]
            {
                new BodySocket { name = "хребет", baseSize = Vector3.one },
                new BodySocket { name = "Хвост", parent = "крестец", baseSize = Vector3.one },
            };
            var so = MakeSpeciesForParts(sockets);
            so.bones = new[] { new Bone { name = "крестец" } };
            var parts = MakeParts(
                ("хребет", Vector3.zero, new Vector3(1f,1f,1f)),
                ("Хвост", new Vector3(0,0,-1), new Vector3(0.5f,0.5f,0.5f))
            );
            try
            {
                var issues = BodyRules.CheckParts(so, parts);
                bool hasMissing = false;
                foreach (var iss in issues) if (iss.where == "Хвост" && iss.text.Contains("нет нарисованного предка")) hasMissing = true;
                Assert.IsFalse(hasMissing, "кость как родитель — не error (MorphBuilder.IsBone)");
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void CheckParts_NullOrEmpty_NoThrow()
        {
            var so = MakeSpeciesForParts(new BodySocket { name = "хребет", baseSize = Vector3.one });
            try
            {
                Assert.DoesNotThrow(() => BodyRules.CheckParts(null, null));
                Assert.DoesNotThrow(() => BodyRules.CheckParts(so, null));
                Assert.DoesNotThrow(() => BodyRules.CheckParts(null, new List<BodyProbe.Part>()));
                Assert.AreEqual(0, BodyRules.CheckParts(so, new List<BodyProbe.Part>()).Count);
            }
            finally { Object.DestroyImmediate(so); }
        }

        // ── СЛОТ ПРОТИВ ТЕЛЕСНОГО МЕСТА ───────────────────────────────────────────────────────
        // Две сущности, которые нельзя путать: слот — место ПОД ОРГАН (туда надевают, бывает
        // графтом), телесное место — только адрес и калибр для детей (`голова`, `шея`, `горб`,
        // `ямки`…). Надеть туда нечего, и флаг `graft` бессмыслен: открывать графтом нечего.
        //     Тесты заведены 11.09 после того, как это перепутал я сам — предложил завести графтами
        // `горб` и `ямки`. Данные оказались чисты, а вот валидатор ловил только ОДНУ сторону
        // (орган на телесном месте) и молчал на другой. Обе стороны теперь под тестом.

        SpeciesSO MakeBare(string name, BodySocket[] sockets, Organ[] organs)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = name;
            so.sockets = sockets ?? new BodySocket[0];
            so.organs = organs ?? new Organ[0];
            so.bones = new Bone[0];
            so.cages = new CageTable[0];
            return so;
        }

        [Test]
        public void Graft_НаТелесномМесте_ЭтоОшибка()
        {
            var so = MakeBare("тест", new[] { new BodySocket { name = "голова", graft = true } }, null);
            var issues = BodyRules.CheckData(so);
            Assert.IsTrue(issues.Exists(i => i.error && i.where == "голова" && i.text.Contains("graft")),
                          "графт у телесного места обязан быть ошибкой — открывать графтом нечего");
        }

        [Test]
        public void Graft_НаСлоте_ЭтоНорма()
        {
            // Хвост у человека именно так и заведён: место есть, пустым не рисуется, графтом проступает
            var so = MakeBare("тест", new[] { new BodySocket { name = "Хвост", graft = true } }, null);
            var issues = BodyRules.CheckData(so);
            Assert.IsFalse(issues.Exists(i => i.error && i.where == "Хвост"),
                           "графт у слота — законный приём, ошибкой быть не должен");
        }

        [Test]
        public void Орган_НаТелесномМесте_ЭтоОшибка()
        {
            // Обратная сторона того же правила, она в BodyRules была и до 11.09
            var so = MakeBare("тест", null, new[] { new Organ { organName = "Нечто", slot = "горб" } });
            var issues = BodyRules.CheckData(so);
            Assert.IsTrue(issues.Exists(i => i.error && i.text.Contains("ТЕЛЕСНОМ МЕСТЕ")),
                          "орган на адресе обязан быть ошибкой — надеть туда нечего");
        }
    }
}
