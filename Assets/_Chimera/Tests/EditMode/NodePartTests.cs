using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>КУСОК НА УЗЛЕ ГРАФА (решение 17.09: нижние ноги — ригблоки). Сторожит три вещи, каждая ломается
    /// молча: кусок встаёт на КОНЕЦ узла в единицах узла; погашенное место его не прячет (иначе волк без лап
    /// ниже запястья); у носителя без узла кусок рисуется по месту, а голого куба места поверх не появляется.</summary>
    public class NodePartTests
    {
        GameObject go;

        [TearDown]
        public void TearDown() { if (go != null) Object.DestroyImmediate(go); }

        static SpeciesSO Species(bool withNode, string name)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = name;                 // разные имена — кэш оболочки по виду
            so.tint = Color.gray;
            so.baseHp = 10;
            so.sockets = new[]
            {
                new BodySocket { name = "хребет", localPos = new Vector3(0f, 1f, 0f), baseSize = new Vector3(0.4f, 0.3f, 1.0f) },
                new BodySocket { name = "Руки", parent = "хребет", attach = 0.8f, mirrorX = true,
                                 attachOffset = new Vector3(0.4f, -1.5f, 0f), baseSize = new Vector3(0.10f, 0.60f, 0.12f) },
            };
            so.organs = new[]
            {
                new Organ { organName = "Хребет", slot = "хребет", chassisOnly = true },
                new Organ { organName = "Лапа", slot = "Руки",
                            visualParts = new[] { new OrganPart { node = "рука", scale = Vector3.one } } },
            };
            if (withNode)
            {
                // узел растёт вверх из (0.2, 1, 0), длина 0.5, радиус конца 0.1 → конец (0.2, 1.5, 0), диаметр 0.2
                so.bones = new[]
                {
                    new Bone { name = "рука", socket = "Руки", origin = new Vector3(0.2f, 1f, 0f), dir = Vector3.zero,
                               length = 0.5f, r0 = 0.1f, r1 = 0.1f, mirrorX = true },
                };
                so.skeletonHides = new[] { "Руки" };
            }
            else
            {
                so.bones = new Bone[0];
                so.skeletonHides = new string[0];
            }
            return so;
        }

        Transform[] Paws(SpeciesSO so)
        {
            go = new GameObject("~ТестУзла");
            MorphBuilder.Build(go.transform, so, so.organs.ToList());
            // оболочку поля BoneMesher тоже называет по слоту, но у неё SkinnedMeshRenderer, а не MeshFilter
            return go.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name == "Руки").Select(m => m.transform).ToArray();
        }

        [Test]
        public void Part_StandsOnNodeEnd_InNodeUnits_EvenOnHiddenPlace()
        {
            var paws = Paws(Species(withNode: true, name: "~узел-лапа"));

            Assert.AreEqual(2, paws.Length, "у пары узлов должна быть пара кусков — и место погашено, а куски на месте");
            var right = paws.OrderByDescending(p => p.position.x).First();
            Assert.AreEqual(0.2f, right.position.x, 0.01f, "кусок не на конце узла по X");
            Assert.AreEqual(1.5f, right.position.y, 0.01f, "кусок не на конце узла: начало узла вместо конца?");
            Assert.AreEqual(0.2f, right.localScale.x, 0.01f, "ширина не в диаметрах конца узла");
            Assert.AreEqual(0.5f, right.localScale.z, 0.01f, "длина не в длинах узла");
            Assert.AreEqual(-0.2f, paws.Min(p => p.position.x), 0.01f, "зеркальный кусок не на зеркальном узле");
        }

        [Test]
        public void NoNodeAtCarrier_PartDrawnByPlace_WithoutPlaceCube()
        {
            // Человек на чистом листе с волчьими ногами: узла нет. Кусок обязан нарисоваться (иначе игрок не видит
            // прижитых лап), а место не должно добавить свой куб — органу-из-кусков-на-узлах куб не нужен
            var paws = Paws(Species(withNode: false, name: "~узел-нет"));
            Assert.AreEqual(2, paws.Length, "кусков должно быть ровно два (пара): ноль — лапы спрятаны, четыре — поверх лёг куб места");
        }
    }
}
