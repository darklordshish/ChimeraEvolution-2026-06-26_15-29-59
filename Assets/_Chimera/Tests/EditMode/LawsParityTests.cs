using System.Collections.Generic;
using System.Linq;
using Chimera.Laws;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>ПАРИТЕТ ЗАКОНОВ С ПРЕЖНИМИ ФОРМУЛАМИ ИГРЫ (план Ф0, допуск — ноль). Эталоны ниже — копии формул, какими они
    /// стояли в игре ДО выноса в `Assets/_Chimera/Laws/` (на `Mathf`); закон на `MathF` обязан давать те же целые на
    /// всей таблице входов. Эталоны не правятся: расхождение — значит, закон изменил поведение игры.</summary>
    public class LawsParityTests
    {
        // ---- эталоны: прежние формулы дословно ----
        static int OldIncoming(int amount, float reduction, float incoming)
            => Mathf.Max(1, Mathf.RoundToInt(amount * (1f - reduction) * incoming));

        static float OldOutgoingMult(float rage, float variance, float morale, float vigor, float boss)
        {
            float m = 1f;
            m *= rage; m *= variance; m *= morale; m *= vigor; m *= boss;
            return m;
        }

        static int OldOutgoing(int damage, float mult) => damage <= 0 ? damage : Mathf.Max(1, Mathf.RoundToInt(damage * mult));
        static int OldBlow(int damage, float mult) => Mathf.RoundToInt(damage * mult);
        static int OldCharge(int damage, float perMeter, float run, float mult) => Mathf.RoundToInt((damage + perMeter * run) * mult);

        static readonly int[] Ints = { -3, 0, 1, 2, 3, 5, 7, 8, 10, 13, 17, 22, 25, 30, 41, 64, 100, 250 };
        static readonly float[] Reductions = { 0f, 0.05f, 0.1f, 0.2f, 0.35f, 0.5f, 0.75f, 0.99f, 1f };
        static readonly float[] Mults = { 0.5f, 0.8f, 0.9f, 1f, 1.1f, 1.25f, 1.3333f, 1.5f, 2f, 2.5f, 3f };
        static readonly float[] Runs = { 0f, 0.7f, 2.5f, 6f, 11.37f };

        [Test]
        public void Incoming_MatchesOldFormula()
        {
            int n = 0;
            foreach (int raw in Ints) foreach (float red in Reductions) foreach (float m in Mults)
            {
                Assert.AreEqual(OldIncoming(raw, red, m), DamageLaw.Incoming(raw, red, m), $"raw {raw} red {red} mult {m}");
                n++;
            }
            Assert.Greater(n, 1000);
        }

        [Test]
        public void Incoming_HalfwayCases_RoundToEven()
        {
            // .5 ровно: 5 * 0.5 = 2.5 -> 2, 7 * 0.5 = 3.5 -> 4 (к чётному — как Mathf.RoundToInt)
            Assert.AreEqual(2, DamageLaw.Incoming(5, 0.5f, 1f));
            Assert.AreEqual(4, DamageLaw.Incoming(7, 0.5f, 1f));
            Assert.AreEqual(OldIncoming(5, 0.5f, 1f), DamageLaw.Incoming(5, 0.5f, 1f));
        }

        [Test]
        public void OutgoingMult_AndOutgoing_MatchOldFormula()
        {
            foreach (float rage in new[] { 1f, 1.3f }) foreach (float variance in new[] { 0.85f, 1f, 1.17f })
            foreach (float morale in new[] { 1f, 1.2f }) foreach (float vigor in new[] { 0.6f, 1f })
            foreach (float boss in new[] { 1f, 1.5f, 2.25f })
            {
                float old = OldOutgoingMult(rage, variance, morale, vigor, boss);
                float mult = DamageLaw.OutgoingMult(rage, variance, morale, vigor, boss);
                Assert.AreEqual(old, mult, 0f, "множитель");
                foreach (int d in Ints) Assert.AreEqual(OldOutgoing(d, old), DamageLaw.Outgoing(d, mult), $"урон {d} мульт {old}");
            }
        }

        [Test]
        public void Blow_MatchesOldFormula()
        {
            foreach (int d in Ints) foreach (float m in Mults)
                Assert.AreEqual(OldBlow(d, m), DamageLaw.Blow(d, m), $"урон {d} мульт {m}");
        }

        [Test]
        public void ChargeBlow_MatchesOldFormula()
        {
            foreach (int d in new[] { 10, 22, 30, 45 }) foreach (float per in new[] { 0f, 1f, 1.5f, 2.2f })
            foreach (float run in Runs) foreach (float m in Mults)
                Assert.AreEqual(OldCharge(d, per, run, m), DamageLaw.Blow(d, per, run, m), $"урон {d} на м {per} разбег {run} мульт {m}");
        }

        // =====================================================================================
        // ЗАКОН ТЕЛА И РОДСТВА (С3): то, что тело выставило компонентам, против (а) прежних формул дословно (на Mathf)
        // и (б) независимого вызова закона. Допуск — ноль.
        // =====================================================================================

        static readonly string[] SpeciesNames = { "Человек", "Волк", "Лось", "Ёж", "Змея" };

        // виды без мест: морфологию тело не строит (на статы она не влияет), прогон остаётся лёгким. Остальное — ассеты как есть
        List<SpeciesSO> LoadSpecies()
        {
            var res = new List<SpeciesSO>();
            foreach (var n in SpeciesNames)
            {
                var so = UnityEditor.AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/" + n + ".asset");
                Assert.IsNotNull(so, "нет ассета вида " + n);
                var copy = Object.Instantiate(so);
                copy.name = n;
                copy.sockets = new BodySocket[0];
                madeSo.Add(copy);
                res.Add(copy);
            }
            return res;
        }

        readonly List<Object> madeSo = new();

        sealed class SpeedSink : MonoBehaviour, IBodyStatConsumer
        {
            public float speed = -1f;
            public void OnBodyStats(float moveSpeed) => speed = moveSpeed;
        }

        // прежняя арифметика Recompute/Express дословно, на Mathf
        struct OldC
        {
            public float hpBonus, stam, stamRegen, atkCd, mv, dash, dashDur, dashCd, reduce, regen, regenOOC, thermal, earMult;
            public static OldC Sup(OldC a, OldC b) => new()
            {
                hpBonus = Mathf.Max(a.hpBonus, b.hpBonus),
                stam = Mathf.Max(a.stam, b.stam), stamRegen = Mathf.Max(a.stamRegen, b.stamRegen),
                atkCd = Mathf.Min(a.atkCd, b.atkCd),
                mv = Mathf.Max(a.mv, b.mv), dash = Mathf.Max(a.dash, b.dash), dashDur = Mathf.Max(a.dashDur, b.dashDur), dashCd = Mathf.Min(a.dashCd, b.dashCd),
                reduce = Mathf.Max(a.reduce, b.reduce), regen = Mathf.Max(a.regen, b.regen),
                regenOOC = Mathf.Max(a.regenOOC, b.regenOOC), thermal = Mathf.Max(a.thermal, b.thermal),
                earMult = Mathf.Max(a.earMult, b.earMult),
            };
        }

        static float OldBlend(float human, float beast, float mult) => human + (beast - human) * mult;

        static OldC OldExpress(Organ h, Organ w, bool own, float m)
        {
            float Scaled(float hv, float wv) => own ? wv * m : OldBlend(hv, wv, m);
            float Timed(float hv, float wv) => own ? wv : OldBlend(hv, wv, m);
            return new OldC
            {
                hpBonus = Scaled(h.hpBonus, w.hpBonus), stam = Scaled(h.staminaBonus, w.staminaBonus),
                stamRegen = Scaled(h.staminaRegenBonus, w.staminaRegenBonus), atkCd = Timed(h.atkCooldown, w.atkCooldown),
                mv = Scaled(h.moveSpeed, w.moveSpeed), dash = Scaled(h.dashSpeed, w.dashSpeed), dashCd = Timed(h.dashCooldown, w.dashCooldown),
                reduce = Scaled(h.damageReduction, w.damageReduction), regen = Scaled(h.regen, w.regen),
                regenOOC = own ? w.regenOOC * m : w.regenOOC, dashDur = w.dashDuration, thermal = w.thermalRange, earMult = w.hearingMult,
            };
        }

        static float OldMultiplier(float expression, int affinity, float start, float full, float maxMult)
        {
            if (expression > 0f) return expression;
            float span = Mathf.Max(1f, full - start);
            float t = Mathf.Clamp01((affinity - start) / span);
            return Mathf.Lerp(1f, maxMult, t);
        }

        int checkedBodies;

        // сверка одного собранного тела: компоненты против прежней арифметики и против независимого вызова закона
        void CheckBody(CreatureBody body, GameObject go, SpeciesSO chassis, Dictionary<string, SpeciesSO> bySpecies,
                       float expression, int affinityToDonors, string label)
        {
            var health = go.GetComponent<Health>();
            var stamina = go.GetComponent<Stamina>();
            var sink = go.GetComponent<SpeedSink>();
            var groups = new Dictionary<string, OldC>();
            var lawSlots = new List<SlotContribution>();
            for (int i = 0; i < body.SlotCount; i++)
            {
                var sv = body.GetSlot(i);
                var vv = body.GetVariants(i).FirstOrDefault(x => x.worn);
                if (vv.organName == null) continue; // пустой химерный слот
                var w = bySpecies[vv.species].organs.First(o => o.organName == vv.organName && o.slot == vv.slotType);
                Organ h = vv.native || sv.chimera ? new Organ() : chassis.organs.FirstOrDefault(o => o.slot == sv.slot) ?? new Organ();
                int aff = vv.native ? body.GetAffinity(chassis.speciesName) : affinityToDonors;
                float m = OldMultiplier(expression, aff, 0f, 100f, 2f);
                Assert.AreEqual(m, AffinityLaw.Multiplier(expression, aff, 0f, 100f, 2f), 0f, label + " мощь");
                var c = OldExpress(h, w, vv.native, m);
                groups[w.slot] = groups.TryGetValue(w.slot, out var prev) ? OldC.Sup(prev, c) : c;
                lawSlots.Add(new SlotContribution
                {
                    slotType = w.slot, installed = !vv.native,
                    contribution = BodyLaw.Express(Wrap(h), Wrap(w), vv.native, m),
                });
            }
            float hpB = 0f, stamF = 0f, stamR = 0f, atk = 0f, mv = 0f, red = 0f, reg = 0f, ooc = 0f;
            foreach (var c in groups.Values)
            {
                hpB += c.hpBonus; stamF += c.stam; stamR += c.stamRegen; atk += c.atkCd; mv += c.mv;
                red += c.reduce; reg += c.regen; ooc += c.regenOOC;
            }
            float baseHp = chassis.baseHp > 0 ? chassis.baseHp : 75f;
            float baseSt = chassis.baseStamina > 0 ? chassis.baseStamina : 100f;
            float baseStR = chassis.baseStaminaRegen > 0f ? chassis.baseStaminaRegen : 12f;
            int oldHp = Mathf.Max(1, Mathf.RoundToInt(baseHp * (1f + hpB) * 1f * 1f));
            var st = BodyLaw.Compute(new BodyInput
            {
                slots = lawSlots, baseHp = baseHp, baseStamina = baseSt, baseStaminaRegen = baseStR,
                varianceHp = 1f, bossHp = 1f, maxDamageReduction = 0.6f, minAtkCooldown = 0.2f,
            });
            Assert.AreEqual(oldHp, health.Max, label + " HP тела против прежней формулы");
            Assert.AreEqual(st.hp, health.Max, label + " HP тела против закона");
            Assert.AreEqual(Mathf.Min(0.6f, Mathf.Clamp01(red)), health.DamageReduction, 0f, label + " броня (прежняя)");
            Assert.AreEqual(st.damageReduction, health.DamageReduction, 0f, label + " броня (закон)");
            Assert.AreEqual(reg, health.RegenPerSecond, 0f, label + " реген (прежний)");
            Assert.AreEqual(st.regen, health.RegenPerSecond, 0f, label + " реген (закон)");
            Assert.AreEqual(ooc, health.OutOfCombatRegen, 0f, label + " реген вне боя");
            Assert.AreEqual(st.regenOutOfCombat, health.OutOfCombatRegen, 0f, label + " реген вне боя (закон)");
            Assert.AreEqual(mv, sink.speed, 0f, label + " скорость (прежняя)");
            Assert.AreEqual(st.moveSpeed, sink.speed, 0f, label + " скорость (закон)");
            float oldSt = Mathf.Max(1f, R(baseSt * (1f + stamF)));
            Assert.AreEqual(oldSt, stamina.Max, 0f, label + " бак (прежний)");
            Assert.AreEqual(Mathf.Max(1f, st.staminaMax), stamina.Max, 0f, label + " бак (закон)");
            float oldStR = R(baseStR * (1f + stamR));
            Assert.AreEqual(oldStR, stamina.RegenPerSecond, 0f, label + " реген стамины (прежний)");
            Assert.AreEqual(st.staminaRegen, stamina.RegenPerSecond, 0f, label + " реген стамины (закон)");
            Assert.AreEqual(Mathf.Max(0.2f, atk), st.attackTempo, 0f, label + " темп (прежний)");
            checkedBodies++;
        }

        // Mono держит float-локаль в регистре как double и не округляет её, пока выражение не ушло в поле/массив; игра же
        // всегда кладёт результат в поле (`RegenPerSecond = …`, `SetMax(…)`). Эталон округляем так же: через массив.
        static float R(float x) { var a = new float[1]; a[0] = x; return a[0]; }

        static OrganValues Wrap(Organ o) => new()
        {
            hpBonus = o.hpBonus, staminaBonus = o.staminaBonus, staminaRegenBonus = o.staminaRegenBonus,
            atkCooldown = o.atkCooldown, moveSpeed = o.moveSpeed, dashSpeed = o.dashSpeed, dashCooldown = o.dashCooldown,
            damageReduction = o.damageReduction, regen = o.regen, regenOOC = o.regenOOC,
            dashDuration = o.dashDuration, thermalRange = o.thermalRange, hearingMult = o.hearingMult,
            enablesScent = o.enablesScent, coldBlooded = o.coldBlooded, camo = o.camo, enablesThermal = o.enablesThermal,
            thorns = o.thorns, venomResist = o.venomResist, bleedResist = o.bleedResist,
            keenHearing = o.keenHearing, insight = o.insight, digestion = o.digestion,
        };

        CreatureBody NewBody(SpeciesSO chassis, SpeciesSO[] donors, out GameObject go)
        {
            go = new GameObject("~паритет " + chassis.speciesName); made.Add(go);
            go.AddComponent<Health>();
            go.AddComponent<SpeedSink>();
            var body = go.AddComponent<CreatureBody>();
            body.Configure(chassis, donors);
            body.ExpandPool(1000);   // цена не должна отказывать в прививке: проверяется арифметика, не экономика
            return body;
        }

        // условия, в которых сверяем каждое собранное тело: авто-кривая родства (0, 50, 100) и фикс-экспрессия NPC (0.45, 2)
        static readonly (float expression, int affinity)[] Conditions = { (0f, 0), (0f, 50), (0f, 100), (0.45f, 0), (2f, 100) };

        void CheckAllConditions(CreatureBody body, GameObject go, SpeciesSO chassis, Dictionary<string, SpeciesSO> by,
                                IEnumerable<SpeciesSO> donors, string label)
        {
            foreach (var (expr, aff) in Conditions)
            {
                body.SetExpression(expr);
                foreach (var d in donors) body.SetAffinity(d.speciesName, aff);
                body.SetAffinity(chassis.speciesName, aff);
                body.Refeed();
                CheckBody(body, go, chassis, by, expr, aff, $"{label} Э{expr} род{aff}");
            }
        }

        [Test]
        public void Body_EverySpecies_NativeAndSingleGrafts_MatchOldFormulaAndLaw()
        {
            var all = LoadSpecies();
            var by = all.ToDictionary(s => s.speciesName);
            checkedBodies = 0;
            foreach (var c in all)
            {
                var pure = NewBody(c, new SpeciesSO[0], out var goP);
                CheckAllConditions(pure, goP, c, by, new SpeciesSO[0], c.speciesName + " родной");
                foreach (var d in all)
                {
                    if (d == c) continue;
                    var body = NewBody(c, new[] { d }, out var go);
                    for (int i = 0; i < body.SlotCount; i++)
                    {
                        var vars = body.GetVariants(i);
                        for (int v = 0; v < vars.Count; v++)
                        {
                            if (vars[v].native) continue;
                            Assert.IsTrue(body.Install(i, v), $"{c.speciesName}+{d.speciesName} слот {i} вариант {v} не встал");
                            CheckAllConditions(body, go, c, by, new[] { d }, $"{c.speciesName}+{d.speciesName} {vars[v].organName}");
                            body.Install(i, vars.FindIndex(x => x.native)); // вернуть родной
                        }
                    }
                }
            }
            Assert.Greater(checkedBodies, 500);
        }

        [Test]
        public void Body_ThreeFullChimeras_MatchOldFormulaAndLaw()
        {
            var all = LoadSpecies();
            var by = all.ToDictionary(s => s.speciesName);
            checkedBodies = 0;
            foreach (var name in new[] { "Человек", "Волк", "Лось" })
            {
                var c = by[name];
                var donors = all.Where(s => s != c).ToArray();
                var body = NewBody(c, donors, out var go);
                // каждый слот — на последний звериный вариант; две пары химерных слотов с сердцами (дубль типа → супремум)
                for (int i = 0; i < body.SlotCount; i++)
                {
                    var vars = body.GetVariants(i);
                    int last = vars.FindLastIndex(x => !x.native);
                    if (last >= 0) body.Install(i, last);
                }
                body.GrantChimeraSlot(); body.GrantChimeraSlot();
                for (int i = 0; i < body.SlotCount; i++)
                {
                    if (!body.GetSlot(i).chimera) continue;
                    var vars = body.GetVariants(i);
                    int idx = vars.FindIndex(x => x.affordable && x.slotType == BodySlots.Heart);
                    if (idx >= 0) body.Install(i, idx);
                }
                Assert.Greater(body.BeastSlots, 2, name + ": химера не собралась");
                CheckAllConditions(body, go, c, by, donors, name + " полная химера");
            }
            Assert.AreEqual(15, checkedBodies);
        }

        // ---- кривые родства: прежние формулы против закона по сетке ----
        [Test]
        public void AffinityLaw_MatchesOldCurves()
        {
            foreach (float expr in new[] { 0f, 0.45f, 0.5f, 2f })
            foreach (float start in new[] { 0f, 80f })
            foreach (float max in new[] { 2f, 3f })
                for (int aff = 0; aff <= 100; aff++)
                    Assert.AreEqual(OldMultiplier(expr, aff, start, 100f, max), AffinityLaw.Multiplier(expr, aff, start, 100f, max), 0f, $"э{expr} с{start} м{max} р{aff}");

            foreach (float per in new[] { 0.008f, 0.01f })
            foreach (int cost in new[] { 1, 2, 3, 4, 5, 6, 8, 10 })
            foreach (float chimMult in new[] { 1f, 2f })
                for (int aff = 0; aff <= 100; aff++)
                {
                    float disc = Mathf.Clamp(aff * per, 0f, 0.8f);
                    Assert.AreEqual(disc, AffinityLaw.Discount(aff, per, 0.8f), 0f);
                    int eff = Mathf.Max(1, Mathf.CeilToInt(cost * (1f - disc)));
                    Assert.AreEqual(eff, AffinityLaw.EffectiveCost(cost, disc));
                    Assert.AreEqual(Mathf.CeilToInt(eff * chimMult), AffinityLaw.SlotCost(eff, chimMult > 1f, chimMult));
                    Assert.AreEqual(eff, AffinityLaw.SlotCost(eff, false, chimMult));
                }
            Assert.AreEqual(7, AffinityLaw.Power(true, 7f, 3f));
            Assert.AreEqual(3, AffinityLaw.Power(false, 7f, 3f));
            Assert.AreEqual(25, AffinityLaw.Pool(20, 5));
        }


        // ---- С4: убийство и химеризация ----
        // эталон родства: прежний код CreditKiller (словарь, Mathf)
        static Dictionary<string, int> OldTally(string chassis, string[] organs, float per)
        {
            var tally = new Dictionary<string, int>();
            if (chassis != null) tally[chassis] = 1;
            foreach (var sp in organs) tally[sp] = (tally.TryGetValue(sp, out var c) ? c : 0) + 1;
            var gains = new Dictionary<string, int>();
            foreach (var kv in tally) gains[kv.Key] = Mathf.Max(1, Mathf.RoundToInt(kv.Value * per));
            return gains;
        }

        [Test]
        public void Tally_MatchesOldFormula()
        {
            var comps = new[]
            {
                new string[0], new[] { "Волк" }, new[] { "Волк", "Волк", "Волк" }, new[] { "Змея", "Волк", "Лось", "Змея" },
                new[] { "Человек", "Человек", "Человек", "Человек", "Человек", "Волк", "Ёж" },
                new[] { "Лось", "Лось", "Лось", "Лось", "Лось", "Лось", "Лось", "Лось", "Лось", "Лось", "Лось", "Лось" },
            };
            int n = 0;
            foreach (var organs in comps) foreach (string chassis in new[] { null, "Волк", "Человек" })
            foreach (float per in new[] { 0.1f, 0.25f, 0.5f, 0.55f, 1f, 1.5f, 2.5f })
            {
                var old = OldTally(chassis, organs, per);
                var gains = KillLaw.Tally(chassis, organs, per);
                Assert.AreEqual(old.Count, gains.Count, $"{chassis} {string.Join(",", organs)} {per}");
                foreach (var kv in gains) Assert.AreEqual(old[kv.Key], kv.Value, $"{kv.Key} · {chassis} {string.Join(",", organs)} {per}");
                n++;
            }
            Assert.Greater(n, 100);
        }

        [Test]
        public void Tally_Examples()
        {
            var g = KillLaw.Tally("Волк", new[] { "Волк", "Змея" }, 0.55f); // Волк: 2 × 0.55 = 1.1 → 1; Змея: 0.55 → 1 (минимум 1)
            Assert.AreEqual(2, g.Count);
            Assert.AreEqual("Волк", g[0].Key); Assert.AreEqual(1, g[0].Value);
            Assert.AreEqual("Змея", g[1].Key); Assert.AreEqual(1, g[1].Value);
            Assert.AreEqual(2, KillLaw.Tally("Лось", new[] { "Лось", "Лось", "Лось" }, 0.5f)[0].Value); // 4 × 0.5 = 2
            Assert.AreEqual(2, KillLaw.Tally("Лось", new[] { "Лось", "Лось" }, 0.5f)[0].Value);          // 3 × 0.5 = 1.5 → 2 (к чётному)
            Assert.AreEqual(0, KillLaw.Tally(null, new string[0], 1f).Count);
        }

        [Test]
        public void Meal_MatchesOldFormula()
        {
            foreach (bool eats in new[] { false, true }) foreach (bool whole in new[] { false, true })
            foreach (bool massive in new[] { false, true }) foreach (float meal in new[] { 0.3f, 0.7f, 1f })
            {
                // прежнее: хищнику Feed(whole ? 1 : meal); массивная туша — стае meal * 0.5
                var m = KillLaw.Meal(eats, whole, massive, meal);
                Assert.AreEqual(eats, m.Fed);
                Assert.AreEqual(eats ? (whole ? 1f : meal) : 0f, m.Killer, 0f);
                Assert.AreEqual(eats && massive ? meal * 0.5f : 0f, eats ? m.Pack : 0f, 0f);
            }
            Assert.AreEqual(0f, KillLaw.Meal(false, false, true, 0.7f).Pack, "не хищник — стаю не кормят");
        }

        // эталон химеризации: прежний TryChimerize на тех же вызовах случайности
        static int OldRoll(List<(string species, int aff)> loot, float mult, XorShiftRandom r)
        {
            if (loot.Count == 0) return -1;
            int pick = r.Range(0, loot.Count);
            float chance = (loot[pick].aff / 100f) * mult;
            if (r.Next01() > chance) return -1;
            return pick;
        }

        [Test]
        public void Roll_MatchesOldLogic_OnFixedSeeds()
        {
            var cases = new List<List<(string, int)>>
            {
                new(),                                                           // пустой лут
                new() { ("Волк", 0) },                                           // шанс 0
                new() { ("Волк", 100) },                                         // шанс 1
                new() { ("Волк", 50), ("Змея", 25), ("Лось", 100) },             // середина
                new() { ("Ёж", 1), ("Ёж", 100), ("Волк", 100), ("Змея", 0), ("Лось", 37) },
            };
            int hits = 0, misses = 0;
            foreach (uint seed in new uint[] { 1, 7, 42, 1234, 99999 })
            foreach (float mult in new[] { 0.5f, 1f, 2f })
            foreach (var c in cases)
            {
                var entries = c.ConvertAll(x => new LootEntry(x.Item1, "орган", x.Item2));
                var rOld = new XorShiftRandom(seed); var rNew = new XorShiftRandom(seed);
                for (int i = 0; i < 40; i++) // подряд на одном потоке: расход случайности тоже должен совпасть
                {
                    int old = OldRoll(c.ConvertAll(x => (x.Item1, x.Item2)), mult, rOld);
                    int now = ChimerizeLaw.Roll(entries, mult, rNew);
                    Assert.AreEqual(old, now, $"сид {seed} мульт {mult} лут {c.Count} шаг {i}");
                    if (now >= 0) hits++; else misses++;
                }
                Assert.AreEqual(rOld.NextUInt(), rNew.NextUInt(), "потоки разошлись — разный расход случайности");
            }
            Assert.Greater(hits, 100); Assert.Greater(misses, 100);
        }

        sealed class ScriptedRandom : ILawRandom
        {
            readonly float v; readonly int idx; public readonly List<string> Calls = new();
            public ScriptedRandom(int idx, float v) { this.idx = idx; this.v = v; }
            public float Next01() { Calls.Add("value"); return v; }
            public int Range(int min, int max) { Calls.Add($"range {min} {max}"); return idx; }
            public float Range(float min, float max) { Calls.Add($"frange {min} {max}"); return min; }
        }

        [Test]
        public void Roll_CallOrder_RangeThenValue_AndEdges()
        {
            var loot = new[] { new LootEntry("Волк", "Пасть", 50), new LootEntry("Змея", "Яд", 100) };
            var r = new ScriptedRandom(1, 0.99f);
            Assert.AreEqual(1, ChimerizeLaw.Roll(loot, 1f, r));                       // шанс 1: 0.99 ≤ 1 → надел
            CollectionAssert.AreEqual(new[] { "range 0 2", "value" }, r.Calls);

            Assert.AreEqual(-1, ChimerizeLaw.Roll(loot, 1f, new ScriptedRandom(0, 0.51f)));  // шанс 0.5 < 0.51
            Assert.AreEqual(0, ChimerizeLaw.Roll(loot, 1f, new ScriptedRandom(0, 0.5f)));    // ровно шанс — проходит (прежнее `>`)
            Assert.AreEqual(-1, ChimerizeLaw.Roll(new[] { new LootEntry("Волк", "Пасть", 0) }, 1f, new ScriptedRandom(0, 0.0001f)));
            Assert.AreEqual(0, ChimerizeLaw.Roll(new[] { new LootEntry("Волк", "Пасть", 0) }, 1f, new ScriptedRandom(0, 0f))); // 0 > 0 нет

            var empty = new ScriptedRandom(0, 0f);
            Assert.AreEqual(-1, ChimerizeLaw.Roll(new LootEntry[0], 1f, empty));
            Assert.IsEmpty(empty.Calls, "пустой лут случайность не тратит");
        }

        [Test]
        public void Fits_MatchesOldPoolCheck()
        {
            int n = 0;
            for (int used = 0; used <= 12; used++) for (int slot = 0; slot <= 4; slot++)
            for (int variant = 0; variant <= 6; variant++) foreach (int pool in new[] { 0, 3, 8, 10, 12 })
            foreach (bool worn in new[] { false, true })
            {
                bool old = used - slot + variant <= pool && !worn; // CanInstall && !WornElsewhere
                Assert.AreEqual(old, ChimerizeLaw.Fits(used, slot, variant, pool, worn));
                n++;
            }
            Assert.Greater(n, 1000);
        }
        // ---- С5: статусы, сытость, реген, скорость, разброс. Эталоны — прежний код компонентов дословно (на Mathf) ----
        // Яд: Venom.cs до выноса. Часы — абсолютные, как Time.time.
        sealed class OldVenom
        {
            int stacks; float expireAt, nextDot;
            public int Stacks(float now) => now < expireAt ? stacks : 0;
            public float Incoming(float now) => Stacks(now) >= 2 ? 1.4f : 1f;
            public void Add(float now, float durMult) { stacks = Mathf.Min(3, Stacks(now) + 1); expireAt = now + 4f * durMult; }
            public int Tick(float now)
            {
                int s = Stacks(now);
                if (s <= 0) return 0;
                if (s >= 3 && now >= nextDot) { nextDot = now + 0.6f; return 3; }
                return 0;
            }
        }

        // Кровь: Bleed.cs до выноса.
        sealed class OldBleed
        {
            int stacks; float expireAt, nextTick;
            public int Stacks(float now) => now < expireAt ? stacks : 0;
            public void Add(float now, float durMult) { stacks = Mathf.Min(8, Stacks(now) + 1); expireAt = now + 3f * durMult; }
            public int Tick(float now, int max)
            {
                if (Stacks(now) < 5) return 0;
                if (now >= nextTick) { nextTick = now + 0.6f; return Mathf.Max(1, Mathf.RoundToInt(max * 0.04f)); }
                return 0;
            }
        }

        [Test]
        public void Venom_StepsMatchOldComponent()
        {
            foreach (float durMult in new[] { 1f, 0.15f })
            foreach (int pattern in new[] { 1, 2, 3, 5 })
            {
                var old = new OldVenom(); var s = new StackState(); var p = VenomParams.Default;
                int dmgOld = 0, dmgNew = 0;
                for (int i = 0; i < 600; i++)
                {
                    float now = i * 0.05f;
                    if (i % (pattern * 7 + 1) == 0) { old.Add(now, durMult); StatusLaw.AddStack(ref s, now, p.MaxStacks, p.StackDuration, durMult); }
                    Assert.AreEqual(old.Stacks(now), StatusLaw.Stacks(s, now), $"стаки t={now}");
                    Assert.AreEqual(old.Incoming(now), StatusLaw.VenomIncoming(StatusLaw.Stacks(s, now), p), $"уязвимость t={now}");
                    int a = old.Tick(now), b = StatusLaw.VenomStep(ref s, now, p);
                    Assert.AreEqual(a, b, $"тик t={now}");
                    dmgOld += a; dmgNew += b;
                }
                Assert.AreEqual(dmgOld, dmgNew);
                if (durMult == 1f && pattern == 1) Assert.Greater(dmgNew, 0, "тиков не было — проверять нечего");
            }
        }

        [Test]
        public void Bleed_StepsMatchOldComponent()
        {
            foreach (float durMult in new[] { 1f, 0.2f })
            foreach (int max in new[] { 30, 100, 333 })
            foreach (int pattern in new[] { 1, 2, 4 })
            {
                var old = new OldBleed(); var s = new StackState(); var p = BleedParams.Default;
                int total = 0;
                for (int i = 0; i < 600; i++)
                {
                    float now = i * 0.05f;
                    if (i % (pattern * 3 + 1) == 0) { old.Add(now, durMult); StatusLaw.AddStack(ref s, now, p.MaxStacks, p.StackDuration, durMult); }
                    Assert.AreEqual(old.Stacks(now), StatusLaw.Stacks(s, now));
                    int a = old.Tick(now, max), b = StatusLaw.BleedStep(ref s, now, p, max);
                    Assert.AreEqual(a, b, $"тик t={now} max {max}");
                    total += b;
                }
                if (durMult == 1f && pattern == 1) Assert.Greater(total, 0);
            }
        }

        [Test]
        public void SlowThornsRage_MatchOldFormulas()
        {
            var sp = SlowParams.Default;
            for (int st = 0; st <= 6; st++)
                Assert.AreEqual(1f - Mathf.Min(0.7f, st * 0.14f), StatusLaw.SlowMult(st, sp), $"стаков {st}");
            foreach (int raw in Ints) foreach (float share in new[] { 0.35f, 0.55f, 1f })
                Assert.AreEqual(Mathf.Max(1, Mathf.RoundToInt(raw * share)), StatusLaw.ThornsReturn(raw, share, 1), $"raw {raw} доля {share}");
            var rp = RageParams.Default;
            foreach (bool on in new[] { false, true }) foreach (float boost in new[] { 1f, 1.7f })
            {
                Assert.AreEqual(on ? 1.25f * boost : 1f, StatusLaw.RageDamage(rp, on, boost));
                Assert.AreEqual(on ? 1.15f : 1f, StatusLaw.RageSpeed(rp, on));
                Assert.AreEqual(on ? 1.5f : 1f, StatusLaw.RageIncoming(rp, on));
                Assert.AreEqual(on ? 1.5f : 1f, StatusLaw.RageStamina(rp, on));
            }
        }

        [Test]
        public void Hunger_MatchesOldSatiety()
        {
            var p = HungerParams.Default;
            Assert.AreEqual(1f / (3f * 60f), HungerLaw.DefaultDecayPerSec(p));
            foreach (float h in new[] { 0f, 0.3f, 0.54f, 0.55f, 0.6f, 0.69f, 0.7f, 0.8f, 0.84f, 0.85f, 1f })
            {
                float min = h >= 0.85f ? 3f : h >= 0.70f ? 2f : h >= 0.55f ? 1.5f : 0.5f;
                Assert.AreEqual(1f / (Mathf.Max(0.1f, min) * 60f), HungerLaw.DecayPerSec(h, p), $"однородность {h}");
            }
            foreach (float f in new[] { 0f, 0.07f, 0.08f, 0.09f, 0.25f, 0.26f, 0.49f, 0.5f, 0.9f, 1f })
            {
                Assert.AreEqual(f >= 0.5f, HungerLaw.IsSated(f, p));
                Assert.AreEqual(f <= 0.25f, HungerLaw.IsHungry(f, p));
                Assert.AreEqual(f <= 0.08f, HungerLaw.IsStarving(f, p));
                Assert.AreEqual(f <= 0.08f ? 0.65f : 1f, HungerLaw.Vigor(f, p));
                Assert.AreEqual(Mathf.Clamp01(f + Mathf.Max(0f, 0.3f)), HungerLaw.Feed(f, 0.3f));
                Assert.AreEqual(Mathf.Clamp01(f + Mathf.Max(0f, -1f)), HungerLaw.Feed(f, -1f));
                Assert.AreEqual(Mathf.Max(0f, f - 0.004f * 0.02f), HungerLaw.Decay(f, 0.004f, 0.02f));
            }
        }

        [Test]
        public void RegenTick_MatchesOldAccumulation()
        {
            // прежнее накопление Health.Update (Floor) и Satiety.Update ((int)) на одном потоке dt и скоростей
            foreach (float rate in new[] { -1f, 0f, 0.3f, 4f, 4.7f, 9f, 14f })
            {
                float oldAcc = 0f, newAcc = 0f; int oldSum = 0, newSum = 0;
                for (int i = 0; i < 500; i++)
                {
                    float dt = 0.01f + (i % 5) * 0.004f;
                    if (rate > 0f)
                    {
                        oldAcc += rate * dt;
                        if (oldAcc >= 1f) { int w = Mathf.FloorToInt(oldAcc); oldAcc -= w; oldSum += w; }
                    }
                    newSum += HungerLaw.RegenTick(ref newAcc, rate, dt);
                    Assert.AreEqual(oldAcc, newAcc, $"остаток, скорость {rate}, шаг {i}");
                }
                Assert.AreEqual(oldSum, newSum, $"скорость {rate}");
            }
            foreach (bool combat in new[] { false, true }) foreach (float factor in new[] { 0f, 0.5f, 1f })
            {
                float old = 3f; if (!combat) old += 2f; old *= factor;
                Assert.AreEqual(old, HungerLaw.RegenRate(3f, 2f, combat, factor));
            }
        }

        [Test]
        public void MoveSpeed_MatchesOldFiveCopies()
        {
            int n = 0;
            foreach (float b in new[] { 3f, 4f, 5.5f, 7.2f })
            foreach (float rage in new[] { 1f, 1.15f }) foreach (float var in new[] { 0.9f, 1f, 1.1f })
            foreach (float breath in new[] { 1f, 0.4f })
            {
                // волк
                Assert.AreEqual(b * rage * var, MoveLaw.Speed(b, rage, var));
                // лось, ёж: отдышка
                Assert.AreEqual(b * rage * var * breath, MoveLaw.Speed(b, rage, var, breath));
                // змея и альфа: ярость в скорость НЕ входит (прежнее поведение, паритет)
                Assert.AreEqual(b * var, MoveLaw.Speed(b, rage, var, rageApplies: false));
                n++;
            }
            Assert.Greater(n, 20);
            float bs = 5f, rg = 1.15f, vr = 1f, sl = 0.7f, vg = 0.65f; // переменные, а не константы: иначе компилятор свернёт произведение в другой точности
            Assert.AreEqual(bs * rg * vr * sl * vg, MoveLaw.Speed(bs, rg, vr, sl, vg));
        }

        // ---- рацион (С8): прежние предикаты «кто кого» — по типу психики цели; психика = доминанта, поэтому по имени доминанты ----
        static readonly string[] DietSpecies = { "Человек", "Волк", "Змея", "Лось", "Ёж" };

        /// <summary>Эталон: что до С8 делала каждая психика с целью, у которой психика вида `target` (null — истинная химера, психики вида нет).</summary>
        static bool OldWantsPrey(string predator, string target, bool hungry) => predator switch
        {
            "Волк" => target == "Змея" || (target == "Ёж" && hungry),   // WolfPsyche.RetargetPrey: змея всегда, ёж только голодным
            "Ёж" => target == "Змея",                                   // HedgehogPsyche.Retarget
            "Лось" => target == "Волк",                                 // MoosePsyche.PickThreat
            _ => false,
        };
        static bool OldWantsShadow(string predator, string target) => predator == "Волк" && target == "Лось";   // WolfPsyche.TryMooseHunt
        static bool OldFlees(string predator, string target) => predator == "Змея" && target == "Ёж";           // SnakePsyche.CheckFlee

        static SpeciesSO LoadSpecies(string n) => UnityEditor.AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{n}.asset");

        [Test]
        public void Diet_MatchesOldPredicates_OnPredatorTargetHungerTable()
        {
            int n = 0;
            var targets = DietSpecies.Concat(new string[] { null });
            foreach (var p in DietSpecies)
            {
                var so = LoadSpecies(p);
                Assert.IsNotNull(so, "нет ассета " + p);
                foreach (var t in targets) foreach (bool hungry in new[] { false, true })
                {
                    Assert.AreEqual(OldWantsPrey(p, t, hungry), DietLaw.Wants(so.diet, t, hungry, DietMode.Prey) > 0f, $"{p} → {t ?? "химера"} голод {hungry}: добыча");
                    Assert.AreEqual(OldWantsShadow(p, t), DietLaw.Wants(so.diet, t, hungry, DietMode.Shadow) > 0f, $"{p} → {t ?? "химера"} голод {hungry}: тень-загон");
                    Assert.AreEqual(OldFlees(p, t), DietLaw.Flees(so.fleesFrom, t), $"{p} → {t ?? "химера"}: бегство");
                    n++;
                }
            }
            Assert.AreEqual(5 * 6 * 2, n);
        }

        [Test]
        public void Diet_ThroughBodies_MatchesOldPredicates()
        {
            // то же через тела: рацион читается по ДОМИНАНТЕ (`MostKin`), а не по шасси или типу психики
            var made = new List<GameObject>();
            try
            {
                var bodies = new Dictionary<string, CreatureBody>();
                foreach (var name in DietSpecies.Where(s => s != "Человек"))
                {
                    var go = new GameObject("~рацион " + name); made.Add(go);
                    var b = go.AddComponent<CreatureBody>();
                    b.Configure(LoadSpecies(name), new SpeciesSO[0]);
                    bodies[name] = b;
                }
                foreach (var p in bodies.Keys) foreach (var t in bodies.Keys) foreach (bool hungry in new[] { false, true })
                {
                    Assert.AreEqual(OldWantsPrey(p, t, hungry), bodies[p].Appetite(bodies[t], hungry, DietMode.Prey) > 0f, $"{p} → {t} голод {hungry}");
                    Assert.AreEqual(OldWantsShadow(p, t), bodies[p].Appetite(bodies[t], hungry, DietMode.Shadow) > 0f, $"{p} → {t} тень");
                    Assert.AreEqual(OldFlees(p, t), bodies[p].Fears(bodies[t]), $"{p} → {t} бегство");
                }
                Assert.AreEqual(0f, bodies["Волк"].Appetite(bodies["Волк"], true, DietMode.Prey), "себя не ест");
                Assert.AreEqual(0f, bodies["Волк"].Appetite(null, true, DietMode.Prey));
            }
            finally { foreach (var go in made) Object.DestroyImmediate(go); }
        }

        [Test]
        public void DietLaw_Synthetic_PicksBestMatchingEntry()
        {
            var diet = new[]
            {
                new DietEntry("А", DietCondition.Always, DietMode.Prey, 1f),
                new DietEntry("Б", DietCondition.WhenHungry, DietMode.Prey, 2f),
                new DietEntry("Б", DietCondition.Always, DietMode.Shadow, 0.5f),
                new DietEntry("В", DietCondition.Always, DietMode.Prey, 0f),
            };
            Assert.AreEqual(1f, DietLaw.Wants(diet, "А", false, DietMode.Prey));
            Assert.AreEqual(0f, DietLaw.Wants(diet, "Б", false, DietMode.Prey), "голодному — а он сыт");
            Assert.AreEqual(2f, DietLaw.Wants(diet, "Б", true, DietMode.Prey));
            Assert.AreEqual(0.5f, DietLaw.Wants(diet, "Б", false, DietMode.Shadow));
            Assert.AreEqual(0.5f, DietLaw.Wants(diet, "Б", false), "без режима: наибольший из подходящих");
            Assert.AreEqual(2f, DietLaw.Wants(diet, "Б", true));
            Assert.AreEqual(0f, DietLaw.Wants(diet, "В", true, DietMode.Prey), "вес ноль — не хочет");
            Assert.AreEqual(0f, DietLaw.Wants(diet, null, true, DietMode.Prey), "истинная химера — ничья добыча");
            Assert.AreEqual(0f, DietLaw.Wants(null, "А", true, DietMode.Prey), "рациона нет");
            Assert.IsTrue(DietLaw.Flees(new[] { "Ёж" }, "Ёж"));
            Assert.IsFalse(DietLaw.Flees(new[] { "Ёж" }, null));
            Assert.IsFalse(DietLaw.Flees(null, "Ёж"));
        }

        [Test]
        public void VarianceRoll_MatchesOldSpawnVariance_OnUnityStream()
        {
            for (int seed = 1; seed <= 25; seed++)
            {
                Random.InitState(seed);
                float d = 1f + Random.Range(-0.15f, 0.15f);   // прежний порядок: урон, скорость, HP
                float s = 1f + Random.Range(-0.1f, 0.1f);
                float h = 1f + Random.Range(-0.15f, 0.15f);
                float nextOld = Random.value;
                Random.InitState(seed);
                var v = VarianceLaw.Roll(0.15f, 0.15f, 0.1f, UnityLawRandom.Instance);
                Assert.AreEqual(d, v.Damage); Assert.AreEqual(s, v.Speed); Assert.AreEqual(h, v.Hp);
                Assert.AreEqual(nextOld, Random.value, "расход случайности разошёлся");
            }
            Random.InitState(System.Environment.TickCount); // не оставлять соседние тесты на детерминированном потоке
            // нулевой разброс — средняя особь
            var avg = VarianceLaw.Roll(0f, 0f, 0f, new XorShiftRandom(7));
            Assert.AreEqual(1f, avg.Hp); Assert.AreEqual(1f, avg.Damage); Assert.AreEqual(1f, avg.Speed);
        }

        [Test]
        public void LiveComponents_ReadLawsConfig_AndMatchOldNumbers()
        {
            var go = new GameObject("~статусы"); made.Add(go);
            var hp = go.AddComponent<Health>();
            var slow = go.AddComponent<Slow>();
            Assert.AreEqual(1f, slow.MoveMult);
            slow.AddStack(); slow.AddStack();
            Assert.AreEqual(1f - 2 * 0.14f, slow.MoveMult, 1e-6f);
            var rage = go.AddComponent<Rage>();
            rage.Enrage(5f);
            Assert.AreEqual(1.15f, rage.SpeedMult); Assert.AreEqual(1.25f, rage.DamageMult); Assert.AreEqual(1.5f, rage.IncomingMult);
            var sat = go.AddComponent<Satiety>();
            Assert.AreEqual(0.5f, sat.SatedAt); Assert.AreEqual(0.25f, sat.HungryAt); Assert.AreEqual(0.08f, sat.StarveAt);

            // подмена конфига доходит до компонентов; возврат — в TearDown (статика переживает тест)
            var cfg = ScriptableObject.CreateInstance<LawsConfig>(); madeSo.Add(cfg);
            cfg.rage.SpeedMult = 2f;
            LawsConfig.Override(cfg);
            Assert.AreEqual(2f, rage.SpeedMult);
            LawsConfig.ResetCurrent();
            Assert.AreEqual(1.15f, rage.SpeedMult);
            Assert.IsNotNull(hp);
        }

        // ---- С7: темп приёма и логово в игровых часах ----

        [Test]
        public void Tempo_Period_AndTimers_MatchOldInlineFormulas()
        {
            int n = 0;
            foreach (float windup in new[] { 0.2f, 0.45f, 0.8f, 1.2f })
            foreach (float cooldown in new[] { 0.7f, 1.4f, 2.5f, 4f })
            foreach (float rhythm in new[] { 0f, 1.1f, 2.2f, 3.5f, 5f })
            {
                // прежнее: перезарядка идёт с запуска, ритм психики — отдельный таймер; период = больший из двух + замах
                float old = Mathf.Max(cooldown, rhythm) + windup;
                Assert.AreEqual(R(old), R(TempoLaw.Period(new TempoRecord { windup = windup, cooldown = cooldown }, rhythm)), 0f);
                foreach (float now in new[] { 0f, 3.7f, 120.25f })
                {
                    Assert.AreEqual(R(now + cooldown), R(TempoLaw.ReadyAt(now, cooldown)), 0f);
                    Assert.AreEqual(R(now + windup), R(TempoLaw.WindupEnd(now, windup)), 0f);
                    float readyAt = now + cooldown;
                    foreach (float shift in new[] { -1f, 0f, 0.1f, 5f })
                        Assert.AreEqual(R(Mathf.Min(R(readyAt + shift), R(now + cooldown * 0.25f))),
                            R(TempoLaw.ReadyAfterCancel(R(readyAt + shift), now, cooldown, TempoLaw.CancelledCooldownShare)), 0f);
                }
                n++;
            }
            Assert.Greater(n, 70);
            Assert.AreEqual(0.25f, TempoLaw.CancelledCooldownShare);
        }

        [Test]
        public void Lair_GameHours_ConversionKeepsTheRuntimeTempo()
        {
            var go = new GameObject("~логово"); made.Add(go);
            var site = go.AddComponent<LairSite>();
            site.respawnSeconds = 30f; site.fearDecayPerSecond = 0.02f;
            float hps = WorldClock.HoursPerRealSecond(WorldClock.DefaultDayMinutes);   // сутки 12 мин → 1/30 ч в секунду
            Assert.AreEqual(R(30f * hps), site.RespawnGameHours, 0f);
            Assert.AreEqual(1f, site.RespawnGameHours, 1e-5f, "30 с мира при сутках 12 мин — ровно игровой час");
            Assert.AreEqual(R(0.02f / hps), site.FearDecayPerGameHour, 0f);
            // обратный перевод возвращает секунды: единица другая, темп тот же
            Assert.AreEqual(30f, LairLaw.RespawnSeconds(site.RespawnGameHours, hps), 1e-4f);
            Assert.AreEqual(0.02f, site.FearDecayPerGameHour * hps, 1e-6f);
            // сутки другой длины: в часах число меняется, в секундах — нет
            Assert.AreEqual(30f * (24f / (6f * 60f)), LairLaw.RespawnHours(30f, WorldClock.HoursPerRealSecond(6f)), 1e-5f);
            Assert.AreEqual(0f, LairLaw.FearDecayPerHour(0.02f, 0f));
        }

        [Test]
        public void AbilityResolve_UsesBodyLawExpression_OnEveryRecord()
        {
            int n = 0;
            foreach (var sp in LoadSpecies())
                foreach (var organ in sp.organs)
                    if (organ.abilities != null)
                        foreach (var rec in organ.abilities)
                            foreach (float power in new[] { 0.45f, 0.5f, 1f, 1.37f, 2f })
                                foreach (bool native in new[] { true, false })
                                {
                                    var resolved = rec.Resolve(null, native, power);
                                    foreach (var f in rec.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
                                    {
                                        if (!f.IsDefined(typeof(ExpressedAttribute), false)) continue;
                                        if (f.FieldType == typeof(int))
                                            Assert.AreEqual(BodyLaw.ExpressInt((int)f.GetValue(rec), 0f, native, power), (int)f.GetValue(resolved), $"{sp.speciesName} {rec.GetType().Name}.{f.Name}");
                                        else
                                            Assert.AreEqual(R(BodyLaw.ExpressNumber((float)f.GetValue(rec), 0f, native, power)), (float)f.GetValue(resolved), 0f, $"{sp.speciesName} {rec.GetType().Name}.{f.Name}");
                                        n++;
                                    }
                                }
            Assert.Greater(n, 100);
            // прежняя арифметика дословно
            float own = 10f, from = 4f, pw = 0.45f;
            Assert.AreEqual(R(own * pw), R(BodyLaw.ExpressNumber(own, from, true, pw)), 0f);
            Assert.AreEqual(R(from + (own - from) * pw), R(BodyLaw.ExpressNumber(own, from, false, pw)), 0f);
            Assert.AreEqual(Mathf.RoundToInt(own * pw), BodyLaw.ExpressInt(own, from, true, pw));
        }

        // ---- живой Health ----
        readonly List<GameObject> made = new();
        [TearDown]
        public void TearDown()
        {
            foreach (var g in made) if (g != null) Object.DestroyImmediate(g);
            made.Clear();
            foreach (var o in madeSo) if (o != null) Object.DestroyImmediate(o);
            madeSo.Clear();
            LawsConfig.ResetCurrent(); // статика конфига переживает тест — вернуть ленивую загрузку
        }

        [Test]
        public void LiveHealth_TakeDamage_MatchesOldFormula()
        {
            foreach (float reduction in new[] { 0f, 0.2f, 0.35f })
            foreach (int raw in new[] { 1, 3, 5, 7, 10, 13, 17 })
            {
                var go = new GameObject("~здоровье"); made.Add(go);
                var hp = go.AddComponent<Health>();
                hp.DamageReduction = reduction;
                int before = hp.Current == 0 ? hp.Max : hp.Current;
                hp.TakeDamage(raw);
                Assert.AreEqual(before - OldIncoming(raw, reduction, 1f), hp.Current, $"броня {reduction} удар {raw}");
            }
        }

        [Test]
        public void LiveHealth_WithRage_MatchesOldFormula()
        {
            var go = new GameObject("~здоровье"); made.Add(go);
            var hp = go.AddComponent<Health>();
            var rage = go.AddComponent<Rage>();
            rage.Enrage(5f);
            Assert.Greater(rage.IncomingMult, 1f, "ярость не включилась — проверять нечего");
            hp.DamageReduction = 0.2f;
            int before = hp.Current == 0 ? hp.Max : hp.Current;
            hp.TakeDamage(9);
            Assert.AreEqual(before - OldIncoming(9, 0.2f, rage.IncomingMult), hp.Current);
        }
    }
}
