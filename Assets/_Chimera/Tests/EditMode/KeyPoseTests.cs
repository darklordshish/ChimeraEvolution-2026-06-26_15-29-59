using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>ПОЗА СТУПЕНИ (`KeyPose`, решение 21.10): кости поворачиваются от покоя шасси к позе цели на вес ключа —
    /// сферически и после привязки, так что кожа гнётся за ними. Позу подаём руками: хребет наклонён на 20°.</summary>
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

                Assert.Less(Quaternion.Angle(InBody(withPose, Bone(withPose)), Tilt * InBody(noPose, Bone(noPose))), 0.5f, "вес 1 — поворот позы целиком");
                var halfTilt = Quaternion.Slerp(Quaternion.identity, Tilt, BodyMesh.KeyStep1);
                Assert.Less(Quaternion.Angle(InBody(halfPose, Bone(halfPose)), halfTilt * InBody(halfNoPose, Bone(halfNoPose))), 0.5f, "ступень 1 — по сфере на вес ступени");

                float moved = Skin(noPose).Zip(Skin(withPose), (a, b) => (a - b).magnitude).Max();
                Assert.Greater(moved, 0.05f, "кожа гнётся за костью — поза после привязки, а не запечена в покой");
            }
            finally
            {
                human.keyPose = saved;
                foreach (var g in made) Object.DestroyImmediate(g);
            }
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
