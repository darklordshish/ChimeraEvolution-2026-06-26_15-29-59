using System.Collections.Generic;
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

        // ---- живой Health ----
        readonly List<GameObject> made = new();
        [TearDown] public void TearDown() { foreach (var g in made) if (g != null) Object.DestroyImmediate(g); made.Clear(); }

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
