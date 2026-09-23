using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Мир леса детерминирован: сид → то же поле высот; правила ловят битый конфиг.
    /// Слайс s1 (ветка forest/s1-worldgen-seed-relief, спека 2026-09-18-forest-s1).
    /// </summary>
    public class WorldGenDeterminismTests
    {
        static WorldGenConfigSO Config(long seed)
        {
            var cfg = ScriptableObject.CreateInstance<WorldGenConfigSO>();
            cfg.seed = seed;
            return cfg;
        }

        [Test]
        public void SameSeed_SameGridHash()
        {
            var a = Config(1337);
            var b = Config(1337);
            Assert.AreEqual(
                WorldHeightField.SampleGridHash(a, 16, 2f),
                WorldHeightField.SampleGridHash(b, 16, 2f),
                "один сид дважды обязан дать то же поле (WorldHeightField)");
        }

        [Test]
        public void DifferentSeeds_DifferentGridHash()
        {
            var a = Config(1337);
            var b = Config(987654321L);
            Assert.AreNotEqual(
                WorldHeightField.SampleGridHash(a, 16, 2f),
                WorldHeightField.SampleGridHash(b, 16, 2f),
                "разные сиды обязаны дать разное поле");
        }

        [Test]
        public void Heights_WithinAmplitudeAndSlope()
        {
            var cfg = Config(1337);
            var issues = WorldRules.CheckGrid(cfg, 32, 1f);
            Assert.IsEmpty(issues,
                "сетка 32×32 шаг 1м: высоты в ±amplitude, уклоны в лимите — " + string.Join("; ", issues));
        }

        [Test]
        public void BadConfig_IsReported()
        {
            var cfg = Config(1);
            cfg.octaves = 0;
            var issues = WorldRules.CheckConfig(cfg);
            Assert.IsNotEmpty(issues, "octaves=0 обязан ловить WorldRules.CheckConfig, а не молчать");
        }

        [Test]
        public void Version_ParticipatesInHash()
        {
            var a = Config(1337);
            a.version = 1;
            var b = Config(1337);
            b.version = 2;
            Assert.AreNotEqual(
                WorldHeightField.SampleGridHash(a, 8, 4f),
                WorldHeightField.SampleGridHash(b, 8, 4f),
                "смена version обязана менять поле: тот же сид на новом алгоритме — другая карта");
        }
    }
}
