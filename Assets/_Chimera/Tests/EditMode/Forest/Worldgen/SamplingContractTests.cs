using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    public class SamplingContractTests
    {
        [Test]
        public void From_NamesMeshSlopeRouteAndNavResolutions()
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            try
            {
                cfg.mapDiameter = 2000f;
                cfg.chunkSize = 250f;
                cfg.navVoxelSize = 0.35f;
                var c = SamplingContract.From(cfg, 48, 128);

                Assert.AreEqual(250f / 48f, c.meshStep, 1e-4f, "шаг меша");
                Assert.AreEqual(2.6042f, c.slopeEpsilon, 1e-3f, "e уклона от шага меша");
                Assert.AreEqual(15.625f, c.routeCell, 1e-3f, "клетка маршрута D/128");
                Assert.AreEqual(0.35f, c.navVoxel, 1e-4f, "NavMesh-воксель из конфига");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }
    }
}
