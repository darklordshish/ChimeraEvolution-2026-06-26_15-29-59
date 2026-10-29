using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.AI;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// Task 1 большого павильона: терраса и каньон входят в SampleGround,
    /// при выключенных флагах мир остаётся прежним.
    /// </summary>
    public class ForestPavilionTests
    {
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

        ForestBuilder Build(bool geography, bool stationLoop = false)
        {
            host = new GameObject("~ForestPavilionProbe");
            host.SetActive(false);
            builder = host.AddComponent<ForestBuilder>();
            builder.buildOnAwake = false;
            builder.randomSeed = false;
            builder.seed = 1337;
            builder.mapDiameter = 2000f;
            builder.chunkSize = 250f;
            builder.terrainQuads = 16;
            builder.buildAsh = false;
            builder.buildRavines = false;
            builder.buildLabComplex = stationLoop;
            builder.buildStationLoop = stationLoop;
            builder.buildStationHill = stationLoop;
            builder.labEntrance = stationLoop ? LabEntrance.Buried : LabEntrance.Rock;
            builder.buildLabPlan = stationLoop;
            builder.labStyle = LabStyle.Bastion;
            builder.minLabDistance = 150f;
            builder.buildTerrace = geography;
            builder.buildCanyon = geography;
            builder.spawnAtEntry = true;
            builder.speciesPool = new List<SpeciesSO>();
            host.SetActive(true);
            builder.Build();
            return builder;
        }

        [UnityTest]
        public IEnumerator Geography_ChangesSampleGround_AndCanyonIsValid()
        {
            var b = Build(true);
            yield return null;

            Assert.IsTrue(b.Terrace.valid, "терраса найдена на Ø2000/1337");
            Assert.IsTrue(b.Canyon.valid, "каньон найден на Ø2000/1337");
            Assert.IsNotNull(b.Canyon.axis, "ось каньона есть");

            int i = b.Canyon.axis.Count / 2;
            var mid = b.Canyon.axis[i];
            var next = b.Canyon.axis[Mathf.Min(i + 1, b.Canyon.axis.Count - 1)];
            var dir = (next - mid).normalized;
            var perp = new Vector2(-dir.y, dir.x);
            var shoulder = mid + perp * (b.Canyon.halfWidth * 1.2f);

            Assert.Greater(b.SampleGround(mid.x, mid.y), b.Hydro.lakeLevel,
                "дно каньона выше воды");
            Assert.Greater(b.SampleGround(shoulder.x, shoulder.y),
                b.SampleGround(mid.x, mid.y) + 1f,
                "борт каньона выше дна");
            Assert.IsTrue(DomeCanyon.Contains(b.Canyon, mid.x, mid.y), "центр — в каньоне");
            Assert.IsFalse(DomeCanyon.Contains(b.Canyon, shoulder.x, shoulder.y), "борт — снаружи каньона");
        }

        [UnityTest]
        public IEnumerator StationLoop_NorthDoor_IsBuilt_AndReachable()
        {
            var b = Build(true, true);
            yield return null;

            Assert.AreNotEqual(Vector3.zero, b.LabNorthDoor, "северная дверь станции построена");
            Assert.IsTrue(b.Hill.valid, "холм станции построен");
            Assert.IsNotNull(GameObject.Find("StationHill"), "видимый StationHill-меш построен");
            Assert.IsTrue(NavMesh.SamplePosition(b.LabNorthDoor, out var hit, 20f, NavMesh.AllAreas),
                "северная дверь на NavMesh");
            Assert.Less(Vector3.Distance(hit.position, b.LabNorthDoor), 3f,
                "точка северной двери достижима");
            Assert.IsTrue(NavMesh.SamplePosition(b.LabDoor, out var southHit, 20f, NavMesh.AllAreas),
                "южная дверь на NavMesh");

            var path = new NavMeshPath();
            Assert.IsTrue(NavMesh.CalculatePath(southHit.position, hit.position, NavMesh.AllAreas, path),
                "путь от центра станции до северной двери существует");
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status,
                "путь до северной двери полный");
            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++)
                length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            Assert.Less(length, 70f, "путь сквозь станцию короче обхода вокруг");
        }

        [UnityTest]
        public IEnumerator StationHill_CoversRotatedPad_AndHasRelief()
        {
            var b = Build(true, true);
            yield return null;

            var hill = GameObject.Find("StationHill");
            Assert.IsNotNull(hill, "StationHill не построен");
            var mf = hill.GetComponent<MeshFilter>();
            Assert.IsNotNull(mf?.sharedMesh, "у StationHill нет меша");
            var size = mf.sharedMesh.bounds.size;
            Assert.GreaterOrEqual(size.x, 58f, "холм уже повёрнутой плиты по X");
            Assert.GreaterOrEqual(size.z, 56f, "холм уже повёрнутой плиты по Z");
            Assert.Greater(size.y, 5f, "холм плоский: нет подъёма");

            // Старые «каменные столы» Buried не должны возвращаться.
            Assert.IsNull(GameObject.Find("UndergroundHill"), "UndergroundHill — плоский стол");
            Assert.IsNull(GameObject.Find("UndergroundHillW"), "UndergroundHillW — плоский стол");
            Assert.IsNull(GameObject.Find("UndergroundHillE"), "UndergroundHillE — плоский стол");
            Assert.IsNull(GameObject.Find("UndergroundWingW"), "UndergroundWingW — плоский стол");
            Assert.IsNull(GameObject.Find("UndergroundWingE"), "UndergroundWingE — плоский стол");
        }

        [UnityTest]
        public IEnumerator Geography_Off_IsNoOp()
        {
            var b = Build(false);
            yield return null;

            Assert.IsFalse(b.Terrace.valid, "без флага террасы нет");
            Assert.IsFalse(b.Canyon.valid, "без флага каньона нет");
            Assert.AreEqual(b.RawGround(300f, 300f), b.SampleGround(300f, 300f), 1e-4f,
                "без географии SampleGround не меняет поле вне площадки");
        }
    }
}
