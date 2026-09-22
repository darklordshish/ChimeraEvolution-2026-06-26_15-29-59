using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Флора купола: детерминизм, min-distance, гейты (вода/потолок/круг),
    /// пустые входы — пусто. Слайс s10i.
    /// </summary>
    public class DomeFloraTests
    {
        static (float h, bool ok) Open(float x, float z) => (0f, true);

        [Test]
        public void Scatter_Deterministic_AndKeepsDistance()
        {
            var a = DomeFlora.Scatter(1337, 111, 60, 200f, 5f, Open);
            var b = DomeFlora.Scatter(1337, 111, 60, 200f, 5f, Open);
            Assert.AreEqual(a.Count, b.Count, "детерминировано");
            Assert.AreEqual(60, a.Count, "в открытую — полный набор");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].pos.x, b[i].pos.x, 1e-6f, "бит-в-бит");
                for (int j = i + 1; j < a.Count; j++)
                {
                    float dx = a[i].pos.x - a[j].pos.x, dz = a[i].pos.z - a[j].pos.z;
                    Assert.GreaterOrEqual(dx * dx + dz * dz, 25f - 1e-3f, "дистанция");
                }
            }
        }

        [Test]
        public void Scatter_RespectsGate_AndCircle()
        {
            var pts = DomeFlora.Scatter(777, 111, 200, 100f, 2f, (x, z) =>
                (0f, x > 0f)); // только восточная половина
            Assert.Greater(pts.Count, 0, "не пусто");
            foreach (var p in pts)
            {
                Assert.Greater(p.pos.x, 0f, "гейт держит");
                Assert.LessOrEqual(new Vector2(p.pos.x, p.pos.z).magnitude, 50f, "в круге");
            }
        }

        [Test]
        public void Scatter_BadInput_Empty()
        {
            Assert.AreEqual(0, DomeFlora.Scatter(1, 1, 0, 100f, 2f, Open).Count, "count 0");
            Assert.AreEqual(0, DomeFlora.Scatter(1, 1, 10, 100f, 0f, Open).Count, "дистанция 0");
            Assert.AreEqual(0, DomeFlora.Scatter(1, 1, 10, 100f, 2f, (x, z) => (0f, false)).Count, "все закрыты");
        }
    }
}
