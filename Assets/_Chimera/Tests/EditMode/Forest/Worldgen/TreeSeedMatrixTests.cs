using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// G7 шаг 3: 20 сидов дают все три схемы, путь стабилен для одного сида
    /// и различается между схемами. Это детерминизм выбора, а не «монетка».
    /// </summary>
    public class TreeSeedMatrixTests
    {
        [Test]
        public void TwentySeeds_ThreeSchemes_AndStablePaths()
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.seed = 1337;
            cfg.worldVersion = 2;
            var hydro = new DomeHydro.State
            {
                lakeRadius = 150f,
                lakeLevel = -1f,
                lakeCenter = new Vector2(-90f, -100f),
            };
            var state = new DomeComposition.State();
            state.forms.Add(new DomeForm
            {
                name = "Плато станции", kind = DomeFormKind.Plateau,
                sign = DomeFormSign.Lift,
            });
            var ids = new HashSet<string>();
            var paths = new HashSet<string>();
            try
            {
                for (int seed = 1; seed <= 20; seed++)
                {
                    var s = DomeSchemes.Select(cfg, seed, new Vector2(-400f, 0f), hydro,
                        (x, z) => 10f);
                    string near = DomeSchemes.SelectNear(s, seed);
                    var a = MapPlan.FromScheme(state, cfg, s.id, near, 48);
                    var b = MapPlan.FromScheme(state, cfg, s.id, near, 48);
                    Assert.AreEqual(a.path, b.path, "путь не стабилен на сиде " + seed);
                    Assert.IsNull(MapPlanRules.Validate(a), "план не прошёл правила");
                    ids.Add(s.id);
                    paths.Add(a.path);
                }
                CollectionAssert.AreEquivalent(
                    new[] { DomeSchemes.TwoCapesSaddle, DomeSchemes.BowlRidge, DomeSchemes.LakeCauldron },
                    ids, "20 сидов не покрыли три схемы");
                Assert.GreaterOrEqual(paths.Count, 3, "меньше трёх разных путей");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }
    }
}
