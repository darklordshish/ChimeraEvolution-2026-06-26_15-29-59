using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Поле павильона детерминировано, в ±amplitude, версия участвует в хэше,
    /// warp меняет форму. Слайс s10a.
    /// </summary>
    public class DomeHeightTests
    {
        static DomeGenConfigSO Config(long seed)
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.seed = seed;
            cfg.randomSeed = false;
            return cfg;
        }

        [Test]
        public void SameSeed_SameGridHash()
        {
            Assert.AreEqual(
                DomeHeightField.SampleGridHash(Config(1337), 16, 2f),
                DomeHeightField.SampleGridHash(Config(1337), 16, 2f),
                "один сид дважды — то же поле");
        }

        [Test]
        public void DifferentSeeds_DifferentGridHash()
        {
            Assert.AreNotEqual(
                DomeHeightField.SampleGridHash(Config(1337), 16, 2f),
                DomeHeightField.SampleGridHash(Config(777), 16, 2f),
                "разные сиды — разное поле");
        }

        [Test]
        public void Heights_WithinAmplitude()
        {
            var cfg = Config(1337);
            for (int iz = 0; iz < 32; iz++)
                for (int ix = 0; ix < 32; ix++)
                {
                    float h = DomeHeightField.SampleHeight(cfg, ix * 4f - 64f, iz * 4f - 64f);
                    Assert.IsTrue(Mathf.Abs(h) <= cfg.amplitude + 1e-4f,
                        $"высота {h} вне ±amplitude в клетке ({ix},{iz})");
                }
        }

        [Test]
        public void Version_ParticipatesInHash()
        {
            var a = Config(1337);
            var b = Config(1337);
            b.version = 2;
            Assert.AreNotEqual(
                DomeHeightField.SampleGridHash(a, 16, 2f),
                DomeHeightField.SampleGridHash(b, 16, 2f),
                "смена версии обязана дать другую карту");
        }

        [Test]
        public void Warp_ChangesShape()
        {
            var a = Config(1337);
            var b = Config(1337);
            b.warpStrength = 0f;
            Assert.AreNotEqual(
                DomeHeightField.SampleGridHash(a, 16, 2f),
                DomeHeightField.SampleGridHash(b, 16, 2f),
                "выключенный warp обязан дать другое поле");
        }
    }
}
