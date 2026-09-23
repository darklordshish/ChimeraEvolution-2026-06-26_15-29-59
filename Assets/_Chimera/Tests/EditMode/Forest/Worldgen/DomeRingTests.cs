using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Кольцо: лента Rin…Rout, пик в коридоре, внутренняя кромка сходится с полем,
    /// нормали вверх (обратный обход). Слайс s10a.
    /// </summary>
    public class DomeRingTests
    {
        static DomeGenConfigSO Config(long seed)
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.seed = seed;
            cfg.randomSeed = false;
            cfg.mapDiameter = 2000f;
            return cfg;
        }

        [Test]
        public void Ring_RadiiAndPeak_FromDiameter()
        {
            var cfg = Config(1337);
            try
            {
                Assert.AreEqual(920f, DomeRockRing.InnerRadius(cfg), 1e-3f, "Rin = R−80");
                Assert.AreEqual(1140f, DomeRockRing.OuterRadius(cfg), 1e-3f, "Rout = R+140");
                float maxPeak = 0f;
                for (int i = 0; i < 64; i++)
                {
                    float a = i / 64f * Mathf.PI * 2f;
                    float h = DomeRockRing.HeightAt(cfg, a, 0.5f);
                    if (h > maxPeak) maxPeak = h;
                }
                Assert.GreaterOrEqual(maxPeak, cfg.ringPeakHeight * 0.9f, "где-то гребень near пика");
                Assert.LessOrEqual(maxPeak, cfg.ringPeakHeight * 1.01f, "пик не выше заданного");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void Ring_MeetsField_AndFacesUp()
        {
            var cfg = Config(1337);
            var mesh = DomeRockRing.BuildRingMesh(cfg, 32, 4);
            try
            {
                Assert.AreEqual(32 * 4 * 6, mesh.vertexCount, "фасетка ленты");
                float rin = DomeRockRing.InnerRadius(cfg);
                for (int i = 0; i < 16; i++)
                {
                    float a = i / 16f * Mathf.PI * 2f;
                    float expected = DomeHeightField.SampleHeight(cfg, Mathf.Cos(a) * rin, Mathf.Sin(a) * rin);
                    Assert.AreEqual(expected, DomeRockRing.HeightAt(cfg, a, 0f), 1e-3f,
                        "внутренняя кромка = высота поля");
                }
                float upSum = 0f;
                foreach (var n in mesh.normals) upSum += n.y;
                Assert.Greater(upSum / mesh.normals.Length, 0.5f, "нормали смотрят вверх, не вниз");
            }
            finally
            {
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void Ring_Deterministic()
        {
            var a = Config(1337);
            var b = Config(1337);
            try
            {
                Assert.AreEqual(DomeRockRing.HeightAt(a, 1.23f, 0.4f), DomeRockRing.HeightAt(b, 1.23f, 0.4f),
                    "то же (сид, угол, t) — та же высота");
                var c = Config(777);
                try
                {
                    Assert.AreNotEqual(DomeRockRing.HeightAt(a, 1.23f, 0.4f), DomeRockRing.HeightAt(c, 1.23f, 0.4f),
                        "другой сид — другое кольцо");
                }
                finally
                {
                    Object.DestroyImmediate(c);
                }
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }
    }
}
