using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// Placer: мелкий террейн + флора + бейк разом — стволы с коллайдерами,
    /// трава на одном shared-материале, угол-угол проходим. Слайс s8.
    /// </summary>
    public class FloraPlacerTests
    {
        [UnityTest]
        public IEnumerator Placer_BuildsColliders_AndKeepsPath()
        {
            var cfg = ScriptableObject.CreateInstance<WorldGenConfigSO>();
            cfg.seed = 4242;
            cfg.amplitude = 2f;
            var flora = ScriptableObject.CreateInstance<FloraConfigSO>();
            flora.treeCount = 4;
            flora.rockCount = 2;
            flora.bushCount = 2;
            flora.grassCount = 10;
            flora.minDistance = 2f;

            var ground = new GameObject("~ForestGround");
            var applier = ground.AddComponent<TerrainApplier>();
            applier.Configure(cfg, 12, 24f);
            applier.Build();
            var placer = ground.AddComponent<FloraPlacer>();
            placer.world = cfg;
            placer.flora = flora;
            placer.Build();
            applier.BakeNavMesh();
            yield return null;

            int trunks = 0;
            Material grassMat = null;
            bool grassShared = true;
            foreach (var col in ground.GetComponentsInChildren<CapsuleCollider>())
                if (col.gameObject.name == "FloraPart") trunks++;
            foreach (var r in ground.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.gameObject.name != "FloraPart") continue;
                var filter = r.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null && filter.sharedMesh.name == "GrassBlade")
                {
                    if (grassMat == null) grassMat = r.sharedMaterial;
                    else if (r.sharedMaterial != grassMat) grassShared = false;
                }
            }
            Assert.Greater(trunks, 0, "стволы-капсулы стоят");
            Assert.IsTrue(grassShared && grassMat != null, "трава на одном shared-материале (авто-инстансинг)");

            Vector3 a, b;
            Assert.IsTrue(applier.TrySample(-10f, -10f, out a), "угол A на NavMesh");
            Assert.IsTrue(applier.TrySample(10f, 10f, out b), "угол B на NavMesh");
            Assert.IsTrue(applier.PathExists(a, b), "флора не заперла карту");
            Assert.IsTrue(applier.BuiltMesh.vertexCount > 0);

            Object.Destroy(ground);
            Object.Destroy(cfg);
            Object.Destroy(flora);
            yield return null;
        }
    }
}
