using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>БИБЛИОТЕКА ФОРМ: кусок называет блок строкой, каталог отвечает мешем.
    /// Сторожит три вещи, каждая из которых ломается МОЛЧА: блок из каталога действительно попадает в
    /// модель; блока нет — кусок не исчезает, а откатывается к примитиву И ПОПАДАЕТ В ДОЛГ; габарит блока
    /// считается как у куба (компенсация по Y — свойство примитива Unity, а не наших данных).</summary>
    public class FormBlocksTests
    {
        GameObject root;

        SpeciesSO MakeSpecies(string block, PartShape shape)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = "Проба";
            so.tint = Color.gray;
            so.mutagenPool = 16;
            so.baseHp = 75;
            so.sockets = new[]
            {
                new BodySocket { name = "хребет", localPos = Vector3.zero, baseSize = new Vector3(0.33f, 0.48f, 1.29f) },
                new BodySocket { name = "голова", parent = "хребет", attach = 1f, baseSize = new Vector3(0.26f, 0.22f, 0.46f) },
                new BodySocket { name = "Пасть", parent = "голова", attach = 1f, baseSize = new Vector3(0.20f, 0.20f, 0.20f) },
            };
            so.organs = new[]
            {
                new Organ { organName = "Хребет", slot = "хребет", chassisOnly = true },
                new Organ { organName = "Пасть", slot = "Пасть", cost = 3,
                            visualParts = new[] { new OrganPart { scale = Vector3.one, shape = shape, block = block } } },
            };
            so.bones = new Bone[0];
            return so;
        }

        GameObject BuildPart(string block, PartShape shape = PartShape.Cube)
        {
            var so = MakeSpecies(block, shape);
            root = new GameObject("ФормаБлоком");
            MorphBuilder.Build(root.transform, so, new[] { so.organs[0], so.organs[1] });
            var container = root.transform.Find("Morph");
            Assert.IsNotNull(container, "контейнер Morph не собран");
            var part = container.GetComponentsInChildren<MeshFilter>(true)
                                .FirstOrDefault(m => m.name == "Пасть");
            Assert.IsNotNull(part, "кусок Пасти не нарисован");
            return part.gameObject;
        }

        static ShapeCatalog CatalogWith(string name, Mesh mesh)
        {
            var cat = ScriptableObject.CreateInstance<ShapeCatalog>();
            cat.blocks = new[] { new ShapeCatalog.Block { name = name, mesh = mesh } };
            return cat;
        }

        static Mesh SomeMesh()
        {
            var probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var mesh = probe.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(probe);
            return mesh;
        }

        [SetUp]
        public void SetUp() => MorphBuilder.SetCatalog(null);   // каталог из Resources в тест не тянем

        [TearDown]
        public void TearDown()
        {
            // ВЕРНУТЬ КАТАЛОГ ИЗ RESOURCES, А НЕ ОСТАВИТЬ «ПУСТОЙ». Прежний `SetCatalog(null)` здесь помечал каталог
            // найденным-и-пустым до перезагрузки домена: после прогона тестов `chimera-species`, карта тел и кадры
            // рисовали все ригблоки кубами без единой ошибки (поймано 17.09 на поставке 4: 26 примитивов у волка)
            MorphBuilder.ResetCatalog();
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void ResetCatalog_ReturnsResourcesCatalog_AfterTestsEmptiedIt()
        {
            MorphBuilder.SetCatalog(null);
            MorphBuilder.ResetCatalog();
            Assert.AreSame(Resources.Load<ShapeCatalog>(ShapeCatalog.ResourceName), MorphBuilder.Catalog,
                           "после сброса билдер не ищет каталог в Resources — блоки нарисуются кубами");
        }

        [Test]
        public void Block_FromCatalog_IsDrawn()
        {
            var mesh = SomeMesh();
            MorphBuilder.SetCatalog(CatalogWith("клин", mesh));

            var part = BuildPart("клин");

            Assert.AreSame(mesh, part.GetComponent<MeshFilter>().sharedMesh, "кусок нарисован не мешем блока");
            Assert.IsFalse(MorphBuilder.MissingBlocks.Contains("клин"), "найденный блок попал в долг");
        }

        [Test]
        public void Block_Missing_FallsBackToPrimitive_AndIsRecorded()
        {
            MorphBuilder.SetCatalog(CatalogWith("капля", SomeMesh()));   // «клина» в каталоге нет

            var part = BuildPart("клин");

            Assert.IsNotNull(part.GetComponent<MeshFilter>().sharedMesh, "кусок пропал вместо отката к примитиву");
            Assert.IsTrue(MorphBuilder.MissingBlocks.Contains("клин"),
                          "ненайденный блок подменён молча — опечатку в имени никто не увидит");
        }

        [Test]
        public void Block_Size_CountedLikeCube_NotCapsule()
        {
            // Блок описан габаритом 1×1×1 с центром в нуле, поэтому масштаб куска идёт в него как есть —
            // даже если у куска заодно проставлен `shape = Capsule`, компенсация примитива блока не касается
            MorphBuilder.SetCatalog(CatalogWith("клин", SomeMesh()));
            var withBlock = BuildPart("клин", PartShape.Capsule).transform.localScale;
            Object.DestroyImmediate(root);

            MorphBuilder.SetCatalog(null);
            var asCube = BuildPart(null).transform.localScale;

            Assert.AreEqual(asCube.y, withBlock.y, 1e-5f, "габарит блока посчитан как у капсулы, а не как у куба");
            Assert.AreEqual(asCube.x, withBlock.x, 1e-5f);
            Assert.AreEqual(asCube.z, withBlock.z, 1e-5f);
        }

        [Test]
        public void Catalog_Find_ByName_OrNull()
        {
            var mesh = SomeMesh();
            var cat = CatalogWith("клин", mesh);
            Assert.AreSame(mesh, cat.Find("клин"));
            Assert.IsNull(cat.Find("брусок"));
            Assert.IsNull(cat.Find(""));
        }
    }
}
