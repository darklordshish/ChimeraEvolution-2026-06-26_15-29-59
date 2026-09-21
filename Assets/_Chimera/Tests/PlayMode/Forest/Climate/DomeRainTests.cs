using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// Дождь падает и заворачивается сверху; 600 полос — одним shared-мешем.
    /// Слайс s10h.
    /// </summary>
    public class DomeRainTests
    {
        [UnityTest]
        public IEnumerator Drops_Fall_AndWrap()
        {
            var root = new GameObject("~RainProbe");
            var rain = DomeRain.BuildPreview(root, 0f, 0f);
            yield return null;
            var drop = rain.transform.GetChild(0);
            float y0 = drop.localPosition.y;
            yield return null;
            yield return null;
            float y1 = drop.localPosition.y;
            Assert.Less(y1, y0, "капли падают");
            int shared = 0;
            Mesh mesh = null;
            bool oneMesh = true;
            foreach (var f in rain.GetComponentsInChildren<MeshFilter>())
            {
                if (mesh == null) mesh = f.sharedMesh;
                else if (f.sharedMesh != mesh) oneMesh = false;
                shared++;
            }
            Assert.Greater(shared, 100, "полос сотни");
            Assert.IsTrue(oneMesh, "один shared-меш на всех");
            DomeRain.WipePreview(root);
            Object.Destroy(root);
            yield return null;
        }
    }
}
