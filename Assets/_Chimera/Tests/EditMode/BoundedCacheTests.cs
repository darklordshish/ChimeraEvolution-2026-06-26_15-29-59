using System.Collections.Generic;
using NUnit.Framework;

namespace Chimera.Tests.EditMode
{
    /// <summary>КЭШ С ПОТОЛКОМ (стена рендереров, 01.10): вытесняется давно не использованное, свежепрочитанное живёт,
    /// вытесненное уходит в `onEvict`.</summary>
    public class BoundedCacheTests
    {
        [Test]
        public void EvictsLeastRecentlyUsed_AndReportsIt()
        {
            var evicted = new List<int>();
            var c = new BoundedCache<string, int>(2, evicted.Add);
            c["a"] = 1; c["b"] = 2;
            Assert.IsTrue(c.TryGetValue("a", out _));   // «a» свежее «b»
            c["c"] = 3;
            CollectionAssert.AreEqual(new[] { 2 }, evicted, "вытеснено не давно не использованное");
            Assert.IsFalse(c.TryGetValue("b", out _));
            Assert.IsTrue(c.TryGetValue("a", out var a) && a == 1);
            Assert.AreEqual(2, c.Count);
        }
    }
}
