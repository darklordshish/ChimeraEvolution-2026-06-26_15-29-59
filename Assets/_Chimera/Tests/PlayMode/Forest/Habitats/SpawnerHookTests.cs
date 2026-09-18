using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// Хук спавнера: очередь логова → живой зверь в радиусе; страх блокирует;
    /// смерть возвращается популяцией и страхом. Создаём программно, чистим в TearDown.
    /// Слайс s2b (ветка forest/s2b-lair-spawner, спека 2026-09-18-forest-s2b).
    /// </summary>
    public class SpawnerHookTests
    {
        readonly List<Object> trash = new List<Object>();

        SpeciesSO MakeSpecies(string name)
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

        LairSite Site(string species, int capacity, Vector3 pos)
        {
            var go = new GameObject("~Lair");
            go.transform.position = pos;
            trash.Add(go);
            var site = go.AddComponent<LairSite>();
            site.speciesName = species;
            site.capacity = capacity;
            site.respawnSeconds = 0.1f;
            return site;
        }

        ForestSpawner Spawner(LairRegistry reg, List<SpeciesSO> pool)
        {
            var go = new GameObject("~Spawner");
            trash.Add(go);
            var spawner = go.AddComponent<ForestSpawner>();
            spawner.Configure(reg, pool);
            return spawner;
        }

        LairRegistry Registry()
        {
            var go = new GameObject("~Registry");
            trash.Add(go);
            return go.AddComponent<LairRegistry>();
        }

        [TearDown]
        public void Clean()
        {
            foreach (var o in trash) if (o != null) Object.Destroy(o);
            trash.Clear();
        }

        [UnityTest]
        public IEnumerator DrainOnce_DrainsQueue()
        {
            var wolf = MakeSpecies("Волк");
            var reg = Registry();
            var site = Site("Волк", 2, new Vector3(0f, 0.5f, 0f));
            reg.Register(site);
            var spawner = Spawner(reg, new List<SpeciesSO> { wolf });
            site.Tick(1f);
            Assert.AreEqual(1, site.pendingSpawns, "очередь накачана тиком");

            Assert.IsTrue(spawner.DrainOnce(site), "хук рожает из очереди");
            Assert.AreEqual(0, site.pendingSpawns, "слот ушёл из очереди");
            Assert.AreEqual(1, site.population, "слот — в популяции");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Spawned_InRadius_NotWhenExhausted()
        {
            var wolf = MakeSpecies("Волк");
            var reg = Registry();
            var site = Site("Волк", 1, new Vector3(0f, 0.5f, 0f));
            site.spawnRadius = 5f;
            reg.Register(site);
            var spawner = Spawner(reg, new List<SpeciesSO> { wolf });
            site.Tick(1f);
            Assert.IsTrue(spawner.DrainOnce(site), "первый слот рождается");

            CreatureBody born = spawner.lastSpawn;
            Assert.IsNotNull(born, "хук отдаёт рождённого напрямую (без поиска по имени)");
            trash.Add(born.gameObject);
            Vector2 flatBorn = new Vector2(born.transform.position.x, born.transform.position.z);
            Vector2 flatSite = new Vector2(site.transform.position.x, site.transform.position.z);
            float dist = Vector2.Distance(flatBorn, flatSite);
            Assert.LessOrEqual(dist, site.spawnRadius + 0.5f, "точка в радиусе логова (по плоскости)");
            Assert.IsTrue(born.GetComponent<Health>().Current > 0f, "выводок жив");

            site.pendingSpawns = 1;
            site.fear = 1f;
            Assert.IsFalse(spawner.DrainOnce(site), "истощённое логово не рожает");
            Assert.AreEqual(1, site.pendingSpawns, "очередь цела");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Death_ReportsAndScares()
        {
            var wolf = MakeSpecies("Волк");
            var reg = Registry();
            var site = Site("Волк", 1, new Vector3(0f, 0.5f, 0f));
            reg.Register(site);
            var spawner = Spawner(reg, new List<SpeciesSO> { wolf });
            site.Tick(1f);
            Assert.IsTrue(spawner.DrainOnce(site), "выводок рождён");

            CreatureBody born = spawner.lastSpawn;
            Assert.IsNotNull(born, "хук отдаёт рождённого напрямую");
            trash.Add(born.gameObject);

            born.GetComponent<Health>().TakeDamage(999999, true);
            yield return null;
            Assert.AreEqual(0, site.population, "смерть вернулась популяцией");
            Assert.Greater(site.fear, 0f, "смерть рядом пугает логово (запах смерти)");
        }
    }
}
