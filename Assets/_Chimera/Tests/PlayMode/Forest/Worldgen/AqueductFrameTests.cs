using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.PlayMode
{
    /// <summary>Task 5: четыре кадра A1–A4 и подпись type/seed/camera.</summary>
    public class AqueductFrameTests
    {
        static readonly string[] Files =
        {
            "A1-along-AW-F-1337.png",
            "A2-3-4-AW-F-1337.png",
            "A3-from-water-AW-F-1337.png",
            "A4-supports-AW-F-1337.png",
        };

        [Test]
        public void FourFrames_Exist_AndAreNonTrivial()
        {
            string dir = Path.GetFullPath("Docs/Диаграммы/Лес/Кадры/17-Aqueduct");
            Assert.IsTrue(Directory.Exists(dir), "нет папки 17-Aqueduct");
            foreach (var name in Files)
            {
                string path = Path.Combine(dir, name);
                Assert.IsTrue(File.Exists(path), "нет кадра: " + name);
                var bytes = File.ReadAllBytes(path);
                Assert.Greater(bytes.Length, 100000, "кадр слишком мал: " + name);
                var tex = new Texture2D(2, 2);
                try
                {
                    Assert.IsTrue(tex.LoadImage(bytes), "не PNG: " + name);
                    Assert.GreaterOrEqual(tex.width, 1200, "ширина: " + name);
                    Assert.GreaterOrEqual(tex.height, 700, "высота: " + name);
                    var px = tex.GetPixels32();
                    int blue = 0, nonUniform = 0;
                    int r0 = px[0].r, g0 = px[0].g, b0 = px[0].b;
                    foreach (var p in px)
                    {
                        if (p.b > p.r + 15 && p.b > p.g + 5) blue++;
                        if (Mathf.Abs(p.r - r0) + Mathf.Abs(p.g - g0) + Mathf.Abs(p.b - b0) > 12)
                            nonUniform++;
                    }
                    Assert.Greater(nonUniform, px.Length / 10, "кадр почти пустой: " + name);
                    Assert.Greater(blue, 0, "нет воды/неба? " + name);
                }
                finally
                {
                    Object.DestroyImmediate(tex);
                }
            }
            Assert.IsTrue(File.Exists(Path.Combine(dir, "README.md")), "нет README кадров");
        }
    }
}
