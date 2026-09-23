using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// NavMesh тайлами собирается и держит углы карты; после себя чисто.
    /// Малый диаметр проверяет plumbing (полный Ø2000 — витрина s10i). Слайс s10a.
    /// </summary>
    public class DomeNavTests
    {
        [UnityTest]
        public IEnumerator TiledBake_HoldsCorners_AndWipes()
        {
            DomePreview.BuildNavPreview("500", "8");
            yield return null;
            try
            {
                Assert.IsNotNull(DomePreview.NavSurface, "поверхность создана");
                Assert.IsNotNull(DomePreview.NavSurface.navMeshData, "бейк запёкся");
                foreach (var corner in new[] { -240f, 240f })
                {
                    Vector3 pos;
                    Assert.IsTrue(DomePreview.SampleNav(corner, corner, out pos),
                        $"угол ({corner},{corner}) на NavMesh");
                    Assert.IsTrue(DomePreview.SampleNav(-corner, corner, out pos),
                        $"угол ({-corner},{corner}) на NavMesh");
                }
            }
            finally
            {
                DomePreview.WipeNavPreview();
            }
            Assert.IsNull(DomePreview.NavRoot, "после Wipe корень снесён");
            Assert.IsNull(DomePreview.NavSurface, "после Wipe поверхность снесена");
        }
    }
}
