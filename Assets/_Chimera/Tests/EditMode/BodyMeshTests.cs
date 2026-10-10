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
        public void Graft_ReturnsBodyToField()
        {
            Species().bodyMesh = Model();
            Build(new[] { so.organs[0], graft });

            var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.IsFalse(smrs.Any(r => r.sharedMesh != null && r.sharedMesh.vertexCount == 3), "с чужим органом тело рисует поле, а не меш вида");
            Assert.IsNotEmpty(smrs, "поле не построено");
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
    }
}
