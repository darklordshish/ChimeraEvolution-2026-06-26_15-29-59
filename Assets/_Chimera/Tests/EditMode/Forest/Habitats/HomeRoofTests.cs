using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Крыша логова: отдельный счётчик (не популяция), полная крыша блокирует очередь,
    /// Release освобождает, IsFull считает всё, смерть популяции крыш не трогает.
    /// Слайс s6 (ветка forest/s6-home-return).
    /// </summary>
    public class HomeRoofTests
    {
        [Test]
        public void Reserve_Idempotent()
        {
            var go = new GameObject("~Lair");
            try
            {
                var site = go.AddComponent<LairSite>();
                site.capacity = 4;
                Assert.IsTrue(site.TryReserve(101), "первый резерв держится");
                Assert.AreEqual(1, site.ReservedCount, "счётчик крыш = 1");
                site.TryReserve(101);
                Assert.AreEqual(1, site.ReservedCount, "повтор того же — no-op (идемпотентен)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void FullRoof_BlocksConsumeSpawn()
        {
            var go = new GameObject("~Lair");
            try
            {
                var site = go.AddComponent<LairSite>();
                site.capacity = 2;
                Assert.IsTrue(site.TryReserve(201), "крыша 1 при пустом доме");
                Assert.IsTrue(site.TryReserve(202), "крыша 2 при пустом доме");
                site.pendingSpawns = 1;
                Assert.IsTrue(site.IsFull, "IsFull считает резерв (0+1+2)");
                Assert.AreEqual(0, site.ConsumeSpawn(), "занятые тела держат очередь");
                Assert.AreEqual(1, site.pendingSpawns, "очередь цела");
                site.Release(201);
                site.Release(202);
                Assert.AreEqual(1, site.ConsumeSpawn(), "после Release очередь идёт");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ReportDeath_DoesNotTouchRoof()
        {
            var go = new GameObject("~Lair");
            try
            {
                var site = go.AddComponent<LairSite>();
                site.capacity = 4;
                site.population = 1;
                site.TryReserve(301);
                site.ReportDeath();
                Assert.AreEqual(0, site.population, "популяция упала");
                Assert.AreEqual(1, site.ReservedCount, "крыша цела (смерть вернувшегося — только Release)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Release_UnknownId_IsNoOp()
        {
            var go = new GameObject("~Lair");
            try
            {
                var site = go.AddComponent<LairSite>();
                site.capacity = 4;
                site.Release(999);
                Assert.AreEqual(0, site.ReservedCount, "чужой Release — no-op");
                Assert.IsFalse(site.IsFull, "пустой дом не полон");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
