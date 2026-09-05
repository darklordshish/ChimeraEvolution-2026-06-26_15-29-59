using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    public class MorphFootYTests
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
                new BodySocket { name = "хребет", localPos = Vector3.zero, baseSize = new Vector3(0.3f, 0.4f, 1f) },
                new BodySocket { name = "голова", parent = "хребет", attach = 1f, baseSize = new Vector3(0.2f, 0.2f, 0.3f) },
            };
            so.organs = new[]
            {
                new Organ { organName = "Хребет", slot = "хребет", chassisOnly = true },
                new Organ { organName = "Пасть", slot = "Пасть", cost = 3 },
            };
            so.bones = new Bone[0];
            return so;
        }

        [UnityTest]
        public IEnumerator Container_Morph_LocalY_Equals_FootY_DefaultCC()
        {
            var so = MakeSpecies();
            var worn = new List<Organ> { so.organs[0] };
            var go = new GameObject("FootY_Default");
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.center = new Vector3(0f, 1f, 0f);
            // footY = center.y - height*0.5 = 1 -1 =0 — высоты заданы ОТ ЗЕМЛИ, контейнер сдвигается к низу капсулы
            float footY = cc.center.y - cc.height * 0.5f;

            MorphBuilder.Build(go.transform, so, worn);
            yield return null;

            var container = go.transform.Find("Morph");
            Assert.IsNotNull(container, "Контейнер Morph должен существовать");
            Assert.AreEqual(footY, container.localPosition.y, 1e-6f, $"localPosition.y {container.localPosition.y} != footY {footY}");

            Object.Destroy(go);
            Object.DestroyImmediate(so);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Container_Morph_LocalY_Equals_FootY_CustomCC()
        {
            var so = MakeSpecies();
            var worn = new List<Organ> { so.organs[0] };
            var go = new GameObject("FootY_Custom");
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.5f;
            cc.center = new Vector3(0f, 0.8f, 0f);
            float footY = cc.center.y - cc.height * 0.5f; // 0.8 -0.75 =0.05

            MorphBuilder.Build(go.transform, so, worn);
            yield return null;

            var container = go.transform.Find("Morph");
            Assert.IsNotNull(container);
            Assert.AreEqual(footY, container.localPosition.y, 1e-6f);
            // без CC — footY 0
            var go2 = new GameObject("FootY_NoCC");
            MorphBuilder.Build(go2.transform, so, worn);
            yield return null;
            var c2 = go2.transform.Find("Morph");
            Assert.IsNotNull(c2);
            Assert.AreEqual(0f, c2.localPosition.y, 1e-6f, "Без CharacterController footY должен быть 0");

            Object.Destroy(go);
            Object.Destroy(go2);
            Object.DestroyImmediate(so);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Container_Morph_Y_Tracks_CC_HeightFromGround()
        {
            var so = MakeSpecies();
            var worn = new List<Organ> { so.organs[0] };
            var go = new GameObject("FootY_Track");
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.center = new Vector3(0f, 1f, 0f);
            MorphBuilder.Build(go.transform, so, worn);
            yield return null;
            var container = go.transform.Find("Morph");
            Assert.AreEqual(0f, container.localPosition.y, 1e-6f);

            // меняем CC и пересобираем — контейнер должен сдвинуться
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            float footY2 = cc.center.y - cc.height * 0.5f; // 0
            MorphBuilder.Build(go.transform, so, worn);
            yield return null;
            container = go.transform.Find("Morph");
            Assert.AreEqual(footY2, container.localPosition.y, 1e-6f);

            Object.Destroy(go);
            Object.DestroyImmediate(so);
            yield return null;
        }
    }
}
