using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>ПОГАШЕННОЕ МЕСТО СТОИТ ПО УЗЛУ ГРАФА (письмо модельной линии OTVET-2026-09-17 §4.1).
    /// Тело рисует граф, а детали головы считаются от места `голова`. Пока погашенное место стояло по своему
    /// плану, детали садились туда, где голова была БЫ по цепочке мест: на графе волка глаза у горла, зубы на
    /// 11 см ниже морды. Тест разносит узел и план на метры, чтобы промах нельзя было спутать с погрешностью.</summary>
    public class HiddenPlaceNodeTests
    {
        GameObject go;

        [TearDown]
        public void TearDown() { if (go != null) Object.DestroyImmediate(go); }

        SpeciesSO Species(bool hideHead)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = hideHead ? "~узел-скрыт" : "~узел-виден";   // разные имена: кэш оболочки по виду
            so.tint = Color.gray;
            so.baseHp = 10;
            so.sockets = new[]
            {
                new BodySocket { name = "хребет", localPos = new Vector3(0f, 0.5f, 0f), baseSize = new Vector3(0.30f, 0.30f, 1.00f) },
                new BodySocket { name = "голова", parent = "хребет", attach = 1f, baseSize = new Vector3(0.20f, 0.20f, 0.40f) },
                new BodySocket { name = "нос", parent = "голова", attach = 1f, baseSize = new Vector3(0.05f, 0.05f, 0.05f) },
            };
            so.organs = new[] { new Organ { organName = "Хребет", slot = "хребет", chassisOnly = true } };

            // УЗЕЛ «голова» УВЕДЁН НА ТРИ МЕТРА ВВЕРХ от плана мест и растёт вперёд по +Z
            so.bones = new[]
            {
                new Bone { name = "голова", socket = "голова", origin = new Vector3(0f, 3f, 0f),
                           dir = new Vector3(90f, 0f, 0f), length = 0.20f, r0 = 0.08f, r1 = 0.06f },
            };
            so.skeletonHides = hideHead ? new[] { "голова" } : new string[0];
            return so;
        }

        Vector3 Nose(SpeciesSO so)
        {
            go = new GameObject("~ТестУзла");
            MorphBuilder.Build(go.transform, so, so.organs.ToList());
            var nose = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "нос");
            Assert.IsNotNull(nose, "деталь «нос» не построена");
            var pos = nose.position;
            Object.DestroyImmediate(go);
            return pos;
        }

        [Test]
        public void HiddenPlace_TakesPoseFromNode()
        {
            var nose = Nose(Species(hideHead: true));

            // начало места = начало узла (0, 3, 0), место длиной 0.4 вдоль узла → центр (0, 3, 0.2),
            // нос на конце места (attach 1) → (0, 3, 0.4). Калибр места, а не узла: морда длиннее черепа
            Assert.AreEqual(3.0f, nose.y, 0.02f, "нос остался по плану мест, а не по узлу графа");
            Assert.AreEqual(0.4f, nose.z, 0.02f, "длина места взята не своя: доли детей поехали");
        }

        [Test]
        public void VisiblePlace_KeepsItsOwnPlan()
        {
            // Контроль: если место НЕ погашено, узел его не двигает — иначе правило задело бы всех,
            // у кого просто совпало имя узла и места
            var nose = Nose(Species(hideHead: false));
            Assert.Less(nose.y, 1.5f, "видимое место уехало к узлу, хотя граф его не гасит");
        }
    }
}
