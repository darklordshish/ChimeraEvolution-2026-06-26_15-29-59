using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Свод: лежит на сфере, грани смотрят внутрь (к центру/полу), апекс и кромка
    /// на проектных высотах. Слайс s10c.
    /// </summary>
    public class DomeVaultTests
    {
        [Test]
        public void Vault_LiesOnSphere_AndFacesInward()
        {
            float radius = 1400f;
            var mesh = DomeVault.BuildVaultMesh(radius, 32, 8);
            try
            {
                Assert.AreEqual(32 * 8 * 6, mesh.vertexCount, "фасетка свода");
                var center = new Vector3(0f, DomeVault.CenterY(radius), 0f);
                foreach (var v in mesh.vertices)
                    Assert.AreEqual(radius, Vector3.Distance(v, center), 1f, "вершины на сфере");
                float inward = 0f;
                var normals = mesh.normals;
                var verts = mesh.vertices;
                for (int i = 0; i < normals.Length; i++)
                {
                    Vector3 toCenter = (center - verts[i]).normalized;
                    inward += Vector3.Dot(normals[i], toCenter);
                }
                Assert.Greater(inward / normals.Length, 0.9f, "нормали смотрят внутрь");
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Vault_ApexAndRim_OnDesignHeights()
        {
            float radius = 1400f;
            Assert.AreEqual(700f, DomeVault.CenterY(radius) + radius, 1e-3f, "апекс Rv/2 над полом");
            var rimY = DomeVault.CenterY(radius) + radius * Mathf.Cos(DomeVault.RimTheta);
            Assert.Less(rimY, 0f, "кромка зарыта ниже пола (за кольцом)");
            var rimR = radius * Mathf.Sin(DomeVault.RimTheta);
            Assert.Greater(rimR, 1140f, "кромка шире кольца Rout=1140");
        }
    }
}
