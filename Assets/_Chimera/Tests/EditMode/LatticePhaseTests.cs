using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>РЕШЁТКА ПОЛЯ ПОСТОЯННАЯ (находка модельной линии, поставка 19 §4). Начало сетки по Y и Z бралось от
    /// габарита узлов: правка КРАЙНЕГО узла сдвигала фазу сетки для всего тела, и огранка менялась везде, а не там, где
    /// правили. У человека щиколотку не трогали, а в профиль она стала на 2 см тоньше — сдвинулась лопатка. При смешении
    /// графа это «перещёлкивание» химеры целиком на каждом графте.</summary>
    public class LatticePhaseTests
    {
        GameObject a, b;

        [TearDown]
        public void TearDown()
        {
            if (a != null) Object.DestroyImmediate(a);
            if (b != null) Object.DestroyImmediate(b);
        }

        static SpeciesSO Body(string name, bool farBone)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = name;              // своё имя: кэш оболочки по виду
            so.tint = Color.gray;
            so.baseHp = 10;
            so.sockets = new[] { new BodySocket { name = "хребет", localPos = new Vector3(0f, 0.5f, 0f), baseSize = new Vector3(0.3f, 0.3f, 0.6f) } };
            so.organs = new[] { new Organ { organName = "Хребет", slot = "хребет", chassisOnly = true } };
            var main = new Bone { name = "хребет", socket = "хребет", origin = new Vector3(0f, 0.5f, -0.3f), dir = new Vector3(90f, 0f, 0f), length = 0.6f, r0 = 0.15f, r1 = 0.15f };
            // мелкая кость в метре ниже и позади: поле у основной не трогает, а габарит по Y и Z раздвигает на долю клетки
            var far = new Bone { name = "хвост", socket = "хребет", origin = new Vector3(0f, -0.613f, -1.371f), dir = new Vector3(90f, 0f, 0f), length = 0.05f, r0 = 0.03f, r1 = 0.03f };
            so.bones = farBone ? new[] { main, far } : new[] { main };
            return so;
        }

        // вершины у основной кости, округлённые до 0.1 мм: сортировать сырые float нельзя — почти равные X
        // встают в разном порядке, и сравнение сопоставляет чужие вершины
        static (int x, int y, int z)[] ShellNear(GameObject go)
        {
            var sk = go.GetComponentsInChildren<SkinnedMeshRenderer>(true).First();
            var m = new Mesh(); sk.BakeMesh(m, true);
            return m.vertices.Select(v => sk.transform.TransformPoint(v))
                .Where(p => p.y > 0.2f && p.z > -0.6f)
                .Select(p => (Mathf.RoundToInt(p.x * 1e4f), Mathf.RoundToInt(p.y * 1e4f), Mathf.RoundToInt(p.z * 1e4f)))
                .OrderBy(k => k).ToArray();
        }

        [Test]
        public void FarNode_DoesNotRefacetTheRestOfTheBody()
        {
            a = new GameObject("~ФазаБез");
            MorphBuilder.Build(a.transform, Body("~фаза-без", false), new[] { new Organ { organName = "Хребет", slot = "хребет", chassisOnly = true } }.ToList());
            b = new GameObject("~ФазаС");
            MorphBuilder.Build(b.transform, Body("~фаза-с", true), new[] { new Organ { organName = "Хребет", slot = "хребет", chassisOnly = true } }.ToList());

            var pa = ShellNear(a);
            var pb = ShellNear(b);
            Assert.AreEqual(pa.Length, pb.Length, "дальняя кость поменяла число вершин у основной — сетка сдвинулась по фазе");
            int moved = pa.Zip(pb, (p, q) => System.Math.Max(System.Math.Abs(p.x - q.x), System.Math.Max(System.Math.Abs(p.y - q.y), System.Math.Abs(p.z - q.z)))).Count(d => d > 1);
            Assert.AreEqual(0, moved, $"дальняя кость сдвинула {moved} вершин у основной больше чем на 0.1 мм — огранка зависит от габарита");
        }
    }
}
