using Chimera.Forest.Water;
using NUnit.Framework;

namespace Chimera.Tests.PlayMode
{
    /// <summary>Task 1: типы акведука как данные и выбор по сиду, без геометрии.</summary>
    public class AqueductTypeTests
    {
        [Test]
        public void TwentySeeds_HavePrimaryType_AndCurrentWaterIsSupported()
        {
            Assert.AreEqual(20, AqueductWaterCatalog.Seeds.Count);
            int lake = 0, river = 0, fall = 0, reservoir = 0;
            foreach (var seed in AqueductWaterCatalog.Seeds)
            {
                Assert.IsTrue(AqueductWaterCatalog.TryGet(seed.Seed, out var row), "TryGet: " + seed.Seed);
                Assert.AreEqual(seed.Seed, row.Seed);
                Assert.AreEqual(AqueductWaterCatalog.PrimaryFor(row.Bodies), row.Primary,
                    "primary: seed " + seed.Seed);
                Assert.AreEqual(row.Primary, AqueductWaterCatalog.PlanFor(row.Seed).Type,
                    "plan type: seed " + row.Seed);
                Assert.IsTrue(AqueductWaterCatalog.PlanFor(row.Seed).ShouldBuild,
                    "все текущие 20 сидов — вода: seed " + row.Seed);
                if (row.HasLake) lake++;
                if (row.HasRiver) river++;
                if (row.HasWaterfall) fall++;
                if (row.HasReservoir) reservoir++;
            }
            Assert.AreEqual(20, lake);
            Assert.AreEqual(20, river);
            Assert.AreEqual(11, fall);
            Assert.AreEqual(0, reservoir);
        }

        [Test]
        public void TypeCodes_AreExact()
        {
            Assert.AreEqual("AW-R", AqueductWaterCatalog.CodeOf(AqueductType.River));
            Assert.AreEqual("AW-L", AqueductWaterCatalog.CodeOf(AqueductType.Lake));
            Assert.AreEqual("AW-RS", AqueductWaterCatalog.CodeOf(AqueductType.Reservoir));
            Assert.AreEqual("AW-F", AqueductWaterCatalog.CodeOf(AqueductType.WaterfallSpillway));
            Assert.AreEqual("absent", AqueductWaterCatalog.CodeOf(AqueductType.Absent));
        }

        [Test]
        public void Priority_IsWaterfallReservoirLakeRiverAbsent()
        {
            Assert.AreEqual(AqueductType.WaterfallSpillway,
                AqueductWaterCatalog.PrimaryFor(WaterBodyKind.Waterfall | WaterBodyKind.Reservoir |
                                                 WaterBodyKind.Lake | WaterBodyKind.River));
            Assert.AreEqual(AqueductType.Reservoir,
                AqueductWaterCatalog.PrimaryFor(WaterBodyKind.Reservoir | WaterBodyKind.Lake));
            Assert.AreEqual(AqueductType.Lake,
                AqueductWaterCatalog.PrimaryFor(WaterBodyKind.Lake | WaterBodyKind.River));
            Assert.AreEqual(AqueductType.River,
                AqueductWaterCatalog.PrimaryFor(WaterBodyKind.River));
            Assert.AreEqual(AqueductType.Absent,
                AqueductWaterCatalog.PrimaryFor(WaterBodyKind.None));
        }

        [Test]
        public void Reservoir_WithoutModel_IsDebt_NotLake()
        {
            var noModel = AqueductWaterCatalog.PlanFor(1,
                WaterBodyKind.Reservoir | WaterBodyKind.Lake, false);
            Assert.IsFalse(noModel.ShouldBuild, "AW-RS без модели не строится");
            Assert.AreEqual(AqueductType.Reservoir, noModel.Type, "не подменять озером");
            StringAssert.Contains("no lake fallback", noModel.Reason);

            var withModel = AqueductWaterCatalog.PlanFor(1,
                WaterBodyKind.Reservoir | WaterBodyKind.Lake, true);
            Assert.IsTrue(withModel.ShouldBuild);
            Assert.AreEqual(AqueductType.Reservoir, withModel.Type);
        }
    }
}
