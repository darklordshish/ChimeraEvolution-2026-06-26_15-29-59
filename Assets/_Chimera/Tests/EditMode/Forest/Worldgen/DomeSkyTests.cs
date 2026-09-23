using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Небо: полдень — солнце в зените и день; полночь — ночь и луна вверху;
    /// туман гуще ночью + пики на рассвете/закате; звёзды на сфере, 2 ступени.
    /// Слайс s10c.
    /// </summary>
    public class DomeSkyTests
    {
        [Test]
        public void Noon_IsDay_SunHigh()
        {
            var s = DomeCelestial.StateAt(10.5f);
            Assert.AreEqual(1f, s.dayT, 1e-4f, "полдень — полный день");
            Assert.Greater(s.sunDir.y, 0.8f, "солнце в зените");
            Assert.Less(s.fogDensity, 0.002f, "днём туман жидкий (масштаб Ø2000)");
        }

        [Test]
        public void Midnight_IsNight_MoonUp()
        {
            var s = DomeCelestial.StateAt(25.5f);
            Assert.AreEqual(0f, s.dayT, 1e-4f, "полночь — ночь");
            Assert.Greater(s.moonDir.y, 0.5f, "луна вверху");
            Assert.Less(s.sunDir.y, 0f, "солнце под горизонтом");
            Assert.Greater(s.fogDensity, 0.001f, "ночью туман гуще дневного");
        }

        [Test]
        public void DawnDusk_PeakFog_AndRampDay()
        {
            var dawn = DomeCelestial.StateAt(0.5f);
            var dusk = DomeCelestial.StateAt(20.5f);
            var noon = DomeCelestial.StateAt(10.5f);
            Assert.Greater(dawn.fogDensity, noon.fogDensity, "рассветный пик тумана");
            Assert.Greater(dusk.fogDensity, noon.fogDensity, "закатный пик тумана");
            Assert.Less(dawn.dayT, 1f, "на рассвете день только занимается");
            Assert.Greater(dusk.duskT, 0.5f, "на закате пояс виден");
            Assert.AreEqual(0f, noon.duskT, 1e-4f, "в полдень пояса нет");
            Assert.AreEqual(0f, DomeCelestial.StateAt(25.5f).duskT, 1e-4f, "ночью пояса нет");
        }

        [Test]
        public void Stars_OnSphere_TwoSteps()
        {
            var center = new Vector3(0f, -700f, 0f);
            var mesh = DomeStars.BuildStarMesh(1337, 100, center, 1400f, 6f);
            try
            {
                Assert.AreEqual(100 * 8 * 3, mesh.vertexCount, "100 октаэдров");
                foreach (var v in mesh.vertices)
                    Assert.AreEqual(1400f, Vector3.Distance(v, center), 30f, "звёзды на сфере ± размер");
                float minR = 2f, maxR = -1f;
                foreach (var c in mesh.colors)
                {
                    Assert.AreEqual(c.r, c.g, 1e-6f, "звёзды нейтральные (r==g)");
                    Assert.LessOrEqual(c.r, c.b, "синеватый оттенок (tint ≤ 1)");
                    if (c.r < minR) minR = c.r;
                    if (c.r > maxR) maxR = c.r;
                }
                Assert.Less(minR, 0.6f, "тусклая ступень присутствует");
                Assert.Greater(maxR, 0.9f, "яркая ступень присутствует");
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
