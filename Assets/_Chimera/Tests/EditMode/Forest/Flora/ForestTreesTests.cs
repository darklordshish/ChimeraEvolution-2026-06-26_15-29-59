using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Срез «деревья и подлесок»: породы по весам, а не k%3; новые меши имеют объём
    /// и плоские нормали. Счётчики и детерминизм scatter сторожат DomeFloraTests.
    /// </summary>
    public class ForestTreesTests
    {
        [Test]
        public void KindFor_UsesPortfolioWeights_NotThirdDead()
        {
            int broadleaf = 0, conifer = 0, umbrella = 0, dead = 0, birch = 0;
            for (int k = 0; k < 2000; k++)
            {
                var kind = DomeFlora.KindFor(1337, k, 2f);
                switch (kind)
                {
                    case FloraKind.PikeTree: broadleaf++; break;
                    case FloraKind.SpruceTree: conifer++; break;
                    case FloraKind.UmbrellaTree: umbrella++; break;
                    case FloraKind.DeadHook: dead++; break;
                    case FloraKind.BirchTree: birch++; break;
                }
            }
            Assert.That(broadleaf / 2000f, Is.InRange(0.36f, 0.49f), "широколист ~40–45%");
            Assert.That(conifer / 2000f, Is.InRange(0.16f, 0.29f), "хвоя ~20–25%");
            Assert.That(umbrella / 2000f, Is.InRange(0.08f, 0.18f), "зонтик — акцент");
            Assert.That(dead / 2000f, Is.InRange(0.06f, 0.14f), "сухостой — не треть леса");
            Assert.That(birch, Is.GreaterThan(0), "берёза есть");
        }

        [Test]
        public void KindFor_IsDeterministic()
        {
            for (int k = 0; k < 100; k++)
                Assert.AreEqual(DomeFlora.KindFor(4, k, 1f), DomeFlora.KindFor(4, k, 1f),
                    "тот же сид и номер — та же порода");
        }

        [Test]
        public void NewTreeMeshes_HaveVolumeAndNormals()
        {
            var meshes = new[]
            {
                FloraMeshKit.BroadleafCrown(4f, 6.9f),
                FloraMeshKit.BirchCrown(2f, 5f),
                FloraMeshKit.PineCrown(3.6f, 12.5f),
                FloraMeshKit.Snag(0.4f, 0.05f, 13.5f, 2f),
                FloraMeshKit.BushNest(0.7f, 5),
                FloraMeshKit.GrassTuft(0.12f, 0.7f),
            };
            try
            {
                foreach (var mesh in meshes)
                {
                    Assert.Greater(mesh.vertexCount, 0, "меш не пуст");
                    Assert.AreEqual(mesh.vertices.Length, mesh.normals.Length, "нормали на все вершины");
                    Assert.Greater(mesh.bounds.size.x, 0.1f, "объём по X");
                    Assert.Greater(mesh.bounds.size.y, 0.1f, "объём по Y");
                    Assert.Greater(mesh.bounds.size.z, 0.1f, "объём по Z");
                }
            }
            finally
            {
                foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
            }
        }
    }
}
