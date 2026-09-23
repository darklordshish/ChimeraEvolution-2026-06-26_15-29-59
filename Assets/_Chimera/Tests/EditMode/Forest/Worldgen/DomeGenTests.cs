using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Конфиг павильона: дефолты чистые, формулы масштабирования от диаметра
    /// дают числа перепроверки (чанки 8, HM 4096, капли ~556k, свод 1400),
    /// сид разрешается детерминированно. Слайс s10a (спека 2026-09-19-forest-s10).
    /// </summary>
    public class DomeGenTests
    {
        static DomeGenConfigSO Config()
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.randomSeed = false;
            return cfg;
        }

        [Test]
        public void Defaults_AreClean()
        {
            var cfg = Config();
            Assert.IsEmpty(DomeGenRules.CheckConfig(cfg),
                "дефолтный конфиг обязан быть чистым");
        }

        [Test]
        public void Diameter2000_GivesVerifiedNumbers()
        {
            var cfg = Config();
            Assert.AreEqual(8, DomeGenRules.ChunkCount(cfg), "чанки: ceil(2000/250)");
            Assert.AreEqual(4096, DomeGenRules.RecommendHeightmapSize(cfg.mapDiameter), "HM под речку 3м");
            int drops = DomeGenRules.RecommendDropCount(cfg.mapDiameter);
            Assert.GreaterOrEqual(drops, 500000, "капель: коридор 500–650k");
            Assert.LessOrEqual(drops, 650000, "капель: коридор 500–650k");
            Assert.AreEqual(1400f, DomeGenRules.VaultRadius(cfg), 1e-3f, "свод 1.4×R");
            Assert.AreEqual(4000f, DomeGenRules.FarPlane(cfg.mapDiameter), 1e-3f, "far 4×R");
            int trees = DomeGenRules.TreeTarget(cfg);
            Assert.GreaterOrEqual(trees, 35000, "деревья: коридор 35–45k");
            Assert.LessOrEqual(trees, 45000, "деревья: коридор 35–45k");
        }

        [Test]
        public void Diameter1200_ScalesDown()
        {
            Assert.AreEqual(5, Mathf.CeilToInt(1200f / 250f), "санity формулы чанков");
            Assert.AreEqual(2048, DomeGenRules.RecommendHeightmapSize(1200f), "малый диаметр — HM 2048");
        }

        [Test]
        public void BadConfig_IsReported()
        {
            var cfg = Config();
            cfg.mapDiameter = 0f;
            cfg.chunkSize = 10f;
            cfg.octaves = 0;
            cfg.navVoxelSize = 5f;
            cfg.vaultRadiusFactor = 1f;
            var issues = DomeGenRules.CheckConfig(cfg);
            Assert.GreaterOrEqual(issues.Count, 5, "битый конфиг ловится по всем полям");
            Assert.AreEqual(1, DomeGenRules.CheckConfig(null).Count, "null-конфиг — одно нарушение");
        }

        [Test]
        public void ResolveSeed_FixedRepeats_TickFlows()
        {
            var cfg = Config();
            Assert.AreEqual(1337, DomeGenRules.ResolveSeed(cfg, 999), "фикс-сид не зависит от тика");
            cfg.randomSeed = true;
            Assert.AreEqual(4242, DomeGenRules.ResolveSeed(cfg, 4242), "randomSeed пробрасывает тик наружу");
        }
    }
}
