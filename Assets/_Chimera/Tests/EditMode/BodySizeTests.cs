using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>РАЗМЕР ТЕЛА (`BodySize`, спеки `2026-10-20-shkala-morfov.md` и `2026-10-20-razmer-hedzhhalka.md`): визуал
    /// масштабируется равномерно от земли, капсула — тем же множителем с низом на месте, корень остаётся 1.</summary>
    public class BodySizeTests
    {
        static GameObject Rig(float ccCenterY, float ccHeight)
        {
            var go = new GameObject("~размер");
            var cc = go.AddComponent<CharacterController>(); cc.height = ccHeight; cc.radius = 0.4f; cc.center = Vector3.up * ccCenterY;
            new GameObject(BodySize.Morph).transform.SetParent(go.transform, false);
            return go;
        }

        [TestCase(1f, 2f, 0.55f)]    // корень на земле (NPC)
        [TestCase(0f, 2f, 1.68f)]    // корень — центр капсулы (игрок)
        public void Apply_ScalesMorphUniformly_KeepsCapsuleBottom(float centerY, float height, float s)
        {
            var go = Rig(centerY, height);
            try
            {
                var cc = go.GetComponent<CharacterController>();
                float bottom = cc.center.y - cc.height * 0.5f;
                var size = go.AddComponent<BodySize>();
                size.Apply(s);
                Assert.That(go.transform.localScale, Is.EqualTo(Vector3.one), "корень с физикой — масштаб 1");
                Assert.That(go.transform.Find(BodySize.Morph).localScale, Is.EqualTo(Vector3.one * s), "визуал — равномерно");
                Assert.AreEqual(height * s, cc.height, 1e-4f);
                Assert.AreEqual(0.4f * s, cc.radius, 1e-4f);
                Assert.AreEqual(bottom, cc.center.y - cc.height * 0.5f, 1e-4f, "низ капсулы — на месте");
                size.Apply(1f);   // повторная пересборка не копит множитель
                Assert.AreEqual(height, cc.height, 1e-4f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        static SpeciesSO Load(string n) => AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{n}.asset");

        [Test]
        public void FromGround_MeasuresFromGround_NotCapsuleBottom()
        {
            var go = Rig(0f, 2f);
            try
            {
                var size = go.AddComponent<BodySize>();
                size.Apply(1f);
                Assert.AreEqual(0.7f, size.FromGround(1.7f), 1e-4f,
                    "глаз 1.7 м от земли при корне в центре капсулы");
                size.Apply(2f);
                Assert.AreEqual(2.4f, size.FromGround(1.7f), 1e-4f,
                    "высота от земли масштабируется");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Of_PureSpecies_IsFormScale_ChimeraAddsGrowth()
        {
            var hog = Load("Ёж"); var human = Load("Человек"); var wolf = Load("Волк");
            Assert.AreEqual(0.55f, BodySize.Of(hog, hog.organs.Where(o => o != null).ToList()), 1e-4f, "чистый Хеджхалк — маленький мутант");
            Assert.AreEqual(1f, BodySize.Of(human, human.organs.Where(o => o != null).ToList()), 1e-4f);
            var worn = human.organs.Where(o => o != null)
                            .Select(o => o.chassisOnly ? o : wolf.organs.FirstOrDefault(w => w != null && w.slot == o.slot && !w.chassisOnly) ?? o).ToList();
            Assert.That(BodySize.Of(human, worn) * 1.85f, Is.InRange(1.88f, 1.96f), "оборотень 1.92 м");
        }

        /// <summary>Ступни уменьшенного ежа — на земле, а не в воздухе и не под ней: масштаб идёт от начала контейнера.</summary>
        [Test]
        public void Hedgehog_Scaled_StandsOnGround()
        {
            var hog = Load("Ёж");
            var go = new GameObject("~ёж");
            try
            {
                var cc = go.AddComponent<CharacterController>(); cc.height = 1f; cc.center = Vector3.up * 0.5f;
                var worn = hog.organs.Where(o => o != null).ToList();
                MorphBuilder.Build(go.transform, hog, worn, null);
                float before = LowestY(go), top0 = HighestY(go);
                go.AddComponent<BodySize>().Apply(BodySize.Of(hog, worn));
                Assert.AreEqual(before * 0.55f, LowestY(go), 0.01f, "низ тела — у земли");
                Assert.AreEqual(top0 * 0.55f, HighestY(go), 0.02f, "верх — ×0.55");
            }
            finally { Object.DestroyImmediate(go); }
        }

        static float LowestY(GameObject go) => Points(go).Min(p => p.y);
        static float HighestY(GameObject go) => Points(go).Max(p => p.y);
        static System.Collections.Generic.IEnumerable<Vector3> Points(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = new Mesh(); r.BakeMesh(m, true);
                foreach (var v in m.vertices) yield return r.transform.TransformPoint(v);
                Object.DestroyImmediate(m);
            }
            foreach (var f in go.GetComponentsInChildren<MeshFilter>(true))
                if (f.sharedMesh != null) foreach (var v in f.sharedMesh.vertices) yield return f.transform.TransformPoint(v);
        }
    }
}
