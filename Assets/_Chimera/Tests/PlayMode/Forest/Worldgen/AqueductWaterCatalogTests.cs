using System.Collections;
using System.Collections.Generic;
using Chimera.Forest.Water;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    public class AqueductWaterCatalogTests
    {
        [Test]
        public void Catalog_HasTwentySeeds_AndTypeCounts()
        {
            Assert.AreEqual(20, AqueductWaterCatalog.Seeds.Count, "20 сидов в каталоге");
            var seeds = new HashSet<int>();
            int lake = 0, river = 0, fall = 0, reservoir = 0, absent = 0, primaryF = 0, primaryL = 0;
            foreach (var row in AqueductWaterCatalog.Seeds)
            {
                Assert.IsTrue(seeds.Add(row.Seed), "сид повторяется: " + row.Seed);
                Assert.GreaterOrEqual(row.Seed, 1);
                Assert.LessOrEqual(row.Seed, 20);
                Assert.AreEqual(AqueductWaterCatalog.PrimaryFor(row.Bodies), row.Primary,
                    $"primary не совпадает с телом воды: seed {row.Seed}");
                if (row.HasLake) lake++;
                if (row.HasRiver) river++;
                if (row.HasWaterfall) fall++;
                if (row.HasReservoir) reservoir++;
                if (row.Primary == AqueductType.Absent) absent++;
                if (row.Primary == AqueductType.WaterfallSpillway) primaryF++;
                if (row.Primary == AqueductType.Lake) primaryL++;
            }
            Assert.AreEqual(20, lake, "озеро на всех 20");
            Assert.AreEqual(20, river, "река на всех 20");
            Assert.AreEqual(11, fall, "водопад/сброс на 11");
            Assert.AreEqual(0, reservoir, "водохранилища в v2 нет");
            Assert.AreEqual(0, absent, "нет сида без воды в текущей выборке");
            Assert.AreEqual(11, primaryF, "AW-F primary");
            Assert.AreEqual(9, primaryL, "AW-L primary");
        }

        [Test]
        public void Absent_IsOnlyWhenNoWater()
        {
            Assert.AreEqual(AqueductType.Absent, AqueductWaterCatalog.PrimaryFor(WaterBodyKind.None));
            Assert.AreEqual(AqueductType.River, AqueductWaterCatalog.PrimaryFor(WaterBodyKind.River));
            Assert.AreEqual(AqueductType.Lake, AqueductWaterCatalog.PrimaryFor(WaterBodyKind.Lake));
            Assert.AreEqual(AqueductType.Reservoir, AqueductWaterCatalog.PrimaryFor(WaterBodyKind.Reservoir));
            Assert.AreEqual(AqueductType.WaterfallSpillway,
                AqueductWaterCatalog.PrimaryFor(WaterBodyKind.Waterfall));
            Assert.AreEqual(AqueductType.WaterfallSpillway,
                AqueductWaterCatalog.PrimaryFor(WaterBodyKind.Lake | WaterBodyKind.Waterfall),
                "водопад имеет приоритет над озером");
            Assert.AreEqual(AqueductType.Lake,
                AqueductWaterCatalog.PrimaryFor(WaterBodyKind.River | WaterBodyKind.Lake),
                "озеро имеет приоритет над рекой");
        }

        [Test]
        public void Reservoir_IsExplicitlyAbsentInCurrentV2()
        {
            foreach (var row in AqueductWaterCatalog.Seeds)
            {
                Assert.IsFalse(row.HasReservoir, "reservoir отсутствует: seed " + row.Seed);
                Assert.AreEqual("not modeled in v2", row.ReservoirReason);
            }
        }

        [UnityTest, Category("Slow"), Timeout(900000)]
        public IEnumerator TwentySeeds_MatchLiveHydro_AndLakeRiverDoNotChange()
        {
            foreach (var row in AqueductWaterCatalog.Seeds)
            {
                var host = new GameObject("~AqueductWaterVerify" + row.Seed);
                host.SetActive(false);
                var b = ForestProfiles.PavilionV2(host, row.Seed, erosion: false);
                b.buildThermalErosion = true;
                b.buildHydraulicErosion = false;
                host.SetActive(true);
                try
                {
                    b.Build();
                    Assert.AreEqual(1, b.Aqueducts, "акведук не поставлен: seed " + row.Seed);
                    var h = b.Hydro;
                    var c = b.Canyon;
                    Assert.IsNotNull(h, "Hydro: seed " + row.Seed);
                    Assert.IsTrue(h.lakeRadius > 0f, "lake: seed " + row.Seed);
                    Assert.AreEqual(row.LakeRadius, h.lakeRadius, 0.01f, "lake radius: seed " + row.Seed);
                    Assert.AreEqual(row.LakeLevel, h.lakeLevel, 0.01f, "lake level: seed " + row.Seed);
                    Assert.AreEqual(row.LakeCenter.x, h.lakeCenter.x, 0.01f, "lake X: seed " + row.Seed);
                    Assert.AreEqual(row.LakeCenter.y, h.lakeCenter.y, 0.01f, "lake Z: seed " + row.Seed);

                    Assert.IsTrue(h.riverPts != null && h.riverPts.Count >= 2, "river: seed " + row.Seed);
                    float len = 0f;
                    for (int i = 1; i < h.riverPts.Count; i++)
                        len += Vector2.Distance(h.riverPts[i - 1], h.riverPts[i]);
                    Assert.AreEqual(row.RiverLength, len, 0.1f, "river length: seed " + row.Seed);
                    var mid = h.riverPts[h.riverPts.Count / 2];
                    Assert.AreEqual(row.RiverMid.x, mid.x, 0.01f, "river mid X: seed " + row.Seed);
                    Assert.AreEqual(row.RiverMid.y, mid.y, 0.01f, "river mid Z: seed " + row.Seed);
                    float bed = h.riverBed[h.riverBed.Count / 2];
                    Assert.AreEqual(row.RiverLevel, bed + 0.25f, 0.01f, "river level: seed " + row.Seed);

                    bool fall = c.valid && c.policy == CanyonPolicy.Wet && c.fallHeight > 0f;
                    Assert.AreEqual(row.HasWaterfall, fall, "waterfall flag: seed " + row.Seed);
                    if (fall)
                    {
                        Assert.AreEqual(row.FallHeight, c.fallHeight, 0.1f, "fall height: seed " + row.Seed);
                        Assert.AreEqual(row.FallMouth.x, c.mouth.x, 0.1f, "fall mouth X: seed " + row.Seed);
                        Assert.AreEqual(row.FallMouth.y, c.mouth.y, 0.1f, "fall mouth Z: seed " + row.Seed);
                        Assert.AreEqual(row.FallLip.x, c.lip.x, 0.1f, "fall lip X: seed " + row.Seed);
                        Assert.AreEqual(row.FallLip.y, c.lip.y, 0.1f, "fall lip Z: seed " + row.Seed);
                    }
                    Assert.IsFalse(row.HasReservoir, "reservoir: seed " + row.Seed);
                }
                finally
                {
                    b.Clear();
                    Object.Destroy(host);
                }
                yield return null;
            }
        }
    }
}
