using NUnit.Framework;

namespace Chimera.Tests.EditMode
{
    public class LabPlanGeneratorTests
    {
        [Test]
        public void SameSeed_IsReproducible()
        {
            var a = LabPlanGenerator.Generate(9, 1, LabEntrance.Underground, LabStyle.InSlope);
            var b = LabPlanGenerator.Generate(9, 1, LabEntrance.Underground, LabStyle.InSlope);
            Assert.AreEqual(a.template, b.template);
            Assert.AreEqual(a.rooms.Count, b.rooms.Count);
            Assert.AreEqual(a.doors.Count, b.doors.Count);
            Assert.AreEqual(a.Room("surgery").x, b.Room("surgery").x, 1e-4f);
        }

        [Test]
        public void Seeds_ProduceOnlyValidPlans()
        {
            for (long seed = 1; seed <= 24; seed++)
            {
                var p = LabPlanGenerator.Generate(seed, 1, LabEntrance.Underground, LabStyle.InSlope);
                var errors = LabPlanRules.Validate(p);
                Assert.IsEmpty(errors, $"сид {seed}: {string.Join("; ", errors)}");
            }
        }

        [Test]
        public void DeepWings_IsValidAndKeepsB02Depth()
        {
            LabPlan deep = null;
            for (long seed = 1; seed <= 64 && deep == null; seed++)
            {
                var p = LabPlanGenerator.Generate(seed, 1, LabEntrance.Underground, LabStyle.InSlope);
                if (p.template == "deep-wings") deep = p;
            }
            Assert.IsNotNull(deep, "на 64 сидах должен найтись deep-wings");
            Assert.IsEmpty(LabPlanRules.Validate(deep), "deep-wings обязан проходить правила");
            Assert.AreEqual(19f, deep.Room("surgery").depth, 0.01f);
            Assert.AreEqual(19f, deep.Room("cells").depth, 0.01f);
        }

        [Test]
        public void Mirror_SwapsSurgeryAndCells()
        {
            LabPlan mirror = null;
            for (long seed = 1; seed <= 64 && mirror == null; seed++)
            {
                var p = LabPlanGenerator.Generate(seed, 1, LabEntrance.Underground, LabStyle.InSlope);
                if (p.template == "mirror") mirror = p;
            }
            Assert.IsNotNull(mirror, "на 64 сидах должен найтись mirror");
            Assert.Greater(mirror.Room("surgery").x, 0f, "хирургия справа");
            Assert.Less(mirror.Room("cells").x, 0f, "клетки слева");
        }
    }
}
