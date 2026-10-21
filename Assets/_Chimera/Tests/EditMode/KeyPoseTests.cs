using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>ПОЗА СТУПЕНИ (`KeyPose`, решение 21.10): кости поворачиваются от покоя шасси к позе цели на вес ключа —
    /// сферически и после привязки, так что кожа гнётся за ними. Позу подаём руками: хребет наклонён на 20° от покоя человека.
    /// Поставку модельной линии сверяет `Pose_FromHandoff_JointsLandOnPoseHeads` (молчит, пока файла позы в handoff нет).</summary>
    public class KeyPoseTests
    {
        const string Spine = "хребет";
        static readonly Quaternion Tilt = Quaternion.AngleAxis(20f, Vector3.right);

        static SpeciesSO Load(string n) => AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{n}.asset");

        static List<Organ> WolfOn(SpeciesSO human, SpeciesSO wolf, int count)
        {
            int n = 0;
            return human.organs.Where(o => o != null).Select(o =>
            {
                if (o.chassisOnly || n >= count) return o;
                var w = wolf.organs.FirstOrDefault(x => x != null && x.slot == o.slot && !x.chassisOnly);
                if (w == null) return o;
                n++; return w;
            }).ToList();
        }

        static GameObject Build(SpeciesSO human, List<Organ> worn)
        {
            var go = new GameObject("~поза");
            MorphBuilder.Build(go.transform, human, worn, null);
            return go;
        }

        static Transform Bone(GameObject go) => go.GetComponentsInChildren<Transform>(true).First(t => t.name == Spine);
        static Quaternion InBody(GameObject go, Transform t) => Quaternion.Inverse(go.transform.Find("Morph").rotation) * t.rotation;

        static List<Matrix4x4[]> Binds(GameObject go) =>
            go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh != null)
              .OrderBy(r => r.name).ThenBy(r => r.sharedMesh.vertexCount).Select(r => r.sharedMesh.bindposes).ToList();

        static IEnumerable<Vector3> Skin(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = new Mesh(); r.BakeMesh(m, true);
                foreach (var v in m.vertices) yield return r.transform.TransformPoint(v);
                Object.DestroyImmediate(m);
            }
        }

        [Test]
        public void Pose_TurnsBones_BySlerpOfKeyWeight_AndSkinFollows()
        {
            var human = Load("Человек"); var wolf = Load("Волк");
            var saved = human.keyPose;
            var made = new List<GameObject>();
            try
            {
                // покой шасси — рамка хребта у чистого человека (ключа нет, суставы цели не применяются)
                human.keyPose = new KeyJoint[0];
                var pure = Build(human, human.organs.Where(o => o != null).ToList()); made.Add(pure);
                var restRot = InBody(pure, Bone(pure));
                var restPos = pure.transform.Find("Morph").InverseTransformPoint(Bone(pure).position);
                var posed = Tilt * restRot;

                var full = WolfOn(human, wolf, 99);
                var half = WolfOn(human, wolf, 5);
                Assert.AreEqual(2, GlobalLayer.Step(human, full).step, "весь набор волка — ступень 2");
                Assert.AreEqual(1, GlobalLayer.Step(human, half).step, "пять органов волка — ступень 1");

                var noPose = Build(human, full); made.Add(noPose);
                var halfNoPose = Build(human, half); made.Add(halfNoPose);
                human.keyPose = new[] { new KeyJoint { key = "Волк", bone = Spine, head = restPos, along = posed * Vector3.up, forward = posed * Vector3.forward } };
                var withPose = Build(human, full); made.Add(withPose);
                var halfPose = Build(human, half); made.Add(halfPose);

                Assert.Less(Quaternion.Angle(InBody(withPose, Bone(withPose)), posed), 0.5f, "вес 1 — кость ровно в рамке позы");
                var halfExpected = Quaternion.Slerp(InBody(halfNoPose, Bone(halfNoPose)), posed, BodyMesh.KeyStep1);
                Assert.Less(Quaternion.Angle(InBody(halfPose, Bone(halfPose)), halfExpected), 0.5f, "ступень 1 — от покоя к позе по сфере на вес ступени");

                // ПРИВЯЗКА — В ПОКОЕ У ВСЕХ: меш тела и куски органов привязаны до позы, поэтому их bindposes с позой и без
                // одинаковы (22.10: поза стояла внутри меша тела, куски Рук привязывались к повёрнутым костям и висели в покое)
                var bindA = Binds(halfNoPose); var bindB = Binds(halfPose);
                Assert.AreEqual(bindA.Count, bindB.Count, "те же рендереры");
                for (int i = 0; i < bindA.Count; i++)
                    for (int k = 0; k < bindA[i].Length; k++)
                        for (int e = 0; e < 16; e++)
                            Assert.AreEqual(bindA[i][k][e], bindB[i][k][e], 1e-4f, $"привязка рендерера №{i} кости №{k} снята не в покое");

                float moved = Skin(noPose).Zip(Skin(withPose), (a, b) => (a - b).magnitude).Max();
                Assert.Greater(moved, 0.05f, "кожа гнётся за костью — поза после привязки, а не запечена в покой");
            }
            finally
            {
                human.keyPose = saved;
                foreach (var g in made) Object.DestroyImmediate(g);
            }
        }

        /// <summary>Поставка позы: при весе 1 головы всех костей из файла встают в его head — цепь кода и цепь, которой модельная
        /// линия выводит суставы покоя, одна и та же (21.10 разошлись: стопа уезжала на 26 см).</summary>
        [Test]
        public void Pose_FromHandoff_JointsLandOnPoseHeads()
        {
            var human = Load("Человек"); var wolf = Load("Волк");
            var pose = human.keyPose?.Where(j => j != null && j.key == "Волк").ToList();
            if (pose == null || pose.Count == 0) Assert.Ignore("файла позы «Волк» в handoff нет");
            var go = Build(human, WolfOn(human, wolf, 99));
            try
            {
                var c = go.transform.Find("Morph");
                var all = c.GetComponentsInChildren<Transform>(true);
                var worst = pose.Select(j => (j.bone, d: all.Where(t => t.name == j.bone).Select(t => (c.InverseTransformPoint(t.position) - j.head).magnitude).DefaultIfEmpty(0f).Min()))
                                .OrderByDescending(x => x.d).First();
                Assert.Less(worst.d, 0.01f, $"сустав «{worst.bone}» мимо позы на {worst.d:0.000} м");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void NoPoseFile_BodyStaysInRest()
        {
            var human = Load("Человек"); var wolf = Load("Волк");
            var saved = human.keyPose;
            try
            {
                human.keyPose = new KeyJoint[0];
                var full = WolfOn(human, wolf, 99);
                var a = Build(human, full); var b = Build(human, full);
                try { Assert.Less(Quaternion.Angle(InBody(a, Bone(a)), InBody(b, Bone(b))), 1e-3f); }
                finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
            }
            finally { human.keyPose = saved; }
        }
    }
}
