using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>СЛИЯНИЕ С ЧУЖИМ МЕСТОМ (`Bone.weld`, критик волка r2–r3, 09.10): между местами поле объединяется почти резко,
    /// и в углу стыка остаётся борозда — на волке тонкая тёмная линия по дну груди и бокам бёдер. Ручка узла заливает
    /// угол: объём тела растёт; ноль — прежнее поведение (общая `Weld`), до треугольника.</summary>
    public class WeldTests
    {
        static SpeciesSO TwoPlaces(float weld)
        {
            var sp = ScriptableObject.CreateInstance<SpeciesSO>();
            sp.speciesName = "проба.слияние~" + System.Guid.NewGuid().ToString("N");
            sp.bones = new[]
            {
                new Bone { name = "ствол", socket = "хребет", length = 0.6f, r0 = 0.1f, r1 = 0.1f, blend = 0.08f, origin = new Vector3(0, 0.5f, 0) },
                new Bone { name = "ветвь", parent = "ствол", socket = "Ноги", attach = 0.5f, length = 0.4f, r0 = 0.09f, r1 = 0.09f,
                           blend = 0.08f, dir = new Vector3(90f, 0f, 0f), weld = weld, layer = BodyLayer.Muscle },
            };
            return sp;
        }

        static (float volume, int tris) Shell(SpeciesSO sp)
        {
            var go = new GameObject("слияние");
            try
            {
                BoneMesher.Build(go.transform, sp, null);
                float vol = 0f; int tris = 0;
                foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var m = r.sharedMesh; var v = m.vertices; var t = m.triangles;
                    for (int i = 0; i < t.Length; i += 3) vol += Vector3.Dot(v[t[i]], Vector3.Cross(v[t[i + 1]], v[t[i + 2]])) / 6f;
                    tris += t.Length / 3;
                }
                return (Mathf.Abs(vol), tris);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void NodeWeld_FillsTheCreaseBetweenPlaces_ZeroKeepsDefault()
        {
            var none = TwoPlaces(0f); var dflt = TwoPlaces(0.3f); var full = TwoPlaces(1f);
            try
            {
                var (v0, t0) = Shell(none); var (vd, td) = Shell(dflt); var (v1, _) = Shell(full);
                Assert.AreEqual(t0, td, "weld = 0 обязан значить общую Weld (0.3) — прежняя оболочка");
                Assert.AreEqual(v0, vd, 1e-6f, "weld = 0 обязан значить общую Weld (0.3) — прежняя оболочка");
                Assert.Greater(v1, v0 * 1.002f, $"weld = 1 не залил угол стыка: объём {v1:0.0000} против {v0:0.0000}");
            }
            finally { Object.DestroyImmediate(none); Object.DestroyImmediate(dflt); Object.DestroyImmediate(full); }
        }
    }
}
