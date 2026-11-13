using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    public class DomeRavinesTests
    {
        [Test]
        public void Apply_EmptyList_IsIdentity()
        {
            var empty = new List<DomeRavines.Ravine>();
            Assert.AreEqual(10f, DomeRavines.Apply(10f, empty, 0f, 0f, 0f), 1e-4f,
                "пустой список не меняет сушу");
            Assert.AreEqual(-3f, DomeRavines.Apply(-3f, empty, 0f, 0f, 0f), 1e-4f,
                "пустой список не поднимает дно озера к воде");
        }

        [Test]
        public void Apply_DeepensCenter_ClampsToWater_AndLeavesOutside()
        {
            var list = new List<DomeRavines.Ravine>
            {
                new DomeRavines.Ravine
                {
                    center = Vector2.zero, angleDeg = 0f,
                    length = 20f, width = 5f, depth = 3f,
                },
            };
            float center = DomeRavines.Apply(10f, list, 0f, 0f, 0f);
            Assert.Less(center, 10f, "центр оврага ниже поля");
            Assert.GreaterOrEqual(center, 0.45f, "дно не ниже воды");
            Assert.AreEqual(10f, DomeRavines.Apply(10f, list, 100f, 0f, 0f), 1e-4f,
                "вне оврага высота не меняется");
        }

        [Test]
        public void Contains_CenterTrue_OutsideFalse()
        {
            var list = new List<DomeRavines.Ravine>
            {
                new DomeRavines.Ravine
                {
                    center = Vector2.zero, angleDeg = 0f,
                    length = 20f, width = 5f, depth = 3f,
                },
            };
            Assert.IsTrue(DomeRavines.Contains(list, 0f, 0f), "центр внутри");
            Assert.IsTrue(DomeRavines.Contains(list, 2f, 0f, 1.2f), "край внутри с запасом");
            Assert.IsFalse(DomeRavines.Contains(list, 40f, 0f), "далеко снаружи");
        }

        [Test]
        public void Build_Seed9_IsDeterministic_AndAwayFromLab()
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.mapDiameter = 400f;
            cfg.chunkSize = 100f;
            cfg.seed = 9;
            var hydro = DomeHydro.Build(cfg, 96);
            Vector2 lab = new Vector2(89f, 67f);
            Vector2 entry = new Vector2(-87f, 50f);
            var a = DomeRavines.Build(cfg, 9, lab, entry, hydro, 120f, 3);
            var b = DomeRavines.Build(cfg, 9, lab, entry, hydro, 120f, 3);
            Assert.Greater(a.Count, 0, "на сиде 9 овраги есть");
            Assert.AreEqual(a.Count, b.Count, "детерминизм по числу");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].center, b[i].center, "тот же центр");
                Assert.Greater(Vector2.Distance(a[i].center, lab), 55f,
                    "овраг не под лабораторией");
                Assert.Greater(Vector2.Distance(a[i].center, entry), 35f,
                    "овраг не у входа");
            }
            Object.DestroyImmediate(cfg);
        }
    }
}
