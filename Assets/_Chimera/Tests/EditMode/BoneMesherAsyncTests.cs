using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>ОБОЛОЧКА В ФОНЕ (спека двух слоёв, шаг 0): расчёт поля ушёл в фоновую задачу, на главном потоке осталась
    /// только упаковка в меши. Сторожит, что фоновая оболочка ТА ЖЕ, что синхронная (до треугольника и вершины), и что
    /// `Ready` честен: до прогрева — нет, после досчёта — да.</summary>
    public class BoneMesherAsyncTests
    {
        static SpeciesSO Copy(string name)
        {
            var sp = Object.Instantiate(AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{name}.asset"));
            sp.speciesName = name + "~фон~" + System.Guid.NewGuid().ToString("N");   // свой ключ кэша
            return sp;
        }

        static (int tris, int verts) Shell(SpeciesSO sp)
        {
            var go = new GameObject("фон");
            try
            {
                BoneMesher.Build(go.transform, sp, null);
                int t = 0, v = 0;
                foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                { t += r.sharedMesh.triangles.Length / 3; v += r.sharedMesh.vertexCount; }
                return (t, v);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Warmed_EqualsSynchronous_AndReadyIsHonest()
        {
            var sync = Copy("Волк");
            var warm = Copy("Волк");
            try
            {
                Assert.IsFalse(BoneMesher.Ready(warm), "оболочки ещё нет, а Ready говорит «готово»");
                BoneMesher.Warm(warm);
                var until = System.DateTime.Now.AddSeconds(20);
                while (!BoneMesher.Ready(warm) && System.DateTime.Now < until) System.Threading.Thread.Sleep(5);
                Assert.IsTrue(BoneMesher.Ready(warm), "фон не досчитал за 20 с");
                Assert.AreEqual(Shell(sync), Shell(warm), "фоновая оболочка разошлась с синхронной");
            }
            finally { Object.DestroyImmediate(sync); Object.DestroyImmediate(warm); }
        }
    }
}
