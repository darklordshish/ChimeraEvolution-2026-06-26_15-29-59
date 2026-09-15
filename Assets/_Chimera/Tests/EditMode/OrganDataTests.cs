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
            // задача 3 — рога и таран
            "AntlerAbility.bleedStacks", "AntlerAbility.damage", "AntlerAbility.halfAngle",
            "AntlerAbility.knockForce", "AntlerAbility.range", "ChargeAbility.chargeSpeed", "ChargeAbility.damage",
            "ChargeAbility.damagePerMeter", "ChargeAbility.duration", "ChargeAbility.hitRadius",
            "ChargeAbility.knockForce", "ChargeAbility.lightChargeMult", "ChargeAbility.maxRange",
            "ChargeAbility.minRange", "ChargeAbility.plowForce", "ChargeAbility.plowRadius",
            "ChargeAbility.staggerTime", "ChargeAbility.stompForce", "ChargeAbility.stompRadius",
            "ChargeAbility.stompStagger", "PlayerAntler.bleedStacks", "PlayerAntler.cooldown", "PlayerAntler.damage",
            "PlayerAntler.force", "PlayerAntler.radius", "PlayerAntler.range", "PlayerCharge.damage",
            "PlayerCharge.force", "PlayerCharge.lightChargeMult", "PlayerCharge.radius", "PlayerCharge.reach",
            // задача 4 — залп, клубок, перекат
            "CurlDefense.curlArmor", "CurlDefense.rollBleed", "CurlDefense.rollDamage", "CurlDefense.rollDrain",
            "CurlDefense.rollGravity", "CurlDefense.rollKnock", "CurlDefense.rollRadius", "CurlDefense.rollSpeed",
            "CurlDefense.rollTurnSpeed", "CurlDefense.staminaDrain", "PlayerQuillVolley.bleedPerQuill",
            "PlayerQuillVolley.cooldown", "PlayerQuillVolley.damagePerQuill", "PlayerQuillVolley.hitRadius",
            "PlayerQuillVolley.quills", "PlayerQuillVolley.range", "PlayerQuillVolley.slowPerQuill",
            "PlayerQuillVolley.speed", "PlayerQuillVolley.spreadAngle", "PlayerRoll.bleedStacks", "PlayerRoll.damage",
            "PlayerRoll.force", "PlayerRoll.radius", "PlayerRoll.reach", "QuillVolley.bleedPerQuill",
            "QuillVolley.blindAimError", "QuillVolley.damagePerQuill", "QuillVolley.hitRadius",
            "QuillVolley.maxRange", "QuillVolley.minRange", "QuillVolley.quills", "QuillVolley.slowPerQuill",
            "QuillVolley.speed", "QuillVolley.spreadAngle",
            // задача 5 — наскок и удар конечностью (меч и пинок игрока — та же роль)
            "LeapAbility.damage", "LeapAbility.duration", "LeapAbility.hitRadius", "LeapAbility.maxRange",
            "LeapAbility.minRange", "LeapAbility.speed", "LeapAbility.up", "LimbStrikeAbility.bleedStacks",
            "LimbStrikeAbility.damage", "LimbStrikeAbility.halfAngle", "LimbStrikeAbility.knockForce",
            "LimbStrikeAbility.range", "PlayerAttack.cooldown", "PlayerAttack.damage", "PlayerAttack.radius",
            "PlayerAttack.range", "PlayerKick.cooldown", "PlayerKick.damage", "PlayerKick.force", "PlayerKick.radius",
            "PlayerKick.range",
            // задача 6 — голос: вой, рёв, клич
            "PlayerBellow.cooldown", "PlayerBellow.fearRadius", "PlayerBellow.rallyRadius", "PlayerHowl.cooldown",
            "PlayerHowl.fearMoraleHit", "PlayerHowl.fearRadius", "PlayerHowl.radius", "PlayerHowl.stunDuration",
            "PlayerScream.boostPerStack", "PlayerScream.cooldown", "PlayerScream.maxBoost",
            // задача 6-бис — захват (в таблице спеки пропущен, см. её §10)
            "Constrict.breakRawThreshold", "Constrict.chokeDamage", "Constrict.chokeInterval", "Constrict.escapeMax",
            "Constrict.escapeMin", "Constrict.grabSlow1", "Constrict.grabSlow2", "Constrict.grabSlow3",
            "Constrict.holdDrain", "Constrict.loosenPerDamage", "Constrict.npcChokeDamage",
            "Constrict.npcChokeInterval", "Constrict.npcLoosenPerDamage", "Constrict.stage2At", "Constrict.stage3At",
            "Constrict.tightenRate", "Constrict.wearInterval", "PlayerConstrict.breakDamage",
            "PlayerConstrict.cooldown", "PlayerConstrict.dragOffset", "PlayerConstrict.escapeKnock",
            "PlayerConstrict.escapeMax", "PlayerConstrict.escapeMin", "PlayerConstrict.grabRange",
            "PlayerConstrict.holdRange", "PlayerConstrict.selfSlow1", "PlayerConstrict.selfSlow2",
            // база доставок — замах общий у всех, уходит последним
            "WindupAbility.windupTime",
        };

        // носители, которых НЕ видно через IAbility: без явной проверки детектор мог бы молча ослепнуть на них
        static readonly string[] CarriersWithoutTryUse = { "PlayerCharge", "PlayerRoll", "CurlDefense", "Constrict" };

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
