using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Chimera.Laws;

namespace ChimeraSim
{
    /// <summary>Сводка видов по данным игры: HP, броня, скорость и урон основного приёма — через законы из `Laws/`, без движка.
    /// Тело вида считается как у природной особи на родном шасси: все родные органы надеты, мощь — `AffinityLaw.Multiplier` от
    /// экспрессии префаба (у человека префаба нет: экспрессия 0, родство 0 → мощь ×1), разброс и боссовость — единица.</summary>
    static class Summary
    {
        public struct Row
        {
            public string name, mainAbility;
            public float expression, power;
            public int hp, damage;
            public float armor, speed, stamina, period, windup, cooldown, psycheRhythm;
        }

        public static Row Compute(SimSpecies sp)
        {
            var p = sp.prefab;
            float power = AffinityLaw.Multiplier(p.expression, 0, p.bonusStartAffinity, p.bonusFullAffinity, p.maxBonusMult);

            var slots = new System.Collections.Generic.List<SlotContribution>();
            foreach (var o in sp.organs)
            {
                var none = new OrganValues();   // родной орган ничего не вытесняет
                slots.Add(new SlotContribution { slotType = o.slot, installed = false, contribution = BodyLaw.Express(none, o.values, true, power) });
            }
            var st = BodyLaw.Compute(new BodyInput
            {
                slots = slots, baseHp = sp.baseHp, baseStamina = sp.baseStamina, baseStaminaRegen = sp.baseStaminaRegen,
                varianceHp = 1f, bossHp = 1f, maxDamageReduction = p.maxDamageReduction, minAtkCooldown = p.minAtkCooldown,
            });

            // основной приём — с наибольшим раскрытым уроном среди родных органов (поле `damage`, у залпа `damagePerQuill`)
            var row = new Row { name = sp.name, expression = p.expression, power = power, hp = st.hp, armor = st.damageReduction,
                speed = MoveLaw.Speed(st.moveSpeed, 1f, 1f), stamina = st.staminaMax, psycheRhythm = p.psycheRhythm, mainAbility = "—" };
            int best = int.MinValue;
            foreach (var o in sp.organs)
                foreach (var a in o.abilities)
                {
                    if (!(a.TryGet("damage", out var d) || a.TryGet("damagePerQuill", out d))) continue;
                    int dmg = d.expressed ? BodyLaw.ExpressInt(d.value, 0f, true, power) : (int)d.value;
                    if (dmg <= best) continue;
                    best = dmg;
                    row.mainAbility = a.type.Replace("Data", "");
                    row.damage = DamageLaw.Outgoing(dmg, DamageLaw.OutgoingMult(1f, 1f, 1f, 1f, 1f));
                    row.windup = a.TryGet("windupTime", out var w) ? w.value : 0f;
                    row.cooldown = a.TryGet("cooldown", out var c) ? c.value : 0f;
                    row.period = TempoLaw.Period(new TempoRecord { windup = row.windup, cooldown = row.cooldown }, p.psycheRhythm);
                }
            return row;
        }

        public static void PrintSpecies(SimData data, TextWriter w)
        {
            var ci = CultureInfo.InvariantCulture;
            w.WriteLine("СВОДКА ВИДОВ (природная особь на родном шасси; законы из Laws/, данные из выгрузки)");
            w.WriteLine("вид       Э     мощь   HP   броня  скорость  стамина  приём       урон  замах  перез.  ритм  период");
            foreach (var sp in data.species)
            {
                var r = Compute(sp);
                w.WriteLine(string.Format(ci, "{0,-9} {1,-5:0.##} {2,-6:0.##} {3,-4} {4,-6:0.###} {5,-9:0.##} {6,-8:0.##} {7,-11} {8,-5} {9,-6:0.##} {10,-7:0.##} {11,-5:0.##} {12:0.##}",
                    r.name, r.expression, r.power, r.hp, r.armor, r.speed, r.stamina, r.mainAbility, r.damage, r.windup, r.cooldown, r.psycheRhythm, r.period));
            }
            w.WriteLine("Э — экспрессия префаба (0 — игрок: мощь по родству 0 → ×1); период = max(перезарядка записи, ритм психики) + замах");
        }

        public static void PrintLairs(SimData data, TextWriter w)
        {
            var ci = CultureInfo.InvariantCulture;
            w.WriteLine("ЛОГОВА павильона (темп из секунд мира в игровые часы — LairLaw)");
            foreach (var g in data.lairs.GroupBy(l => l.species).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var first = g.First();
                w.WriteLine(string.Format(ci, "{0,-6} логов {1,-3} ёмкость {2,-3} (всего {3,-3}) рождение раз в {4:0.##} ч, спад страха {5:0.##}/ч",
                    g.Key, g.Count(), first.capacity, g.Sum(l => l.capacity), first.respawnGameHours, first.fearDecayPerGameHour));
            }
        }
    }
}
