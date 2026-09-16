using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>ОБОЛОЧКА ПОЛЯ ГРАНЯМИ (решение геймдизайнера 17.09 по тройке затенения: «плоское, клетка ×1,5»). Гладкая
    /// оболочка рядом с гранёными ригблоками читалась двумя разными языками — лапы и морда выглядели приклеенными.
    /// Грань — это треугольник со СВОИМИ вершинами: общая вершина усредняет нормаль соседей, и грань снова сглаживается.
    /// Ломается молча: картинка «чуть мягче», ошибок нет.</summary>
    public class FlatShellTests
    {
        GameObject go;

        [TearDown]
        public void TearDown() { if (go != null) Object.DestroyImmediate(go); }

        [Test]
        public void FieldShell_HasNoSharedVertices_AndKeepsWeights()
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = "~оболочка-гранями";     // своё имя: кэш оболочки по виду
            so.tint = Color.gray;
            so.baseHp = 10;
            so.sockets = new[] { new BodySocket { name = "хребет", localPos = new Vector3(0f, 0.5f, 0f), baseSize = new Vector3(0.3f, 0.3f, 0.6f) } };
            so.organs = new[] { new Organ { organName = "Хребет", slot = "хребет", chassisOnly = true } };
            so.bones = new[]
            {
                new Bone { name = "хребет", socket = "хребет", origin = new Vector3(0f, 0.5f, -0.3f),
                           dir = new Vector3(90f, 0f, 0f), length = 0.6f, r0 = 0.15f, r1 = 0.15f },
            };

            go = new GameObject("~ТестГраней");
            MorphBuilder.Build(go.transform, so, so.organs.ToList());
            var mesh = go.GetComponentsInChildren<SkinnedMeshRenderer>(true).First().sharedMesh;

            Assert.Greater(mesh.triangles.Length, 0, "оболочка пуста");
            Assert.AreEqual(mesh.triangles.Length, mesh.vertexCount,
                "у треугольников общие вершины — нормаль усреднится, и оболочка снова станет гладкой");
            Assert.AreEqual(mesh.vertexCount, mesh.boneWeights.Length, "разварка потеряла веса скиннинга");
        }
    }
}
