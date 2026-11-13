using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>G7 шаг 1: текущий мир выражается планом-данными, без смены геометрии.</summary>
    public class MapPlanTests
    {
        static DomeGenConfigSO Cfg()
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.treeCount = 2000;
            cfg.treeDensityTarget = 3120f;
            cfg.forestThreshold = 0.20f;
            cfg.buildRelief = true;
            return cfg;
        }

        static DomeComposition.State State(string form = "Гряда-силуэт")
        {
            var s = new DomeComposition.State { flipped = false };
            s.forms.Add(new DomeForm
            {
                name = form,
                kind = DomeFormKind.Capsule,
                sign = DomeFormSign.Lift,
            });
            s.declared.Add(new DomeComposition.Placed
            {
                role = DomeComposition.Role.Ridge,
                name = form,
                applied = true,
            });
            return s;
        }

        [Test]
        public void Legacy_SerializesAndRestores()
        {
            var cfg = Cfg();
            var p = MapPlan.FromLegacy(State(), cfg);
            string json = p.ToJson();
            Assert.IsNotEmpty(json, "план не сериализуется");
            var back = MapPlan.FromJson(json);
            Assert.IsNotNull(back, "JSON не читается");
            Assert.AreEqual(p.path, back.path, "path");
            Assert.AreEqual(p.identity, back.identity, "identity");
            Assert.AreEqual(p.signature, back.signature, "signature");
            Assert.AreEqual(p.forms.Count, back.forms.Count, "forms");
            Assert.AreEqual(p.nodes.Count, back.nodes.Count, "nodes");
            Assert.AreEqual(p.nodes[0].variants.Count, back.nodes[0].variants.Count, "variants");
            Assert.AreEqual(p.nodes[0].variants[0].id, back.nodes[0].variants[0].id, "variant id");
            Assert.IsNull(MapPlanRules.Validate(back), "восстановленный план не проходит правила");
            Object.DestroyImmediate(cfg);
        }

        [Test]
        public void Legacy_PathIsSchemeLegacy_AndSignatureHasNoMetres()
        {
            var cfg = Cfg();
            var p = MapPlan.FromLegacy(State(), cfg);
            StringAssert.Contains("scheme=legacy", p.path);
            StringAssert.Contains("near=current", p.path);
            StringAssert.Contains("station=current", p.path);
            StringAssert.Contains("canyon=current", p.path);
            StringAssert.DoesNotContain("center", p.signature);
            StringAssert.DoesNotContain("height", p.signature);
            Object.DestroyImmediate(cfg);
        }

        [Test]
        public void TwentyStates_HaveSameLegacyPath()
        {
            var cfg = Cfg();
            string path = null;
            for (int i = 0; i < 20; i++)
            {
                var p = MapPlan.FromLegacy(State("Форма-" + i), cfg);
                if (path == null) path = p.path;
                Assert.AreEqual(path, p.path, "путь зависел от состава — это не legacy-путь");
            }
            Object.DestroyImmediate(cfg);
        }

        [Test]
        public void Identity_ChangesWithFormName()
        {
            var cfg = Cfg();
            var a = MapPlan.FromLegacy(State("A"), cfg);
            var b = MapPlan.FromLegacy(State("B"), cfg);
            Assert.AreNotEqual(a.identity, b.identity, "identity не видит имя формы");
            Assert.AreEqual(a.path, b.path, "path не должен зависеть от имени формы");
            Object.DestroyImmediate(cfg);
        }

        [Test]
        public void Identity_AndSignature_AreSeparate()
        {
            var cfg = Cfg();
            var a = MapPlan.FromLegacy(State(), cfg);
            cfg.treeDensityTarget = 4000f;
            var b = MapPlan.FromLegacy(State(), cfg);
            Assert.AreEqual(a.signature, b.signature,
                "плотность — число, а не класс силуэта: signature её не несёт");
            Assert.AreNotEqual(a.identity, b.identity,
                "identity обязан видеть числовой параметр");
            Object.DestroyImmediate(cfg);
        }

        [Test]
        public void TwentySeeds_HaveSameLegacyPath()
        {
            string path = null;
            for (int i = 0; i < 20; i++)
            {
                var cfg = Cfg();
                cfg.seed = i;
                var p = MapPlan.FromLegacy(State(), cfg);
                if (path == null) path = p.path;
                Assert.AreEqual(path, p.path, "legacy-путь не зависит от сида");
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void Legacy_PlanIsInTraceShape()
        {
            var cfg = Cfg();
            var p = MapPlan.FromLegacy(State(), cfg);
            var layers = new List<string>();
            foreach (var n in p.nodes) layers.Add(n.id);
            CollectionAssert.Contains(layers, "scheme");
            CollectionAssert.Contains(layers, "near");
            CollectionAssert.Contains(layers, "station");
            CollectionAssert.Contains(layers, "canyon");
            Assert.AreEqual(4, p.nodes.Count, "legacy-план должен назвать четыре ветви");
            Object.DestroyImmediate(cfg);
        }
    }
}
