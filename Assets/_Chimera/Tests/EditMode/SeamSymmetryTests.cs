using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>ШОВ МЕЖДУ СЛОТАМИ ПОЛЯ СИММЕТРИЧЕН ПО X (находка модельной линии, поставки 6 §6 и 9 §5). У ежа голова на оси
    /// выходила вправо до 0.156, а влево до −0.106 (вершин 82 / 44), и справа от рыльца читался бугор. Сдвиг графа на долю
    /// клетки картину не менял — значит, не фаза сетки, а правило владения: вершина брала владельца у ПЕРВОГО угла ячейки,
    /// а первый угол всегда со стороны −X. Справа от оси он смотрит к центру, слева — наружу.</summary>
    public class SeamSymmetryTests
    {
        GameObject go;

        [TearDown]
        public void TearDown() { if (go != null) Object.DestroyImmediate(go); }

        [Test]
        public void TwoSlotsOnAxis_ShareSeamSymmetrically()
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = "~шов-по-оси";      // своё имя: кэш оболочки по виду
            so.tint = Color.gray;
            so.baseHp = 10;
            so.sockets = new[]
            {
                new BodySocket { name = "хребет", localPos = new Vector3(0f, 0.5f, 0f), baseSize = new Vector3(0.3f, 0.3f, 0.6f) },
                new BodySocket { name = "голова", parent = "хребет", attach = 1f, baseSize = new Vector3(0.2f, 0.2f, 0.3f) },
            };
            so.organs = new[] { new Organ { organName = "Хребет", slot = "хребет", chassisOnly = true } };
            // широкий корпус позади и узкая голова впереди, обе кости на оси: шов между ними на боках идёт наискось по X
            so.bones = new[]
            {
                new Bone { name = "хребет", socket = "хребет", origin = new Vector3(0f, 0.5f, -0.4f), dir = new Vector3(90f, 0f, 0f), length = 0.5f, r0 = 0.22f, r1 = 0.22f },
                new Bone { name = "голова", socket = "голова", origin = new Vector3(0f, 0.5f, 0.1f), dir = new Vector3(90f, 0f, 0f), length = 0.3f, r0 = 0.13f, r1 = 0.10f },
            };

            go = new GameObject("~ТестШва");
            MorphBuilder.Build(go.transform, so, so.organs.ToList());

            foreach (var sk in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = new Mesh(); sk.BakeMesh(m, true);
                var xs = m.vertices.Select(v => sk.transform.TransformPoint(v).x).ToArray();
                int right = xs.Count(x => x > 0.001f), left = xs.Count(x => x < -0.001f);
                Assert.AreEqual(xs.Max(), -xs.Min(), 0.005f, $"оболочка «{sk.name}» несимметрична по X: {xs.Min():0.000}…{xs.Max():0.000}");
                Assert.AreEqual(right, left, Mathf.Max(2, (right + left) / 50), $"у «{sk.name}» вершин справа {right}, слева {left}");
            }
        }
    }
}
