using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// КАРТА КАК ИСТОЧНИК, шаг 5а (спека `2026-11-18-forest-karta-kak-istochnik` §4): зоны, связи, общие места,
    /// водопои, укрытия, точки интереса, дороги, ясень и маяк — слои карты. Каждый слой совпадает с тем, что собрал мир,
    /// переживает JSON и входит в хеш.
    /// </summary>
    public class ForestMapLayersTests
    {
        static GameObject host;
        static ForestBuilder builder;

        [OneTimeSetUp]
        public void BuildWorld()
        {
            host = new GameObject("~ForestMapLayersProbe");
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
        public IEnumerator Layers_MatchWorld()
        {
            var m = builder.Map;
            var w = builder.World;
            Assert.Greater(m.zones.Count, 0, "зоны есть");
            Assert.AreEqual(w.Zones.Count, m.zones.Count, "зоны");
            for (int i = 0; i < m.zones.Count; i++) Assert.AreEqual(w.Zones[i].center, m.zones[i].center, $"зона {i}");
            Assert.AreEqual(w.Links.Count, m.links.Count, "связи");
            Assert.AreEqual(w.Commons.Count, m.commons.Count, "общие места");
            Assert.AreEqual(w.Waterholes.Count, m.waterholes.Count, "водопои");
            Assert.AreEqual(w.CoverPods.Count, m.coverPods.Count, "укрытия");
            Assert.AreEqual(w.Pois.All.Count, m.pois.Count, "точки интереса");
            Assert.IsNotNull(w.Roads, "у v2 есть дороги");
            Assert.AreEqual(w.Roads.roads.Count, m.roads.Count, "дороги");
            for (int i = 0; i < m.roads.Count; i++)
                Assert.AreEqual(w.Roads.roads[i].points.Count, m.roads[i].points.Count, $"точки дороги {i}");
            Assert.AreEqual(w.HasAsh, m.ash.has, "ясень");
            Assert.AreEqual(w.Ash.pos, m.ash.pos, "место ясеня");
            yield break;
        }

        [UnityTest]
        public IEnumerator Beacon_InMap_MatchesMast()
        {
            var m = builder.Map;
            Assert.IsTrue(m.beacon.has, "маяк в карте");
            Assert.AreEqual(builder.buildBeacon, m.beacon.on, "галка маяка");
            Vector3 mast = builder.BuiltRoot.transform.TransformPoint(m.beacon.basePos);
            Assert.Less(Vector3.Distance(builder.Beacon.transform.position, mast), 1e-3f, "основание маяка");
            Assert.AreEqual(builder.BeaconSettings.height, m.beacon.height, 1e-5f, "высота маяка");
            yield break;
        }

        [UnityTest]
        public IEnumerator Layers_SurviveJson_AndEnterHash()
        {
            var back = ForestMap.FromJson(builder.Map.ToJson());
            Assert.AreEqual(builder.Map.hash, ForestMap.ComputeHash(back), "хеш переживает JSON");
            Assert.AreEqual(builder.Map.roads.Count, back.roads.Count, "дороги переживают JSON");
            var z = back.zones[0];
            z.radius += 1f;
            back.zones[0] = z;
            Assert.AreNotEqual(builder.Map.hash, ForestMap.ComputeHash(back), "зона входит в хеш");
            yield break;
        }
    }
}
