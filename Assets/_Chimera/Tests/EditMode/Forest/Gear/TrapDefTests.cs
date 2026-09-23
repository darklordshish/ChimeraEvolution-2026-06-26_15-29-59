using NUnit.Framework;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Деф ловушки: валидный чист, битый ловится. Слайс s4c.
    /// </summary>
    public class TrapDefTests
    {
        static TrapDef Good()
        {
            return new TrapDef { id = "snare", damage = 12, bleedStacks = 2, slowStacks = 1, radius = 2f, uses = 1 };
        }

        [Test]
        public void Valid_IsClean()
        {
            Assert.IsEmpty(TrapDef.Validate(Good()), "корректный силок чист");
        }

        [Test]
        public void Bad_IsReported()
        {
            var bad = Good();
            bad.damage = -1;
            bad.radius = 0f;
            bad.uses = 0;
            Assert.GreaterOrEqual(TrapDef.Validate(bad).Count, 3, "урон −1, радиус 0, заряды 0 — всё ловится");
            Assert.IsNotEmpty(TrapDef.Validate(null), "null ловится");
        }
    }
}
