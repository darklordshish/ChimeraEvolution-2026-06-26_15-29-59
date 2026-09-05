using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    public class HitEffectsTests
    {
        GameObject srcGo;
        GameObject tgtGo;
        Health srcH;
        Health tgtH;

        [SetUp]
        public void SetUp()
        {
            srcGo = new GameObject("HitSrc");
            tgtGo = new GameObject("HitTgt");
            srcH = srcGo.AddComponent<Health>();
            tgtH = tgtGo.AddComponent<Health>();
            // ставим предсказуемый макс
            srcH.SetMaxHealth(100);
            tgtH.SetMaxHealth(100);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(srcGo);
            Object.DestroyImmediate(tgtGo);
        }

        [Test]
        public void Hit_Apply_AllNineKinds_DoesNotThrow()
        {
            var hit = new Hit(srcH, Vector3.zero);
            var effects = new HitEffect[]
            {
                HitEffect.Damage(5),
                HitEffect.LifeSteal(3),
                HitEffect.Knockback(2f),
                HitEffect.RegenDebuff(0.5f, 1f),
                HitEffect.Stun(0.3f),
                HitEffect.Venom(),
                HitEffect.Rage(1f),
                HitEffect.Bleed(),
                HitEffect.Slow(),
            };
            Assert.AreEqual(9, effects.Length, "Должно быть 9 EffectKind");
            foreach (var e in effects)
                Assert.DoesNotThrow(() => hit.Apply(tgtH, e), $"Hit.Apply не должен бросать для {e.Kind}");
        }

        [Test]
        public void Hit_Apply_NullTarget_DoesNotThrow()
        {
            var hit = new Hit(srcH, Vector3.zero);
            Assert.DoesNotThrow(() => hit.Apply(null, HitEffect.Damage(10)));
        }

        [Test]
        public void Health_TakeDamage_WithRage_MultiplierApplied()
        {
            tgtH.SetMaxHealth(100);
            Assert.AreEqual(100, tgtH.Current);
            var rage = tgtGo.AddComponent<Rage>();
            rage.Enrage(5f);
            // Rage IncomingMult = 1.5 пока Enraged
            Assert.AreEqual(1.5f, rage.IncomingMult, 1e-4f);
            tgtH.TakeDamage(10);
            // 10 * 1.5 =15
            Assert.AreEqual(85, tgtH.Current, "С яростью урон должен быть 15");
        }

        [Test]
        public void Health_TakeDamage_WithVenom_MultiplierApplied()
        {
            tgtH.SetMaxHealth(100);
            var venom = tgtGo.AddComponent<Venom>();
            venom.AddStack();
            venom.AddStack(); // 2 стака => vulnerability 1.4
            Assert.AreEqual(1.4f, venom.IncomingMult, 1e-4f);
            tgtH.TakeDamage(10);
            // 10 *1.4=14
            Assert.AreEqual(86, tgtH.Current, "С ядом (2 стака) урон должен быть 14");
        }

        [Test]
        public void Health_TakeDamage_WithRageAndVenom_Combined()
        {
            tgtH.SetMaxHealth(100);
            var rage = tgtGo.AddComponent<Rage>();
            rage.Enrage(5f);
            var venom = tgtGo.AddComponent<Venom>();
            venom.AddStack(); venom.AddStack();
            // combined 1.5 *1.4 =2.1 => 10*2.1=21
            tgtH.TakeDamage(10);
            Assert.AreEqual(79, tgtH.Current, "Комбо ярость+яд должно давать 21 урона");
        }

        [Test]
        public void Health_TakeDamage_BaselineWithoutMultipliers()
        {
            tgtH.SetMaxHealth(100);
            tgtH.TakeDamage(10);
            Assert.AreEqual(90, tgtH.Current);
        }
    }
}
