using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// КАРТА КАК ИСТОЧНИК, шаг 1 (спека `2026-11-18-forest-karta-kak-istochnik` §4, §9): `ForestMap` — факты мира v2 в
    /// метрах на плоскости купола, с JSON и хешем содержания. На шаге 1 карта — снимок собранного мира; поток «мир читает
    /// карту» — шаги 2–5. Мир павильона v2 (`PavilionV2Zone`, сид 1337) собирается раз на класс.
    /// </summary>
    public class ForestMapTests
    {
        static GameObject host;
        static ForestBuilder builder;

        [OneTimeSetUp]
        public void BuildWorld() => builder = Make(ref host, labComplex: true);

        [OneTimeTearDown]
        public void TearDownWorld()
        {
            if (builder != null) builder.Clear();
            if (host != null) Object.Destroy(host);
            builder = null; host = null;
        }

        static ForestBuilder Make(ref GameObject h, bool labComplex)
        {
            h = new GameObject("~ForestMapProbe");
            h.SetActive(false);
            var b = ForestProfiles.PavilionV2Zone(h, 1337L);
            b.buildOnAwake = false;
            b.buildLabComplex = labComplex;
            h.SetActive(true);
            b.Build();
            return b;
        }

        [UnityTest]
        public IEnumerator Map_IsBuiltForV2()
        {
            var m = builder.Map;
            Assert.IsNotNull(m, "у собранного мира v2 есть карта");
            Assert.AreEqual(1, m.schema);
            Assert.AreEqual(2, m.worldVersion);
            Assert.AreEqual(1337L, m.seed);
            Assert.AreEqual(2000f, m.diameter, 1e-3f);
            Assert.IsFalse(string.IsNullOrEmpty(m.hash), "хеш содержания посчитан");
            yield break;
        }

        [UnityTest]
        public IEnumerator Map_MatchesWorld()
        {
            var m = builder.Map;
            var root = builder.BuiltRoot.transform;
            Assert.IsTrue(m.lab.has, "лаборатория в карте");
            Assert.IsTrue(builder.TryGetExit(out Vector3 door, out Quaternion facing), "у мира есть дверь");
            Vector3 mapDoor = root.TransformPoint(m.lab.door);
            Assert.AreEqual(door.x, mapDoor.x, 1e-3f, "дверь карты = дверь выхода босса (x)");
            Assert.AreEqual(door.z, mapDoor.z, 1e-3f, "дверь карты = дверь выхода босса (z)");
            Vector3 fwd = facing * Vector3.forward;
            Assert.AreEqual(fwd.x, m.lab.outward.x, 1e-3f, "направление наружу (x)");
            Assert.AreEqual(fwd.z, m.lab.outward.y, 1e-3f, "направление наружу (z)");
            Assert.AreEqual(builder.SpawnPoint, m.points.spawn, "точка появления");
            Assert.AreEqual(builder.Entry, m.points.entry, "вход");
            Assert.AreEqual(builder.Hydro.lakeRadius, m.water.lakeRadius, 1e-4f, "радиус озера");
            Assert.AreEqual(builder.Hydro.riverPts.Count, m.water.river.Count, "точки реки");
            Assert.Greater(m.ring.inner, 0f, "кольцо");
            yield break;
        }

        [UnityTest]
        public IEnumerator RoundTrip_KeepsHash()
        {
            var m = builder.Map;
            var back = ForestMap.FromJson(m.ToJson());
            Assert.IsNotNull(back);
            Assert.AreEqual(m.hash, ForestMap.ComputeHash(back), "JSON туда и обратно — тот же хеш");
            Assert.AreEqual(m.hash, back.hash, "хеш переживает JSON");
            yield break;
        }

        [UnityTest]
        public IEnumerator SameSeed_SameMapHash()
        {
            GameObject h2 = null;
            var b2 = Make(ref h2, labComplex: true);
            try { Assert.AreEqual(builder.Map.hash, b2.Map.hash, "тот же сид — та же карта"); }
            finally { b2.Clear(); Object.Destroy(h2); }
            yield break;
        }

        [UnityTest]
        public IEnumerator Areas_AreSane()
        {
            var a = builder.Map.areas;
            float r = builder.Map.ring.inner;
            float circle = Mathf.PI * r * r / 1e6f;
            Assert.AreEqual(circle, a.totalKm2, circle * 0.01f, "вся площадь — круг внутри кольца");
            Assert.Greater(a.landKm2, 0f, "суша есть");
            Assert.Less(a.landKm2, a.totalKm2, "вода отнимает площадь");
            yield break;
        }

        [UnityTest]
        public IEnumerator MissingLab_GivesEmptyLabLayer()
        {
            GameObject h2 = null;
            var b2 = Make(ref h2, labComplex: false);
            try
            {
                Assert.IsNotNull(b2.Map, "карта есть и без лаборатории");
                Assert.IsFalse(b2.Map.lab.has, "слой лаборатории пуст");
            }
            finally { b2.Clear(); Object.Destroy(h2); }
            yield break;
        }
    }
}
