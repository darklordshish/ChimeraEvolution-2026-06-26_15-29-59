using System.Collections;
using Chimera.Forest.Water;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>Task 4: четыре ракурса A1–A4 существуют и смотрят в объект.</summary>
    public class AqueductCameraTests
    {
        [UnityTest]
        public IEnumerator FourPresets_AreFinite_AndLookAtObject()
        {
            var host = new GameObject("~AqueductCameraGate");
            host.SetActive(false);
            var b = ForestProfiles.PavilionV2(host, 1337L, erosion: false);
            b.buildThermalErosion = true;
            host.SetActive(true);
            try
            {
                b.Build();
                Transform aq = null;
                foreach (var t in b.BuiltRoot.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("Aqueduct_")) { aq = t; break; }
                Assert.IsNotNull(aq, "нет акведука для камер");
                var shots = AqueductCameraRig.Presets(aq.gameObject);
                Assert.AreEqual(4, shots.Length, "A1–A4");
                for (int i = 0; i < shots.Length; i++)
                {
                    Assert.AreEqual((AqueductShotId)i, shots[i].Id, "порядок A1–A4");
                    Assert.IsTrue(IsFinite(shots[i].Position), "position finite: " + shots[i].Id);
                    Assert.IsTrue(IsFinite(shots[i].Target), "target finite: " + shots[i].Id);
                    Assert.Greater(Vector3.Distance(shots[i].Position, shots[i].Target), 1f,
                        "камера не в точке цели: " + shots[i].Id);
                }
            }
            finally
            {
                b.Clear();
                Object.Destroy(host);
            }
            yield return null;
        }

        static bool IsFinite(Vector3 v) =>
            !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
            !float.IsNaN(v.y) && !float.IsInfinity(v.y) &&
            !float.IsNaN(v.z) && !float.IsInfinity(v.z);
    }
}
