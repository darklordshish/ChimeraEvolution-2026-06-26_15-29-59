using Chimera.Forest.Water;
using NUnit.Framework;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// Task 1: выбор «где ставится / absent» как данные. Это ещё не посадка:
    /// prefab, опоры, terrain и NavMesh не создаются.
    /// </summary>
    public class AqueductPlacementTests
    {
        [Test]
        public void TwentySeeds_SelectWaterType_AndNoneIsAbsent()
        {
            int built = 0, absent = 0;
            foreach (var row in AqueductWaterCatalog.Seeds)
            {
                var plan = AqueductWaterCatalog.PlanFor(row.Seed);
                Assert.AreEqual(row.Seed, plan.Seed);
                Assert.AreEqual(row.Primary, plan.Type, "тип по сиду: " + row.Seed);
                Assert.IsTrue(plan.ShouldBuild, "вода есть: " + row.Seed);
                built++;
            }
            Assert.AreEqual(20, built, "все 20 текущих сидов имеют воду");
            Assert.AreEqual(0, absent, "текущая выборка не даёт absent");

            var none = AqueductWaterCatalog.PlanFor(7, WaterBodyKind.None);
            Assert.IsFalse(none.ShouldBuild);
            Assert.AreEqual(AqueductType.Absent, none.Type);
            Assert.AreEqual("no water body", none.Reason);
        }

        [Test]
        public void UnknownSeed_IsAbsent()
        {
            var plan = AqueductWaterCatalog.PlanFor(999);
            Assert.IsFalse(plan.ShouldBuild);
            Assert.AreEqual(AqueductType.Absent, plan.Type);
            StringAssert.Contains("not in catalog", plan.Reason);
        }

        [Test]
        public void LakeAndRiver_DoNotFallbackToReservoir()
        {
            var lake = AqueductWaterCatalog.PlanFor(1,
                WaterBodyKind.Lake | WaterBodyKind.River);
            Assert.IsTrue(lake.ShouldBuild);
            Assert.AreEqual(AqueductType.Lake, lake.Type);

            var river = AqueductWaterCatalog.PlanFor(1, WaterBodyKind.River);
            Assert.IsTrue(river.ShouldBuild);
            Assert.AreEqual(AqueductType.River, river.Type);

            var reservoir = AqueductWaterCatalog.PlanFor(1, WaterBodyKind.Reservoir);
            Assert.IsFalse(reservoir.ShouldBuild, "AW-RS без модели — долг");
            Assert.AreEqual(AqueductType.Reservoir, reservoir.Type);
        }

        [Test]
        public void WaterfallWins_OverLakeAndReservoir()
        {
            var plan = AqueductWaterCatalog.PlanFor(2,
                WaterBodyKind.Waterfall | WaterBodyKind.Lake | WaterBodyKind.River);
            Assert.IsTrue(plan.ShouldBuild);
            Assert.AreEqual(AqueductType.WaterfallSpillway, plan.Type);
        }
    }
}
