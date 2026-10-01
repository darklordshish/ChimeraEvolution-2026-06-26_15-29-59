#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>СТЕНА РЕНДЕРЕРОВ (консилиум формы, 01.10): жёстко несомые детали места запекаются в один скиннед-меш.
    /// Сторожит, что рендереров стало кратно меньше, а геометрия та же: общие границы тела и число треугольников
    /// совпадают с незапечённой сборкой, детали с паспортом (глаза) остались отдельными.</summary>
    public class BakeRigidTests
    {
        static (int rends, int tris, Bounds bounds, int marked) Build(SpeciesSO sp, bool bake)
        {
            MorphBuilder.BakeParts = bake;
            var go = new GameObject("запекание");
            try
            {
                var cc = go.AddComponent<CharacterController>(); cc.height = 2; cc.center = Vector3.up;
                MorphBuilder.Build(go.transform, sp, sp.organs.ToList(), null);
                var rs = go.GetComponentsInChildren<Renderer>().Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
                int tris = 0;
                var b = new Bounds();
                bool first = true;
                // ГРАНИЦЫ — ПО ВЕРШИНАМ, а не `Renderer.bounds`: у скиннед-рендерера в кадре создания они ещё не посчитаны
                // и врут на десяток сантиметров (поймано 01.10). Скиннед — через BakeMesh, то есть после скиннинга
                foreach (var r in rs)
                {
                    Mesh m;
                    if (r is SkinnedMeshRenderer s) { m = new Mesh(); s.BakeMesh(m, true); tris += s.sharedMesh.triangles.Length / 3; }
                    else { m = r.GetComponent<MeshFilter>()?.sharedMesh; if (m != null) tris += m.triangles.Length / 3; }
                    if (m == null) continue;
                    foreach (var v in m.vertices)
                    {
                        var w = r.transform.TransformPoint(v);
                        if (first) { b = new Bounds(w, Vector3.zero); first = false; } else b.Encapsulate(w);
                    }
                }
                return (rs.Length, tris, b, go.GetComponentsInChildren<PartMark>().Length);
            }
            finally { Object.DestroyImmediate(go); MorphBuilder.BakeParts = true; }
        }

        [UnityTest]
        public IEnumerator Hedgehog_FewerRenderers_SameGeometry()
        {
            yield return null;
            var sp = AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/Ёж.asset");
            var raw = Build(sp, bake: false);
            var baked = Build(sp, bake: true);
            Assert.Less(baked.rends * 5, raw.rends, $"рендереров {raw.rends} → {baked.rends}: стена не снята");
            Assert.AreEqual(raw.tris, baked.tris, "запекание потеряло или добавило треугольники");
            Assert.Less(Vector3.Distance(raw.bounds.min, baked.bounds.min) + Vector3.Distance(raw.bounds.max, baked.bounds.max), 0.002f,
                        "границы тела разошлись — детали уехали при запекании");
            Assert.AreEqual(raw.marked, baked.marked, "деталь с паспортом (глаз) запеклась — микшер цвета её потеряет");
        }
    }
}
#endif
