using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// КАРТА КАК ИСТОЧНИК, шаг 0 (спека `2026-11-18-forest-karta-kak-istochnik` §5 сторож 3, §11): отпечаток павильона v2
    /// (`ForestProfiles.PavilionV2Zone`, сид 1337) закреплён ДО переноса вычислений в карту. Шаги переноса не имеют права
    /// его сдвинуть: красный тест здесь значит «перенос поменял мир», а не «обновите константу». Константы меняются только
    /// вместе с осознанной правкой мира — и с новой строкой `CapturedAt`.
    /// Кроме `WorldFingerprint` и хешей `ForestBaselineTests` держит то, чего отпечаток не видит: стволы и дороги.
    /// </summary>
    public class ForestMapV2BaselineTests
    {
        const string CapturedAt = "2026-11-18 @ f90c7631";

        const ulong FingerprintV2Zone = 0x13caced0be50081fUL;
        const ulong GroundHashV2Zone = 0x2fa3643d8d606bfaUL;
        const ulong AddressHashV2Zone = 0xb2b5339d67577080UL;
        const ulong LairHashV2Zone = 0x419d462d773a5063UL;
        const ulong SpaceHashV2Zone = 0x6b6c35e3aca64433UL;
        const ulong TreeHashV2Zone = 0xfedb111043e6791aUL;
        const ulong RoadHashV2Zone = 0x0dc0a501713ac8f1UL;

        GameObject host;
        ForestBuilder builder;

        [TearDown]
        public void TearDown()
        {
            if (builder != null) builder.Clear();
            if (host != null) Object.Destroy(host);
            builder = null;
            host = null;
        }

        ForestBuilder Build()
        {
            host = new GameObject("~ForestMapV2Baseline");
            host.SetActive(false);
            builder = ForestProfiles.PavilionV2Zone(host, 1337L);
            builder.buildOnAwake = false;
            host.SetActive(true);
            builder.Build();
            return builder;
        }

        [UnityTest]
        public IEnumerator PavilionV2Zone_PinsWorld()
        {
            var b = Build();
            yield return null;
            Assert.AreEqual(2, b.WorldVersion, "профиль — v2");
            Assert.IsFalse(b.buildCanyon, "каньон заморожен");
            Assert.IsFalse(b.buildAqueduct, "акведук заморожен");
            Assert.IsNotNull(b.Plan, "у v2 есть MapPlan");
            Report("FingerprintV2Zone", b.Fingerprint);
            Report("GroundHashV2Zone", ForestBaselineTests.GroundHash(b));
            Report("AddressHashV2Zone", ForestBaselineTests.AddressHash(b));
            Report("LairHashV2Zone", ForestBaselineTests.LairHash(b));
            Report("SpaceHashV2Zone", ForestBaselineTests.SpaceHash(b));
            Assert.AreEqual(FingerprintV2Zone, b.Fingerprint, $"{CapturedAt}: отпечаток павильона v2 изменился");
            Assert.AreEqual(GroundHashV2Zone, ForestBaselineTests.GroundHash(b), $"{CapturedAt}: пол v2 изменился");
            Assert.AreEqual(AddressHashV2Zone, ForestBaselineTests.AddressHash(b), $"{CapturedAt}: адреса v2 изменились");
            Assert.AreEqual(LairHashV2Zone, ForestBaselineTests.LairHash(b), $"{CapturedAt}: логова v2 изменились");
            Assert.AreEqual(SpaceHashV2Zone, ForestBaselineTests.SpaceHash(b), $"{CapturedAt}: разметка v2 изменилась");
        }

        [UnityTest]
        public IEnumerator PavilionV2Zone_PinsTreesAndRoads()
        {
            var b = Build();
            yield return null;
            Report("TreeHashV2Zone", TreeHash(b));
            Report("RoadHashV2Zone", RoadHash(b));
            Assert.AreEqual(TreeHashV2Zone, TreeHash(b), $"{CapturedAt}: стволы v2 изменились");
            Assert.AreEqual(RoadHashV2Zone, RoadHash(b), $"{CapturedAt}: дороги v2 изменились");
        }

        /// <summary>Стволы: число деревьев и координаты твёрдых стволов по порядку.</summary>
        internal static ulong TreeHash(ForestBuilder b)
        {
            ulong h = 14695981039346656037UL;
            ForestBaselineTests.Mix(ref h, b.Trees);
            foreach (var p in b.TreeColliderPositions)
            {
                ForestBaselineTests.MixQ(ref h, p.x);
                ForestBaselineTests.MixQ(ref h, p.y);
                ForestBaselineTests.MixQ(ref h, p.z);
            }
            return h;
        }

        /// <summary>Дороги: точки, ширина, главные и броды по порядку.</summary>
        internal static ulong RoadHash(ForestBuilder b)
        {
            ulong h = 14695981039346656037UL;
            if (b.Roads == null) return h;
            foreach (var r in b.Roads.roads)
            {
                ForestBaselineTests.MixQ(ref h, r.width);
                ForestBaselineTests.Mix(ref h, r.primary ? 1 : 0);
                ForestBaselineTests.Mix(ref h, r.ford ? 1 : 0);
                if (r.points == null) continue;
                foreach (var p in r.points) { ForestBaselineTests.MixQ(ref h, p.x); ForestBaselineTests.MixQ(ref h, p.y); }
            }
            return h;
        }

        static void Report(string name, ulong v) => Debug.Log($"[ForestMapV2Baseline] const ulong {name} = 0x{v:x16}UL;");
    }
}
