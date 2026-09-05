using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    public class DestroyLagTests
    {
        SpeciesSO MakeSpecies()
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = "Волк";
            so.tint = Color.gray;
            so.mutagenPool = 16;
            so.baseHp = 50;
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

        int CountNamed(Transform root, string name)
        {
            int n = 0;
            for (int i = 0; i < root.childCount; i++) if (root.GetChild(i).name == name) n++;
            return n;
        }

        int CountContains(Transform root, string sub)
        {
            int n = 0;
            for (int i = 0; i < root.childCount; i++) if (root.GetChild(i).name.Contains(sub)) n++;
            return n;
        }

        [UnityTest]
        public IEnumerator MorphBuilder_DoubleRecompute_DeadContainer_Deactivated()
        {
            var so = MakeSpecies();
            var worn = new List<Organ> { so.organs[0] };
            var go = new GameObject("DestroyLag_Morph");
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.center = new Vector3(0f, 1f, 0f);

            // первый билд
            MorphBuilder.Build(go.transform, so, worn);
            yield return null;
            Assert.AreEqual(1, CountNamed(go.transform, "Morph"), "После первого билда должен быть один Morph");
            var c1 = go.transform.Find("Morph");
            Assert.IsTrue(c1.gameObject.activeSelf, "Живой Morph активен");

            // второй билд в том же кадре без yield — старый должен стать Morph~dead + inactive
            MorphBuilder.Build(go.transform, so, worn);
            // сразу после второго билда, до конца кадра, старый объект ещё не уничтожен (Destroy отложен)
            Assert.AreEqual(1, CountNamed(go.transform, "Morph"), "После второго билда должен остаться ровно один живой Morph");
            Assert.AreEqual(1, CountContains(go.transform, "~dead"), "Старый контейнер должен быть переименован в ~dead");
            Transform dead = null;
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var ch = go.transform.GetChild(i);
                if (ch.name.Contains("~dead")) dead = ch;
            }
            Assert.IsNotNull(dead, "Должен найтись ~dead");
            Assert.IsFalse(dead.gameObject.activeSelf, "Старый контейнер должен быть SetActive(false) сразу (MorphBuilder.cs:28)");

            // после конца кадра ~dead уничтожается
            yield return null;
            // Find с ~dead должен исчезнуть (Destroy сработал)
            Assert.AreEqual(0, CountContains(go.transform, "~dead"), "После конца кадра ~dead должен быть уничтожен");
            Assert.AreEqual(1, CountNamed(go.transform, "Morph"), "Живой Morph остаётся один");

            Object.Destroy(go);
            Object.DestroyImmediate(so);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CreatureBody_DoubleRecompute_NoDuplicateMorph()
        {
            var so = MakeSpecies();
            // через CreatureBody: двойной Recompute за кадр (Install + Refeed)
            var go = new GameObject("DestroyLag_Body");
            go.AddComponent<Health>();
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.center = new Vector3(0f, 1f, 0f);
            var body = go.AddComponent<CreatureBody>();
            body.Configure(so, new SpeciesSO[0]);
            yield return null;
            int n1 = CountNamed(go.transform, "Morph");
            Assert.AreEqual(1, n1, "После Configure должен быть один Morph");

            // форсим двойной Recompute за кадр: второй Configure без yield
            body.Configure(so, new SpeciesSO[0]);
            // сразу — только один живой Morph, старый ~dead inactive
            Assert.AreEqual(1, CountNamed(go.transform, "Morph"));
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var ch = go.transform.GetChild(i);
                if (ch.name.Contains("~dead")) Assert.IsFalse(ch.gameObject.activeSelf);
            }
            yield return null;
            Assert.AreEqual(1, CountNamed(go.transform, "Morph"));
            Assert.AreEqual(0, CountContains(go.transform, "~dead"));

            Object.Destroy(go);
            Object.DestroyImmediate(so);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MorphBuilder_TripleBuild_OnlyOneAlive()
        {
            var so = MakeSpecies();
            var worn = new List<Organ> { so.organs[0] };
            var go = new GameObject("DestroyLag_Triple");
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.center = new Vector3(0f, 1f, 0f);
            MorphBuilder.Build(go.transform, so, worn);
            MorphBuilder.Build(go.transform, so, worn);
            MorphBuilder.Build(go.transform, so, worn);
            // до конца кадра — один живой, два мёртвых
            Assert.AreEqual(1, CountNamed(go.transform, "Morph"));
            Assert.AreEqual(2, CountContains(go.transform, "~dead"));
            foreach (Transform ch in go.transform)
                if (ch.name.Contains("~dead")) Assert.IsFalse(ch.gameObject.activeSelf);
            yield return null;
            Assert.AreEqual(1, CountNamed(go.transform, "Morph"));
            Assert.AreEqual(0, CountContains(go.transform, "~dead"));

            Object.Destroy(go);
            Object.DestroyImmediate(so);
            yield return null;
        }
    }
}
