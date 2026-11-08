using Chimera.Forest.Water;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;

namespace Chimera.Tests.PlayMode
{
    /// <summary>Task 3: вода акведука и сохранность озера/реки.</summary>
    public class AqueductWaterTests
    {
        [UnityTest]
        public IEnumerator Seed1337_HasAqueductWater_AndLakeRiverStay()
        {
            var host = new GameObject("~AqueductWaterGate");
            host.SetActive(false);
            var b = ForestProfiles.PavilionV2(host, 1337L, erosion: false);
            b.buildThermalErosion = true;
            host.SetActive(true);
            try
            {
                b.Build();
                Assert.AreEqual(1, b.Aqueducts, "акведук должен быть один");
                var root = b.BuiltRoot.transform;
                Assert.IsNull(root.Find("CanyonAuthorGraded"), "старый aqueduct-меш не должен дублироваться");
                Transform aq = null;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("Aqueduct_")) { aq = t; break; }
                Assert.IsNotNull(aq, "нет объекта Aqueduct_");
                var water = aq.Find("Water");
                Assert.IsNotNull(water, "нет Water у акведука");
                Assert.IsTrue(water.gameObject.activeInHierarchy, "wet: вода включена");
                AqueductPlacement.SetWater(aq.gameObject, false);
                Assert.IsFalse(water.gameObject.activeInHierarchy, "dry: вода выключена");
                AqueductPlacement.SetWater(aq.gameObject, true);

                Assert.IsNotNull(b.Hydro, "Hydro");
                Assert.IsTrue(b.Hydro.lakeRadius > 0f, "озеро не удалено");
                Assert.IsTrue(b.Hydro.riverPts != null && b.Hydro.riverPts.Count >= 2, "река не удалена");
                Assert.IsNotNull(GameObject.Find("Lake"), "меш озера не удалён");
                Assert.IsNotNull(GameObject.Find("River"), "меш реки не удалён");
                var aqBounds = new Bounds(aq.position, Vector3.zero);
                bool first = true;
                foreach (var r in aq.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r.enabled) continue;
                    if (first) { aqBounds = r.bounds; first = false; }
                    else aqBounds.Encapsulate(r.bounds);
                }
                Assert.LessOrEqual(Mathf.Abs(aqBounds.min.y - b.SampleGround(aq.position.x, aq.position.z)), 0.5f,
                    "опоры должны стоять на terrain, без виса");
            }
            finally
            {
                b.Clear();
                Object.Destroy(host);
            }
            yield return null;
        }
    }
}
