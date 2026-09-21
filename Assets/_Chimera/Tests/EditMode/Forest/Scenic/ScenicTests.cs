using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Карта леса: детерминизм цветов, размеры, маркировка крутизны,
    /// согласованность с WorldRules. Хэш — по цветам, не по байтам PNG.
    /// Слайс s5 (ветка forest/s5-forest-map-shots, спека 2026-09-18-forest-s5).
    /// </summary>
    public class ScenicTests
    {
        static WorldGenConfigSO Config(long seed)
        {
            var cfg = ScriptableObject.CreateInstance<WorldGenConfigSO>();
            cfg.seed = seed;
            return cfg;
        }

        [Test]
        public void SameSeed_SameColors()
        {
            var a = ForestMapBuilder.BuildColors(Config(1337), 16, 2f);
            var b = ForestMapBuilder.BuildColors(Config(1337), 16, 2f);
            Assert.AreEqual(ForestMapBuilder.ColorsHash(a), ForestMapBuilder.ColorsHash(b),
                "тот же сид — те же цвета карты");
        }

        [Test]
        public void DifferentSeeds_DifferentColors()
        {
            var a = ForestMapBuilder.BuildColors(Config(1337), 16, 2f);
            var b = ForestMapBuilder.BuildColors(Config(987654321L), 16, 2f);
            Assert.AreNotEqual(ForestMapBuilder.ColorsHash(a), ForestMapBuilder.ColorsHash(b),
                "иначе детектор пропустит константную заглушку");
        }

        [Test]
        public void Highland_IsNotSnow()
        {
            Color peak = ForestMapBuilder.ReliefColor(1f);
            Assert.Less(peak.grayscale, 0.7f, "верх — сухая охра, не снег (кость отменена s10d)");
            Assert.Greater(peak.r, peak.b, "верх тёплый (r > b), не ледяной");
            Color valley = ForestMapBuilder.ReliefColor(0f);
            Assert.Greater(valley.g, valley.r, "низ — зелёный");
        }

        [Test]
        public void Dims_MatchResolution()
        {
            var colors = ForestMapBuilder.BuildColors(Config(7), 24, 2f);
            var mask = ForestMapBuilder.BuildSteepMask(Config(7), 24, 2f);
            Assert.AreEqual(24 * 24, colors.Length, "цветов — res²");
            Assert.AreEqual(24 * 24, mask.Length, "маска — res²");
        }

        [Test]
        public void SteepCells_Marked()
        {
            var flat = Config(11);
            flat.amplitude = 0f;
            var flatMask = ForestMapBuilder.BuildSteepMask(flat, 16, 1f);
            int flatCount = 0;
            foreach (var m in flatMask) if (m) flatCount++;
            Assert.AreEqual(0, flatCount, "плоский мир — 0 крутых");

            var steep = Config(12);
            steep.amplitude = 12f;
            steep.baseFrequency = 0.08f;
            steep.maxSlopeDegrees = 10f;
            var steepMask = ForestMapBuilder.BuildSteepMask(steep, 32, 1f);
            int steepCount = 0;
            foreach (var m in steepMask) if (m) steepCount++;
            Assert.Greater(steepCount, 0, "крутой мир обязан помечаться");
        }

        [Test]
        public void Stats_ConsistentWithRules()
        {
            var cfg = Config(1337);
            var stats = ForestMapBuilder.BuildStats(cfg, 32, 1f);
            var issues = WorldRules.CheckGrid(cfg, 32, 1f);
            Assert.IsEmpty(issues, "дефолтный конфиг чист по правилам");
            Assert.AreEqual(0, stats.steepCells, "и статистика подтверждает: крутых нет");
            Assert.LessOrEqual(stats.minH, stats.maxH, "min ≤ max");
        }
    }
}
