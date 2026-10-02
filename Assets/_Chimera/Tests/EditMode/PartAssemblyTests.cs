using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>СБОРКА ДЕТАЛЕЙ (спека конструктора §10, пилот — передняя нога волка). Деталь синтетическая: два бокса вокруг
    /// костей `плечо` и `предплечье` волка, вес 1 к своей кости. Сторожит: на родном волке деталь встаёт там, где сделана,
    /// и поле её цепи не строится; на человеке с волчьими руками бокс предплечья садится на середину предплечья носителя;
    /// левая сторона — зеркало правой.</summary>
    public class PartAssemblyTests
    {
        static SpeciesSO Load(string n) => AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{n}.asset");
        readonly List<Object> trash = new();

        [SetUp] public void SetUp() => ChainSwap.ResetCache();
        [TearDown] public void TearDown() { foreach (var o in trash) if (o != null) Object.DestroyImmediate(o); trash.Clear(); ChainSwap.ResetCache(); }

        static Vector3 Mid(SpeciesSO sp, string bone)
        {
            var by = sp.bones.ToDictionary(b => b.name);
            var (p, r) = SkeletonBuilder.Place(by[bone], by, new Dictionary<string, (Vector3, Quaternion)>());
            return (p + SkeletonBuilder.Tip(by[bone], p, r)) * 0.5f;
        }

        /// <summary>Копия волка с синтетической деталью «Руки»: бокс 4 см вокруг середины каждой кости цепи.</summary>
        SpeciesSO WolfWithPart()
        {
            var wolf = Object.Instantiate(Load("Волк"));
            wolf.speciesName = "Волк";   // имя — то же: деталь вида, а не нового вида
            trash.Add(wolf);
            var bones = new[] { "плечо", "предплечье" };
            var verts = new List<Vector3>(); var tris = new List<int>(); var w = new List<BoneWeight>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cm = cube.GetComponent<MeshFilter>().sharedMesh;
            for (int b = 0; b < bones.Length; b++)
            {
                var c = Mid(wolf, bones[b]);
                int start = verts.Count;
                foreach (var v in cm.vertices) { verts.Add(c + v * 0.04f); w.Add(new BoneWeight { boneIndex0 = b, weight0 = 1f }); }
                foreach (var t in cm.triangles) tris.Add(start + t);
            }
            Object.DestroyImmediate(cube);
            var mesh = new Mesh(); mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.boneWeights = w.ToArray();
            mesh.bindposes = new[] { Matrix4x4.identity, Matrix4x4.identity, Matrix4x4.identity };
            trash.Add(mesh);
            // АРМАТУРА — ВЕСЬ ГРАФ, КАК В FBX ЛИНИИ: лишняя кость `хребет` без весов. Деталь ею не владеет — поле хребта живо
            wolf.parts = new[] { new BodyPart { slot = BodySlots.Arms, plan = "четвероногий", mesh = mesh, bones = bones.Append("хребет").ToArray(), mirror = true } };
            return wolf;
        }

        static (Vector3 right, Vector3 left) BoxCentroids(GameObject go, int box)
        {
            var smr = go.GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.sharedMesh.name.Contains("деталь"));
            var m = new Mesh(); smr.BakeMesh(m, true);
            var v = m.vertices; Object.DestroyImmediate(m);
            int per = 24, half = v.Length / 2;   // куб Unity — 24 вершины; правая сторона — первая половина
            Vector3 Avg(int from) { var s = Vector3.zero; for (int i = 0; i < per; i++) s += v[from + i]; return s / per; }
            var root = go.transform.Find("Morph");
            return (root.InverseTransformPoint(smr.transform.TransformPoint(Avg(box * per))),
                    root.InverseTransformPoint(smr.transform.TransformPoint(Avg(half + box * per))));
        }

        GameObject Build(SpeciesSO chassis, List<Organ> worn)
        {
            var go = new GameObject("деталь"); trash.Add(go);
            var cc = go.AddComponent<CharacterController>(); cc.height = 2f; cc.center = Vector3.up;
            MorphBuilder.Build(go.transform, chassis, worn, null);
            return go;
        }

        [Test]
        public void NativeWolf_PartSitsWhereMade_FieldSkipsItsChain()
        {
            var wolf = WolfWithPart();
            var body = ChainSwap.Compose(wolf, wolf.organs);
            Assert.AreNotSame(wolf, body, "волк со своей деталью — составное тело");
            CollectionAssert.IsSubsetOf(new[] { "плечо", "предплечье" }, body.fieldSkip, "поле рисует цепь, которую рисует деталь");
            CollectionAssert.DoesNotContain(body.fieldSkip, "хребет", "деталь выключила поле кости без весов — тело пропадёт");

            var go = Build(wolf, wolf.organs.ToList());
            var (r, l) = BoxCentroids(go, 1);
            var mid = Mid(wolf, "предплечье");
            Assert.Less(Vector3.Distance(r, mid), 0.002f, "на родном шасси деталь уехала с места, где сделана");
            Assert.Less(Vector3.Distance(l, new Vector3(-mid.x, mid.y, mid.z)), 0.002f, "левая сторона — не зеркало правой");
        }

        [Test]
        public void WolfArmsOnHuman_PartRidesCarrierChain()
        {
            var wolf = WolfWithPart();
            var human = Load("Человек");
            var worn = human.organs.Where(o => o.slot != BodySlots.Arms).ToList();
            worn.Insert(0, wolf.organs.First(o => o.slot == BodySlots.Arms));
            var body = ChainSwap.Compose(human, worn);
            Assert.IsNotNull(body.placedParts, "деталь волка не выбрана для человека");

            var go = Build(human, worn);
            var (r, _) = BoxCentroids(go, 1);
            Assert.Less(Vector3.Distance(r, Mid(body, "предплечье")), 0.01f, "бокс предплечья не сел на предплечье носителя");
        }
    }
}
