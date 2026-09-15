using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// ДЕТЕКТОР «ДАННЫЕ В ОРГАНАХ» (спека `2026-09-12-dannye-v-organah.md` §8). Все числа приёма живут в
    /// записи органа. У носителя приёма (<see cref="IAbilityCarrier"/>) сериализованное число допустимо
    /// только с <see cref="NotOrganDataAttribute"/> и причиной. Остальное — ДОЛГ: он перечислен ниже и
    /// сжимается задача за задачей. Тест валится в обе стороны: число вне долга (долг растёт) и снятое,
    /// но оставшееся в списке (сторож протух). К задаче 8 список обязан опустеть.
    /// </summary>
    public class OrganDataTests
    {
        // ДОЛГ: «Класс.поле» — числа приёмов, ещё не переехавшие в запись органа
        static readonly HashSet<string> Debt = new(StringComparer.Ordinal)
        {
            // ПУСТО: числа всех приёмов переехали в записи органов (задачи 2–6-бис). Сюда число попадает только
            // на время переезда; законное число индивида — на поле с [NotOrganData("причина")]
        };

        // носители, которых НЕ видно через IAbility: без явной проверки детектор мог бы молча ослепнуть на них
        static readonly string[] CarriersWithoutTryUse = { "PlayerCharge", "PlayerRoll", "CurlDefense", "Constrict" };

        // ВКЛЮЧАТЕЛЬ ПРИЁМА — ЗАПИСЬ ОРГАНА (задача 8). Булевы `enables*` у органа остались только у чувств:
        // их перевод в данные — шаг 5 ревизии, не эта спека. Снимешь флаг чувства — вычеркни его отсюда
        static readonly string[] SenseFlags = { "enablesScent", "enablesThermal" };

        const BindingFlags Own = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        static IEnumerable<Type> Carriers() =>
            typeof(IAbilityCarrier).Assembly.GetTypes()
                .Where(t => t.IsClass && typeof(IAbilityCarrier).IsAssignableFrom(t));

        // сериализует ли Unity это поле: public или [SerializeField], не static/readonly/const, не [NonSerialized]
        static bool IsSerialized(FieldInfo f) =>
            !f.IsStatic && !f.IsInitOnly && !f.IsLiteral
            && !f.IsDefined(typeof(NonSerializedAttribute))
            && (f.IsPublic || f.IsDefined(typeof(SerializeField)));

        static bool IsNumber(Type t) => t == typeof(float) || t == typeof(int);

        static SortedSet<string> NumbersOutsideOrgan()
        {
            var found = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var t in Carriers())
                foreach (var f in t.GetFields(Own))
                    if (IsSerialized(f) && IsNumber(f.FieldType) && !f.IsDefined(typeof(NotOrganDataAttribute)))
                        found.Add(t.Name + "." + f.Name);
            return found;
        }

        static string Lines(IEnumerable<string> items) => string.Join("\n", items.Select(s => "  " + s));

        [Test]
        public void Organ_HasNoAbilityFlags_OnlySenseFlagsLeft()
        {
            var flags = typeof(Organ).GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(f => f.FieldType == typeof(bool) && f.Name.StartsWith("enables", StringComparison.Ordinal))
                .Select(f => f.Name).ToList();
            var unexpected = flags.Where(n => !SenseFlags.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToList();
            Assert.IsEmpty(unexpected, "включатель приёма — запись органа (AbilityData), а не булев флаг:\n" + Lines(unexpected));
            var stale = SenseFlags.Where(n => !flags.Contains(n)).ToList();
            Assert.IsEmpty(stale, "флаг чувства снят, а в списке остался — вычеркни:\n" + Lines(stale));
        }

        [Test]
        public void EveryAbility_IsCarrier_AndHiddenCarriersAreSeen()
        {
            var asm = typeof(IAbilityCarrier).Assembly;
            var dodgers = asm.GetTypes()
                .Where(t => t.IsClass && typeof(IAbility).IsAssignableFrom(t) && !typeof(IAbilityCarrier).IsAssignableFrom(t))
                .Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
            Assert.IsEmpty(dodgers, "приём с TryUse, но без IAbilityCarrier, — его числа детектор не видит:\n" + Lines(dodgers));

            var seen = new HashSet<string>(Carriers().Select(t => t.Name));
            var blind = CarriersWithoutTryUse.Where(n => !seen.Contains(n)).ToList();
            Assert.IsEmpty(blind, "носитель без TryUse выпал из охвата детектора:\n" + Lines(blind));
        }

        [Test]
        public void NoNewNumbers_OutsideOrgan()
        {
            var grown = NumbersOutsideOrgan().Where(n => !Debt.Contains(n)).ToList();
            Assert.IsEmpty(grown,
                "число приёма у носителя, а не в записи органа. Перенеси в AbilityData или, если это законно число " +
                "индивида (ощущение, физика тела, отладка), пометь [NotOrganData(\"причина\")]:\n" + Lines(grown));
        }

        [Test]
        public void Debt_IsNotStale()
        {
            var live = NumbersOutsideOrgan();
            var stale = Debt.Where(n => !live.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToList();
            Assert.IsEmpty(stale, "число уже снято с носителя, а в списке долга осталось — вычеркни:\n" + Lines(stale));
        }

        [Test]
        public void NotOrganData_AlwaysHasReason()
        {
            var silent = Carriers()
                .SelectMany(t => t.GetFields(Own).Select(f => (t, f)))
                .Where(p => p.f.GetCustomAttribute<NotOrganDataAttribute>() is { } a && string.IsNullOrWhiteSpace(a.Reason))
                .Select(p => p.t.Name + "." + p.f.Name).ToList();
            Assert.IsEmpty(silent, "[NotOrganData] без причины — пометка обязана объяснять, почему число не из органа:\n" + Lines(silent));
        }
    }
}
