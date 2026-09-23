using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// Бункер в бейке: мелкий террейн + комплекс → углы семплятся, стены с коллайдерами.
    /// Полный путь угол-угол НЕ меряем — бункер вправе перекрыть. Слайс s9.
    /// </summary>
    public class RuinsBakeTests
    {
        [UnityTest]
        public IEnumerator Bunker_BakesWithColliders_AndCornersReachable()
        {
            var cfg = ScriptableObject.CreateInstance<WorldGenConfigSO>();
            cfg.seed = 4242;
            cfg.amplitude = 2f;
            var ground = new GameObject("~RuinsGround");
            var applier = ground.AddComponent<TerrainApplier>();
            applier.Configure(cfg, 12, 24f);
            applier.Build();
            var bunker = ground.AddComponent<BunkerPlacer>();
            bunker.world = cfg;
            bunker.seed = 4242;
            bunker.Build();
            applier.BakeNavMesh();
            yield return null;

            Assert.IsTrue(applier.IsBaked, "бейк со стенами собрался");
            int boxes = 0;
            foreach (var col in ground.GetComponentsInChildren<BoxCollider>())
                if (col.gameObject.name == "BunkerPart") boxes++;
            Assert.Greater(boxes, 10, "стены/обломки с боксами");
            Vector3 a, b;
            Assert.IsTrue(applier.TrySample(-10f, -10f, out a), "угол A семплится");
            Assert.IsTrue(applier.TrySample(10f, 10f, out b), "угол B семплится");

            Object.Destroy(ground);
            Object.Destroy(cfg);
            yield return null;
        }
    }
}
