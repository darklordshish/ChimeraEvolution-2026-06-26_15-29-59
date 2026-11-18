using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// СТОРОЖ ОДНОЙ КОПИИ ЗАКОНОВ (план Ф0, С5). Произведение скорости хода и формула входящего урона живут в `Laws/`
    /// (`MoveLaw.Speed`, `DamageLaw.Incoming`); текстовый поиск по `Scripts/` не должен находить их второй раз — иначе
    /// симулятор и игра расходятся молча, как было с пятью копиями скорости (у змеи и альфы ярость тихо выпала).
    /// Метод тот же, что у `PsycheDataTests`: машина ищет, слова не нужны.
    /// </summary>
    public class LawsSingleCopyTests
    {
        // множитель скорости от опционального статуса: `* (rage != null ? rage.SpeedMult : 1f)` и родня (Breath.MoveMult, Vigor)
        static readonly Regex SpeedProduct = new(@"\*\s*\(\s*\w+\s*!=\s*null\s*\?\s*\w+\.(SpeedMult|MoveMult|Vigor)\b", RegexOptions.Compiled);
        // формула входящего урона: (1f - броня)
        static readonly Regex IncomingFormula = new(@"1f\s*-\s*[A-Za-z_.]*[Rr]eduction\b", RegexOptions.Compiled);

        // поиск психики другого вида по ТИПУ компонента: `GetComponentInParent<SnakePsyche>()` и родня — так добычу опознавали до С8
        static readonly Regex PsycheLookup = new(@"(?:GetComponent(?:InParent|InChildren)?|TryGetComponent)<(\w+Psyche)>", RegexOptions.Compiled);

        static string ScriptsRoot => Path.Combine(Application.dataPath, "_Chimera", "Scripts");

        static string[] Hits(Regex rx) =>
            Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories)
                .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (f, i, line)))
                .Where(t => !t.line.TrimStart().StartsWith("//") && rx.IsMatch(t.line))
                .Select(t => $"{Path.GetFileName(t.f)}:{t.i + 1}  {t.line.Trim()}")
                .ToArray();

        [Test]
        public void Detector_SeesOldShapes()
        {
            Assert.IsTrue(SpeedProduct.IsMatch("float Speed => moveSpeed * (rage != null ? rage.SpeedMult : 1f)"));
            Assert.IsTrue(SpeedProduct.IsMatch("* (Breath != null ? Breath.MoveMult : 1f);"));
            Assert.IsFalse(SpeedProduct.IsMatch("float rate = RegenPerSecond * (rage != null ? rage.StaminaRegenMult : 1f);"), "реген стамины — не скорость хода");
            Assert.IsTrue(IncomingFormula.IsMatch("amount * (1f - DamageReduction) * incoming"));
            Assert.IsTrue(Directory.Exists(ScriptsRoot));
        }

        [Test]
        public void MoveSpeedProduct_OnlyThroughLaw()
        {
            var hits = Hits(SpeedProduct);
            Assert.IsEmpty(hits, "произведение скорости хода вне `Laws/MoveLaw` — звать MoveLaw.Speed:\n" + string.Join("\n", hits));
        }

        [Test]
        public void Detector_SeesPsycheTypeLookup()
        {
            Assert.AreEqual("SnakePsyche", PsycheLookup.Match("var snake = col.GetComponentInParent<SnakePsyche>();").Groups[1].Value);
            Assert.AreEqual("HedgehogPsyche", PsycheLookup.Match("if (hp.GetComponent<HedgehogPsyche>() != null)").Groups[1].Value);
            Assert.AreEqual("WolfPsyche", PsycheLookup.Match("if (target.TryGetComponent<WolfPsyche>(out var wolf))").Groups[1].Value);
        }

        /// <summary>Добыча и угроза — по рациону вида и доминанте цели (`DietLaw`, `CreatureBody.Appetite/Fears`, С8), а не по типу
        /// психики, которую на цели случайно нашли. Допустим только свой тип в своём файле — вызов метода собственной психики
        /// (цепь рёва лосей: `MoosePsyche.cs` зовёт `Provoke` у соседа-лося) — это не выбор добычи. Нужен иной поиск по типу
        /// психики — добавлять сюда исключение с причиной, а не ослаблять регулярку.</summary>
        [Test]
        public void PreyAndThreat_NotByPsycheType()
        {
            var enemies = Path.Combine(ScriptsRoot, "Enemies");
            Assert.IsTrue(Directory.Exists(enemies));
            var hits = Directory.GetFiles(enemies, "*.cs", SearchOption.AllDirectories)
                .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (f, i, line)))
                .Where(t => !t.line.TrimStart().StartsWith("//"))
                .Select(t => (t, m: PsycheLookup.Match(t.line)))
                .Where(x => x.m.Success && x.m.Groups[1].Value != Path.GetFileNameWithoutExtension(x.t.f))
                .Select(x => $"{Path.GetFileName(x.t.f)}:{x.t.i + 1}  {x.t.line.Trim()}")
                .ToArray();
            Assert.IsEmpty(hits, "добыча/угроза по типу психики цели — звать `CreatureBody.Appetite`/`Fears` (рацион по доминанте):\n" + string.Join("\n", hits));
        }

        [Test]
        public void IncomingDamageFormula_OnlyThroughLaw()
        {
            var hits = Hits(IncomingFormula);
            Assert.IsEmpty(hits, "формула входящего урона вне `Laws/DamageLaw` — звать DamageLaw.Incoming:\n" + string.Join("\n", hits));
        }
    }
}
