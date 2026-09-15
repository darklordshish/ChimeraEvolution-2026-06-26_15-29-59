using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// ДЕТЕКТОР ГРАНИЦЫ ОРГАНА И ПСИХИКИ (спека 16.09 §2). Психика — управление телом: решения, восприятие, ритм выбора.
    /// Всё, что описывает ПРИЁМ — цена, перезарядка, замах, сигнал, отлёт, урон, расход, интервал, — живёт в записи органа,
    /// иначе химера с лосиными ногами таранила бы без цены, а лось, обросший чужими органами, — по чужой.
    /// Число психики с таким именем обязано нести [NotOrganData("причина")] (это решение, а не число приёма) или стоять
    /// в долге. Долг валится в обе стороны, как у носителей (OrganDataTests).
    /// </summary>
    public class PsycheDataTests
    {
        // СЛОВА ИМЕНИ, а не подстроки: «rescueRadius» — радиус спасения, в нём нет сигнала «cue». Имя режется по camelCase
        static readonly HashSet<string> AbilityWords = new(StringComparer.Ordinal)
            { "cost", "cooldown", "windup", "cue", "knock", "damage", "drain", "interval" };

        static bool IsAbilityLike(string name) =>
            Regex.Split(name, "(?=[A-Z])").Any(word => AbilityWords.Contains(word.ToLowerInvariant()));

        // ДОЛГ: «Психика.поле» — числа, чья принадлежность ждёт решения геймдизайнера
        static readonly HashSet<string> Debt = new(StringComparer.Ordinal)
        {
            // затраты хода, а не приёма: органу ног или психике? Тем же вопросом у игрока рывок и спринт (спека 16.09 §2)
            "MoosePsyche.chaseDrain", "WolfPsyche.chaseDrain",
        };

        const BindingFlags Own = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        static IEnumerable<Type> Psyches() =>
            typeof(IBodyStatConsumer).Assembly.GetTypes()
                .Where(t => t.IsClass && typeof(IBodyStatConsumer).IsAssignableFrom(t) && typeof(MonoBehaviour).IsAssignableFrom(t));

        static bool IsSerialized(FieldInfo f) =>
            !f.IsStatic && !f.IsInitOnly && !f.IsLiteral && !f.IsDefined(typeof(NonSerializedAttribute))
            && (f.IsPublic || f.IsDefined(typeof(SerializeField)));

        static SortedSet<string> Suspects()
        {
            var found = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var t in Psyches())
                foreach (var f in t.GetFields(Own))
                    if (IsSerialized(f) && (f.FieldType == typeof(float) || f.FieldType == typeof(int))
                        && IsAbilityLike(f.Name) && !f.IsDefined(typeof(NotOrganDataAttribute)))
                        found.Add(t.Name + "." + f.Name);
            return found;
        }

        static string Lines(IEnumerable<string> items) => string.Join("\n", items.Select(s => "  " + s));

        [Test]
        public void NameWords_NotSubstrings()
        {
            Assert.IsTrue(IsAbilityLike("rattleCue"), "сигнал гремка — слово «Cue»");
            Assert.IsTrue(IsAbilityLike("chargeCost"), "цена тарана — слово «Cost»");
            Assert.IsTrue(IsAbilityLike("cooldown"), "имя из одного слова");
            Assert.IsFalse(IsAbilityLike("rescueRadius"), "«rescue» — не сигнал, подстрока не в счёт");
        }

        [Test]
        public void Detector_SeesPsyches()
        {
            Assert.GreaterOrEqual(Psyches().Count(), 5, "детектор не видит психик — сменился их общий интерфейс?");
        }

        [Test]
        public void NoAbilityNumbers_InPsyche()
        {
            var grown = Suspects().Where(n => !Debt.Contains(n)).ToList();
            Assert.IsEmpty(grown,
                "число приёма в психике. Перенеси в запись органа (носитель платит и ждёт сам) или, если это решение психики, " +
                "пометь [NotOrganData(\"причина\")]:\n" + Lines(grown));
        }

        [Test]
        public void Debt_IsNotStale()
        {
            var live = Suspects();
            var stale = Debt.Where(n => !live.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToList();
            Assert.IsEmpty(stale, "число уже решено, а в долге осталось — вычеркни:\n" + Lines(stale));
        }

        [Test]
        public void NotOrganData_InPsyche_HasReason()
        {
            var silent = Psyches().SelectMany(t => t.GetFields(Own).Select(f => (t, f)))
                .Where(p => p.f.GetCustomAttribute<NotOrganDataAttribute>() is { } a && string.IsNullOrWhiteSpace(a.Reason))
                .Select(p => p.t.Name + "." + p.f.Name).ToList();
            Assert.IsEmpty(silent, "[NotOrganData] без причины:\n" + Lines(silent));
        }
    }
}
