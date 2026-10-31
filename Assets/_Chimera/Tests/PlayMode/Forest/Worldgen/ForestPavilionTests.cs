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
            builder.patchyFlora = stationLoop;
            builder.treeCount = stationLoop ? 1500 : 700;
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
        public IEnumerator LabDoor_To_Atrium_IsComplete()
        {
            var b = Build(true, true);
            yield return null;

            Assert.AreNotEqual(Vector3.zero, b.LabAtrium, "LabAtrium не выставлен");
            Assert.IsTrue(NavMesh.SamplePosition(b.Entry, out var entryHit, 20f, NavMesh.AllAreas),
                "Entry не на NavMesh");
            Assert.IsTrue(NavMesh.SamplePosition(b.LabDoor, out var doorHit, 20f, NavMesh.AllAreas),
                "LabDoor не на NavMesh");
            Assert.IsTrue(NavMesh.SamplePosition(b.LabAtrium, out var atriumHit, 20f, NavMesh.AllAreas),
                "LabAtrium не на NavMesh");

            var path = new NavMeshPath();
            Assert.IsTrue(NavMesh.CalculatePath(doorHit.position, atriumHit.position,
                NavMesh.AllAreas, path), "путь LabDoor → LabAtrium не посчитан");
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status,
                "LabDoor → LabAtrium не полный");
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
        public IEnumerator Clearings_ArePresent_AndDry()
        {
            var b = Build(true, true);
            yield return null;

            Assert.GreaterOrEqual(b.Clearings.Count, 4, "полян-арен меньше четырёх");
            Assert.LessOrEqual(b.Clearings.Count, 6, "полян-арен больше шести");
            foreach (var c in b.Clearings)
            {
                float h = b.SampleGround(c.center.x, c.center.y);
                Assert.Greater(h, b.Hydro.lakeLevel + 0.5f, "поляна в воде");
            }
        }

        [UnityTest]
        public IEnumerator Corridors_ConnectClearings_AndAreDry()
        {
            var b = Build(true, true);
            yield return null;

            Assert.GreaterOrEqual(b.Corridors.Count, b.Clearings.Count, "коридоров меньше, чем полян: у арены не два выхода");
            foreach (var c in b.Corridors)
            {
                Assert.GreaterOrEqual(c.width, 16f, "коридор уже 16 м");
                Vector2 mid = (c.a + c.b) * 0.5f;
                Assert.Greater(b.SampleGround(mid.x, mid.y), b.Hydro.lakeLevel + 0.5f,
                    "коридор в воде");
                Assert.IsTrue(ForestSpace.ContainsCorridor(b.Corridors, mid.x, mid.y),
                    "середина коридора не попадает в разметку");
            }
            for (int i = 0; i < b.Clearings.Count; i++)
                Assert.GreaterOrEqual(ForestSpace.Exits(b.Clearings, b.Corridors, i), 2,
                    $"у арены {i} меньше двух выходов");
        }
        [UnityTest]
        public IEnumerator CoverPods_ArePresent_AndDry()
        {
            var b = Build(true, true);
            yield return null;

            Assert.GreaterOrEqual(b.CoverPods.Count, 20, "укрытий меньше двадцати");
            Assert.GreaterOrEqual(b.CoverPodsBuilt, 20, "геометрии укрытий меньше двадцати");
            Assert.LessOrEqual(b.CoverPodsBuilt, 30, "геометрии укрытий больше тридцати");
            Assert.LessOrEqual(b.CoverPods.Count, 30, "укрытий больше тридцати");
            foreach (var p in b.CoverPods)
                Assert.Greater(b.SampleGround(p.center.x, p.center.y),
                    b.Hydro.lakeLevel + 0.5f, "укрытие в воде");
        }
        [UnityTest]
        public IEnumerator TreeColliders_ArePresent()
        {
            var b = Build(true, true);
            yield return null;

            Assert.GreaterOrEqual(b.TreeColliders, 300, "коллайдеров стволов меньше трёхсот");
            Assert.LessOrEqual(b.TreeColliders, 500, "коллайдеров стволов больше пятисот");
        }
        [UnityTest]
        public IEnumerator Sight_ArenaClear_TrunksBlock()
        {
            var b = Build(true, true);
            yield return null;

            Assert.Greater(b.TreeColliderPositions.Count, 0, "нет стволов-коллайдеров");
            foreach (var c in b.Clearings)
            {
                float h = b.SampleGround(c.center.x, c.center.y);
                Vector3 center = new Vector3(c.center.x, h + 1.6f, c.center.y);
                Vector3 a = center + new Vector3(-10f, 0f, 0f);
                Vector3 d = center + new Vector3(10f, 0f, 0f);
                Assert.IsFalse(ForestSight.TrunksBlock(a, d, b.TreeColliderPositions), "ствол в арене");
            }

            Vector3 t = b.TreeColliderPositions[0];
            Vector3 left = new Vector3(t.x - 3f, t.y + 1.6f, t.z);
            Vector3 right = new Vector3(t.x + 3f, t.y + 1.6f, t.z);
            Assert.IsTrue(ForestSight.TrunksBlock(left, right, b.TreeColliderPositions),
                "ствол не рвёт луч");
        }
        [UnityTest]
        public IEnumerator StationLoop_Crevice_IsNotBuriedByGround()
        {
            var b = Build(true, true);
            yield return null;

            Vector2 back = ((Vector2)b.LabNorthDoor - b.LabCenter).normalized;
            float depth = DomeLabComplex.DepthFor(LabEntrance.Buried);
            for (int i = 0; i <= 12; i++)
            {
                float t = i / 12f;
                Vector2 p = b.LabCenter +
                    back * (DomeLabComplex.HalfDepth + DomeLabComplex.CreviceLength * t);
                float floorY = b.LabNorthDoor.y +
                    (depth - DomeLabComplex.Plinth) * t;
                Assert.LessOrEqual(b.SampleGround(p.x, p.y), floorY + 0.6f,
                    $"поле лежит крышкой над расщелиной на t={t:F2}");
            }
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
