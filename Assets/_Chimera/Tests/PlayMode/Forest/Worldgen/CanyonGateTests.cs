using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// P0 шага 6: каньон проверяется на РЕАЛЬНОМ v2-мире (терраса, станция, маски),
    /// а не на синтетической паре entry/lab. 1337 обязан дать wet/dry, не absent.
    /// </summary>
    public class CanyonGateTests
    {
        GameObject host;
        ForestBuilder builder;

        [UnityTest]
        public IEnumerator Seed1337_Canyon_IsWetOrDry_WithRealV2()
        {
            host = new GameObject("~CanyonGateV2");
            host.SetActive(false);
            builder = ForestProfiles.PavilionV2(host, 1337L, erosion: false);
            host.SetActive(true);
            builder.Build();
            yield return null;

            Assert.IsTrue(builder.Canyon.valid, "каньон v2 не собран");
            Assert.AreNotEqual(CanyonPolicy.Absent, builder.Canyon.policy,
                "1337: каньон не может быть absent");
            Assert.IsTrue(builder.Canyon.policy == CanyonPolicy.Wet ||
                          builder.Canyon.policy == CanyonPolicy.Dry,
                "каньон v2 — wet/dry, не " + builder.Canyon.policy);
            Assert.IsTrue(builder.Canyon.attempts != null &&
                          builder.Canyon.attempts.Count > 0,
                "причины попыток каньона потеряны");
            Assert.IsTrue(builder.HasStation, "станция должна быть собрана");
            Assert.IsTrue(builder.Terrace.valid && builder.Terrace.radius > 0f,
                "терраса должна быть собрана");
            // Один меш каньона: гладкие борта/полка; у wet — ступени водопада.
            Assert.IsNotNull(GameObject.Find("CanyonAuthor"), "нет авторского меша");
            Assert.IsNotNull(GameObject.Find("CanyonSkirtW"), "нет юбки W");
            Assert.IsNotNull(GameObject.Find("CanyonSkirtE"), "нет юбки E");
            if (builder.Canyon.policy == CanyonPolicy.Wet)
                Assert.IsNotNull(GameObject.Find("CanyonWater"), "нет воды каньона");
            else
                Assert.IsNull(GameObject.Find("CanyonWater"), "dry: воды быть не должно");
        }

        /// <summary>
        /// Решение B: вырез по 20-точечной оси/Wout, collider пола и NavMesh.
        /// Террейн не должен давать второго collider'а под footprint; игрок не проваливается.
        /// </summary>
        [UnityTest]
        public IEnumerator Seed1337_Handoff_CutAndColliderKeepPlayerOnFloor()
        {
            host = new GameObject("~CanyonHandoffGate");
            host.SetActive(false);
            builder = ForestProfiles.PavilionV2(host, 1337L, erosion: false);
            host.SetActive(true);
            builder.Build();
            Physics.SyncTransforms();
            yield return null;

            Assert.IsTrue(builder.HandoffCanyon, "1337 должен идти graded-мешем");
            var root = builder.BuiltRoot != null ? builder.BuiltRoot.transform : null;
            Assert.IsNotNull(root, "мир не собран");
            var colliderTransform = root.Find("CanyonAuthorGraded/CanyonCollider");
            Assert.IsNotNull(colliderTransform, "нет CanyonCollider");
            var collider = colliderTransform.GetComponent<MeshCollider>();
            Assert.IsNotNull(collider, "нет MeshCollider");
            Assert.IsNotNull(collider.sharedMesh, "collider без меша");
            Assert.IsTrue(collider.sharedMesh.isReadable, "MeshCollider не Read/Write — NavMesh не соберётся");

            int checkedPoints = 0, navOk = 0;
            foreach (var p in DomeCanyonHandoff.Axis)
            {
                var origin = new Vector3(p.x, 40f, p.y);
                Assert.IsTrue(Physics.Raycast(origin, Vector3.down, out var hit, 120f),
                    $"нет пола под осью ({p.x:F1};{p.y:F1})");
                Assert.AreEqual("CanyonCollider", hit.collider.name,
                    $"под осью terrain вместо каньона ({p.x:F1};{p.y:F1})");
                checkedPoints++;
                if (NavMesh.SamplePosition(hit.point + Vector3.up * 0.5f,
                        out _, 4f, NavMesh.AllAreas))
                    navOk++;
            }
            Assert.GreaterOrEqual(checkedPoints, 14, "нужно 14–20 точек оси");
            Assert.AreEqual(checkedPoints, navOk, "NavMesh не покрывает дно каньона");

            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.name.StartsWith("Chunk_") || mf.sharedMesh == null) continue;
                foreach (var v in mf.sharedMesh.vertices)
                    Assert.IsFalse(DomeCanyonHandoff.Inside(v.x, v.z),
                        $"террейн оставил вершину под footprint в {mf.name}: {v}");
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (builder != null) builder.Clear();
            if (host != null) Object.Destroy(host);
            host = null;
            builder = null;
        }
    }
}
