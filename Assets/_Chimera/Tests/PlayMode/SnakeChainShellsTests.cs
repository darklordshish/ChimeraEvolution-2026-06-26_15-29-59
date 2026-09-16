using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>ОБОЛОЧКА СЛОТА — НЕ ЗВЕНО ЗМЕИ. `BoneMesher` кладёт в контейнер `Morph` скиннед-меши слотов с
    /// ТЕМИ ЖЕ именами, что и звенья цепи (`шея`, `хребет`, `Хвост`). Цепь отбирала детей по имени и брала
    /// оболочки в звенья: замер модельной линии 17.09 — 18 «звеньев», из них три были оболочками, хвостовая
    /// первой у головы. Ошибки при этом нет, змея просто ползёт со сбитой дистанцией первых звеньев.</summary>
    public class SnakeChainShellsTests
    {
        GameObject root;

        [TearDown]
        public void TearDown() { if (root != null) Object.Destroy(root); }

        [UnityTest]
        public IEnumerator Chain_TakesLinks_NotSlotShells()
        {
            root = new GameObject("~ЗмеяТест");
            var chain = root.AddComponent<SnakeBodyChain>();

            var morph = new GameObject("Morph").transform;
            morph.SetParent(root.transform, false);

            // оболочка слота — ровно то, что кладёт BoneMesher: одноимённый объект со скиннед-мешем
            var shell = new GameObject("хребет", typeof(SkinnedMeshRenderer)).transform;
            shell.SetParent(morph, false);

            // настоящие звенья — примитивы мест
            var neck = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
            neck.name = "шея";
            neck.SetParent(morph, false);
            neck.localPosition = new Vector3(0f, 0f, -0.3f);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
            body.name = "хребет";
            body.SetParent(morph, false);
            body.localPosition = new Vector3(0f, 0f, -0.6f);

            yield return null;
            chain.RebuildFromMorph();

            var segments = (Transform[])typeof(SnakeBodyChain)
                .GetField("segments", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(chain);

            Assert.IsFalse(segments.Contains(shell), "оболочка слота попала в звенья цепи");
            Assert.AreEqual(2, segments.Length, "звеньев должно быть ровно два — шея и хребет");
        }
    }
}
