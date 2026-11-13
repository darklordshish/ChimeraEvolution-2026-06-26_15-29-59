using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Срез C: рубильник спавна у логова. `spawnEnabled = false` — место ЖИВЁТ (страх тает,
    /// события идут), но очередь НЕ капает и хук не рожает: иначе в кадр включения из логова
    /// вылезла бы накопленная толпа, а совиное гнездо без тела филина рожало бы пустышку.
    /// <para>
    /// Чистая логика без сцены: тикаем явным dt, спавнер дёргаем DrainOnce/Tick — без
    /// ChimeraFactory (до него дело доходить не должно, в этом и смысл).
    /// </para>
    /// </summary>
    public class LairSpawnGateTests
    {
        readonly List<GameObject> garbage = new List<GameObject>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in garbage) if (go != null) Object.DestroyImmediate(go);
            garbage.Clear();
        }

        LairSite Site(int capacity = 4, bool spawnEnabled = true)
        {
            var go = new GameObject("~Lair");
            garbage.Add(go);
            var site = go.AddComponent<LairSite>();
            site.speciesName = "Змея";
            site.capacity = capacity;
            site.respawnSeconds = 0.01f;
            site.spawnEnabled = spawnEnabled;
            return site;
        }

        LairRegistry Registry()
        {
            var go = new GameObject("~Registry");
            garbage.Add(go);
            return go.AddComponent<LairRegistry>();
        }

        ForestSpawner Spawner(LairRegistry reg)
        {
            var go = new GameObject("~Spawner");
            garbage.Add(go);
            var spawner = go.AddComponent<ForestSpawner>();
            spawner.Configure(reg, new List<SpeciesSO>());
            return spawner;
        }

        [Test]
        public void SpawnDisabled_DoesNotQueue_ButFearDecays()
        {
            var off = Site(spawnEnabled: false);
            var on = Site(spawnEnabled: true);
            off.fear = 0.5f;
            on.fear = 0.5f;

            off.Tick(1f);
            on.Tick(1f);

            Assert.AreEqual(0, off.pendingSpawns,
                "выключенное место очередь НЕ копит (иначе включение выплюнет толпу)");
            Assert.Less(off.fear, 0.5f, "но страх тает: место живёт, а не замирает");
            Assert.Greater(on.pendingSpawns, 0,
                "контроль: тот же тик у включённого места очередь качает — дело в рубильнике");
        }

        [Test]
        public void SpawnDisabled_DrainOnceFalse()
        {
            var reg = Registry();
            var site = Site(capacity: 4, spawnEnabled: false);
            site.pendingSpawns = 1; // как будто очередь осталась с прошлой жизни
            reg.Register(site);
            var spawner = Spawner(reg);

            Assert.IsFalse(spawner.DrainOnce(site), "выключенное место не рожает даже из полной очереди");
            Assert.AreEqual(1, site.pendingSpawns, "очередь цела: она не тратится и не растёт");
            Assert.AreEqual(0, site.population, "популяция не тронута");
            Assert.IsNull(spawner.lastSpawn, "рождённого нет");
        }

        /// <summary>
        /// Тик спавнера обязан сначала тикнуть сайт (страх тает), и только потом решать,
        /// рожать ли. Перепутай порядок — у выключенного места страх замрёт, и «закрытое
        /// логово» станет вечно истощённым.
        /// </summary>
        [Test]
        public void Spawner_Tick_TicksDisabledSiteForFear_ButDoesNotQueue()
        {
            var reg = Registry();
            var site = Site(spawnEnabled: false);
            site.fear = 0.5f;
            reg.Register(site);
            var spawner = Spawner(reg);

            spawner.Tick(1f);

            Assert.Less(site.fear, 0.5f, "страх выключенного места тает на тике спавнера");
            Assert.AreEqual(0, site.pendingSpawns, "а очередь при этом стоит");
            Assert.IsNull(spawner.lastSpawn, "и рождённых нет");
        }

        [Test]
        public void Registry_Clear_Empties()
        {
            var reg = Registry();
            var a = Site();
            var b = Site();
            reg.Register(a);
            reg.Register(b);
            Assert.AreEqual(2, reg.Count, "оба логова в реестре");

            reg.Clear();

            Assert.AreEqual(0, reg.Count, "Clear опустошает реестр (пересборка мира)");
            Assert.AreEqual(0, reg.Sites.Count, "перечисление для спавнера тоже пусто");
            Assert.IsNull(reg.NearestHome("Змея", Vector3.zero), "дом искать негде — null, а не снесённый сайт");

            reg.Register(a);
            Assert.AreEqual(1, reg.Count, "после Clear реестр снова принимает логова");
        }
    }
}
