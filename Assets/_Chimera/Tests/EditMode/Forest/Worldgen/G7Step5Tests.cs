using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// G7 шаг 5: три параметра и материал. v2 меняет ясень (крона/опоры), кольцо
    /// (ярусы/осыпь), воду (полка берега) и полосы склона (10–18° слито с травой).
    /// v1-путь этих изменений не видит.
    /// </summary>
    public class G7Step5Tests
    {
        static DomeGenConfigSO Config()
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.seed = 1337;
            cfg.randomSeed = false;
            cfg.mapDiameter = 2000f;
            cfg.chunkSize = 250f;
            cfg.worldVersion = 2;
            return cfg;
        }

        static Material Mat() => new Material(Shader.Find("Universal Render Pipeline/Lit"));

        [Test]
        public void Ash_Gate_V2HasRaisedCrownBranchesAndNestSupports()
        {
            var cfg = Config();
            var bark = Mat(); var crown = Mat(); var owl = Mat();
            var parent = new GameObject("~AshGate");
            try
            {
                var root = DomeAshSite.BuildGeometry(parent.transform, Vector3.zero,
                    bark, crown, owl, null, 1f, stylized: true);
                var names = new HashSet<string>();
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) names.Add(t.name);
                CollectionAssert.IsSupersetOf(names,
                    new[] { "AshCrownLobeA", "AshCrownLobeB", "AshCrownLobeC",
                        "AshBranchA", "AshBranchB", "AshBranchC",
                        "AshNestSupportW", "AshNestSupportE" },
                    "v2-ясень остался грибом: нет долей кроны/ветвей/опор");
                Assert.GreaterOrEqual(DomeAshSite.ScaleFor(cfg, DomeSchemes.LakeCauldron),
                    0.1f, "ясень не получил параметр схемы");
            }
            finally
            {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(bark); Object.DestroyImmediate(crown);
                Object.DestroyImmediate(owl); Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void Ring_Gate_V2HasTiers_AndRadiiAreFixed()
        {
            var cfg = Config();
            try
            {
                float rin = DomeRockRing.InnerRadius(cfg);
                float rout = DomeRockRing.OuterRadius(cfg);
                var oldMesh = DomeRockRing.BuildRingMesh(cfg, 64, 6, false);
                var v2Mesh = DomeRockRing.BuildRingMesh(cfg, 64, 6, true);
                Assert.AreEqual(oldMesh.vertexCount, v2Mesh.vertexCount,
                    "v2-кольцо изменило сетку, а не силуэт");
                Assert.AreEqual(rin, DomeRockRing.InnerRadius(cfg), 0f);
                Assert.AreEqual(rout, DomeRockRing.OuterRadius(cfg), 0f);
                float oldMax = MaxY(oldMesh), v2Max = MaxY(v2Mesh);
                Assert.AreNotEqual(oldMax, v2Max, "у кольца нет ярусов/осыпи: та же гладкая лента");
                Assert.Greater(v2Max, oldMax, "v2-ярус не поднимает силуэт кольца");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        static float MaxY(Mesh m)
        {
            float y = float.MinValue;
            foreach (var v in m.vertices) if (v.y > y) y = v.y;
            return y;
        }

        [Test]
        public void Water_Gate_V2HasLakeShelf_With3To5mAndLowSlope()
        {
            var hydro = new DomeHydro.State
            {
                lakeCenter = Vector2.zero,
                lakeRadius = 100f,
                lakeLevel = 0f,
            };
            var shelf = DomeWaterMesh.LakeShelf(hydro);
            Assert.Greater(shelf.vertexCount, 0, "полки берега нет");
            float rMin = float.MaxValue, rMax = float.MinValue;
            float yMin = float.MaxValue, yMax = float.MinValue;
            foreach (var v in shelf.vertices)
            {
                float r = new Vector2(v.x, v.z).magnitude;
                rMin = Mathf.Min(rMin, r);
                rMax = Mathf.Max(rMax, r);
                yMin = Mathf.Min(yMin, v.y);
                yMax = Mathf.Max(yMax, v.y);
            }
            Assert.AreEqual(5f, rMax - rMin, 0.5f, "полка не 3–5 м");
            Assert.That(yMax - yMin, Is.LessThanOrEqualTo(0.8f),
                "полка круче 6° на такой ширине");
        }

        [Test]
        public void SlopeBands_Gate_V2HasNoTurfStripeBelow18()
        {
            Assert.AreEqual(DomeSlopeBands.Turf, DomeSlopeBands.Index(15f, 10f, 10f, 1337, 1),
                "v1-полоса 10–18° должна остаться прежней");
            Assert.AreEqual(DomeSlopeBands.Grass, DomeSlopeBands.Index(15f, 10f, 10f, 1337, 2),
                "v2: 10–18° слито с травой — «нарисованная тропа» из A уходит");
            Assert.AreEqual(DomeSlopeBands.Turf, DomeSlopeBands.Index(20f, 10f, 10f, 1337, 2),
                "v2: Turf начинается с 18°");
            Assert.AreNotEqual(DomePalette.Path, DomeSlopeBands.ColorOf(DomeSlopeBands.Grass),
                "Path не должен быть цветом полосы склона");
        }
    }
}
