using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>СКЕЛЕТ ПО ВЕСУ КЛЮЧА (q-skelet-po-vesu, 15.10): кости тела до привязки меша сдвигаются к суставам цели на вес
    /// ключа. Сторож трёх свойств: на весе 0 скелет прежний; на весе 1 кость стоит на суставе цели; тело в покое — та же
    /// форма-ключ, что без сдвига костей (привязка снята со сдвинутых костей, значит в покое скиннинг тождественный).</summary>
    public class KeySkeletonTests
    {
        static SpeciesSO Human => AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/Человек.asset");
        GameObject go;

        [TearDown]
        public void TearDown()
        {
            BodyMesh.ForceKey = null; BodyMesh.ForceWeight = 0f; BodyMesh.ResetCache();
            if (go != null) Object.DestroyImmediate(go);
            if (savedPose != null) { Human.keyPose = savedPose; savedPose = null; }
        }

        KeyJoint[] savedPose;

        /// <summary>Суставы цели ставятся, только пока у ключа нет позы (при позе её ставит `KeyPose`, суставы не применяются —
        /// 22.10). Сторож мерит путь суставов, поэтому позу на время сборки снимает.</summary>
        Transform Build(float w)
        {
            if (go != null) Object.DestroyImmediate(go);
            if (savedPose == null) { savedPose = Human.keyPose ?? new KeyJoint[0]; Human.keyPose = new KeyJoint[0]; }
            BodyMesh.ForceKey = "Волк"; BodyMesh.ForceWeight = w; BodyMesh.ResetCache();
            go = new GameObject("~скелет-ключ");
            var cc = go.AddComponent<CharacterController>(); cc.height = 2f; cc.center = Vector3.up;
            MorphBuilder.Build(go.transform, Human, Human.organs.Where(o => o != null).ToList(), null);
            return go.transform.Find("Morph");
        }

        static Transform Bone(Transform morph, string name) => morph.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        [Test]
        public void Bones_FollowTargetJoints_ByKeyWeight()
        {
            var h = Human;
            if (h?.bodyMesh == null || h.keyJoints == null || h.keyJoints.Length == 0) Assert.Inconclusive("у человека нет меша или суставов под ключом «Волк»");
            var j = h.keyJoints.First(x => x.key == "Волк" && x.bone == "голень");
            var morph = Build(0f);
            var base0 = morph.InverseTransformPoint(Bone(morph, "голень").position);
            morph = Build(1f);
            var full = morph.InverseTransformPoint(Bone(morph, "голень").position);
            Assert.Less((full - j.head).magnitude, 0.002f, "на весе 1 голень не на суставе цели");
            Assert.Greater((full - base0).magnitude, 0.01f, "сустав цели совпал с человеческим — проверять нечего");
            morph = Build(0.5f);
            var half = morph.InverseTransformPoint(Bone(morph, "голень").position);
            Assert.Less((half - Vector3.Lerp(base0, j.head, 0.5f)).magnitude, 0.002f, "на весе 0.5 голень не посередине");
        }

        /// <summary>Кость модели, которой нет в графе (`стопа` — плюсна, 15.10), тоже едет к суставу цели: скакательному
        /// суставу лапы есть чем гнуться.</summary>
        [Test]
        public void ModelOnlyBone_Foot_FollowsTheHock()
        {
            var h = Human;
            var j = h?.keyJoints?.FirstOrDefault(x => x.key == "Волк" && x.bone == "стопа");
            if (h?.bodyMesh == null || j == null) Assert.Inconclusive("у человека нет кости `стопа` под ключом «Волк»");
            var morph = Build(1f);
            var foot = Bone(morph, "стопа");
            Assert.IsNotNull(foot, "кость стопы из модели не достроена");
            Assert.Less((morph.InverseTransformPoint(foot.position) - j.head).magnitude, 0.002f, "на весе 1 стопа не у скакательного сустава цели");
        }

        [Test]
        public void RestShape_IsTheKeyShape_NotDistortedBySkeleton()
        {
            var h = Human;
            if (h?.bodyMesh == null || h.keyJoints == null || h.keyJoints.Length == 0) Assert.Inconclusive("у человека нет меша или суставов под ключом «Волк»");
            Vector3[] Rest(bool skeleton)
            {
                var saved = h.keyJoints;
                if (!skeleton) h.keyJoints = new KeyJoint[0];
                try
                {
                    var morph = Build(1f);
                    var smr = morph.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "Ноги");
                    var m = new Mesh(); smr.BakeMesh(m, true);
                    var v = m.vertices.Select(p => morph.InverseTransformPoint(smr.transform.TransformPoint(p))).ToArray();
                    Object.DestroyImmediate(m);
                    return v;
                }
                finally { h.keyJoints = saved; }
            }
            var a = Rest(false); var b = Rest(true);
            float worst = a.Zip(b, (p, q) => (p - q).magnitude).Max();
            Assert.Less(worst, 0.001f, $"сдвиг костей исказил тело в покое на {worst * 100f:0.0} см");
        }
    }
}
