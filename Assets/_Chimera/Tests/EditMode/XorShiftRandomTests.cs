using Chimera.Laws;
using NUnit.Framework;

namespace Chimera.Tests.EditMode
{
    /// <summary>Детерминизм случайности законов (план Ф0, С1): последовательность `XorShiftRandom(42)` одна в игре и в
    /// консоли `Tools/Sim` (она печатает те же пять чисел).</summary>
    public class XorShiftRandomTests
    {
        static readonly uint[] Seed42 = { 11355432u, 2836018348u, 476557059u, 3648046016u, 3759983556u };

        [Test]
        public void Seed42_GivesPinnedSequence()
        {
            var r = new XorShiftRandom(42);
            for (int i = 0; i < Seed42.Length; i++) Assert.AreEqual(Seed42[i], r.NextUInt(), $"число {i}");
        }

        [Test]
        public void SameSeed_SameStream_DifferentSeed_Differs()
        {
            var a = new XorShiftRandom(7); var b = new XorShiftRandom(7); var c = new XorShiftRandom(8);
            bool differs = false;
            for (int i = 0; i < 20; i++)
            {
                float x = a.Next01();
                Assert.AreEqual(x, b.Next01());
                if (x != c.Next01()) differs = true;
            }
            Assert.IsTrue(differs);
        }

        [Test]
        public void Next01_InUnitRange_AndRangeInBounds()
        {
            var r = new XorShiftRandom(123);
            for (int i = 0; i < 1000; i++)
            {
                float f = r.Next01();
                Assert.GreaterOrEqual(f, 0f); Assert.Less(f, 1f);
                int n = r.Range(-3, 4);
                Assert.GreaterOrEqual(n, -3); Assert.Less(n, 4);
            }
        }

        [Test]
        public void ZeroSeed_DoesNotStick()
        {
            var r = new XorShiftRandom(0);
            Assert.AreNotEqual(0u, r.NextUInt());
        }
    }
}
