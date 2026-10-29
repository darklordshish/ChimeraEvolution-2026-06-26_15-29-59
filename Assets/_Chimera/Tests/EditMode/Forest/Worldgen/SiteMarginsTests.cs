using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    public class SiteMarginsTests
    {
        [Test]
        public void TwentySeeds_StationHasMargins_NoFallback()
        {
            for (long seed = 1; seed <= 20; seed++)
            {
                var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
                try
                {
                    cfg.seed = seed;
                    cfg.randomSeed = false;
                    cfg.mapDiameter = 2000f;
                    var hydro = DomeHydro.Build(cfg, 128);
                    var spot = DomeLabSite.Find(cfg, seed, new Vector2(-900f, 0f), hydro,
                        LabStyle.Bastion, 150f, LabEntrance.Buried,
                        attempts: 512, preferBest: true, lattice: true);

                    Assert.IsTrue(spot.found, $"сид {seed}: место не найдено");
                    Assert.IsFalse(spot.fallback, $"сид {seed}: сработал непроверенный fallback");
                    Assert.GreaterOrEqual(spot.margins.min, 0f,
                        $"сид {seed}: минимальная маржа отрицательна ({spot.margins.min:F2})");
                }
                finally
                {
                    Object.DestroyImmediate(cfg);
                }
            }
        }
    }
}
