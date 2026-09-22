using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>ЗВЕНО ЦЕПИ — РИГБЛОК `звено` (предложение модельной линии, поставка 7 §4). Капсула с шаром-суставом на звено
    /// давали змее ~27 тыс. треугольников из 27.6 на клетке 0.042 — почти вся цена вида была в примитивах цепи. Блок
    /// звена не сходится торцом в точку, как капсула, поэтому шар на стыке не нужен. Нет блока в каталоге — цепь
    /// рисуется прежними примитивами, а имя копится в долге библиотеки.</summary>
    public class ChainLinkBlockTests
    {
        GameObject go;

        [TearDown]
        public void TearDown()
        {
            MorphBuilder.ResetCatalog();
            if (go != null) Object.DestroyImmediate(go);
        }

        static SpeciesSO Chain(string name)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = name;
            so.tint = Color.gray;
            so.baseHp = 10;
            so.sockets = new[]
            {
                new BodySocket { name = "хребет", localPos = new Vector3(0f, 0.2f, 0f), chain = 4, linkLength = 0.3f, linkDiameter = 0.2f, solid = true },
            };
            so.organs = new[] { new Organ { organName = "Хребет", slot = "хребет", chassisOnly = true } };
            return so;
        }

        static Mesh SomeMesh()
        {
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mesh = probe.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(probe);
            return mesh;
        }

        MeshFilter[] Build(SpeciesSO so)
        {
            go = new GameObject("~ТестЦепи");
            MorphBuilder.Build(go.transform, so, so.organs.ToList());
            return go.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name.StartsWith("хребет")).ToArray();
        }

        [Test]
        public void ChainLink_IsBlock_WithoutJointSpheres()
        {
            var link = SomeMesh();
            var cat = ScriptableObject.CreateInstance<ShapeCatalog>();
            cat.blocks = new[] { new ShapeCatalog.Block { name = "звено", mesh = link } };
            MorphBuilder.SetCatalog(cat);

            var parts = Build(Chain("~цепь-блоком"));

            Assert.AreEqual(4, parts.Length, "на звено должен быть ровно один кусок: суставов-шаров при блоке не нужно");
            Assert.IsTrue(parts.All(p => p.sharedMesh == link), "звено нарисовано не блоком");
            Assert.IsTrue(parts.All(p => p.GetComponent<Collider>() != null), "звено плотного места потеряло коллайдер — змею не во что бить");
        }

        [Test]
        public void ChainLink_WithoutBlock_KeepsPrimitives()
        {
            MorphBuilder.SetCatalog(null);
            var parts = Build(Chain("~цепь-примитивами"));
            Assert.Greater(parts.Length, 4, "без блока звено рисуется капсулой с суставами, как раньше");
        }
    }
}
