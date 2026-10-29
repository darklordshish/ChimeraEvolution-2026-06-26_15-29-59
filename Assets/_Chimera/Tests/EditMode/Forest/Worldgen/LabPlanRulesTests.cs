using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    public class LabPlanRulesTests
    {
        [Test]
        public void FixedPlan_IsValid()
        {
            var errors = LabPlanRules.Validate(LabPlanFixed.Create());
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void DoorOffSharedWall_IsReported()
        {
            var p = LabPlanFixed.Create();
            var d = p.doors[0];
            d.z = 0f; // далеко от стен lobby/hall
            p.doors[0] = d;
            var errors = LabPlanRules.Validate(p);
            Assert.IsTrue(errors.Exists(e => e.Contains("общей стене")),
                string.Join("\n", errors));
        }

        [Test]
        public void FixedPlan_MatchesB02Numbers()
        {
            var p = LabPlanFixed.Create();
            Assert.AreEqual(46f, p.footprintX, 0.01f);
            Assert.AreEqual(36f, p.footprintZ, 0.01f);
            Assert.AreEqual(14f, p.Room("hall").width, 0.01f);
            Assert.AreEqual(14f, p.Room("hall").depth, 0.01f);
            Assert.AreEqual(6, p.rooms.Count);
            Assert.AreEqual(5, p.doors.Count);
            foreach (var d in p.doors)
            {
                Assert.GreaterOrEqual(d.width, 4f);
                Assert.GreaterOrEqual(d.height, 4f);
            }
        }
    }
}
