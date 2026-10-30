using NUnit.Framework;

namespace Chimera.Tests.EditMode
{
    public class LabPlanBspTests
    {
        [Test]
        public void TwentySeeds_AreValid_AndDeterministic()
        {
            for (long seed = 1; seed <= 20; seed++)
            {
                var a = LabPlanBsp.Create(seed);
                var b = LabPlanBsp.Create(seed);
                Assert.AreEqual(a.rooms.Count, b.rooms.Count, $"сид {seed}: число комнат");
                Assert.AreEqual(a.doors.Count, b.doors.Count, $"сид {seed}: число дверей");
                for (int i = 0; i < a.rooms.Count; i++)
                    Assert.AreEqual(a.rooms[i].x, b.rooms[i].x, 1e-4f,
                        $"сид {seed}: комната {a.rooms[i].id} не детерминирована");

                var errors = LabPlanRules.Validate(a);
                Assert.IsEmpty(errors, $"сид {seed}: {string.Join("; ", errors)}");
                Assert.AreEqual(6, a.rooms.Count, $"сид {seed}: комнат");
                Assert.AreEqual(5, a.doors.Count, $"сид {seed}: дверей");
            }
        }
    }
}
