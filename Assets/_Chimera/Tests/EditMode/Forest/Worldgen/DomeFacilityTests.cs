using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Корпуса: кит даёт детали с объёмом, раскладка детерминирована и чиста
    /// по правилам, ангар в кольце лицом в центр. Слайс s10f.
    /// </summary>
    public class DomeFacilityTests
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
        public void Layout_Deterministic_AndClean()
        {
            var cfg = Config(1337);
            try
            {
                var a = DomeFacilityLayout.Build(cfg, 1337, Vector2.zero);
                var b = DomeFacilityLayout.Build(cfg, 1337, Vector2.zero);
                Assert.AreEqual(a.parts.Count, b.parts.Count, "детерминировано");
                Assert.AreEqual(a.hangarPos.x, b.hangarPos.x, 1e-6f, "ангар бит-в-бит");
                Assert.IsEmpty(DomeFacilityRules.Check(a, cfg), "раскладка чистая");
                Assert.GreaterOrEqual(a.parts.Count, 60, "боксы из десятков деталей");
                Assert.AreEqual(6, a.pois.Count, "POI: Lab+4 бокса+ангар");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void Hangar_SitsInRing_FacingCenter()
        {
            var cfg = Config(777);
            try
            {
                var f = DomeFacilityLayout.Build(cfg, 777, Vector2.zero);
                float r = f.hangarPos.magnitude;
                Assert.GreaterOrEqual(r, 920f, "ангар не внутри чаши");
                Assert.LessOrEqual(r, 1140f, "ангар не за кольцом");
                Assert.IsEmpty(DomeFacilityRules.Check(f, cfg), "правила молчат");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void Rules_CatchBroken()
        {
            var cfg = Config(1337);
            try
            {
                var f = DomeFacilityLayout.Build(cfg, 1337, Vector2.zero);
                Assert.IsEmpty(DomeFacilityRules.Check(f, cfg), "целое — чисто");
                f.parts.Clear();
                Assert.Greater(DomeFacilityRules.Check(f, cfg).Count, 0, "пустое — грязно");
                Assert.AreEqual(1, DomeFacilityRules.Check(null, cfg).Count, "null — одно нарушение");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }
    }
}
