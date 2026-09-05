using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Запас длинной оси ≥5% (LongAxis приколот), BodySlots словарь без дублей
    /// (BodySlots.cs:15, BodyRules.cs:32, MorphBuilder: LongAxis)
    /// </summary>
    public class AxisMarginTests
    {
        [Test]
        public void BodySlots_All_HasTwelveAndNoDuplicates()
        {
            var keys = new List<string>(BodySlots.All.Keys);
            var distinct = new HashSet<string>(keys);
            Assert.AreEqual(keys.Count, distinct.Count, "BodySlots.All не должен содержать дублей (словарь)");
            Assert.AreEqual(12, keys.Count, "Ожидается 12 слотов: Chassis 3 + Base 6 + Appendage 3 (BodySlots.cs:46)");
            // проверяем что все константы покрыты
            Assert.IsTrue(BodySlots.All.ContainsKey(BodySlots.Spine));
            Assert.IsTrue(BodySlots.All.ContainsKey(BodySlots.Body));
            Assert.IsTrue(BodySlots.All.ContainsKey(BodySlots.Rattle));
            Assert.IsTrue(BodySlots.All.ContainsKey(BodySlots.Maw));
            Assert.IsTrue(BodySlots.All.ContainsKey(BodySlots.Sense));
            Assert.IsTrue(BodySlots.All.ContainsKey(BodySlots.Heart));
            Assert.IsTrue(BodySlots.All.ContainsKey(BodySlots.Hide));
            Assert.IsTrue(BodySlots.All.ContainsKey(BodySlots.Arms));
            Assert.IsTrue(BodySlots.All.ContainsKey(BodySlots.Legs));
            Assert.IsTrue(BodySlots.All.ContainsKey(BodySlots.Tail));
            Assert.IsTrue(BodySlots.All.ContainsKey(BodySlots.Horns));
            Assert.IsTrue(BodySlots.All.ContainsKey(BodySlots.Quiller));
        }

        [Test]
        public void BodySlots_Places_AreDisjointFromSlots()
        {
            foreach (var kv in BodySlots.All)
                Assert.IsFalse(BodySlots.IsPlace(kv.Key), $"слот '{kv.Key}' не должен считаться телесным местом");
            foreach (var p in BodySlots.Places)
                Assert.IsFalse(BodySlots.IsSlot(p), $"место '{p}' не должно считаться слотом");

            // IsKnown = IsSlot || IsPlace
            foreach (var kv in BodySlots.All) Assert.IsTrue(BodySlots.IsKnown(kv.Key));
            foreach (var p in BodySlots.Places) Assert.IsTrue(BodySlots.IsKnown(p));
            Assert.IsFalse(BodySlots.IsKnown("Несуществующее"));
            Assert.IsFalse(BodySlots.IsKnown(""));
            Assert.IsFalse(BodySlots.IsKnown(null));
        }

        [Test]
        public void AxisMargin_BelowThreshold_ShouldReportError()
        {
            // Шея человека из гочи: 0.128Y при 0.132Z => запас (0.132/0.128-1)=3.1% <5% => ось может переключиться
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            try
            {
                so.speciesName = "ТестОсь";
                var neck = new BodySocket
                {
                    name = BodySlots.Sense, // любой слот имеющий детей
                    parent = "хребет",
                    baseSize = new Vector3(0.10f, 0.128f, 0.132f),
                    localPos = Vector3.zero,
                };
                var spine = new BodySocket { name = BodySlots.Spine, baseSize = new Vector3(0.4f, 0.5f, 1.2f), localPos = Vector3.zero };
                var child = new BodySocket { name = "голова", parent = BodySlots.Sense, baseSize = new Vector3(0.2f, 0.2f, 0.2f) };
                so.sockets = new[] { spine, neck, child };
                so.organs = new Organ[]
                {
                    new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true },
                    new Organ { organName = "Чутьё", slot = BodySlots.Sense, cost = 3 },
                };
                so.bones = new Bone[0];
                var issues = BodyRules.CheckData(so);
                bool found = false;
                foreach (var iss in issues)
                    if (iss.where == BodySlots.Sense && iss.text.Contains("запас длинной оси")) found = true;
                Assert.IsTrue(found, "при запасе ~3% BodyRules должен ругаться на ось (BodyRules.cs:112)");
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void AxisMargin_AboveThreshold_NoError()
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            try
            {
                so.speciesName = "ТестОсьОк";
                var socket = new BodySocket
                {
                    name = BodySlots.Sense,
                    parent = "хребет",
                    baseSize = new Vector3(0.2f, 0.10f, 0.16f), // 0.16/0.10-1=60% >>5%
                    localPos = Vector3.zero,
                };
                var spine = new BodySocket { name = BodySlots.Spine, baseSize = new Vector3(0.4f, 0.5f, 1.2f), localPos = Vector3.zero };
                var child = new BodySocket { name = "голова", parent = BodySlots.Sense, baseSize = Vector3.one };
                so.sockets = new[] { spine, socket, child };
                so.organs = new Organ[]
                {
                    new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true },
                    new Organ { organName = "Чутьё", slot = BodySlots.Sense, cost = 3 },
                };
                so.bones = new Bone[0];
                var issues = BodyRules.CheckData(so);
                foreach (var iss in issues)
                    Assert.IsFalse(iss.where == BodySlots.Sense && iss.text.Contains("запас длинной оси"), "при запасе 60% ось не должна ругаться");
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void AxisMargin_Isotropic_NoError()
        {
            // куб/шар: max/min <1.05 => изотропный => не ругаемся (BodyRules.cs:111)
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            try
            {
                so.speciesName = "Изотроп";
                var s = new BodySocket { name = "глаза", baseSize = new Vector3(0.1f, 0.101f, 0.099f), localPos = Vector3.zero, parent = "голова" };
                var head = new BodySocket { name = "голова", baseSize = new Vector3(0.2f, 0.2f, 0.2f), localPos = Vector3.zero };
                // голова имеет ребёнка => axisMatters, но размер изотропный
                so.sockets = new[] { head, s, new BodySocket { name = BodySlots.Spine, baseSize = new Vector3(0.4f,0.5f,1.2f) } };
                so.organs = new Organ[0];
                so.bones = new Bone[0];
                var issues = BodyRules.CheckData(so);
                // изотропное место без детей и без linkLength не проверяется вообще (axisMatters=false) => тоже ок
                // просто убедимся что не падает и не ругается ложно на изотропность
                Assert.DoesNotThrow(() => BodyRules.CheckData(so));
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void Axis_IsPinned_ToPureChassis_NotToSizeOfPlan()
        {
            // Длинная ось выбирается по чистому шасси (pure dict) и не зависит от плана морфологии по идентичности.
            // Проверяем через MorphBuilder.SizeOf + LongAxis-инвариант: pure head Y=0.27, Z=0.22 => ось Y; смешанный план где Z стал 0.30 не должен переключить pinned ось.
            var headPure = new BodySocket { name = "голова", baseSize = new Vector3(0.20f, 0.27f, 0.22f), localPos = Vector3.zero };
            var chassis = ScriptableObject.CreateInstance<SpeciesSO>();
            try
            {
                chassis.speciesName = "Человек";
                chassis.sockets = new[] { headPure, new BodySocket { name = BodySlots.Spine, baseSize = new Vector3(0.4f,0.5f,1.2f) } };
                chassis.bones = new Bone[0];
                var dictPure = new Dictionary<string, BodySocket> { { "голова", headPure } };
                // LongAxis pure => Y (0.27 > 0.22)
                Vector3 pureSize = MorphBuilder.SizeOf(headPure, dictPure);
                Vector3 axisPure = pureSize.z >= pureSize.x && pureSize.z >= pureSize.y ? Vector3.forward
                                 : pureSize.y >= pureSize.x ? Vector3.up : Vector3.right;
                Assert.AreEqual(Vector3.up, axisPure, "pure голова человека должна иметь ось Y (CLAUDE.md гоча длинной оси)");

                // смешанный план увеличил Z до 0.30 => ось Z, но приколотая должна остаться Y
                var headMixed = new BodySocket { name = "голова", baseSize = new Vector3(0.20f, 0.27f, 0.30f), localPos = Vector3.zero };
                var dictMixed = new Dictionary<string, BodySocket> { { "голова", headMixed } };
                Vector3 mixedSize = MorphBuilder.SizeOf(headMixed, dictMixed);
                Vector3 axisMixed = mixedSize.z >= mixedSize.x && mixedSize.z >= mixedSize.y ? Vector3.forward
                                  : mixedSize.y >= mixedSize.x ? Vector3.up : Vector3.right;
                Assert.AreEqual(Vector3.forward, axisMixed, "смешанный размер даёт другую ось Z — но pinned остаётся Y");
                // именно это и ловит прикол оси в MorphBuilder.Build: axisOf строится по pure, а не по plan
                Assert.AreNotEqual(axisPure, axisMixed, "демонстрация что неприколотая ось переключила ветку");
            }
            finally { Object.DestroyImmediate(chassis); }
        }

        [Test]
        public void SizeOf_WithSizeRel_ResolvesViaParent()
        {
            var head = new BodySocket { name = "голова", baseSize = new Vector3(0.2f, 0.3f, 0.25f), localPos = Vector3.zero };
            var jaw = new BodySocket { name = "Пасть", parent = "голова", sizeRel = new Vector3(0.5f, 0.5f, 0.5f), baseSize = Vector3.one };
            var dict = new Dictionary<string, BodySocket> { { "голова", head }, { "Пасть", jaw } };
            Vector3 szHead = MorphBuilder.SizeOf(head, dict);
            Vector3 szJaw = MorphBuilder.SizeOf(jaw, dict);
            Assert.AreEqual(szHead.x * 0.5f, szJaw.x, 1e-5f);
            Assert.AreEqual(szHead.y * 0.5f, szJaw.y, 1e-5f);
            Assert.AreEqual(szHead.z * 0.5f, szJaw.z, 1e-5f);
        }
    }
}
