using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Джоба считает то же поле, что скаляр (1:1), детерминирована между запусками.
    /// Слайс s10a.
    /// </summary>
    public class DomeHeightJobTests
    {
        static DomeGenConfigSO Config(long seed)
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.seed = seed;
            cfg.randomSeed = false;
            return cfg;
        }

        [Test]
        public void Job_MatchesScalarField()
        {
            var cfg = Config(1337);
            const int res = 16;
            const float step = 4f;
            var heights = new NativeArray<float>(res * res, Allocator.TempJob);
            try
            {
                var job = new DomeHeightJob
                {
                    p = DomeHeightField.DomeHeightParams.From(cfg),
                    x0 = -32f,
                    z0 = -32f,
                    step = step,
                    res = res,
                    heights = heights,
                };
                job.Schedule(res * res, 64).Complete();
                for (int iz = 0; iz < res; iz++)
                    for (int ix = 0; ix < res; ix++)
                    {
                        float expected = DomeHeightField.SampleHeight(cfg, -32f + ix * step, -32f + iz * step);
                        Assert.AreEqual(expected, heights[iz * res + ix], 1e-6f,
                            $"джоба = скаляр в клетке ({ix},{iz})");
                    }
            }
            finally
            {
                heights.Dispose();
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void Job_IsDeterministicAcrossRuns()
        {
            var cfg = Config(777);
            const int n = 64;
            var a = new NativeArray<float>(n, Allocator.TempJob);
            var b = new NativeArray<float>(n, Allocator.TempJob);
            try
            {
                var p = DomeHeightField.DomeHeightParams.From(cfg);
                new DomeHeightJob { p = p, x0 = 0f, z0 = 0f, step = 2f, res = 8, heights = a }.Schedule(n, 64).Complete();
                new DomeHeightJob { p = p, x0 = 0f, z0 = 0f, step = 2f, res = 8, heights = b }.Schedule(n, 64).Complete();
                for (int i = 0; i < n; i++)
                    Assert.AreEqual(a[i], b[i], "два прогона джобы — бит-в-бит");
            }
            finally
            {
                a.Dispose();
                b.Dispose();
                Object.DestroyImmediate(cfg);
            }
        }
    }
}
