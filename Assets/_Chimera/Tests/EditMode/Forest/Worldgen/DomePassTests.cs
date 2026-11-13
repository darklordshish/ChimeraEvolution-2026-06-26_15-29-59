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

        /// <summary>
        /// Крылья лаборатории достижимы, а ангар — ЗА кольцом: с S3 кольцо стало
        /// маской, и связность сквозь скалу больше не проходит. Это правило, а не регресс.
        /// </summary>
        [Test]
        public void Lab_ReachesWings_AndRingSealsHangar()
        {
            var cfg = Config(1337);
            try
            {
                var hydro = DomeHydro.Build(cfg, 128);
                var f = DomeFacilityLayout.Build(cfg, 1337, Vector2.zero);
                // S3 §2.4: связность мерится по полу мира (Hydro0 + формы), а не по H0.
                var terrain = new DomeTerrain
                {
                    cfg = cfg,
                    hydro = hydro,
                    worldVersion = cfg.WorldVersion,
                    ravines = new DomeRavines.Ravine[0],
                };
                float rin = DomeRockRing.InnerRadius(cfg);
                var blocked = DomePass.BlockedGrid(cfg, hydro, f, 128,
                    terrain.SampleGround, rin);
                foreach (var id in new[] { "Arena", "Cells", "Surgery", "Rest" })
                {
                    Vector2 target = Target(f, id);
                    Assert.IsTrue(DomePass.Connected(blocked, cfg.mapDiameter, f.labCenter, target),
                        $"лаборатория → {id} ({Poi(f, id)})");
                }
                Vector2 hangar = Target(f, "Hangar");
                Assert.Greater(hangar.magnitude, rin,
                    "ангар по замыслу за кольцом — тест про стену, а не про ангар");
                Assert.IsFalse(DomePass.Connected(blocked, cfg.mapDiameter, f.labCenter, hangar),
                    $"ангар ({Poi(f, "Hangar")}) за кольцом не должен быть достижим пешком");
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

        /// <summary>
        /// S3 §3.2: кольцо — маска. Раньше `BlockedGrid` кольца не знал и связность
        /// проходила сквозь скальную стену.
        /// </summary>
        [Test]
        public void Ring_BlocksEveryCellBehindInnerRadius()
        {
            var cfg = Config(1337);
            try
            {
                var hydro = DomeHydro.Build(cfg, 96);
                var terrain = new DomeTerrain
                {
                    cfg = cfg, hydro = hydro, worldVersion = cfg.WorldVersion,
                    ravines = new DomeRavines.Ravine[0],
                };
                float rin = DomeRockRing.InnerRadius(cfg);
                int res = 128;
                var blocked = DomePass.BlockedGrid(cfg, hydro, null, res,
                    terrain.SampleGround, rin);
                float cell = cfg.mapDiameter / res;
                // Вне кольца — блок; внутри решение принимает рельеф/вода, не кольцо.
                for (int ix = 0; ix < res; ix++)
                    for (int iz = 0; iz < res; iz++)
                    {
                        float x = -cfg.mapDiameter / 2 + (ix + 0.5f) * cell;
                        float z = -cfg.mapDiameter / 2 + (iz + 0.5f) * cell;
                        if (new Vector2(x, z).magnitude < rin + 5f) continue;
                        if (new Vector2(x, z).magnitude > cfg.mapDiameter / 2f) continue;
                        Assert.IsTrue(blocked[ix, iz], $"клетка за rin не заблокирована: {x:F0};{z:F0}");
                    }
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
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
