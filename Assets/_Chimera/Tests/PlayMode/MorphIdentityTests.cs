using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    public class MorphIdentityTests
    {
        SpeciesSO MakeSpecies(string name, Color tint, int pool = 100)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = name;
            so.tint = tint;
            so.mutagenPool = pool;
            so.baseHp = 75;
            so.baseStamina = 100;
            so.baseStaminaRegen = 10f;
            so.sockets = new[]
            {
                new BodySocket { name = "хребет", localPos = Vector3.zero, baseSize = new Vector3(0.335f, 0.485f, 1.295f) },
                new BodySocket { name = "голова", parent = "хребет", attach = 1f, attachOffset = new Vector3(0f, 0.2f, 0f), baseSize = new Vector3(0.263f, 0.22f, 0.469f) },
                new BodySocket { name = "Пасть", parent = "голова", attach = 1f, attachOffset = new Vector3(0f, -0.1f, 0.2f), baseSize = new Vector3(0.13f, 0.09f, 0.22f) },
                new BodySocket { name = "Сердце", parent = "хребет", attach = 0.5f, baseSize = new Vector3(0.27f, 0.56f, 0.56f), inner = true },
            };
            so.organs = new[]
            {
                new Organ { organName = "Хребет", slot = "хребет", chassisOnly = true },
                new Organ { organName = "Пасть", slot = "Пасть", cost = 3 },
                new Organ { organName = "Сердце", slot = "Сердце", cost = 3 },
                new Organ { organName = "Шкура", slot = "Шкура", cost = 3 },
            };
            so.bones = new Bone[0];
            so.skeletonHides = new string[0];
            return so;
        }

        Bounds CombinedBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(root.position, Vector3.zero);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        [UnityTest]
        public IEnumerator Build_NativeComposition_Equals_BuildLayers3_WithinMicron()
        {
            var wolf = MakeSpecies("Волк", new Color(0.5f, 0.5f, 0.52f));
            var human = MakeSpecies("Человек", new Color(0.9f, 0.72f, 0.62f));
            // родной состав: шасси волк + его органы (chassisOnly + Пасть/Сердце)
            var worn = new List<Organ> { wolf.organs[0], wolf.organs[1] };

            var goA = new GameObject("MorphIdentity_A");
            var ccA = goA.AddComponent<CharacterController>();
            ccA.height = 2f; ccA.center = new Vector3(0f, 1f, 0f);
            goA.AddComponent<CreatureBody>();
            MorphBuilder.Build(goA.transform, wolf, worn);
            yield return null;
            Bounds bA = CombinedBounds(goA.transform);

            var goB = new GameObject("MorphIdentity_B");
            var ccB = goB.AddComponent<CharacterController>();
            ccB.height = 2f; ccB.center = new Vector3(0f, 1f, 0f);
            goB.AddComponent<CreatureBody>();
            // тот же вызов — до микрона идентичность (И5): на родном составе результат тождественен сегодняшнему (buildLayers 3)
            wolf.buildLayers = 3;
            MorphBuilder.Build(goB.transform, wolf, worn);
            yield return null;
            Bounds bB = CombinedBounds(goB.transform);

            const float micron = 1e-6f;
            Assert.AreEqual(bA.center.x, bB.center.x, micron, "center.x расхождение > микрона");
            Assert.AreEqual(bA.center.y, bB.center.y, micron, "center.y расхождение > микрона");
            Assert.AreEqual(bA.center.z, bB.center.z, micron, "center.z расхождение > микрона");
            Assert.AreEqual(bA.extents.x, bB.extents.x, micron, "extents.x расхождение > микрона");
            Assert.AreEqual(bA.extents.y, bB.extents.y, micron, "extents.y расхождение > микрона");
            Assert.AreEqual(bA.extents.z, bB.extents.z, micron, "extents.z расхождение > микрона");

            // также проверяем через CreatureBody.Configure на родном составе
            var goC = new GameObject("MorphIdentity_C");
            var ccC = goC.AddComponent<CharacterController>();
            ccC.height = 2f; ccC.center = new Vector3(0f, 1f, 0f);
            goC.AddComponent<Health>();
            var body = goC.AddComponent<CreatureBody>();
            // donors включают сам вид чтобы Configure собрал слоты
            body.Configure(wolf, new[] { human, wolf });
            yield return null;
            Bounds bC = CombinedBounds(goC.transform);
            // на родном составе (волк) body остаётся волком — bounds как у прямого Build в пределах микрона
            Assert.AreEqual(bA.extents.magnitude, bC.extents.magnitude, 1e-5f, "CreatureBody родной состав должен дать тот же габарит");

            Object.Destroy(goA);
            Object.Destroy(goB);
            Object.Destroy(goC);
            Object.DestroyImmediate(wolf);
            Object.DestroyImmediate(human);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Build_TwiceSameArgs_GivesSameBounds()
        {
            var wolf = MakeSpecies("Волк", Color.gray);
            var worn = new List<Organ> { wolf.organs[0], wolf.organs[1] };
            var go = new GameObject("MorphIdentity_Twice");
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.center = new Vector3(0f, 1f, 0f);
            MorphBuilder.Build(go.transform, wolf, worn);
            yield return null;
            Bounds b1 = CombinedBounds(go.transform);
            MorphBuilder.Build(go.transform, wolf, worn);
            yield return null;
            Bounds b2 = CombinedBounds(go.transform);
            const float micron = 1e-6f;
            Assert.AreEqual(b1.center.x, b2.center.x, micron);
            Assert.AreEqual(b1.center.y, b2.center.y, micron);
            Assert.AreEqual(b1.center.z, b2.center.z, micron);
            Object.Destroy(go);
            Object.DestroyImmediate(wolf);
            yield return null;
        }
    }
}
