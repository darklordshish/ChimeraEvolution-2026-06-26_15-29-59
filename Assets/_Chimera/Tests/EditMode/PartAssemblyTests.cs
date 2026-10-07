using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>СБОРКА ДЕТАЛЕЙ (спека конструктора §10). Деталь синтетическая: два бокса вокруг двух костей цепи волка, вес 1
    /// к своей кости. На родном волке деталь встаёт там, где сделана, поле её цепи не строится, левая сторона — зеркало.
    /// С 06.10 (спека `2026-10-06-ruki-kist.md`) «Руки» чужому шасси — только кистью на шве `запястье`; механизмы переноса
    /// цепи между планами (суставы носителя, узел-риг, культя) сторожатся на НОГАХ — там цепь подставляется.</summary>
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

        /// <summary>Копия волка с синтетической деталью места `slot`: бокс 4 см вокруг середины каждой кости цепи.</summary>
        SpeciesSO WolfWithPart(string slot = BodySlots.Arms, string b0 = "плечо", string b1 = "предплечье")
        {
            var wolf = Object.Instantiate(Load("Волк"));
            wolf.speciesName = "Волк";   // имя — то же: деталь вида, а не нового вида
            trash.Add(wolf);
            var bones = new[] { b0, b1 };
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
            wolf.parts = new[] { new BodyPart { slot = slot, plan = "четвероногий", mesh = mesh, bones = bones.Append("хребет").ToArray(), mirror = true } };
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

        /// <summary>«Руки» чужому шасси — только кистью (06.10): вся передняя нога волка на человека не ставится и цепь руки
        /// не подставляется — человек с волчьим аугментом рук остаётся при своих плече и предплечье.</summary>
        [Test]
        public void WholeArmPart_NotGraftedToOtherChassis()
        {
            var wolf = WolfWithPart();
            var human = Load("Человек");
            var worn = human.organs.Where(o => o.slot != BodySlots.Arms).ToList();
            worn.Insert(0, wolf.organs.First(o => o.slot == BodySlots.Arms));
            var body = ChainSwap.Compose(human, worn);
            Assert.IsFalse(body.placedParts != null && body.placedParts.Length > 0, "вся нога волка встала на человека");
            CollectionAssert.AreEquivalent(human.bones.Where(b => b.limb == "перед").Select(b => b.name),
                                           body.bones.Where(b => b.limb == "перед").Select(b => b.name), "цепь руки человека подменена");
        }

        /// <summary>ЧЕСТНАЯ КИСТЬ (06.10): деталь от настоящего запястья волка (вес на кости с меткой `запястье`) встаёт в
        /// кадр кости носителя с той же меткой — человеческого предплечья — у его запястья; поле предплечья живо.</summary>
        [Test]
        public void HandPart_SitsAtCarrierWrist()
        {
            var wolf = Object.Instantiate(Load("Волк")); wolf.speciesName = "Волк"; trash.Add(wolf);
            var by = wolf.bones.ToDictionary(b => b.name);
            var wristBone = wolf.bones.First(b => b.limb == "перед" && b.mark?.b == "запястье");
            var (wp, wr) = SkeletonBuilder.Place(wristBone, by, new Dictionary<string, (Vector3, Quaternion)>());
            var c = SkeletonBuilder.Tip(wristBone, wp, wr) + wr * Vector3.up * 0.04f;   // 4 см за запястьем
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cm = cube.GetComponent<MeshFilter>().sharedMesh;
            var verts = cm.vertices.Select(v => c + v * 0.02f).ToList();
            var tris = cm.triangles;
            Object.DestroyImmediate(cube);
            var mesh = new Mesh(); mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
            mesh.boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0 = 0, weight0 = 1f }, verts.Count).ToArray();
            trash.Add(mesh);
            var hand = new BodyPart { slot = BodySlots.Arms, plan = "четвероногий", mesh = mesh, bones = new[] { wristBone.name }, mirror = true, seam = "запястье" };
            wolf.parts = new[] { hand };

            var human = Load("Человек");
            var worn = human.organs.Where(o => o.slot != BodySlots.Arms).ToList();
            worn.Insert(0, wolf.organs.First(o => o.slot == BodySlots.Arms));
            var body = ChainSwap.Compose(human, worn);
            Assert.IsTrue(body.placedParts != null && body.placedParts.Any(p => p.part == hand), "кисть волка не выбрана для человека");
            var fore = human.bones.First(b => b.limb == "перед" && b.mark?.b == "запястье").name;
            Assert.IsFalse(body.fieldSkip != null && body.fieldSkip.Contains(fore), "кисть выключила поле предплечья носителя");

            var go = Build(human, worn);
            var (r, _) = BoxCentroids(go, 0);
            var hb = human.bones.ToDictionary(b => b.name);
            var (hp, hr) = SkeletonBuilder.Place(hb[fore], hb, new Dictionary<string, (Vector3, Quaternion)>());
            var wrist = SkeletonBuilder.Tip(hb[fore], hp, hr);
            Assert.Less(Vector3.Distance(r, wrist), 0.08f, "кисть не у запястья носителя");
            var smr = go.GetComponentsInChildren<SkinnedMeshRenderer>().First(x => x.sharedMesh.name.Contains("деталь"));
            Assert.IsTrue(smr.bones.Any(t => t.name == fore), "кисть не ведётся предплечьем носителя");
        }

        /// <summary>Культя видна только при адаптации плана: у волка плечевая кость в туше, у двуногого это плечо руки
        /// (кадр модельной линии 02.10 — без культи «рука краба»).</summary>
        [Test]
        public void Stump_ShownOnlyOnOtherPlan()
        {
            var wolf = WolfWithPart(BodySlots.Legs, "бедро", "голень");
            var part = wolf.parts[0];
            var stump = new Mesh();
            var c = Mid(wolf, "бедро");
            stump.SetVertices(new List<Vector3> { c, c + Vector3.up * 0.02f, c + Vector3.forward * 0.02f });
            stump.SetTriangles(new[] { 0, 1, 2 }, 0);
            stump.boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0 = 0, weight0 = 1f }, 3).ToArray();
            trash.Add(stump);
            part.stump = stump; part.stumpBones = new[] { "бедро" };

            var own = Build(wolf, wolf.organs.ToList());
            Assert.IsNull(own.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "культя"), "на своём плане культя в туше — не рисуется");

            var human = Load("Человек");
            Assert.AreNotEqual(part.plan, human.Plan, "человек должен быть другого плана, иначе тест ничего не проверяет");
            var worn = human.organs.Where(o => o.slot != BodySlots.Legs).ToList();
            worn.Insert(0, wolf.organs.First(o => o.slot == BodySlots.Legs));
            var go = Build(human, worn);
            Assert.IsNotNull(go.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "культя"), "на чужом плане культя — плечо руки, а её нет");
            CollectionAssert.Contains(ChainSwap.Compose(human, worn).fieldSkip, PartAssembly.BodyName(ChainSwap.Compose(human, worn), wolf, "бедро"),
                                      "видимая культя и поле рисуют плечо дважды");
        }

        /// <summary>Вершина культи на кости ВЫШЕ корня цепи (`лопатка` — переход весов к корпусу) ставится кадром корня
        /// цепи, а не кадром чужой лопатки: лопатки волка и человека разные, перенос через них стягивал фланец культи в
        /// шип (кадр 02.10, поставка v6).</summary>
        [Test]
        public void StumpAboveChainRoot_PlacedByChainRoot()
        {
            var wolf = WolfWithPart(BodySlots.Legs, "бедро", "голень");
            var part = wolf.parts[0];
            var by = wolf.bones.ToDictionary(b => b.name);
            var (shoulder, _) = SkeletonBuilder.Place(by["бедро"], by, new Dictionary<string, (Vector3, Quaternion)>());
            var stump = new Mesh();
            stump.SetVertices(new List<Vector3> { shoulder, shoulder + Vector3.up * 0.001f, shoulder + Vector3.forward * 0.001f });
            stump.SetTriangles(new[] { 0, 1, 2 }, 0);
            stump.boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0 = 0, weight0 = 1f }, 3).ToArray();
            trash.Add(stump);
            part.stump = stump; part.stumpBones = new[] { "крестец" };

            var human = Load("Человек");
            var worn = human.organs.Where(o => o.slot != BodySlots.Legs).ToList();
            worn.Insert(0, wolf.organs.First(o => o.slot == BodySlots.Legs));
            var body = ChainSwap.Compose(human, worn);
            var cby = body.bones.ToDictionary(b => b.name);
            var (carrierShoulder, _) = SkeletonBuilder.Place(cby[PartAssembly.BodyName(body, wolf, "бедро")], cby, new Dictionary<string, (Vector3, Quaternion)>());

            var go = Build(human, worn);
            var smr = go.GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.name == "культя");
            var m = new Mesh(); smr.BakeMesh(m, true);
            var v = go.transform.Find("Morph").InverseTransformPoint(smr.transform.TransformPoint(m.vertices[0]));
            Object.DestroyImmediate(m);
            Assert.AreEqual(BodySlots.Legs, part.slot);
            Assert.Less(Vector3.Distance(v, carrierShoulder), 0.01f, "вершина на лопатке ушла от плеча носителя — перенесена кадром чужой лопатки");
        }

        /// <summary>Вес на кости ВЫШЕ шва (лопатка — переход к корпусу, поставка v6d) не делает её корнем цепи: поле лопатки
        /// живо и у родного волка, и у носителя.</summary>
        [Test]
        public void WeightAboveSeam_DoesNotSkipCarrierBone()
        {
            var wolf = WolfWithPart();
            var part = wolf.parts[0];
            var mesh = Object.Instantiate(part.mesh); trash.Add(mesh);
            var w = mesh.boneWeights; w[0] = new BoneWeight { boneIndex0 = 3, weight0 = 0.5f, boneIndex1 = 0, weight1 = 0.5f }; mesh.boneWeights = w;
            part.mesh = mesh; part.bones = part.bones.Append("лопатка").ToArray(); part.seam = "плечо";

            Assert.AreEqual("плечо", PartAssembly.ChainRoot(part, wolf), "корень цепи — кость на шве, а не верхняя кость с весом");
            var body = ChainSwap.Compose(wolf, wolf.organs);
            CollectionAssert.DoesNotContain(body.fieldSkip, "лопатка", "вес у подмышки выключил поле лопатки волка");
            CollectionAssert.Contains(body.fieldSkip, "плечо");
        }

        /// <summary>АДАПТАЦИЯ ПЛАНА — В КОСТЯХ (геймдизайнер 02.10): на носителе другого плана цепь с деталью встаёт по его
        /// суставам — локоть и запястье волчьей руки там же, где у человека. Прежде цепь держала волчий зигзаг, а руку
        /// разворачивал ключ формы, и локоть меша расходился с локтем кости.</summary>
        [Test]
        public void OtherPlan_ChainFollowsCarrierJoints()
        {
            var wolf = WolfWithPart(BodySlots.Legs, "бедро", "голень");
            var human = Load("Человек");
            Assert.AreNotEqual(wolf.parts[0].plan, human.Plan);
            var worn = human.organs.Where(o => o.slot != BodySlots.Legs).ToList();
            worn.Insert(0, wolf.organs.First(o => o.slot == BodySlots.Legs));
            var body = ChainSwap.Compose(human, worn);

            Vector3 TipOf(SpeciesSO sp, System.Func<Bone, bool> pick)
            {
                var by = sp.bones.ToDictionary(b => b.name);
                var b = sp.bones.First(pick);
                var (pos, rot) = SkeletonBuilder.Place(b, by, new Dictionary<string, (Vector3, Quaternion)>());
                return SkeletonBuilder.Tip(b, pos, rot);
            }
            foreach (var joint in new[] { "колено", "скакательный" })
            {
                var want = TipOf(human, b => b.limb == "зад" && b.mark?.b == joint);
                var got = TipOf(body, b => b.limb == "зад" && b.mark?.b == joint);
                Assert.Less(Vector3.Distance(want, got), 0.01f, $"{joint} волчьей цепи не на суставе человека");
            }
        }

        /// <summary>Сустав ниже границы поля (письмо модельной линии 02.10): у волка `запястье` — на узле-риге `пясть` под
        /// концом `предплечье`. Отрезок «предплечье + пясть» встаёт на запястье носителя целиком; пясть поля не даёт.</summary>
        [Test]
        public void RigNodeJoint_SegmentLandsOnCarrierJoint()
        {
            var wolf = WolfWithPart(BodySlots.Legs, "бедро", "голень");
            // с поставки модельной линии 06.10 `пясть` есть у волка на самом деле; до неё — синтетика
            if (!wolf.bones.Any(b => b.name == "плюсна"))
            {
                var bones = wolf.bones.Select(b => JsonUtility.FromJson<Bone>(JsonUtility.ToJson(b))).ToList();
                var fore = bones.First(b => b.name == "голень");
                var rig = new Bone { name = "плюсна", parent = "голень", limb = fore.limb, layer = BodyLayer.Rig, attach = 1f,
                                     length = 0.3f, r0 = 0.02f, r1 = 0.02f, mark = new BoneMarks { b = fore.mark.b } };
                fore.mark = new BoneMarks { a = fore.mark?.a };
                bones.Add(rig);
                wolf.bones = bones.ToArray();
            }

            var human = Load("Человек");
            var worn = human.organs.Where(o => o.slot != BodySlots.Legs).ToList();
            worn.Insert(0, wolf.organs.First(o => o.slot == BodySlots.Legs));
            var body = ChainSwap.Compose(human, worn);
            Vector3 TipOf(SpeciesSO sp, string name)
            {
                var by = sp.bones.ToDictionary(b => b.name);
                var (pos, rot) = SkeletonBuilder.Place(by[name], by, new Dictionary<string, (Vector3, Quaternion)>());
                return SkeletonBuilder.Tip(by[name], pos, rot);
            }
            var wrist = TipOf(human, human.bones.First(b => b.limb == "зад" && b.mark?.b == "скакательный").name);
            Assert.Less(Vector3.Distance(TipOf(body, "плюсна"), wrist), 0.01f, "узел-риг с меткой запястья не на запястье носителя");
            Assert.Greater(Vector3.Distance(TipOf(body, "голень"), wrist), 0.02f, "предплечье легло на запястье — отрезок не цельный");
            Assert.AreEqual(BodyLayer.Rig, body.bones.First(b => b.name == "плюсна").layer);
        }

        /// <summary>Деталь, которая не встанет, поле не гасит (аудит Codex 04.10): кость с весом, которой нет у тела, — часть
        /// рисуется полем, а не дырой.</summary>
        [Test]
        public void UnplaceablePart_KeepsField()
        {
            var wolf = WolfWithPart();
            var part = wolf.parts[0];
            part.bones = part.bones.Select(b => b == "предплечье" ? "нет-такой-кости" : b).ToArray();
            var body = ChainSwap.Compose(wolf, wolf.organs);
            Assert.IsFalse(body.placedParts != null && body.placedParts.Any(p => p.part == part), "неставимая деталь выбрана");
            Assert.IsFalse(body.fieldSkip != null && body.fieldSkip.Contains("плечо"), "неставимая деталь выключила поле — дыра");
        }

        /// <summary>Шов из паспорта в метрах (`ellipse_m`, `ring_m`) — импорт читал `ellipse`, которого нет.</summary>
        [Test]
        public void SeamMetres_ReadFromPassport()
        {
            var pts = SpeciesHandoff.ReadRingM("{\"seam\": {\"ring_m\": [[0.1, 0.2, 0.3], [-1e-2, 2, 3.5]], \"ellipse_m\": [0.03, 0.05]}}");
            Assert.AreEqual(2, pts.Length);
            Assert.AreEqual(new Vector3(-0.01f, 2f, 3.5f), pts[1]);
            var wolf = Load("Волк");
            var real = wolf.parts?.FirstOrDefault(p => p != null && p.slot == BodySlots.Arms);
            Assert.IsNotNull(real, "у волка нет детали «Руки»");
            Assert.Greater(real.ellipse.x, 0f, "эллипс шва не доехал из паспорта (ellipse_m)");
            Assert.AreEqual(8, real.ringM?.Length ?? 0, "точки кольца не доехали из паспорта (ring_m)");
        }
        /// <summary>Кисть волка из точек вокруг его запястья: `pts(tip, axis)` — вершины в теле волка, вес 1 на кость с меткой
        /// `запястье`. Возвращает копию волка с этой деталью.</summary>
        SpeciesSO WolfWithHand(System.Func<Vector3, Vector3, List<Vector3>> pts, out string wristBoneName)
        {
            var wolf = Object.Instantiate(Load("Волк")); wolf.speciesName = "Волк"; trash.Add(wolf);
            var by = wolf.bones.ToDictionary(b => b.name);
            var wristBone = wolf.bones.First(b => b.limb == "перед" && b.mark?.b == "запястье");
            wristBoneName = wristBone.name;
            var (wp, wr) = SkeletonBuilder.Place(wristBone, by, new Dictionary<string, (Vector3, Quaternion)>());
            var verts = pts(SkeletonBuilder.Tip(wristBone, wp, wr), wr * Vector3.up);
            var tris = new List<int>();
            for (int i = 0; i + 2 < verts.Count; i += 3) { tris.Add(i); tris.Add(i + 1); tris.Add(i + 2); }
            var mesh = new Mesh(); mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
            mesh.boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0 = 0, weight0 = 1f }, verts.Count).ToArray();
            trash.Add(mesh);
            wolf.parts = new[] { new BodyPart { slot = BodySlots.Arms, plan = "четвероногий", mesh = mesh, bones = new[] { wristBone.name }, mirror = false, seam = "запястье" } };
            return wolf;
        }

        static List<Vector3> Baked(GameObject go)
        {
            var smr = go.GetComponentsInChildren<SkinnedMeshRenderer>().First(x => x.sharedMesh.name.Contains("деталь"));
            var m = new Mesh(); smr.BakeMesh(m, true);
            var root = go.transform.Find("Morph");
            var res = m.vertices.Select(v => root.InverseTransformPoint(smr.transform.TransformPoint(v))).ToList();
            Object.DestroyImmediate(m);
            return res;
        }

        /// <summary>КАЛИБР НА КОЛЬЦЕ ЗАПЯСТЬЯ НЕПРЕРЫВЕН (письмо модельной линии 07.10): точка за долю миллиметра до конца кости и
        /// за долю миллиметра после — соседи и на носителе. Прежде на кости брался радиус начала, за концом — конца, и кольцо
        /// у лося расходилось на ≈4.5 см («манжета»).</summary>
        [Test]
        public void HandRing_CalibreContinuousAcrossWrist()
        {
            var wolf = WolfWithHand((tip, axis) =>
            {
                var side = Vector3.Cross(axis, Vector3.forward).normalized * 0.03f;
                return new List<Vector3> { tip - axis * 1e-4f + side, tip + axis * 1e-4f + side, tip + axis * 0.02f };
            }, out _);
            var human = Load("Человек");   // не опорная — подгонки к земле нет, виден только калибр
            var worn = human.organs.Where(o => o.slot != BodySlots.Arms).ToList();
            worn.Insert(0, wolf.organs.First(o => o.slot == BodySlots.Arms));
            var v = Baked(Build(human, worn));
            Assert.Less(Vector3.Distance(v[0], v[1]), 0.005f, "калибр прыгает на кольце запястья");
        }

        /// <summary>КИСТЬ НА ОПОРНОЙ КОНЕЧНОСТИ СТОИТ НА ЗЕМЛЕ (стойка за шасси): волчья кисть на лосе — низ на земле, а не в
        /// воздухе (письмо модельной линии 07.10: коготь висел в 4 см).</summary>
        [Test]
        public void HandOnStanceLimb_ReachesGround()
        {
            // кисть настоящей длины: у самого волка достаёт до земли (как лапа под запястьем)
            var wolf = WolfWithHand((tip, axis) =>
            {
                float h = tip.y / Mathf.Max(0.1f, -axis.y);
                return new List<Vector3> { tip + axis * 0.3f * h, tip + axis * 0.6f * h + Vector3.forward * 0.02f, tip + axis * h };
            }, out _);
            var moose = Load("Лось");
            Assert.IsTrue(moose.stanceLimbs != null && moose.stanceLimbs.Contains(BodySlots.Arms), "у лося «Руки» — опорные");
            var worn = moose.organs.Where(o => o.slot != BodySlots.Arms).ToList();
            worn.Insert(0, wolf.organs.First(o => o.slot == BodySlots.Arms));
            var low = Baked(Build(moose, worn)).Min(p => p.y);
            Assert.AreEqual(0f, low, 0.01f, "низ кисти на опорной конечности не на земле");
        }
    }
}
