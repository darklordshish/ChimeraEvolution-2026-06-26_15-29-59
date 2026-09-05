using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// Venom/Bleed/Slow/Stagger + Resist, Betrayal.Erosion 2.5с
    /// (Combat/Statuses/*)
    /// </summary>
    public class StatusStackTests
    {
        GameObject go;
        Health health;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("StatusStack");
            health = go.AddComponent<Health>();
            health.SetMaxHealth(100);
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator Venom_Stacks_Vulnerability_AndResist()
        {
            var venom = go.AddComponent<Venom>();
            yield return null;

            // 1 стак — реген стоп, но без уязвимости
            venom.AddStack();
            Assert.AreEqual(1, venom.Stacks);
            Assert.AreEqual(1f, venom.IncomingMult, 1e-4f, "1 стак: уязвимости нет (порог 2)");

            venom.AddStack();
            Assert.AreEqual(2, venom.Stacks);
            Assert.AreEqual(1.4f, venom.IncomingMult, 1e-4f, "2 стака: входящий урон ×1.4 (Venom.cs:13)");

            // Health получает множитель
            int before = health.Current;
            health.TakeDamage(10);
            // 10*1.4=14
            Assert.AreEqual(before - 14, health.Current);

            // Resist режет жизнь стака до 0.15 => не накапливается
            var resist = go.AddComponent<VenomResist>();
            // длительность стаков с резистом 0.15×4с=0.6с; подождем 0.7с и проверим что стаки спали, но без резиста держались бы 4с
            venom.AddStack(); // третий (кап 3)
            Assert.AreEqual(3, venom.Stacks);
            // снять резист не получится — проверим что DurationMult =0.15
            Assert.AreEqual(0.15f, resist.DurationMult, 0.01f);

            // Ещё один go без резиста для сравнения
            var go2 = new GameObject("VenomNoResist");
            var h2 = go2.AddComponent<Health>(); h2.SetMaxHealth(100);
            var v2 = go2.AddComponent<Venom>();
            v2.AddStack(); v2.AddStack();
            yield return new WaitForSeconds(0.7f);
            // с резистом стаки уже протухли
            Assert.AreEqual(0, venom.Stacks, "с VenomResist стаки должны протухнуть за 0.6с");
            // без резиста ещё живы
            Assert.AreEqual(2, v2.Stacks, "без резиста стаки живут 4с");

            Object.Destroy(go2);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Bleed_Threshold_PercentDmg_AndResist()
        {
            var bleed = go.AddComponent<Bleed>();
            yield return null;
            // Threshold 5 (Bleed.cs:12)
            for (int i=0;i<4;i++) bleed.AddStack();
            Assert.AreEqual(4, bleed.Stacks);
            // ниже порога — Update не бьёт (проверим через отсутствие урона за тик)
            int before = health.Current;
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(before, health.Current, "ниже порога кровопотери нет");

            bleed.AddStack(); // 5 => порог достигнут
            Assert.AreEqual(5, bleed.Stacks);
            // дождаться тика (0.6с)
            yield return new WaitForSeconds(0.7f);
            Assert.Less(health.Current, before, "за порогом должна быть кровопотеря % от Max (Bleed.cs:46)");

            // Resist 0.3 (BleedResist.cs:15)
            var resist = go.AddComponent<BleedResist>();
            Assert.AreEqual(0.3f, resist.DurationMult, 0.01f);
            // новый объект: с резистом стаки живут 0.9с, без — 3с
            var go2 = new GameObject("BleedResistCheck");
            var b2 = go2.AddComponent<Bleed>();
            go2.AddComponent<Health>().SetMaxHealth(100);
            b2.AddStack(); b2.AddStack(); b2.AddStack(); b2.AddStack(); b2.AddStack();
            var br2 = go2.AddComponent<BleedResist>();
            // добавим свежую пачку с резистом
            var go3 = new GameObject("BleedWithResist");
            go3.AddComponent<Health>().SetMaxHealth(100);
            var b3 = go3.AddComponent<Bleed>();
            go3.AddComponent<BleedResist>();
            b3.AddStack(); b3.AddStack(); b3.AddStack(); b3.AddStack(); b3.AddStack();
            yield return new WaitForSeconds(1.1f);
            Assert.AreEqual(0, b3.Stacks, "с BleedResist стаки протухают за ~0.9с");
            // без резиста ещё жив (создадим контроль без резиста)
            var go4 = new GameObject("BleedNoResist2");
            go4.AddComponent<Health>().SetMaxHealth(100);
            var b4 = go4.AddComponent<Bleed>();
            b4.AddStack(); b4.AddStack(); b4.AddStack(); b4.AddStack(); b4.AddStack();
            yield return new WaitForSeconds(1.1f);
            // уже 1.1с прошло — без резиста 3с => ещё жив
            Assert.AreEqual(5, b4.Stacks, "без резиста стаки живут 3с");

            Object.Destroy(go2); Object.Destroy(go3); Object.Destroy(go4);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Slow_Stacks_MoveMult_Cap()
        {
            var slow = go.AddComponent<Slow>();
            yield return null;

            Assert.AreEqual(1f, slow.MoveMult, 1e-4f, "без стаков =1");
            slow.AddStack();
            // perStack 0.14 (Slow.cs:17)
            Assert.AreEqual(1f - 0.14f, slow.MoveMult, 0.001f);
            slow.AddStack(); slow.AddStack();
            Assert.AreEqual(1f - 0.42f, slow.MoveMult, 0.001f);
            // кап 0.7
            for (int i=0;i<10;i++) slow.AddStack();
            Assert.AreEqual(1f - 0.7f, slow.MoveMult, 0.001f, "потолок 0.7 (Slow.cs:18)");
            // истечение 2.5с
            yield return new WaitForSeconds(2.6f);
            Assert.AreEqual(0, slow.Stacks, "после 2.5с стаки истекли");
            Assert.AreEqual(1f, slow.MoveMult, 1e-4f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Stagger_Hitstun_And_Stun()
        {
            var stagger = go.AddComponent<Stagger>();
            yield return null;
            // Awake подписался на onDamaged -> Hitstun 0.35с
            Assert.IsFalse(stagger.IsStaggered);
            Assert.IsFalse(stagger.IsStunned);
            health.TakeDamage(5);
            Assert.IsTrue(stagger.IsStaggered, "после попадания должен быть Hitstun (Stagger.cs:25)");
            Assert.IsFalse(stagger.IsStunned);

            // прямой стан
            stagger.Stun(1.2f);
            Assert.IsTrue(stagger.IsStunned);
            Assert.IsTrue(stagger.IsStaggered, "стан включает стаггер (Stagger.cs:16)");

            yield return new WaitForSeconds(0.5f);
            // Hitstun (0.35) истёк, но стан (1.2) ещё держит
            Assert.IsTrue(stagger.IsStunned);
            Assert.IsTrue(stagger.IsStaggered);
            yield return new WaitForSeconds(0.8f);
            Assert.IsFalse(stagger.IsStunned, "стан 1.2с истёк");
            Assert.IsFalse(stagger.IsStaggered);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Betrayal_Erosion_Stacks_And_Expiry_2_5s()
        {
            var betrayal = go.AddComponent<Betrayal>();
            var wolf = ScriptableObject.CreateInstance<SpeciesSO>();
            wolf.speciesName = "Волк";
            try
            {
                betrayal.Hit(wolf);
                betrayal.Hit(wolf);
                float e0 = betrayal.Erosion(wolf);
                Assert.AreEqual(0.24f, e0, 0.001f, "2 стака ×0.12 (Betrayal.cs:14)");

                // эрозия по виду изолирована
                var moose = ScriptableObject.CreateInstance<SpeciesSO>();
                moose.speciesName = "Лось";
                Assert.AreEqual(0f, betrayal.Erosion(moose), 1e-5f);
                Object.DestroyImmediate(moose);

                yield return new WaitForSeconds(1f);
                Assert.AreEqual(0.24f, betrayal.Erosion(wolf), 0.001f, "через 1с стаки ещё живы (stackLife 2.5с)");

                yield return new WaitForSeconds(1.6f); // суммарно 2.6с >2.5
                Assert.AreEqual(0f, betrayal.Erosion(wolf), 1e-5f, "после 2.5с стаки истекли");

                // повторный Hit после истечения
                betrayal.Hit(wolf);
                Assert.AreEqual(0.12f, betrayal.Erosion(wolf), 0.001f);
            }
            finally { Object.DestroyImmediate(wolf); }
            yield return null;
        }
    }
}
