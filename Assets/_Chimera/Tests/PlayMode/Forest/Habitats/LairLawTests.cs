using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// Закон логова (спека `2026-11-13-forest-logovo-po-zakonam` §3, §5 п. 4–5): жилец — живое тело, у которого
    /// логово домом; занятость — живые жильцы, а не рождённые здесь; страх — от свидетелей, без метки вида.
    /// Нигде нет ветки по составу: химера и чистый вид проходят одни и те же проверки.
    /// </summary>
    public class LairLawTests
    {
        readonly List<Object> trash = new List<Object>();

        SpeciesSO Species(string name)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = name;
            so.tint = Color.gray;
            so.mutagenPool = 100;
            so.baseHp = 60;
            so.baseStamina = 100;
            so.baseStaminaRegen = 10f;
            so.sockets = new BodySocket[0];
            so.organs = new[] { new Organ { organName = name + "-shard", slot = "Руки", cost = 2 } };
            trash.Add(so);
            return so;
        }

        LairSite Site(string species, int capacity, Vector3 pos, float fearRadius = 18f)
        {
            var go = new GameObject("~Логово-" + species);
            go.transform.position = pos;
            trash.Add(go);
            var site = go.AddComponent<LairSite>();
            site.speciesName = species;
            site.capacity = capacity;
            site.fearRadius = fearRadius;
            return site;
        }

        CreatureBody Body(SpeciesSO sp, Vector3 pos)
        {
            var b = ChimeraFactory.Spawn(sp, new[] { sp }, pos, "~" + sp.speciesName);
            trash.Add(b.gameObject);
            return b;
        }

        [TearDown]
        public void Clean()
        {
            foreach (var o in trash) if (o != null) Object.Destroy(o);
            trash.Clear();
        }

        [UnityTest]
        public IEnumerator HomeChange_FreesPlace_WhileAlive()
        {
            var wolf = Species("Волк");
            var a = Site("Волк", 2, new Vector3(800f, 0f, 800f));
            var b = Site("Ёж", 2, new Vector3(860f, 0f, 800f));
            var body = Body(wolf, new Vector3(800f, 0f, 805f));
            Assert.IsTrue(a.Claim(body), "рождённый у логова — жилец");
            Assert.AreEqual(1, a.population);

            Assert.IsTrue(b.Claim(body), "тело сменило дом (так решит психика после метаморфозы)");
            yield return null;
            Assert.AreEqual(0, a.population, "старое логово освободилось, хотя тело живо");
            Assert.AreEqual(1, b.population, "новое логово держит его как любого жильца");
            Assert.AreSame(b, body.home, "дом один");
        }

        [UnityTest]
        public IEnumerator ForeignBody_ThatClaims_HoldsPlace_FullLairRefuses()
        {
            var wolf = Species("Волк");
            var site = Site("Волк", 1, new Vector3(900f, 0f, 700f));
            var foreign = Body(wolf, new Vector3(905f, 0f, 700f)); // рождён не логовом
            Assert.IsTrue(site.Claim(foreign), "пришлый взял логово домом");
            Assert.AreEqual(1, site.population, "пришлый держит место, как рождённый");
            var second = Body(wolf, new Vector3(910f, 0f, 700f));
            Assert.IsFalse(site.Claim(second), "полное логово отказывает");
            Assert.IsNull(second.home, "отказ не даёт дома");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Death_FreesPlace_WithoutReport()
        {
            var wolf = Species("Волк");
            var site = Site("Волк", 2, new Vector3(700f, 0f, 900f));
            var body = Body(wolf, new Vector3(700f, 0f, 905f));
            site.Claim(body);
            body.GetComponent<Health>().TakeDamage(999999, true);
            yield return null;
            Assert.AreEqual(0, site.population, "мёртвый не жилец — место свободно само");
        }

        [UnityTest]
        public IEnumerator Fear_FromWitness_NotFromSpeciesLabel()
        {
            var wolf = Species("Волк");
            var reg = new GameObject("~Реестр").AddComponent<LairRegistry>();
            trash.Add(reg.gameObject);
            var spawner = new GameObject("~Спавнер").AddComponent<ForestSpawner>();
            trash.Add(spawner.gameObject);
            spawner.Configure(reg, new List<SpeciesSO> { wolf });

            // Логово ежа далеко от места смерти (за радиусом страха), но его жилец стоит рядом и слышит.
            var seen = Site("Ёж", 2, new Vector3(1000f, 0f, 1000f));
            var far = Site("Волк", 2, new Vector3(1300f, 0f, 1000f)); // вид жертвы тот же, но никто не видел
            reg.Register(seen);
            reg.Register(far);
            var witness = Body(Species("Ёж"), new Vector3(1080f, 0f, 1000f));
            seen.Claim(witness);
            var senses = witness.GetComponent<Senses>() ?? witness.gameObject.AddComponent<Senses>();
            senses.Set(SenseKind.Hearing, 40f);

            Vector3 death = new Vector3(1100f, 0f, 1000f); // 100 м от логова ежа, 20 м от свидетеля, 200 м от волчьего
            spawner.ReportDeathAt(death, null);
            yield return null;
            Assert.Greater(seen.fear, 0f, "жилец слышал смерть — логово ежа боится, хотя жертва не ёж");
            Assert.AreEqual(0f, far.fear, "волчье логово не видело — не боится, хотя вид тот же");
        }

        Health PlainHealth(Vector3 pos)
        {
            var go = new GameObject("~Смертный");
            go.transform.position = pos;
            trash.Add(go);
            var h = go.AddComponent<Health>();
            return h;
        }

        [UnityTest]
        public IEnumerator AnyDeath_FiresOncePerDeath_WithPointAndKiller_AgainAfterRevive()
        {
            var victim = PlainHealth(new Vector3(10f, 0f, 20f));
            var killer = PlainHealth(new Vector3(0f, 0f, 0f));
            victim.LastAttacker = killer;
            int n = 0; Health gotV = null, gotK = null; Vector3 gotAt = default;
            System.Action<Health, Vector3, Health> sub = (v, at, k) => { n++; gotV = v; gotAt = at; gotK = k; };
            Health.AnyDeath += sub;
            try
            {
                victim.TakeDamage(999999, true);
                victim.TakeDamage(999999, true); // мёртвый второй раз не умирает
                Assert.AreEqual(1, n, "ровно один сигнал на смерть");
                Assert.AreSame(victim, gotV);
                Assert.AreEqual(new Vector3(10f, 0f, 20f), gotAt);
                Assert.AreSame(killer, gotK, "убийца — LastAttacker");

                yield return null; // destroyOnDeath: объект снесён в конце кадра
            }
            finally { Health.AnyDeath -= sub; }

            // Revive: второй объект без сноса (destroyOnDeath по умолчанию true — оживляем до конца кадра)
            var v2 = PlainHealth(Vector3.zero);
            System.Action<Health, Vector3, Health> sub2 = (v, at, k) => { if (v == v2) n += 100; };
            Health.AnyDeath += sub2;
            try
            {
                v2.TakeDamage(999999, true);
                v2.Revive();
                v2.TakeDamage(999999, true);
                Assert.AreEqual(201, n, "после Revive следующая смерть шлёт снова");
            }
            finally { Health.AnyDeath -= sub2; }
        }

        [UnityTest]
        public IEnumerator AnyDeath_ThrowingSubscriber_DoesNotBreakDeath()
        {
            var victim = PlainHealth(Vector3.zero);
            bool deathEvent = false, second = false;
            victim.onDeath.AddListener(() => deathEvent = true);
            System.Action<Health, Vector3, Health> bad = (v, at, k) => throw new System.InvalidOperationException("подписчик сломан");
            System.Action<Health, Vector3, Health> good = (v, at, k) => second = true;
            Health.AnyDeath += bad; Health.AnyDeath += good;
            try
            {
                LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("подписчик сломан"));
                victim.TakeDamage(999999, true);
                Assert.IsTrue(victim.IsDead);
                Assert.IsTrue(deathEvent, "onDeath отработал");
                Assert.IsTrue(second, "следующий подписчик получил сигнал");
            }
            finally { Health.AnyDeath -= bad; Health.AnyDeath -= good; }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerDeath_NearLair_ScaresResidents_ViaAnyDeath()
        {
            var wolf = Species("Волк");
            var reg = new GameObject("~Реестр").AddComponent<LairRegistry>();
            trash.Add(reg.gameObject);
            var spawner = new GameObject("~Спавнер").AddComponent<ForestSpawner>();
            trash.Add(spawner.gameObject);
            spawner.Configure(reg, new List<SpeciesSO> { wolf });
            var site = Site("Волк", 2, new Vector3(1500f, 0f, 1500f));
            reg.Register(site);
            var far = Site("Волк", 2, new Vector3(1900f, 0f, 1500f));
            reg.Register(far);

            // «Игрок» не рождён лесом: тело с Health рядом с порогом логова
            var player = Body(Species("Человек"), new Vector3(1505f, 0f, 1500f));
            yield return null;
            Assert.AreEqual(0f, site.fear);
            player.GetComponent<Health>().TakeDamage(999999, true);
            yield return null;
            Assert.Greater(site.fear, 0f, "смерть чужого тела у порога пугает логово без ReportDeathAt руками");
            Assert.AreEqual(0f, far.fear, "дальнее логово не видело");
        }
    }
}
