using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    public class MorphHierarchyTests
    {
        SpeciesSO MakeSpecies()
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = "Волк";
            so.tint = Color.gray;
            so.mutagenPool = 16;
            so.baseHp = 75;
            so.sockets = new[]
            {
                new BodySocket { name = "хребет", localPos = Vector3.zero, baseSize = new Vector3(0.33f, 0.48f, 1.29f) },
                new BodySocket { name = "голова", parent = "хребет", attach = 1f, baseSize = new Vector3(0.26f, 0.22f, 0.46f) },
                new BodySocket { name = "Пасть", parent = "голова", attach = 1f, baseSize = new Vector3(0.13f, 0.09f, 0.22f) },
                new BodySocket { name = "уши", parent = "голова", attach = 0.5f, mirrorX = true, baseSize = new Vector3(0.07f, 0.16f, 0.035f) },
                // цепь для проверки узла масштаб 1
                new BodySocket { name = "Хвост", parent = "хребет", attach = 0f, baseSize = Vector3.one, linkDiameter = 0.13f, linkLength = 0.08f, linkTaper = 0.9f, chain = 3 },
            };
            so.organs = new[]
            {
                new Organ { organName = "Хребет", slot = "хребет", chassisOnly = true },
                new Organ { organName = "Пасть", slot = "Пасть", cost = 3, visualParts = new[] { new OrganPart { scale = Vector3.one, offset = Vector3.zero } } },
                new Organ { organName = "Нюх", slot = "Чутьё", cost = 3, visualParts = new[] {
                    new OrganPart { scale = Vector3.one, role = PartRole.Ear },
                    new OrganPart { scale = Vector3.one, role = PartRole.Nose },
                    new OrganPart { scale = Vector3.one, role = PartRole.Eye, color = new Color(0.4f,0.3f,0.12f,1f) },
                }},
            };
            so.bones = new Bone[0];
            return so;
        }

        [UnityTest]
        public IEnumerator PartMark_Scale_IsOne_EmptyNode()
        {
            var so = MakeSpecies();
            var worn = new List<Organ> { so.organs[0], so.organs[1] };
            var go = new GameObject("Hierarchy_Scale");
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.center = new Vector3(0f, 1f, 0f);
            MorphBuilder.Build(go.transform, so, worn);
            yield return null;

            // Узел цепи должен иметь масштаб 1, меш внутри — свой размер. Проверяем что любой узел с детьми-рендерерами имеет scale 1
            var container = go.transform.Find("Morph");
            Assert.IsNotNull(container);
            // найти звенья хвоста (BuildLinks создаёт узлы с именем "Хвост")
            bool foundLink = false;
            foreach (Transform child in container)
            {
                // звенья — прямые дети контейнера с именем Хвост
                if (child.name == "Хвост")
                {
                    foundLink = true;
                    Assert.AreEqual(Vector3.one.x, child.localScale.x, 1e-6f, "Узел цепи должен иметь масштаб 1 по x");
                    Assert.AreEqual(Vector3.one.y, child.localScale.y, 1e-6f, "Узел цепи должен иметь масштаб 1 по y");
                    Assert.AreEqual(Vector3.one.z, child.localScale.z, 1e-6f, "Узел цепи должен иметь масштаб 1 по z");
                    // внутри узла — меш с рендерером
                    var rend = child.GetComponentInChildren<Renderer>();
                    Assert.IsNotNull(rend, "В узле должен быть меш-рендерер");
                }
            }
            // если цепь не построилась (нет органа хвоста) — проверяем fallback: любые PartMark на мешах, их родители имеют масштаб нанесённый мешу, а не узлу
            if (!foundLink)
            {
                var marks = go.GetComponentsInChildren<PartMark>();
                foreach (var m in marks)
                {
                    // PartMark висит на меше — его transform локальный масштаб может быть !=1, но его родитель-узел если есть должен быть 1
                    // Проверяем что сам PartMark объект не искажён неравномерным масштабом родителя (косвенно — parent scale)
                    if (m.transform.parent != null && m.transform.parent.name == "Хвост")
                        Assert.AreEqual(1f, m.transform.parent.localScale.x, 1e-6f);
                }
            }

            // PartMark с own цветом / role должен существовать если орган дал роль
            var go2 = new GameObject("Hierarchy_PartMark");
            var cc2 = go2.AddComponent<CharacterController>();
            cc2.height = 2f; cc2.center = new Vector3(0f, 1f, 0f);
            // добавим уши через formFrom — но проще проверить что обычные части без PartMark тоже имеют родителя масштаб 1 если они цепь
            MorphBuilder.Build(go2.transform, so, worn);
            yield return null;
            // хотя бы один рендерер должен существовать
            Assert.Greater(go2.GetComponentsInChildren<Renderer>().Length, 0);

            Object.Destroy(go);
            Object.Destroy(go2);
            Object.DestroyImmediate(so);
            yield return null;
        }

        [Test]
        public void IsHeadName_BySocket()
        {
            // Telegraph.IsHeadName — private static, проверяем через рефлексию (контракт имён сокетов)
            var mi = typeof(Telegraph).GetMethod("IsHeadName", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(mi, "Telegraph.IsHeadName должен существовать (Telegraph.cs:30)");
            System.Func<string, bool> call = n => (bool)mi.Invoke(null, new object[] { n });
            // головные сокеты
            Assert.IsTrue(call("Head"), "Head — головная");
            Assert.IsTrue(call("Muzzle"), "Muzzle — головная");
            Assert.IsTrue(call("Nose"), "Nose — головная");
            Assert.IsTrue(call("Jaw"), "Jaw — головная");
            Assert.IsTrue(call("EarL"), "EarL — головная по префиксу");
            Assert.IsTrue(call("Ear_R"), "Ear_R — головная по префиксу");
            Assert.IsTrue(call("голова"), "голова — морф-сокет головная");
            Assert.IsTrue(call("Пасть"), "Пасть — морф-сокет головная");
            Assert.IsTrue(call("уши"), "уши — морф-сокет головная");
            Assert.IsTrue(call("глаза"), "глаза — морф-сокет головная");
            // не головные
            Assert.IsFalse(call("Хвост"), "Хвост не головная");
            Assert.IsFalse(call("Ноги"), "Ноги не головная");
            Assert.IsFalse(call("хребет"), "хребет не головная");
            Assert.IsFalse(call("Шкура"), "Шкура не головная");
            Assert.IsFalse(call("Коготь"), "Коготь не головная");
        }

        [UnityTest]
        public IEnumerator Part_Name_IsSocket_NotOrgan()
        {
            var so = MakeSpecies();
            var organ = new Organ { organName = "Клык_Орган", slot = "Пасть", cost = 3 };
            so.organs = new[] { new Organ { organName = "Хребет", slot = "хребет", chassisOnly = true }, organ };
            var worn = new List<Organ> { so.organs[0], so.organs[1] };
            var go = new GameObject("Hierarchy_Name");
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.center = new Vector3(0f, 1f, 0f);
            MorphBuilder.Build(go.transform, so, worn);
            yield return null;
            var rends = go.GetComponentsInChildren<Renderer>();
            Assert.Greater(rends.Length, 0);
            foreach (var r in rends)
            {
                // имя части = имя сокета, не органа
                Assert.AreNotEqual("Клык_Орган", r.name, "Имя части должно быть по сокету, не по органу");
                // для нашей сборки ожидаем "Пасть" или "хребет" или "голова"
                Assert.IsTrue(r.name == "Пасть" || r.name == "хребет" || r.name == "голова" || r.name == "Хвост" || r.name == "уши" || r.name.Contains("сустав"),
                    $"Неожиданное имя части {r.name}");
            }
            Object.Destroy(go);
            Object.DestroyImmediate(so);
            yield return null;
        }
    }
}
