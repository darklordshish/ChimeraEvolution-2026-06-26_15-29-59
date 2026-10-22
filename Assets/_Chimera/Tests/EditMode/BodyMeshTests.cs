using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>МЕШ ЧИСТОГО ВИДА (спека `2026-10-09-chistyj-vid-meshem.md`). Модель встаёт на скелет графа ПО ИМЕНАМ
    /// костей, а поза костей самой модели в расчёт не идёт: экспорт Blender→FBX крутит оси по-своему и вешает на корень
    /// масштаб. Поэтому модель в тестах намеренно «кривая» — корень ×100 с поворотом, кости развёрнуты как попало, — а
    /// вершины обязаны лечь в систему тела точь-в-точь.</summary>
    public class BodyMeshTests
    {
        GameObject go, model;
        SpeciesSO so;
        Organ graft;

        [TearDown]
        public void TearDown()
        {
            BodyMesh.Enabled = true;
            BodyMesh.ForceKey = null;
            foreach (var o in new Object[] { go, model, so }) if (o != null) Object.DestroyImmediate(o);
        }

        // точки тела (метры, система тела): по одной вершине на кость + треугольник вокруг, чтобы меш был мешем
        static readonly Vector3 OnSpine = new(0f, 0.5f, 0.1f), OnHead = new(0f, 0.6f, 0.45f), OnJaw = new(0f, 0.5f, 0.5f);
        static readonly Vector3 JawJoint = new(0f, 0.52f, 0.35f);

        SpeciesSO Species()
        {
            so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = "~меш-вида";
            so.tint = Color.gray;
            so.baseHp = 10;
            so.sockets = new[]
            {
                new BodySocket { name = "хребет", localPos = new Vector3(0f, 0.5f, 0f), baseSize = new Vector3(0.3f, 0.3f, 0.6f) },
                new BodySocket { name = "Пасть", parent = "хребет", localPos = new Vector3(0f, 0f, 0.4f), baseSize = new Vector3(0.1f, 0.1f, 0.2f),
                                 parts = new[] { new OrganPart { scale = Vector3.one } } },
            };
            so.organs = new[]
            {
                new Organ { organName = "Хребет", slot = "хребет", chassisOnly = true },
                new Organ { organName = "Пасть", slot = "Пасть", visualParts = new[] { new OrganPart { scale = Vector3.one } } },
            };
            so.bones = new[]
            {
                new Bone { name = "хребет", socket = "хребет", origin = new Vector3(0f, 0.5f, -0.3f),
                           dir = new Vector3(90f, 0f, 0f), length = 0.6f, r0 = 0.15f, r1 = 0.15f },
                new Bone { name = "голова", parent = "хребет", socket = "голова", origin = new Vector3(0f, 0.55f, 0.3f),
                           dir = new Vector3(90f, 0f, 0f), length = 0.2f, r0 = 0.1f, r1 = 0.08f },
            };
            graft = new Organ { organName = "Чужая пасть", slot = "Пасть", visualParts = new[] { new OrganPart { scale = Vector3.one } } };
            return so;
        }

        /// <summary>Модель «как из FBX»: корень с масштабом и поворотом, арматура, кости с произвольными поворотами,
        /// объект `голова` с тремя вершинами — на хребте, на голове и на челюсти (кости `челюсть` в графе нет).</summary>
        GameObject Model()
        {
            model = new GameObject("~модель");
            model.transform.localScale = Vector3.one * 100f;
            model.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var arm = new GameObject("Armature").transform; arm.SetParent(model.transform, false);
            // модель стоит в нуле, так что мир теста и есть система тела; искажение корня кости компенсируют сами
            Transform Bone(string name, Transform parent, Vector3 bodyPos, Vector3 euler)
            {
                var t = new GameObject(name).transform;
                t.SetParent(parent, false);
                t.position = bodyPos;
                t.rotation = Quaternion.Euler(euler);
                return t;
            }
            var spine = Bone("хребет", arm, new Vector3(0f, 0.4f, -0.2f), new Vector3(13f, 71f, -40f));
            var head = Bone("голова", spine, new Vector3(0f, 0.6f, 0.3f), new Vector3(-55f, 10f, 120f));
            var jaw = Bone("челюсть", head, JawJoint, new Vector3(20f, 0f, 0f));

            var obj = new GameObject("голова");
            obj.transform.SetParent(model.transform, false);
            var bones = new[] { spine, head, jaw };
            // вершины — в локальной системе объекта: он ребёнок искажённого корня
            var w2o = obj.transform.worldToLocalMatrix;
            var body = new[] { OnSpine, OnHead, OnJaw };
            var mesh = new Mesh { name = "голова" };
            mesh.vertices = body.Select(p => w2o.MultiplyPoint3x4(p)).ToArray();
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.boneWeights = Enumerable.Range(0, 3).Select(i => new BoneWeight { boneIndex0 = i, weight0 = 1f }).ToArray();
            mesh.bindposes = bones.Select(b => b.worldToLocalMatrix * obj.transform.localToWorldMatrix).ToArray();
            // ФОРМА-КЛЮЧ «Волк»: вершина головы уезжает на 0.1 м вперёд в теле (дельта — в системе объекта)
            var delta = new Vector3[3]; delta[1] = w2o.MultiplyVector(KeyShift);
            mesh.AddBlendShapeFrame("Волк", 100f, delta, null, null);
            var smr = obj.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            return model;
        }

        GameObject Build(System.Collections.Generic.IReadOnlyList<Organ> worn)
        {
            go = new GameObject("~ТестМешаВида");
            MorphBuilder.Build(go.transform, so, worn);
            return go;
        }

        Vector3[] Baked(SkinnedMeshRenderer smr)
        {
            var m = new Mesh();
            smr.BakeMesh(m, true);
            var v = m.vertices.Select(p => go.transform.InverseTransformPoint(smr.transform.TransformPoint(p))).ToArray();
            Object.DestroyImmediate(m);
            return v;
        }

        static void Near(Vector3 want, Vector3 got, string what) =>
            Assert.Less((want - got).magnitude, 1e-3f, $"{what}: ждали {want:F4}, вышло {got:F4}");

        [Test]
        public void PureSpecies_DrawsItsMesh_OnGraphSkeleton_ByBoneNames()
        {
            Species().bodyMesh = Model();
            Build(so.organs.ToList());

            var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.AreEqual(1, smrs.Length, "рендерер один — объект модели; оболочка поля строиться не должна");
            Assert.AreEqual("голова", smrs[0].name, "имя рендерера = имя объекта модели (контракт имён частей)");
            Assert.IsEmpty(go.GetComponentsInChildren<MeshRenderer>(true), "детали органов у меша вида не рисуются — всё видимое в меше");

            var skeleton = go.GetComponentsInChildren<Transform>(true).First(t => t.name == "Skeleton");
            foreach (var b in smrs[0].bones) Assert.IsTrue(b.IsChildOf(skeleton), $"кость «{b.name}» — не из скелета графа");

            var v = Baked(smrs[0]);
            Near(OnSpine, v[0], "вершина хребта"); Near(OnHead, v[1], "вершина головы"); Near(OnJaw, v[2], "вершина челюсти");
        }

        [Test]
        public void ExtraBone_IsCreatedUnderItsParent_AndMovesItsVertices()
        {
            Species().bodyMesh = Model();
            Build(so.organs.ToList());

            var jaw = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "челюсть");
            Assert.IsNotNull(jaw, "кость сверх графа не создана");
            Assert.AreEqual("голова", jaw.parent.name, "кость сверх графа висит на родителе по имени");
            Near(JawJoint, go.transform.InverseTransformPoint(jaw.position), "ось челюсти");

            var smr = go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
            jaw.Rotate(Vector3.right, 35f, Space.World);
            var v = Baked(smr);
            Near(OnHead, v[1], "голова при открытой пасти");
            Near(JawJoint + Quaternion.AngleAxis(35f, Vector3.right) * (OnJaw - JawJoint), v[2], "челюсть при открытой пасти");
        }

        [Test]
        public void DottedObject_KeepsSlotName_AndCarriesItsOwnColor()
        {
            Species().bodyMesh = Model();
            var src = model.GetComponentInChildren<SkinnedMeshRenderer>();
            src.name = "голова.зубы";
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", new Color(0.9f, 0.88f, 0.8f, 1f));
            src.sharedMaterial = mat;
            Build(so.organs.ToList());
            Object.DestroyImmediate(mat);

            var smr = go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
            Assert.AreEqual("голова", smr.name, "кусок со своим цветом носит имя слота — иначе его не спрячет первое лицо");
            Assert.IsTrue(smr.TryGetComponent<PartMark>(out var mark) && mark.HasOwn, "свой цвет объекта не дошёл до паспорта детали");
            Assert.Less(Mathf.Abs(mark.own.r - 0.9f), 1e-3f);
        }

        /// <summary>ОРГАН НА ШАБЛОНЕ (22.10): объект `<слот>@<вид>` рисуется, только пока привит орган этого вида, носит имя слота,
        /// снимает свой объект шасси того же слота и растёт формой-ключом `рост` на вес из `OrganObjectsFor`.</summary>
        [Test]
        public void OrganObject_ShownOnlyWithItsGraft_ReplacesOwnSlot_AndGrows()
        {
            Species().bodyMesh = Model();
            var own = model.GetComponentInChildren<SkinnedMeshRenderer>();
            var organ = Object.Instantiate(own.gameObject, own.transform.parent);
            organ.name = BodyMesh.OrganObject("голова", "Волк");
            var mesh = Object.Instantiate(own.sharedMesh);
            mesh.AddBlendShapeFrame(BodyMesh.GrowthShape, 100f, new[] { Vector3.zero, Vector3.one, Vector3.zero }, null, null);
            organ.GetComponent<SkinnedMeshRenderer>().sharedMesh = mesh;

            Build(so.organs.ToList());
            var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.AreEqual(1, smrs.Length, "без прививки орган на шаблоне не рисуется");
            Assert.Less(smrs[0].sharedMesh.GetBlendShapeIndex(BodyMesh.GrowthShape), 0, "нарисован объект органа, а не свой");

            var container = smrs[0].transform.parent;
            var skeleton = go.GetComponentsInChildren<Transform>(true).First(t => t.name == "Skeleton");
            foreach (var r in smrs) Object.DestroyImmediate(r.gameObject);
            var grown = new System.Collections.Generic.Dictionary<string, float> { [organ.name] = 0.4f };
            // без записи — поверх своего (роговой кусок на своей кисти)
            BodyMesh.Place(container, skeleton, model, null, jointsOf: so, organObjects: grown);
            smrs = container.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.AreEqual(2, smrs.Length, "без записи орган на шаблоне садится поверх своего объекта");
            foreach (var r in smrs) Object.DestroyImmediate(r.gameObject);
            // по записи — снимает свой объект слота (уши Чутья вместо ушей носителя)
            so.organObjectRules = new[] { new OrganObjectRule { objectName = organ.name, hides = new[] { "голова" } } };
            BodyMesh.Place(container, skeleton, model, null, jointsOf: so, organObjects: grown);
            smrs = container.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.AreEqual(1, smrs.Length, "по записи орган на шаблоне снимает свой объект шасси");
            Assert.AreEqual("голова", smrs[0].name, "имя органа на шаблоне — слот (контракт имён частей)");
            int shape = smrs[0].sharedMesh.GetBlendShapeIndex(BodyMesh.GrowthShape);
            Assert.GreaterOrEqual(shape, 0, "это не объект органа");
            Assert.AreEqual(40f, smrs[0].GetBlendShapeWeight(shape), 1e-3f, "рост — на вес доли");
            Object.DestroyImmediate(mesh);
        }

        static readonly Vector3 KeyShift = new(0f, 0f, 0.1f);

        /// <summary>ИДЕНТИЧНОСТЬ КЛЮЧОМ (опыт 14.10): вес формы-ключа вида-цели ставится на меш шасси — тело течёт к виду.</summary>
        [Test]
        public void SpeciesKey_WeightMovesTheBody()
        {
            Species().bodyMesh = Model();
            BodyMesh.ForceKey = "Волк"; BodyMesh.ForceWeight = 0.5f;
            Build(so.organs.ToList());
            var v = Baked(go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single());
            Near(OnHead + KeyShift * 0.5f, v[1], "вершина головы при весе ключа 0.5");
            Near(OnSpine, v[0], "ключ сдвинул то, чего не двигает");
        }

        /// <summary>ВАРИАНТ Б (геймдизайнер 19.10): с чужим органом тело ОСТАЁТСЯ мешем вида — орган без куска пока не виден.
        /// До 19.10 этот сторож требовал обратного («графт возвращает тело на поле»); с куском выключенным — по-прежнему поле.</summary>
        [Test]
        public void Graft_KeepsSpeciesMesh_FieldOnlyWhenPiecesOff()
        {
            Species().bodyMesh = Model();
            Build(new[] { so.organs[0], graft });
            var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.IsTrue(smrs.Any(r => r.sharedMesh != null && r.sharedMesh.vertexCount == 3), "с чужим органом тело ушло с меша вида");

            bool was = PartAssembly.MeshPieces;
            PartAssembly.MeshPieces = false;
            try
            {
                Build(new[] { so.organs[0], graft });
                smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.IsFalse(smrs.Any(r => r.sharedMesh != null && r.sharedMesh.vertexCount == 3), "куски выключены, а химера на меше вида");
                Assert.IsNotEmpty(smrs, "поле не построено");
            }
            finally { PartAssembly.MeshPieces = was; }
        }

        [Test]
        public void Switch_ReturnsPureSpeciesToField()
        {
            Species().bodyMesh = Model();
            BodyMesh.Enabled = false;
            Build(so.organs.ToList());
            Assert.IsNull(go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "челюсть"), "при выключенном меше вида его кости не создаются");
            Assert.IsFalse(go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => r.sharedMesh.vertexCount == 3));
        }
        /// <summary>ПРАВАЯ КОЛОНКА ЛИСТА — СТУПЕНЬ 2 (геймдизайнер 15.10): на чужом шасси ступень 3 недостижима, поэтому цель
        /// ключа, снятая с правой колонки, — это полный вес на ступени 2. Человек со всеми волчьими органами — ключ «Волк» = 1.</summary>
        [Test]
        public void SpeciesKey_FullWeightAtStepTwo()
        {
            var human = UnityEditor.AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/Человек.asset");
            var wolf = UnityEditor.AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/Волк.asset");
            var worn = human.organs.Where(o => o != null).Select(o => o.chassisOnly ? o : wolf.organs.FirstOrDefault(w => w != null && w.slot == o.slot) ?? o).ToList();
            var (target, step) = GlobalLayer.Step(human, worn);
            if (step != 2 || target != wolf) Assert.Inconclusive($"набор не дал ступень 2 волка (ступень {step})");
            var keys = BodyMesh.KeysFor(human, worn);
            Assert.AreEqual(1f, keys["Волк"], 1e-6f, "на ступени 2 ключ вида — полный: цель ключа снята с правой колонки листа");
            Assert.AreEqual(BodyMesh.KeyStep1, BodyMesh.KeyWeight(1), 1e-6f);
        }
    }
}
