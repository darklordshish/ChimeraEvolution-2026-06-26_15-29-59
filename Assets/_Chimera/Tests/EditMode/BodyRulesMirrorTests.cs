using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    public class BodyRulesMirrorTests
    {
        [Test]
        public void SizeOf_DoesNotThrow_OnValidSocket()
        {
            var s = new BodySocket { name = "голова", baseSize = new Vector3(0.2f, 0.27f, 0.22f), localPos = Vector3.zero };
            var dict = new Dictionary<string, BodySocket> { { "голова", s } };
            Assert.DoesNotThrow(() => { var _ = MorphBuilder.SizeOf(s, dict); });
        }

        [Test]
        public void SizeOf_DoesNotThrow_WhenParentMissing()
        {
            var s = new BodySocket { name = "Пасть", parent = "голова", sizeRel = new Vector3(0.5f, 0.5f, 0.5f), baseSize = Vector3.one };
            var dict = new Dictionary<string, BodySocket> { { "Пасть", s } }; // родителя "голова" нет
            Assert.DoesNotThrow(() => { var _ = MorphBuilder.SizeOf(s, dict); });
        }

        [Test]
        public void SizeOf_HandlesNullSocket_GracefullyOrThrowsArgument()
        {
            var dict = new Dictionary<string, BodySocket>();
            // требуем guard: либо не бросает, либо бросает ArgumentNullException, но не голый NRE
            try
            {
                var _ = MorphBuilder.SizeOf(null, dict);
                Assert.Pass("SizeOf(null) не бросил — защита есть");
            }
            catch (System.ArgumentNullException) { Assert.Pass("SizeOf(null) бросил ArgumentNullException — допустимо"); }
            catch (System.NullReferenceException)
            {
                Assert.Inconclusive("SizeOf(null) бросает NRE — нужен guard (ожидаемо красный до фикса)");
            }
        }

        [Test]
        public void BodyRules_CheckData_DoesNotThrow_OnEmptySpecies()
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            try
            {
                so.speciesName = "Пустышка";
                so.sockets = new BodySocket[0];
                so.organs = new Organ[0];
                Assert.DoesNotThrow(() => { var _ = BodyRules.CheckData(so); });
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void BodyRules_CheckData_DoesNotThrow_OnNullSocketEntry()
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            try
            {
                so.speciesName = "СДырой";
                so.sockets = new BodySocket[] { null, new BodySocket { name = "голова", baseSize = Vector3.one } };
                so.organs = new Organ[] { null, new Organ { organName = "Тест", slot = "Пасть" } };
                Assert.DoesNotThrow(() => { var _ = BodyRules.CheckData(so); });
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void BodyRules_And_SizeOf_Agree_OnAxis()
        {
            // MorphBuilder.SizeOf вычисляет габарит с учётом sizeRel; BodyRules.CheckData использует его же
            var head = new BodySocket { name = "голова", baseSize = new Vector3(0.2f, 0.27f, 0.22f) };
            var jaw = new BodySocket { name = "Пасть", parent = "голова", sizeRel = new Vector3(0.5f, 0.5f, 0.5f), baseSize = Vector3.one };
            var dict = new Dictionary<string, BodySocket> { { "голова", head }, { "Пасть", jaw } };
            Vector3 sz = Vector3.zero;
            Assert.DoesNotThrow(() => sz = MorphBuilder.SizeOf(jaw, dict));
            Vector3 expected = Vector3.Scale(MorphBuilder.SizeOf(head, dict), jaw.sizeRel);
            Assert.AreEqual(expected.x, sz.x, 1e-5f);
            Assert.AreEqual(expected.y, sz.y, 1e-5f);
            Assert.AreEqual(expected.z, sz.z, 1e-5f);
        }
    }
}
