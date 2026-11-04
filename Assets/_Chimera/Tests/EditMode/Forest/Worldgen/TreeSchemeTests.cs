using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// G7 шаг 3: три схемы — данные дерева, а не монетка. Схема выбирается
    /// по сиду только среди прошедших requires/forbids; ветвь `near` — часть схемы;
    /// ближние формы имеют H 2–6 (cape 4–8) и грань 22–28°.
    /// </summary>
    public class TreeSchemeTests
    {
        static DomeGenConfigSO Cfg()
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.seed = 1337;
            cfg.mapDiameter = 2000f;
            cfg.worldVersion = 2;
            return cfg;
        }

        static DomeHydro.State Hydro() => new DomeHydro.State
        {
            lakeRadius = 150f,
            lakeLevel = -1f,
            lakeCenter = new Vector2(-90f, -100f),
        };

        [Test]
        public void TwentySeeds_CoverEveryScheme_AndAreDeterministic()
        {
            var cfg = Cfg();
            var hydro = Hydro();
            var counts = new Dictionary<string, int>();
            foreach (var s in DomeSchemes.All) counts[s.id] = 0;
            string previous = null;
            try
            {
                for (int seed = 1; seed <= 20; seed++)
                {
                    var a = DomeSchemes.Select(cfg, seed, new Vector2(-400f, 0f), hydro,
                        (x, z) => 10f);
                    var b = DomeSchemes.Select(cfg, seed, new Vector2(-400f, 0f), hydro,
                        (x, z) => 10f);
                    Assert.AreEqual(a.id, b.id, "схема не детерминирована на сиде " + seed);
                    Assert.IsFalse(string.IsNullOrEmpty(a.id), "схема не названа");
                    counts[a.id]++;
                    if (a.near != null)
                        foreach (string n in a.near)
                            Assert.Contains(n, new[] { "ridge", "hollow", "cape", "hybrid" });
                    previous = a.id;
                }
                Assert.IsNotNull(previous);
                foreach (var kv in counts)
                    Assert.GreaterOrEqual(kv.Value, 2, $"схема {kv.Key} реже 2/20");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void NearBranch_HasThreeLowForms_InRange_WithTwoSides()
        {
            var entry = new Vector2(-400f, 0f);
            Vector2 u = new Vector2(0f, 1f);
            Vector2 v = new Vector2(-1f, 0f);
            foreach (var scheme in DomeSchemes.All)
                foreach (string near in scheme.near)
                {
                    var forms = DomeSchemes.NearForms(scheme.id, near, 1337, entry, u, v, null);
                    Assert.AreEqual(3, forms.Length, $"{scheme.id}/{near}: не три низкие формы");
                    bool lift = false;
                    var laterals = new List<float>();
                    foreach (var f in forms)
                    {
                        float d = Vector2.Distance(entry, f.center);
                        Assert.That(d, Is.InRange(30f, 150f),
                            $"{scheme.id}/{near}: форма вне 0–150 м");
                        Assert.That(f.height, Is.InRange(2f, 6f),
                            $"{scheme.id}/{near}: H={f.height:F1} вне 2–6");
                        Assert.LessOrEqual(f.MaxSlopeDegrees, 22f,
                            $"{scheme.id}/{near}: форма сама >22°");
                        Assert.That(f.SlopeStepDegrees(1f), Is.LessThanOrEqualTo(2f),
                            $"{scheme.id}/{near}: C1-шаг >2°");
                        float side = Mathf.Abs(Vector2.Dot(f.center - entry, v));
                        Assert.Greater(side, f.diameter * 0.5f + 30f,
                            $"{scheme.id}/{near}: форма ближе 30 м к оси");
                        if (f.sign == DomeFormSign.Lift) lift = true;
                    }
                    Assert.IsTrue(lift, $"{scheme.id}/{near}: все формы только выемки");
                }
        }

        [Test]
        public void Plan_FromScheme_HasPathNodesAndNoMetres()
        {
            var cfg = Cfg();
            var state = new DomeComposition.State();
            state.forms.Add(new DomeForm
            {
                name = "Плато станции", kind = DomeFormKind.Plateau,
                sign = DomeFormSign.Lift,
            });
            var p = MapPlan.FromScheme(state, cfg, DomeSchemes.TwoCapesSaddle, "cape", 48);
            try
            {
                StringAssert.Contains("scheme=" + DomeSchemes.TwoCapesSaddle, p.path);
                StringAssert.Contains("near=cape", p.path);
                StringAssert.Contains("hero=", p.signature);
                StringAssert.DoesNotContain("center", p.signature);
                Assert.IsNull(MapPlanRules.Validate(p), "план схемы не проходит правила");
                var ids = new List<string>();
                foreach (var n in p.nodes) ids.Add(n.id);
                CollectionAssert.Contains(ids, "scheme");
                CollectionAssert.Contains(ids, "near");
                Assert.AreEqual(4, p.nodes.Count);
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void HeroForms_AreRealGeometry_PerScheme()
        {
            var hydro = new DomeHydro.State
            {
                lakeRadius = 120f, lakeLevel = -1f,
                lakeCenter = new Vector2(100f, 80f),
            };
            var entry = new Vector2(-400f, 0f);
            Vector2 u = new Vector2(0f, 1f), v = new Vector2(-1f, 0f);
            var two = DomeSchemes.HeroForms(DomeSchemes.TwoCapesSaddle, 1337, entry, u, v, hydro);
            var bowl = DomeSchemes.HeroForms(DomeSchemes.BowlRidge, 1337, entry, u, v, hydro);
            var lake = DomeSchemes.HeroForms(DomeSchemes.LakeCauldron, 1337, entry, u, v, hydro);
            Assert.GreaterOrEqual(two.Length, 1, "у «двух мысов» нет героя в HeroForms");
            Assert.GreaterOrEqual(bowl.Length, 2, "чаша с грядой не даёт геометрию");
            Assert.GreaterOrEqual(lake.Length, 2, "озеро в котле не даёт геометрию");
            CollectionAssert.AreNotEquivalent(
                System.Array.ConvertAll(bowl, f => f.name),
                System.Array.ConvertAll(lake, f => f.name),
                "схемы отличаются только путём, а не формами");
        }

        [Test]
        public void Select_And_Near_AreSeedStable()
        {
            var cfg = Cfg();
            var hydro = Hydro();
            var a = DomeSchemes.Select(cfg, 1337, new Vector2(-400f, 0f), hydro, (x, z) => 10f);
            var b = DomeSchemes.Select(cfg, 1337, new Vector2(-400f, 0f), hydro, (x, z) => 10f);
            Assert.AreEqual(a.id, b.id);
            Assert.AreEqual(DomeSchemes.SelectNear(a, 1337), DomeSchemes.SelectNear(b, 1337));
            Object.DestroyImmediate(cfg);
        }
    }
}
