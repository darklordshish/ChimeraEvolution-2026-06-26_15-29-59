using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Вода на данных: впадина заливается до обода, сток идёт вниз, аккумуляция
    /// собирается в устье, русло монотонно и глубже исходного, озеро детектится.
    /// Слайс s10b.
    /// </summary>
    public class DomeWaterTests
    {
        static float[,] PitGrid()
        {
            var h = new float[7, 7];
            for (int ix = 0; ix < 7; ix++)
                for (int iz = 0; iz < 7; iz++)
                    h[ix, iz] = 10f;
            h[3, 3] = 4f;
            h[3, 2] = 8f;
            return h;
        }

        static float[,] SlopeGrid()
        {
            var h = new float[9, 9];
            for (int ix = 0; ix < 9; ix++)
                for (int iz = 0; iz < 9; iz++)
                    h[ix, iz] = 20f - ix;
            return h;
        }

        [Test]
        public void Pit_IsFilledToRim()
        {
            var h = PitGrid();
            var filled = DomeWater.PriorityFlood(h, 0.01f);
            Assert.Greater(filled[3, 3], h[3, 3], "впадина поднята");
            Assert.LessOrEqual(filled[3, 3], 10f + 0.1f, "заливка не выше обода");
            Assert.AreEqual(h[0, 0], filled[0, 0], 1e-6f, "вне впадин поле не тронуто");
        }

        [Test]
        public void Slope_DrainsToEdge_AndAccumulates()
        {
            var h = SlopeGrid();
            var filled = DomeWater.PriorityFlood(h, 0.01f);
            var dir = DomeWater.FlowDir(filled);
            Assert.AreEqual(0, dir[4, 4], "на склоне сток по +x (D8=0)");
            var path = DomeWater.TraceRiver(dir, 2, 4);
            Assert.Greater(path.Count, 2, "трек идёт до границы");
            Assert.AreEqual(8, path[path.Count - 1].x, "трек кончается на границе x=8");
            var acc = DomeWater.Accumulation(filled, dir);
            Assert.Greater(acc[8, 4], acc[2, 4], "аккумуляция растёт к устью");
        }

        [Test]
        public void Carve_IsMonotonic_AndBelowOriginal()
        {
            var h = SlopeGrid();
            var filled = DomeWater.PriorityFlood(h, 0.01f);
            var dir = DomeWater.FlowDir(filled);
            var path = DomeWater.TraceRiver(dir, 1, 4);
            var carved = DomeWater.CarveRiver(h, path, 1.5f, 0.5f, 0.01f);
            for (int i = 1; i < path.Count; i++)
                Assert.Less(carved[path[i].x, path[i].y], carved[path[i - 1].x, path[i - 1].y],
                    $"русло монотонно вниз в точке {i}");
            foreach (var cell in path)
                Assert.Less(carved[cell.x, cell.y], h[cell.x, cell.y], "русло глубже исходного");
        }

        [Test]
        public void Lake_DetectedInPit_NotOnRim()
        {
            var h = PitGrid();
            var filled = DomeWater.PriorityFlood(h, 0.01f);
            var lake = DomeWater.LakeMask(h, filled, 0.01f);
            Assert.IsTrue(lake[3, 3], "центр впадины — озеро");
            Assert.IsFalse(lake[0, 0], "угол — не озеро");
            Assert.IsFalse(lake[3, 0], "обод — не озеро");
        }

        [Test]
        public void Flood_Deterministic()
        {
            var a = DomeWater.PriorityFlood(PitGrid(), 0.01f);
            var b = DomeWater.PriorityFlood(PitGrid(), 0.01f);
            for (int ix = 0; ix < 7; ix++)
                for (int iz = 0; iz < 7; iz++)
                    Assert.AreEqual(a[ix, iz], b[ix, iz], "заливка детерминирована");
        }
    }
}
