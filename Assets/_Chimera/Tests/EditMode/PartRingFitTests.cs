using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>КУСОК МЕША ВИДА НА ЧУЖОМ ТЕЛЕ — посадка кольцом (пилот Б, спека `2026-10-09-chistyj-vid-meshem.md`). Кусок
    /// встаёт целиком, одним масштабом по отношению эллипсов шва, и несёт своё место со всем, что на нём висит. Правила
    /// выбора сняты с матрицы химер 10.10: первая версия ставила кусок и своему шасси (пропали чужие уши и рога на волке)
    /// и змее, где детали повисли в воздухе. В игре целые куски выключены (`PartAssembly.WholePieces`) — сторож включает их сам.</summary>
    public class PartRingFitTests
    {
        static SpeciesSO Load(string n) => AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{n}.asset");
        readonly List<Object> trash = new();

        [SetUp] public void SetUp() { PartAssembly.WholePieces = true; ChainSwap.ResetCache(); }
        [TearDown]
        public void TearDown()
        {
            PartAssembly.WholePieces = false;
            foreach (var o in trash) if (o != null) Object.DestroyImmediate(o);
            trash.Clear();
            ChainSwap.ResetCache();
        }

        static BodyPart Head(SpeciesSO wolf)
        {
            var p = wolf.parts?.FirstOrDefault(x => x != null && x.ringFit);
            if (p == null) Assert.Inconclusive("у волка нет куска с посадкой кольцом — паспорт `volk-golova-kusok` не принят");
            return p;
        }

        static List<Organ> With(SpeciesSO chassis, SpeciesSO donor, string slot)
        {
            var worn = chassis.organs.Where(o => o != null && o.slot != slot).ToList();
            worn.Insert(0, donor.organs.First(o => o.slot == slot));
            return worn;
        }

        GameObject Build(SpeciesSO chassis, List<Organ> worn)
        {
            var go = new GameObject("~кольцо"); trash.Add(go);
            var cc = go.AddComponent<CharacterController>(); cc.height = 2f; cc.center = Vector3.up;
            MorphBuilder.Build(go.transform, chassis, worn, null);
            return go;
        }

        static BodyPartView Whole(GameObject go) =>
            go.GetComponentsInChildren<BodyPartView>(true).FirstOrDefault(v => !string.IsNullOrEmpty(v.whole));

        [Test]
        public void WolfHead_OnHuman_SitsWhole_AtSeamScale_AndHidesCarrierFace()
        {
            var wolf = Load("Волк"); var human = Load("Человек");
            var head = Head(wolf);
            var go = Build(human, With(human, wolf, head.slot));

            var piece = Whole(go);
            Assert.IsNotNull(piece, "кусок на чужое тело не встал");
            Assert.AreEqual("голова", piece.whole, "кусок головы обязан назвать место, которое несёт целиком");
            Assert.AreEqual(head.slot, piece.name, "имя рендерера куска — слот (контракт имён частей)");

            // места на голове носителя не рисуются: голова приехала со своими ушами, глазами и носом
            var onHead = new[] { "уши", "глаза", "нос" };
            var drawn = go.GetComponentsInChildren<Renderer>(true).Where(r => r.GetComponent<BodyPartView>() == null).Select(r => r.name);
            CollectionAssert.IsEmpty(drawn.Where(onHead.Contains), "под целой головой нарисованы места носителя");

            // масштаб — один, по шву: габарит куска на носителе = габарит в теле донора × k
            float k = PartAssembly.RingScale(ChainSwap.Compose(human, With(human, wolf, head.slot)), wolf, head);
            Assert.That(k, Is.InRange(0.3f, 1f), "шея человека тоньше волчьей — масштаб обязан быть меньше единицы");
            var own = head.mesh.vertices.Select(v => head.toBody.MultiplyPoint3x4(v)).ToArray();
            float donorSpan = Span(own);
            var smr = piece.GetComponent<SkinnedMeshRenderer>();
            float carrierSpan = Span(smr.sharedMesh.vertices);
            Assert.AreEqual(donorSpan * k, carrierSpan, donorSpan * 0.01f, "кусок растянут не одним масштабом шва");
        }

        // наибольшее расстояние от центра вершин — мера размера, не зависящая от поворота куска
        static float Span(Vector3[] v)
        {
            var c = v.Aggregate(Vector3.zero, (a, b) => a + b) / v.Length;
            return v.Max(p => (p - c).magnitude);
        }

        /// <summary>ХИМЕРА НА МЕШЕ ВИДА: у шасси есть меш, чужой орган приехал куском — тело остаётся объектами меша шасси,
        /// место куска отдано куску, поле не строится. Язык формы у вида и химеры один (вариант Б спеки).</summary>
        [Test]
        public void MeshChassis_WithForeignPiece_KeepsItsMesh_AndGivesThePlaceAway()
        {
            var wolf = Load("Волк"); var human = Load("Человек");
            var head = Head(wolf);
            if (human.bodyMesh == null) Assert.Inconclusive("у человека нет меша вида — `Models/chelovek_mesh.fbx` не принят");
            var go = Build(human, With(human, wolf, head.slot));

            var piece = Whole(go);
            Assert.IsNotNull(piece, "кусок на меш шасси не встал");
            var own = go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.GetComponent<BodyPartView>() == null).ToList();
            CollectionAssert.IsNotEmpty(own, "тело шасси не нарисовано");
            var src = new HashSet<string>(human.bodyMesh.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.sharedMesh.vertexCount + "|" + r.name.Split('.')[0]));
            foreach (var r in own)
                Assert.IsTrue(src.Contains(r.sharedMesh.vertexCount + "|" + r.name), $"рендерер «{r.name}» — не объект меша шасси: тело собрано полем");
            CollectionAssert.DoesNotContain(own.Select(r => r.name), piece.whole, "своя голова шасси осталась под чужой");
            CollectionAssert.IsEmpty(go.GetComponentsInChildren<MeshRenderer>(true), "у химеры на меше детали органов не рисуются");
        }

        [Test]
        public void OwnChassis_NeverTakesItsWholePiece()
        {
            var wolf = Load("Волк"); var elk = Load("Лось");
            Head(wolf);
            // волк с чужим Чутьём — на поле, и своя голова-кусок проглотила бы лосиные уши
            var go = Build(wolf, With(wolf, elk, BodySlots.Sense));
            Assert.IsNull(Whole(go), "своему шасси кусок меша вида не ставится");
        }

        [Test]
        public void Snake_KeepsOldPath()
        {
            var wolf = Load("Волк"); var snake = Load("Змея");
            var head = Head(wolf);
            Assert.IsNull(Whole(Build(snake, With(snake, wolf, head.slot))), "кусок встал на змею: её голова — единственный узел графа, тело — звенья; матрица 10.10 дала семь И2");
        }

        [Test]
        public void InGame_WholePiecesAreOff()
        {
            PartAssembly.WholePieces = false;
            ChainSwap.ResetCache();
            var wolf = Load("Волк"); var human = Load("Человек");
            var head = Head(wolf);
            Assert.IsNull(Whole(Build(human, With(human, wolf, head.slot))), "целая голова от одного органа выключена до нарезки по органам");
        }
    }
}
