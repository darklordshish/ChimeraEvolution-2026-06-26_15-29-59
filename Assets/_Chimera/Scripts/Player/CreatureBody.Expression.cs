using Chimera.Laws;
using UnityEngine;

// PARTIAL-SPLIT #3 (рефактор ядра): ДВИЖОК ЭКСПРЕССИИ вынесен сюда из CreatureBody.cs — тот же класс,
// НОЛЬ изменений поведения. Концерн: «как ОДИН надетый орган раскрывается в статы» (правило бленда/мощи).
// Contribution = вклад органа; Express = единое правило раскрытия; ChassisOrgan/EmptyOrgan — его матчасть; арифметика — в `Chimera.Laws.BodyLaw`.
// АГРЕГАЦИЯ вкладов и РАЗДАЧА по компонентам (Recompute) — отдельный концерн, остались в CreatureBody.cs
// (зовут Express/Contribution.Sup через partial). Потребитель мощи BonusMultiplier — в CreatureBody.Affinity.cs.
public partial class CreatureBody
{
    // вклад одного надетого органа в статы тела (после бленда/экспрессии)
    struct Contribution
    {
        public float hpBonus, stam, stamRegen, atkCd, mv, dash, dashDur, dashCd, reduce, regen, regenOOC, thermal;
        public bool scent, cold, camo, thermalOn, digest;
        public bool thorns, venomResist; // иглы-ответка, ядоупорность (ёж)
        public bool bleedResist;  // кровеупорность (лосиное сердце)
        public bool insight; // ЧУТЬЁ УЧЁНОГО: распознавание намерений + числа состояний (человеческое Чутьё)
        public bool keenEar;  // ОСТРЫЙ СЛУХ: различение вида источника + волны звука на экране
        public float earMult; // множитель дальности слуха (супремум дублей)

        // СУПРЕМУМ дублей одного типа слота — закон тела (`BodyLaw.Sup`): скаляры max (кулдауны min), флаги OR.
        // Дубль оси силу НЕ растит (второе сердце ≠ ×2 регена) — окупается только НОВЫМ направлением.
        public static Contribution Sup(Contribution a, Contribution b) => FromLaw(BodyLaw.Sup(a.ToLaw(), b.ToLaw()));

        // Поля тут те же, что в `OrganContribution` закона: тип остался вложенным ради сторожей и порядка кода тела,
        // вся арифметика — в `Chimera.Laws`
        internal OrganContribution ToLaw() => new()
        {
            hpBonus = hpBonus, stam = stam, stamRegen = stamRegen, atkCd = atkCd, mv = mv, dash = dash, dashDur = dashDur,
            dashCd = dashCd, reduce = reduce, regen = regen, regenOOC = regenOOC, thermal = thermal,
            scent = scent, cold = cold, camo = camo, thermalOn = thermalOn, digest = digest,
            thorns = thorns, venomResist = venomResist, bleedResist = bleedResist, insight = insight, keenEar = keenEar,
            earMult = earMult,
        };

        internal static Contribution FromLaw(in OrganContribution c) => new()
        {
            hpBonus = c.hpBonus, stam = c.stam, stamRegen = c.stamRegen, atkCd = c.atkCd, mv = c.mv, dash = c.dash, dashDur = c.dashDur,
            dashCd = c.dashCd, reduce = c.reduce, regen = c.regen, regenOOC = c.regenOOC, thermal = c.thermal,
            scent = c.scent, cold = c.cold, camo = c.camo, thermalOn = c.thermalOn, digest = c.digest,
            thorns = c.thorns, venomResist = c.venomResist, bleedResist = c.bleedResist, insight = c.insight, keenEar = c.keenEar,
            earMult = c.earMult,
        };
    }

    /// <summary>Адаптер ассета органа во вход закона (числа органа без типов Unity).</summary>
    internal static OrganValues ToLaw(Organ o) => new()
    {
        hpBonus = o.hpBonus, staminaBonus = o.staminaBonus, staminaRegenBonus = o.staminaRegenBonus,
        atkCooldown = o.atkCooldown, moveSpeed = o.moveSpeed, dashSpeed = o.dashSpeed, dashCooldown = o.dashCooldown,
        damageReduction = o.damageReduction, regen = o.regen, regenOOC = o.regenOOC,
        dashDuration = o.dashDuration, thermalRange = o.thermalRange, hearingMult = o.hearingMult,
        enablesScent = o.enablesScent, coldBlooded = o.coldBlooded, camo = o.camo, enablesThermal = o.enablesThermal,
        thorns = o.thorns, venomResist = o.venomResist, bleedResist = o.bleedResist,
        keenHearing = o.keenHearing, insight = o.insight, digestion = o.digestion,
    };

    static readonly Organ EmptyOrgan = new(); // «нет базы»: бленд от нуля = чистый орган × мощь

    // родной орган шасси для типа слота — ТО, ЧТО ДОНОРСКИЙ ВЫТЕСНЯЕТ (база экспрессии).
    // Живёт в теле, а не полем слота: база — функция шасси, слот её только использует
    Organ ChassisOrgan(string slotName)
    {
        if (chassis == null || chassis.organs == null) return null;
        foreach (var o in chassis.organs) if (o.slot == slotName) return o;
        return null;
    }

    /// <summary>ЕДИНОЕ ПРАВИЛО ЭКСПРЕССИИ для любого органа в любом слоте (было двумя копипаст-ветками).
    /// База — то, что орган ВЫТЕСНИЛ:
    ///  • РОДНОЙ орган шасси не вытесняет ничего (он и есть оригинал) → раскрывается ОТ СЕБЯ: величины ×мощь;
    ///  • ДОНОРСКИЙ вытесняет родной орган этого слота → блендится ОТ НЕГО (низкое родство ≈ человек,
    ///    высокое ≈ зверь, овершут — за зверя). В ХИМЕРНОМ слоте вытеснять нечего (орган ДОПОЛНИТЕЛЬНЫЙ) → от нуля.
    /// Режим решает `native`, а НЕ «есть ли база»: иначе донорский орган в химерном слоте поменял бы
    /// поведение времён и вне-боевого регена (там база пуста, но раскрытие всё равно «графтовое»).</summary>
    Contribution Express(Slot sl)
    {
        var pick = sl.Pick;
        Organ w = pick.organ;
        bool own = pick.native;   // родной орган шасси — раскрывается от себя
        Organ h = own || sl.chimera ? EmptyOrgan : ChassisOrgan(sl.name) ?? EmptyOrgan;
        float m = BonusMultiplier(pick.species); // у родного варианта species = вид шасси → та же ручка

        return Contribution.FromLaw(BodyLaw.Express(ToLaw(h), ToLaw(w), own, m)); // арифметика — закон тела
    }

}
