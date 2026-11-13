using System.Collections.Generic;
using NUnit.Framework;

namespace Chimera.Tests.EditMode
{
    /// <summary>G7 шаг 1: дерево — не DAG; параметр — не ветвь; fallback назван.</summary>
    public class MapPlanRulesTests
    {
        static MapPlan Plan()
        {
            var p = new MapPlan
            {
                path = "scheme=legacy/near=current/station=current/canyon=current",
                identity = "id",
                signature = "sig",
            };
            p.nodes.Add(new DecisionNode
            {
                id = "scheme",
                variants = new List<Variant>
                {
                    new Variant { id = "legacy" },
                },
            });
            return p;
        }

        [Test]
        public void OneVariant_IsParameter_NotBranch()
        {
            var n = new DecisionNode { id = "near" };
            n.variants.Add(new Variant { id = "current" });
            Assert.IsTrue(MapPlanRules.IsParameter(n));
            var p = Plan();
            p.nodes.Add(n);
            Assert.IsNull(MapPlanRules.Validate(p));
        }

        [Test]
        public void StableOrder_DoesNotDependOnArrayOrder()
        {
            var a = new DecisionNode { id = "x" };
            a.variants.Add(new Variant { id = "b" });
            a.variants.Add(new Variant { id = "a" });
            var b = new DecisionNode { id = "x" };
            b.variants.Add(new Variant { id = "a" });
            b.variants.Add(new Variant { id = "b" });
            var oa = MapPlanRules.StableOrder(a);
            var ob = MapPlanRules.StableOrder(b);
            Assert.AreEqual("a", oa[0].id);
            Assert.AreEqual("b", oa[1].id);
            Assert.AreEqual(oa[0].id, ob[0].id);
            Assert.AreEqual(oa[1].id, ob[1].id);
        }

        [Test]
        public void MissingParent_AndCycle_AreReported()
        {
            var p = Plan();
            p.nodes.Add(new DecisionNode { id = "orphan", parent = "nope" });
            StringAssert.Contains("родителя", MapPlanRules.Validate(p));

            var q = Plan();
            var ca = new DecisionNode { id = "a", parent = "b" };
            ca.variants.Add(new Variant { id = "v" });
            var cb = new DecisionNode { id = "b", parent = "a" };
            cb.variants.Add(new Variant { id = "v" });
            q.nodes.Add(ca);
            q.nodes.Add(cb);
            StringAssert.Contains("цикл", MapPlanRules.Validate(q));
        }

        [Test]
        public void Fallback_MustNameAnExistingVariant()
        {
            var p = Plan();
            var n = new DecisionNode { id = "near" };
            n.variants.Add(new Variant { id = "current", fallback = "missing" });
            p.nodes.Add(n);
            StringAssert.Contains("fallback", MapPlanRules.Validate(p));

            n.variants[0].fallback = "current";
            Assert.IsNull(MapPlanRules.Validate(p), "fallback на существующий вариант законен");
        }

        [Test]
        public void RequiresAndForbids_CannotOverlap()
        {
            var p = Plan();
            var n = new DecisionNode { id = "near" };
            n.variants.Add(new Variant
            {
                id = "current",
                requires = new List<string> { "wet" },
                forbids = new List<string> { "wet" },
            });
            p.nodes.Add(n);
            StringAssert.Contains("спорят", MapPlanRules.Validate(p));
        }

        [Test]
        public void Applies_ChecksRequiresAndForbids()
        {
            var v = new Variant
            {
                requires = new List<string> { "wet" },
                forbids = new List<string> { "dry" },
            };
            Assert.IsTrue(MapPlanRules.Applies(v, new[] { "wet" }));
            Assert.IsFalse(MapPlanRules.Applies(v, new[] { "wet", "dry" }));
            Assert.IsFalse(MapPlanRules.Applies(v, new[] { "dry" }));
        }
    }
}
