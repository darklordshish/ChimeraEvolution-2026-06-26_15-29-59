using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// КАРТА КАК ИСТОЧНИК, шаг 7 (спека §6 п. 4; ГеймБосс 18.11): кусты корма — слой карты по сиду, а не случайная раскладка
    /// по навмешу. Плотность прежняя (160 кустов в квадрате ±850 м, разнос 8 м — числа сцены павильона). Куст стоит на
    /// проходимой суше внутри кольца; раскладку меняет только сид. Граф получает вершины «корм».
    /// </summary>
    public class ForestMapForageTests
    {
        static GameObject host;
        static ForestBuilder builder;

        [OneTimeSetUp]
        public void BuildWorld()
        {
            host = new GameObject("~ForestMapForageProbe");
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
        public IEnumerator Forage_InMap_OnWalkableLand()
        {
            var m = builder.Map;
            Assert.AreEqual(ForestForage.DefaultCount, m.forage.Count, "кустов столько, сколько в сцене павильона");
            float walk = m.relief.Config.WalkMaxDegrees;
            foreach (var f in m.forage)
            {
                Assert.LessOrEqual(Mathf.Abs(f.pos.x), ForestForage.DefaultHalfExtent + 1e-3f, "в квадрате раскидки");
                Assert.LessOrEqual(f.pos.magnitude, m.ring.inner, "внутри кольца");
                Assert.GreaterOrEqual(ForestMapRaster.WaterDistance(m.water, f.pos), 0f, "не в воде");
                Assert.LessOrEqual(ForestForage.SlopeAt(m, f.pos), walk + 1e-3f, "на ходимом уклоне");
                Assert.AreEqual(m.relief.Height(f.pos.x, f.pos.y), f.y, 1e-4f, "высота — с рельефа карты");
            }
            for (int i = 0; i < m.forage.Count; i++)
                for (int j = i + 1; j < m.forage.Count; j++)
                    Assert.GreaterOrEqual(Vector2.Distance(m.forage[i].pos, m.forage[j].pos), ForestForage.DefaultSpacing - 1e-3f, "кусты не слипаются");
            yield break;
        }

        [UnityTest]
        public IEnumerator Forage_DependsOnlyOnSeed()
        {
            var m = builder.Map;
            var again = ForestForage.Plan(m, m.seed, ForestForage.DefaultCount, ForestForage.DefaultHalfExtent, ForestForage.DefaultSpacing);
            Assert.AreEqual(m.forage.Count, again.Count);
            for (int i = 0; i < again.Count; i++) Assert.AreEqual(m.forage[i].pos, again[i].pos, "тот же сид — те же кусты");
            var other = ForestForage.Plan(m, m.seed + 1, ForestForage.DefaultCount, ForestForage.DefaultHalfExtent, ForestForage.DefaultSpacing);
            Assert.AreNotEqual(m.forage[0].pos, other[0].pos, "другой сид — другая раскладка");
            yield break;
        }

        [UnityTest]
        public IEnumerator ForageSpawner_PlacesBushesFromMap()
        {
            var go = new GameObject("~ForageFromMap");
            go.AddComponent<ForageSpawner>();
            try
            {
                yield return null; // Start
                var bushes = go.GetComponentsInChildren<Forage>();
                Assert.AreEqual(builder.Map.forage.Count, bushes.Length, "кустов столько, сколько в слое корма карты");
                var root = builder.BuiltRoot.transform;
                for (int i = 0; i < bushes.Length; i++)
                {
                    var f = builder.Map.forage[i];
                    var want = root.TransformPoint(new Vector3(f.pos.x, f.y, f.pos.y));
                    Assert.Less(Vector3.Distance(want, bushes[i].transform.position), 1e-3f, $"куст {i} — в точке карты");
                }
            }
            finally { Object.Destroy(go); }
        }

        [UnityTest]
        public IEnumerator Forage_EntersHash_AndGraph()
        {
            var back = ForestMap.FromJson(builder.Map.ToJson());
            Assert.AreEqual(builder.Map.hash, ForestMap.ComputeHash(back), "хеш переживает JSON");
            var f = back.forage[0];
            f.pos.x += 1f;
            back.forage[0] = f;
            Assert.AreNotEqual(builder.Map.hash, ForestMap.ComputeHash(back), "корм входит в хеш");
            back = ForestMap.FromJson(builder.Map.ToJson());
            back.Restore();
            var r = ForestMapRaster.Build(back);
            var g = ForestMapGraph.Build(back, r);
            Assert.AreEqual(back.forage.Count, g.vertices.Count(v => v.kind == ForestMapGraph.VertexKind.Forage), "у каждого куста — вершина графа");
            yield break;
        }
    }
}
