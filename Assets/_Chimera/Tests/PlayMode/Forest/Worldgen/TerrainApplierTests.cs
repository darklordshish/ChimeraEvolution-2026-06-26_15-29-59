using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// Аппликатор строит фасеточный меш из поля высот и печёт связный NavMesh; после себя чистит.
    /// Слайс s1b (ветка forest/s1b-terrain-applier, спека 2026-09-18-forest-s1b).
    /// </summary>
    public class TerrainApplierTests
    {
        [UnityTest]
        public IEnumerator Applier_BuildsFacetedMesh_AndBakesConnectedNavMesh()
        {
            var cfg = ScriptableObject.CreateInstance<WorldGenConfigSO>();
            cfg.seed = 4242;
            cfg.amplitude = 2f;
            cfg.baseFrequency = 0.02f;
            cfg.octaves = 3;

            var go = new GameObject("~ForestProbe");
            var applier = go.AddComponent<TerrainApplier>();
            applier.Configure(cfg, 16, 32f);
            applier.Build();

            Assert.IsNotNull(applier.BuiltMesh, "Build обязан построить меш");
            Assert.AreEqual(16 * 16 * 6, applier.BuiltMesh.vertexCount,
                "фасетка: 6 вершин на квад (как грани поля)");
            Assert.Greater(applier.BuiltMesh.normals[0].y, 0.9f,
                "нормали смотрят вверх (+Y), иначе террейн невидим сверху");

            applier.BakeNavMesh();
            yield return null;
            Assert.IsTrue(applier.IsBaked, "NavMesh обязан запечься (BuildNavMesh)");

            for (int i = 0; i < 8; i++)
            {
                float x = SeededHash.ToFloatSigned(SeededHash.Hash(cfg.seed, i, 7)) * 14f;
                float z = SeededHash.ToFloatSigned(SeededHash.Hash(cfg.seed, i, 77)) * 14f;
                Vector3 pos;
                Assert.IsTrue(applier.TrySample(x, z, out pos),
                    $"сидированная точка {i} ({x:F1},{z:F1}) обязана стоять на NavMesh");
            }

            Vector3 a;
            Vector3 b;
            Assert.IsTrue(applier.TrySample(-14f, -14f, out a), "угол A на NavMesh");
            Assert.IsTrue(applier.TrySample(14f, 14f, out b), "угол B на NavMesh");
            Assert.IsTrue(applier.PathExists(a, b), "угол A связан путём PathComplete с углом B");

            applier.Clear();
            Object.Destroy(go);
            Object.Destroy(cfg);
        }
    }
}
