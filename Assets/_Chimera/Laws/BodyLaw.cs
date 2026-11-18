using System;
using System.Collections.Generic;

namespace Chimera.Laws
{
    /// <summary>Числа одного органа, какими их читает закон тела: поля `Organ` без типов Unity.</summary>
    public struct OrganValues
    {
        public float hpBonus, staminaBonus, staminaRegenBonus;
        public float atkCooldown, moveSpeed, dashSpeed, dashCooldown, damageReduction, regen, regenOOC;
        public float dashDuration, thermalRange, hearingMult;
        public bool enablesScent, coldBlooded, camo, enablesThermal, thorns, venomResist, bleedResist;
        public bool keenHearing, insight, digestion;
    }

    /// <summary>Вклад одного надетого органа в статы тела (после бленда и экспрессии).</summary>
    public struct OrganContribution
    {
        public float hpBonus, stam, stamRegen, atkCd, mv, dash, dashDur, dashCd, reduce, regen, regenOOC, thermal;
        public bool scent, cold, camo, thermalOn, digest;
        public bool thorns, venomResist, bleedResist, insight, keenEar;
        public float earMult;
    }

    /// <summary>Вклад слота: родной тип слота органа (по нему группируются дубли) и признак «надет звериный».</summary>
    public struct SlotContribution
    {
        public string slotType;
        public bool installed;
        public OrganContribution contribution;
    }

    /// <summary>Вход закона тела. Пустой список — вкладов нет. `Zero` у множителей читается игрой: отсутствующий разброс
    /// и боссовость подаются единицей.</summary>
    public struct BodyInput
    {
        public IReadOnlyList<SlotContribution> slots;
        public float baseHp, baseStamina, baseStaminaRegen;
        public float varianceHp, bossHp;
        public float maxDamageReduction, minAtkCooldown;
    }

    /// <summary>Итог закона тела: то, что игра раздаёт компонентам.</summary>
    public struct BodyStats
    {
        public int hp;                         // макс. HP (не меньше 1)
        public float hpBonus;                  // сумма бонусов групп (доля базы)
        public float damageReduction, regen, regenOutOfCombat;
        public float attackTempo;              // кулдаун атаки с полом
        public float moveSpeed, dash, dashDuration, dashCooldown;
        public float staminaMax, staminaRegen;
        public float thermalRange, earMult;
        public int beastSlots;                 // сколько надето звериных органов
        public bool scent, coldBlooded, camo, thermal, digest, insight, keenEar, thorns, venomResist, bleedResist;
    }

    /// <summary>
    /// Закон тела (Ф-1, Ф-2): раскрытие органа, свод дублей супремумом, сумма групп, HP, броня, темп, стамина.
    /// Прежний код `CreatureBody.Recompute` и `CreatureBody.Expression`: float и MathF, порядок операций сохранён.
    /// </summary>
    public static class BodyLaw
    {
        /// <summary>Пол рывка-кулдауна (`Mathf.Max(0.05f, dashCd)` в `Recompute`).</summary>
        public const float MinDashCooldown = 0.05f;

        /// <summary>Человеч.значение + (звериное − человеч.) × множитель.</summary>
        public static float Blend(float human, float beast, float mult) => human + (beast - human) * mult;

        /// <summary>Единое правило экспрессии органа. `held` — что орган вытеснил (пустой орган для родного и химерного слота),
        /// `worn` — надетый, `own` — родной орган шасси, `m` — мощь (множитель родства или фикс-экспрессия).</summary>
        public static OrganContribution Express(in OrganValues held, in OrganValues worn, bool own, float m)
        {
            float Scaled(float hv, float wv) => own ? wv * m : Blend(hv, wv, m);
            float Timed(float hv, float wv) => own ? wv : Blend(hv, wv, m);
            return new OrganContribution
            {
                hpBonus = Scaled(held.hpBonus, worn.hpBonus),
                stam = Scaled(held.staminaBonus, worn.staminaBonus),
                stamRegen = Scaled(held.staminaRegenBonus, worn.staminaRegenBonus),
                atkCd = Timed(held.atkCooldown, worn.atkCooldown),
                mv = Scaled(held.moveSpeed, worn.moveSpeed),
                dash = Scaled(held.dashSpeed, worn.dashSpeed),
                dashCd = Timed(held.dashCooldown, worn.dashCooldown),
                reduce = Scaled(held.damageReduction, worn.damageReduction),
                regen = Scaled(held.regen, worn.regen),
                regenOOC = own ? worn.regenOOC * m : worn.regenOOC,
                dashDur = worn.dashDuration, thermal = worn.thermalRange,
                scent = worn.enablesScent,
                cold = worn.coldBlooded, camo = worn.camo, thermalOn = worn.enablesThermal,
                digest = worn.digestion,
                insight = worn.insight,
                keenEar = worn.keenHearing, earMult = worn.hearingMult,
                thorns = worn.thorns, venomResist = worn.venomResist,
                bleedResist = worn.bleedResist,
            };
        }

        /// <summary>Супремум дублей одного типа слота: скаляры max (кулдауны min), флаги OR.</summary>
        public static OrganContribution Sup(in OrganContribution a, in OrganContribution b) => new()
        {
            hpBonus = LawMath.Max(a.hpBonus, b.hpBonus),
            stam = LawMath.Max(a.stam, b.stam), stamRegen = LawMath.Max(a.stamRegen, b.stamRegen),
            atkCd = LawMath.Min(a.atkCd, b.atkCd),
            mv = LawMath.Max(a.mv, b.mv), dash = LawMath.Max(a.dash, b.dash), dashDur = LawMath.Max(a.dashDur, b.dashDur), dashCd = LawMath.Min(a.dashCd, b.dashCd),
            reduce = LawMath.Max(a.reduce, b.reduce), regen = LawMath.Max(a.regen, b.regen),
            regenOOC = LawMath.Max(a.regenOOC, b.regenOOC), thermal = LawMath.Max(a.thermal, b.thermal),
            scent = a.scent || b.scent,
            cold = a.cold || b.cold, camo = a.camo || b.camo,
            thermalOn = a.thermalOn || b.thermalOn,
            digest = a.digest || b.digest,
            insight = a.insight || b.insight,
            keenEar = a.keenEar || b.keenEar, earMult = LawMath.Max(a.earMult, b.earMult),
            thorns = a.thorns || b.thorns, venomResist = a.venomResist || b.venomResist,
            bleedResist = a.bleedResist || b.bleedResist,
        };

        /// <summary>Статы тела: группировка вкладов по типу слота, супремум внутри группы, сумма групп, HP, потолок
        /// брони, пол темпа, бак и реген стамины. Группы суммируются в порядке первого появления типа (как словарь без удалений).</summary>
        public static BodyStats Compute(in BodyInput input)
        {
            var groups = new Dictionary<string, OrganContribution>();
            var order = new List<string>();
            var s = new BodyStats();
            if (input.slots != null)
                foreach (var sl in input.slots)
                {
                    if (sl.installed) s.beastSlots++;
                    if (groups.TryGetValue(sl.slotType, out var prev)) groups[sl.slotType] = Sup(prev, sl.contribution);
                    else { groups[sl.slotType] = sl.contribution; order.Add(sl.slotType); }
                }

            float hpBonusF = 0f, stamF = 0f, stamRegF = 0f;
            float atkCd = 0f, mv = 0f, dash = 0f, dashDur = 0f, dashCd = 0f, reduce = 0f, regen = 0f, regenOOC = 0f, thermal = 0f;
            float earMult = 0f;
            foreach (var key in order)
            {
                var c = groups[key];
                hpBonusF += c.hpBonus; stamF += c.stam; stamRegF += c.stamRegen;
                atkCd += c.atkCd; mv += c.mv; dash += c.dash; dashDur += c.dashDur; dashCd += c.dashCd;
                reduce += c.reduce; regen += c.regen; regenOOC += c.regenOOC; thermal += c.thermal;
                s.scent |= c.scent;
                s.coldBlooded |= c.cold; s.camo |= c.camo; s.thermal |= c.thermalOn;
                s.digest |= c.digest;
                s.insight |= c.insight;
                s.keenEar |= c.keenEar; earMult = LawMath.Max(earMult, c.earMult);
                s.thorns |= c.thorns; s.venomResist |= c.venomResist;
                s.bleedResist |= c.bleedResist;
            }

            s.hpBonus = hpBonusF;
            s.attackTempo = LawMath.Max(input.minAtkCooldown, atkCd);
            s.moveSpeed = mv; s.dash = dash; s.dashDuration = dashDur;
            s.dashCooldown = LawMath.Max(MinDashCooldown, dashCd);
            int hp = (int)MathF.Round(input.baseHp * (1f + hpBonusF) * input.varianceHp * input.bossHp);
            s.hp = Math.Max(1, hp);
            s.damageReduction = LawMath.Min(input.maxDamageReduction, LawMath.Clamp01(reduce));
            s.regen = regen; s.regenOutOfCombat = regenOOC;
            s.staminaMax = input.baseStamina * (1f + stamF);
            s.staminaRegen = input.baseStaminaRegen * (1f + stamRegF);
            s.thermalRange = thermal; s.earMult = earMult;
            return s;
        }
    }
}
