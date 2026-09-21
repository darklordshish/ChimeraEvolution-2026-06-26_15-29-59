using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Склон: маска ловит крутизну, устья — на склоне 35–55° с ровной площадкой,
    /// гнёзда — в кольце 15–40м от устья, всё детерминировано. Слайс s10e.
    /// </summary>
    public class DomeSlopeTests
    {
        static DomeGenConfigSO Config(long seed)
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.seed = seed;
            cfg.randomSeed = false;
            cfg.mapDiameter = 2000f;
            cfg.amplitude = 12f;
            cfg.baseFrequency = 0.08f;
            return cfg;
        }

        [Test]
        public void SlopeMask_MarksSteep_ConsistentWithSlopeAt()
        {
            var cfg = Config(1337);
            try
            {
                var mask = DomeSlope.SlopeMask(cfg, 0f, 0f, 500f, 32, 30f);
                int steep = 0;
                for (int ix = 0; ix < 32; ix++)
                    for (int iz = 0; iz < 32; iz++)
                    {
                        float x = -250f + (ix + 0.5f) * 500f / 32;
                        float z = -250f + (iz + 0.5f) * 500f / 32;
                        bool expected = DomeSlope.SlopeAt(cfg, x, z) >= 30f;
                        Assert.AreEqual(expected, mask[ix, iz], $"маска = SlopeAt в клетке ({ix},{iz})");
                        if (mask[ix, iz]) steep++;
                    }
                Assert.Greater(steep, 0, "на amp-12/freq-0.08 крутизна есть");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void Mouths_OnSlope_WithFlatPad_AndSpread()
        {
            var cfg = Config(1337);
            try
            {
                var mouths = DomeSlope.FindMouths(cfg, 3, 96);
                Assert.GreaterOrEqual(mouths.Count, 2, "устья нашлись (≥2)");
                foreach (var m in mouths)
                {
                    float slope = DomeSlope.SlopeAt(cfg, m.pos.x, m.pos.y);
                    Assert.GreaterOrEqual(slope, 35f, "устье на склоне");
                    Assert.LessOrEqual(slope, 55f, "склон не стена");
                    Assert.Less(m.padSlope, 15f, "площадка ровная");
                    Assert.Less(new Vector2(m.pos.x, m.pos.y).magnitude, 1000f, "устье в круге");
                }
                for (int i = 0; i < mouths.Count; i++)
                    for (int j = i + 1; j < mouths.Count; j++)
                        Assert.GreaterOrEqual(Vector2.Distance(mouths[i].pos, mouths[j].pos), 150f, "устья врозь");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void Nests_RingAroundMouth_AndDeterministic()
        {
            var cfg = Config(1337);
            try
            {
                var mouths = DomeSlope.FindMouths(cfg, 2, 96);
                Assert.Greater(mouths.Count, 0, "есть устья для гнёзд");
                var a = DomeSlope.FindNests(cfg, 4242, mouths, 2);
                var b = DomeSlope.FindNests(cfg, 4242, mouths, 2);
                Assert.AreEqual(a.Count, b.Count, "детерминировано");
                Assert.Greater(a.Count, 0, "гнёзда нашлись");
                for (int i = 0; i < a.Count; i++)
                {
                    Assert.AreEqual(a[i].pos.x, b[i].pos.x, 1e-6f, "позиции бит-в-бит");
                    float d = Vector2.Distance(a[i].pos, mouths[a[i].mouth].pos);
                    Assert.GreaterOrEqual(d, 15f, "гнездо не под дверью");
                    Assert.LessOrEqual(d, 41f, "гнездо у устья");
                }
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }
    }
}
