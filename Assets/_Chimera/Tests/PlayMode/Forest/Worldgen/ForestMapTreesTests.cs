using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// КАРТА КАК ИСТОЧНИК, шаг 3 (спека `2026-11-18-forest-karta-kak-istochnik` §6): лес делится на план (2D — точки,
    /// порода, масштаб, лежит/стоит, твёрдый ствол) и постановку (3D). План — слой `trees` карты; логова зверей читают
    /// стволы из карты, а не из мира. Единственная обратная связь 3D→план устранена. Отпечаток мира при этом тот же —
    /// его держит `ForestMapV2BaselineTests` (стволы и логова — свои хеши).
    /// </summary>
    public class ForestMapTreesTests
    {
        static GameObject host;
        static ForestBuilder builder;

        [OneTimeSetUp]
        public void BuildWorld()
        {
            host = new GameObject("~ForestMapTreesProbe");
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
        public IEnumerator Trees_InMap_MatchPlantedForest()
        {
            var trees = builder.Map.trees;
            Assert.IsNotNull(trees, "у карты v2 есть слой деревьев");
            Assert.AreEqual(builder.Trees, trees.Count, "в карте столько деревьев, сколько посажено");
            int solid = 0, bad = 0;
            var planted = builder.TreeColliderPositions;
            foreach (var t in trees)
            {
                if (!t.solid) continue;
                if (solid >= planted.Count || t.pos != planted[solid]) bad++;
                solid++;
            }
            Assert.AreEqual(planted.Count, solid, "твёрдых стволов в карте столько же, сколько коллайдеров в мире");
            Assert.AreEqual(0, bad, "твёрдые стволы карты — те же точки и в том же порядке");
            yield break;
        }

        [UnityTest]
        public IEnumerator AnimalLairs_ReadTreesFromMap()
        {
            Assert.IsNotNull(builder.World.MapTrees, "мир снял план леса до постановки");
            Assert.AreSame(builder.World.MapTrees, builder.Map.trees, "карта держит тот же план, по которому сажали и искали логова");
            yield break;
        }

        [UnityTest]
        public IEnumerator Trees_SurviveJson_AndEnterHash()
        {
            var back = ForestMap.FromJson(builder.Map.ToJson());
            Assert.AreEqual(builder.Map.trees.Count, back.trees.Count, "деревья переживают JSON");
            Assert.AreEqual(builder.Map.hash, ForestMap.ComputeHash(back), "хеш тот же");
            var t = back.trees[0];
            t.pos.x += 1f;
            back.trees[0] = t;
            Assert.AreNotEqual(builder.Map.hash, ForestMap.ComputeHash(back), "дерево входит в хеш карты");
            yield break;
        }
    }
}
