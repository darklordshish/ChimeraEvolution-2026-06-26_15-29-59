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
    }
}
