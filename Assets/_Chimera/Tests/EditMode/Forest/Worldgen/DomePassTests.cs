using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Связность: лаборатория достаёт до боксов и ангара; вода/стены запирают,
    /// детерминировано. Слайс s10f.
    /// </summary>
    public class DomePassTests
    {
        static DomeGenConfigSO Config(long seed)
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.seed = seed;
            cfg.randomSeed = false;
            cfg.mapDiameter = 2000f;
            return cfg;
        }

        static string Poi(DomeFacilityLayout.Facility f, string id)
        {
            foreach (var p in f.pois)
                if (p.id == id) return p.pos.ToString("F0");
            return "?";
        }

        [Test]
        public void Lab_ReachesWings_AndHangar()
        {
            var cfg = Config(1337);
            try
            {
                var hydro = DomeHydro.Build(cfg, 128);
                var f = DomeFacilityLayout.Build(cfg, 1337, Vector2.zero);
                var blocked = DomePass.BlockedGrid(cfg, hydro, f, 128);
                foreach (var id in new[] { "Arena", "Cells", "Surgery", "Rest" })
                {
                    Vector2 target = Target(f, id);
                    Assert.IsTrue(DomePass.Connected(blocked, cfg.mapDiameter, f.labCenter, target),
                        $"лаборатория → {id} ({Poi(f, id)})");
                }
                Vector2 hangar = Target(f, "Hangar");
                Assert.IsTrue(DomePass.Connected(blocked, cfg.mapDiameter, f.labCenter, hangar),
                    $"лаборатория → ангар ({Poi(f, "Hangar")})");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        static Vector2 Target(DomeFacilityLayout.Facility f, string id)
        {
            foreach (var p in f.pois)
                if (p.id == id) return p.pos;
            return f.labCenter;
        }

        [Test]
        public void Water_Blocks_AndOpenPasses()
        {
            var blocked = new bool[8, 8];
            for (int i = 0; i < 8; i++) blocked[3, i] = true; // река-стена
            Assert.IsFalse(DomePass.Connected(blocked, 100f, new Vector2(-40f, 0f), new Vector2(40f, 0f)),
                "стена запирает");
            Assert.IsTrue(DomePass.Connected(new bool[8, 8], 100f, new Vector2(-40f, 0f), new Vector2(40f, 0f)),
                "пустое проходимо");
        }
    }
}
