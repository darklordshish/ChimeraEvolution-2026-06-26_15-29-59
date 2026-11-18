using System.Collections;
using Chimera.Laws;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// КАРТА КАК ИСТОЧНИК, шаг 4 (спека `2026-11-18-forest-karta-kak-istochnik` §6 п. 3, §9): логова — слой карты. Логово
    /// сначала записывается в план (адрес на плоскости, вид, тип, ёмкость, радиусы, темп), потом `LairSite` ставится по
    /// записи. В карте темп и спад страха — в ИГРОВЫХ ЧАСАХ, переводом закона `LairLaw` (одно место перевода, ГеймБосс
    /// 18.11). Адреса не сдвинулись — держит `ForestMapV2BaselineTests` (хеш логов).
    /// </summary>
    public class ForestMapLairsTests
    {
        static GameObject host;
        static ForestBuilder builder;

        [OneTimeSetUp]
        public void BuildWorld()
        {
            host = new GameObject("~ForestMapLairsProbe");
            host.SetActive(false);
            builder = ForestProfiles.PavilionV2Zone(host, 1337L);
            builder.buildOnAwake = false;
            host.SetActive(true);
            builder.Build();
        }

        [OneTimeTearDown]
        public void TearDownWorld()
        {
            if (builder != null) builder.Clear();
            if (host != null) Object.Destroy(host);
            builder = null; host = null;
        }

        [UnityTest]
        public IEnumerator Lairs_InMap_MatchSites_InGameHours()
        {
            var map = builder.Map;
            Assert.IsNotNull(map.lairs, "у карты есть слой логов");
            Assert.AreEqual(builder.Lairs.Count, map.lairs.Count, "в карте все логова мира, в том же порядке");
            var root = builder.BuiltRoot.transform;
            for (int i = 0; i < map.lairs.Count; i++)
            {
                var m = map.lairs[i];
                var s = builder.Lairs[i];
                string who = $"логово {i} ({m.name})";
                Assert.AreEqual(s.speciesName, m.species, who + ": вид");
                Assert.AreEqual(s.areaType, m.type, who + ": тип");
                Assert.Less(Vector3.Distance(s.transform.position, root.TransformPoint(m.pos)), 1e-3f, who + ": адрес");
                Assert.AreEqual(s.capacity, m.capacity, who + ": ёмкость");
                Assert.AreEqual(s.spawnEnabled, m.spawnEnabled, who + ": рожает");
                Assert.AreEqual(s.homeRadius, m.homeRadius, 1e-5f, who + ": радиус дома");
                Assert.AreEqual(s.fearRadius, m.fearRadius, 1e-5f, who + ": радиус страха");
                Assert.AreEqual(s.fearPerKill, m.fearPerKill, 1e-5f, who + ": страх за смерть");
                Assert.AreEqual(s.exhaustFear, m.exhaustFear, 1e-5f, who + ": порог истощения");
                Assert.AreEqual(s.RespawnGameHours, m.respawnHours, 1e-5f, who + ": интервал рождения в игровых часах");
                Assert.AreEqual(s.FearDecayPerGameHour, m.fearDecayPerHour, 1e-5f, who + ": спад страха за игровой час");
            }
            yield break;
        }

        [UnityTest]
        public IEnumerator Lairs_HoursComeFromLairLaw()
        {
            var m = builder.Map.lairs[0];
            float hps = LairSite.GameHoursPerSecond;
            Assert.AreEqual(LairLaw.RespawnHours(builder.Lairs[0].respawnSeconds, hps), m.respawnHours, 1e-6f, "перевод — закон LairLaw");
            yield break;
        }

        [UnityTest]
        public IEnumerator Lairs_SurviveJson_AndEnterHash()
        {
            var back = ForestMap.FromJson(builder.Map.ToJson());
            Assert.AreEqual(builder.Map.lairs.Count, back.lairs.Count, "логова переживают JSON");
            Assert.AreEqual(builder.Map.hash, ForestMap.ComputeHash(back), "хеш тот же");
            back.lairs[0].capacity += 1;
            Assert.AreNotEqual(builder.Map.hash, ForestMap.ComputeHash(back), "ёмкость логова входит в хеш");
            yield break;
        }
    }
}
