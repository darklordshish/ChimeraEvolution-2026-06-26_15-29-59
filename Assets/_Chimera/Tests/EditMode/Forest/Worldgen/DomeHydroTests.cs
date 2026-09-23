using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Гидрология на реальном поле (сид 1337): озеро находится, река монотонна,
    /// русло ниже исходного поля, дно озера ниже уровня, всё детерминировано.
    /// Слайс s10b.
    /// </summary>
    public class DomeHydroTests
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
        public void Lake_FoundWithSaneSize_AndBedBelowLevel()
        {
            var cfg = Config(1337);
            var s = DomeHydro.Build(cfg, 128);
            try
            {
                Assert.Greater(s.lakeRadius, 20f, "озеро не лужа");
                Assert.LessOrEqual(s.lakeRadius, 150f, "радиус капается spec max");
                Assert.Less(new Vector2(s.lakeCenter.x, s.lakeCenter.y).magnitude, 1000f, "озеро внутри карты");
                float bed = DomeHydro.SampleHydro(cfg, s, s.lakeCenter.x, s.lakeCenter.y);
                Assert.Less(bed, s.lakeLevel, "дно по центру ниже уровня воды");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void River_IsMonotonic_AndCarved()
        {
            var cfg = Config(1337);
            var s = DomeHydro.Build(cfg, 128);
            try
            {
                Assert.GreaterOrEqual(s.riverPts.Count, 2, "река из ≥2 точек");
                float len = 0f;
                for (int i = 1; i < s.riverPts.Count; i++)
                    len += Vector2.Distance(s.riverPts[i - 1], s.riverPts[i]);
                Assert.GreaterOrEqual(len, 100f, "река не лужа (≥100м на сетке 128)");
                Assert.LessOrEqual(len, 650f, "река в коридоре спеки (устье ≤600м + шаг)");
                for (int i = 1; i < s.riverBed.Count; i++)
                    Assert.Less(s.riverBed[i], s.riverBed[i - 1], $"русло монотонно в точке {i}");
                int mid = s.riverPts.Count / 2;
                float carved = DomeHydro.SampleHydro(cfg, s, s.riverPts[mid].x, s.riverPts[mid].y);
                float orig = DomeHeightField.SampleHeight(cfg, s.riverPts[mid].x, s.riverPts[mid].y);
                Assert.Less(carved, orig, "русло вырезано ниже исходного поля");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void Hydro_Deterministic()
        {
            var a = Config(1337);
            var b = Config(1337);
            try
            {
                var sa = DomeHydro.Build(a, 64);
                var sb = DomeHydro.Build(b, 64);
                Assert.AreEqual(sa.lakeCenter.x, sb.lakeCenter.x, 1e-6f, "центр озера детерминирован");
                Assert.AreEqual(sa.lakeLevel, sb.lakeLevel, 1e-6f, "уровень детерминирован");
                Assert.AreEqual(sa.riverPts.Count, sb.riverPts.Count, "длина реки детерминирована");
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }
    }
}
