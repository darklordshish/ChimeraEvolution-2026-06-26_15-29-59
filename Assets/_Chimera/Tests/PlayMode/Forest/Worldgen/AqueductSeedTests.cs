using Chimera.Forest.Water;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.PlayMode
{
    /// <summary>Task 3: 20 сидов — тип/посадка/отсутствие по каталогу.</summary>
    public class AqueductSeedTests
    {
        [Test]
        public void TwentySeeds_HavePlan_AndAbsentIsNotBuilt()
        {
            int water = 0, absent = 0;
            foreach (var row in AqueductWaterCatalog.Seeds)
            {
                var plan = AqueductWaterCatalog.PlanFor(row.Seed);
                Assert.AreEqual(row.Primary, plan.Type, "seed " + row.Seed);
                Assert.IsTrue(plan.ShouldBuild, "вода есть: " + row.Seed);
                Assert.AreNotEqual(AqueductType.Absent, plan.Type, "seed " + row.Seed);
                water++;
            }
            Assert.AreEqual(20, water);
            var none = AqueductWaterCatalog.PlanFor(1, WaterBodyKind.None);
            Assert.IsFalse(none.ShouldBuild);
            Assert.AreEqual(AqueductType.Absent, none.Type);
            absent++;
            Assert.AreEqual(1, absent);
        }

        [Test]
        public void Seed1337_World_HasAqueductWaterfall()
        {
            var host = new GameObject("~AqueductSeedWorld");
            host.SetActive(false);
            var b = ForestProfiles.PavilionV2(host, 1337L, erosion: false);
            b.buildThermalErosion = true;
            host.SetActive(true);
            try
            {
                b.Build();
                Assert.AreEqual(1, b.Aqueducts);
                Transform aq = null;
                foreach (var t in b.BuiltRoot.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("Aqueduct_")) { aq = t; break; }
                Assert.IsNotNull(aq);
                StringAssert.Contains("AW-F", aq.name);
            }
            finally
            {
                b.Clear();
                Object.Destroy(host);
            }
        }
    }
}
