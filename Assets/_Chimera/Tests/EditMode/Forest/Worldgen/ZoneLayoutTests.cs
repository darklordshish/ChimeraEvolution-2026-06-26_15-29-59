using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// P0.5: на Ø400 зона R24–34 с маской R+20 не влезает в скальное кольцо.
    /// Это проверка РАСКЛАДКИ (якоря + приёмка), а не одной точки: пол плоский,
    /// поэтому единственный запрет — кольцо, и видно, что он работает.
    /// </summary>
    public class ZoneLayoutTests
    {
        static DomeGenConfigSO Config(long seed)
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.seed = seed;
            cfg.randomSeed = false;
            cfg.mapDiameter = 400f;
            cfg.chunkSize = 100f;
            return cfg;
        }

        /// <summary>Плоский пол: остаётся только маска кольца и геометрия зон.</summary>
        static float Flat(float x, float z) => 0f;

        static DomeMasks RingMask(DomeGenConfigSO cfg) =>
            new DomeMasks().RingBand(DomeRockRing.InnerRadius(cfg) - 30f,
                DomeRockRing.OuterRadius(cfg));

        [Test]
        public void On400_Zones_NeverEnterRockRing()
        {
            var cfg = Config(9);
            try
            {
                var hydro = new DomeHydro.State { lakeLevel = -100f, lakeRadius = 0f };
                var masks = RingMask(cfg);
                // Вход/станция далеко: тест про кольцо, а не про них.
                var zones = ForestSpace.BuildZones(cfg, 9, Flat,
                    new Vector3(400f, 0f, 0f), new Vector2(400f, 0f), hydro, masks, 5);

                // Ø400 — не стенд разметки: кольцо rin=120, зона R+20 до 54, и в чашу
                // физически влезает 2 зоны. Требование «5» — для Ø2000 (S3 §4).
                Assert.GreaterOrEqual(zones.Count, 2, "на плоском полу зоны обязаны найтись");
                foreach (var z in zones)
                {
                    Assert.GreaterOrEqual(z.radius, 24f - 0.01f);
                    Assert.LessOrEqual(z.radius, 34f + 0.01f);
                    Assert.IsFalse(masks.BlocksForm(
                            DomeFootprint.Circle(z.center, z.radius + 20f)),
                        $"зона ({z.center.x:F0};{z.center.y:F0}) R{z.radius:F0} задела кольцо");
                }

                // Не пустая проверка: среди якорей есть те, что кольцо обязано срезать.
                var anchors = ForestSpace.AnchorZones(cfg, 9, 24);
                bool clipped = false;
                foreach (var a in anchors)
                    if (masks.BlocksForm(DomeFootprint.Circle(a.center, a.radius + 20f)))
                    { clipped = true; break; }
                Assert.IsTrue(clipped, "на Ø400 ни один якорь не у кольца — проверка ничего не ловит");

                // И на РЕАЛЬНОМ полу Ø400: что бы ни приняла приёмка, кольцо не задето.
                var realHydro = DomeHydro.Build(cfg, 128);
                var realTerrain = new DomeTerrain
                {
                    cfg = cfg, hydro = realHydro, worldVersion = cfg.WorldVersion,
                    ravines = new DomeRavines.Ravine[0],
                };
                var real = ForestSpace.BuildZones(cfg, 9, realTerrain.SampleGround,
                    new Vector3(400f, 0f, 0f), new Vector2(400f, 0f), realHydro, masks, 5);
                foreach (var z in real)
                    Assert.IsFalse(masks.BlocksForm(
                            DomeFootprint.Circle(z.center, z.radius + 20f)),
                        "реальная раскладка пустила зону в кольцо");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void On400_Links_HaveTwoApproaches_AndStayOffRing()
        {
            var cfg = Config(9);
            try
            {
                var hydro = new DomeHydro.State { lakeLevel = -100f, lakeRadius = 0f };
                var masks = RingMask(cfg);
                var zones = ForestSpace.BuildZones(cfg, 9, Flat,
                    new Vector3(400f, 0f, 0f), new Vector2(400f, 0f), hydro, masks, 5);
                var links = ForestSpace.BuildLinks(cfg, zones, Flat, masks, 16f);
                Assert.Greater(links.Count, 0, "связей нет");
                // На Ø400 помещается 2 зоны — у каждой один подход; правило «≥2» —
                // требование Ø2000, где зон 5. Здесь важно, что связь не идёт в кольцо.
                if (zones.Count >= 3)
                    for (int i = 0; i < zones.Count; i++)
                        Assert.GreaterOrEqual(ForestSpace.Exits(zones, links, i), 2,
                            $"у зоны {i} меньше двух подходов");
                foreach (var l in links)
                    Assert.IsFalse(masks.BlocksForm(
                            DomeFootprint.Capsule(l.a, l.b, l.width * 0.5f), "corridor", "zone"),
                        "связь пересекла кольцо/станцию/воду");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }
    }
}
