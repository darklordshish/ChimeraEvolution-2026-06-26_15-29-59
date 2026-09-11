using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// BodyRules CheckParts и CheckData: вложенность мест по замеру и слот против телесного места.
    /// Бюджет клетки (324 квада) сторожился здесь до 12.09 — клетка отменена 10.09 и снята из кода вместе с ним.
    /// </summary>
    public class BodyRulesCheckTests
    {
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
