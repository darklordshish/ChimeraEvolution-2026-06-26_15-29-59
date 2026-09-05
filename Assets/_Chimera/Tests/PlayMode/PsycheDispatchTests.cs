using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    public class PsycheDispatchTests
    {
        SpeciesSO MakeSpecies(string name, Color tint, string[] slots)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = name;
            so.tint = tint;
            so.mutagenPool = 100;
            so.baseHp = 50;
            so.baseStamina = 100;
            so.baseStaminaRegen = 10f;
            so.sockets = new BodySocket[0];
            var organs = new List<Organ>();
            foreach (var s in slots)
                organs.Add(new Organ { organName = name + "-" + s, slot = s, cost = 2 });
            organs.Add(new Organ { organName = name + "-Хребет", slot = "хребет", chassisOnly = true });
            so.organs = organs.ToArray();
            return so;
        }

        CreatureBody MakeBody(SpeciesSO chassis, SpeciesSO[] donors, string goName)
        {
            var go = new GameObject(goName);
            go.AddComponent<Health>();
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.center = new Vector3(0f, 1f, 0f);
            // NavLocomotion требуется психикам, но Dispatch сам его не требует — добавим чтобы не падал Require
            go.AddComponent<NavLocomotion>();
            var body = go.AddComponent<CreatureBody>();
            body.Configure(chassis, donors);
            return body;
        }

        int FindSlot(CreatureBody body, string slot) { for (int i = 0; i < body.SlotCount; i++) if (body.GetSlot(i).slot == slot) return i; return -1; }
        int FindVariant(CreatureBody body, int slotIdx, string species) { var v = body.GetVariants(slotIdx); for (int i = 0; i < v.Count; i++) if (v[i].species == species) return i; return -1; }

        [UnityTest]
        public IEnumerator Dispatch_PureHuman_FallbackToChimeraAlpha()
        {
            string[] slots = new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги" };
            var human = MakeSpecies("Человек", new Color(0.9f, 0.72f, 0.62f), slots);
            var wolf = MakeSpecies("Волк", new Color(0.5f, 0.5f, 0.52f), slots);
            var body = MakeBody(human, new[] { wolf }, "Dispatch_Human");
            yield return null;
            // чистая идентичность человека =1 → MostKin = Человек, но психики Человек нет → фолбэк химера-альфа
            var dom = body.MostKin(out var tier);
            Assert.IsNotNull(dom, "Pure human MostKin должен быть Человек");
            Assert.AreEqual("Человек", dom.speciesName);
            PsycheDispatch.Attach(body);
            yield return null;
            Assert.IsNotNull(body.GetComponent<ChimeraAlphaPsyche>(), "Диспатч pure human должен дать ChimeraAlphaPsyche (PsycheDispatch.cs:17 фолбэк)");
            Assert.IsNull(body.GetComponent<WolfPsyche>(), "Не должен дать WolfPsyche");

            Object.Destroy(body.gameObject);
            Object.DestroyImmediate(human);
            Object.DestroyImmediate(wolf);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Dispatch_WolfDominant_GivesWolfPsyche()
        {
            string[] slots = new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги" };
            var human = MakeSpecies("Человек", new Color(0.9f, 0.72f, 0.62f), slots);
            var wolf = MakeSpecies("Волк", new Color(0.5f, 0.5f, 0.52f), slots);
            var body = MakeBody(human, new[] { wolf }, "Dispatch_Wolf");
            yield return null;
            // ставим все слоты волчьи → Identity волка 0.9 → Medium → WolfPsyche
            foreach (var slotName in slots)
            {
                int sIdx = FindSlot(body, slotName);
                if (sIdx < 0) continue;
                int wIdx = FindVariant(body, sIdx, "Волк");
                if (wIdx >= 0) body.Install(sIdx, wIdx);
            }
            yield return null;
            var dom = body.MostKin(out var tier);
            Assert.IsNotNull(dom);
            Assert.AreEqual("Волк", dom.speciesName, "После графтов доминанта должна стать Волк");
            Assert.GreaterOrEqual(tier, KinTier.Medium, "Для диспатча нужен Medium+");
            PsycheDispatch.Attach(body);
            yield return null;
            Assert.IsNotNull(body.GetComponent<WolfPsyche>(), "Доминанта Волк Medium+ → WolfPsyche");
            Assert.IsNull(body.GetComponent<ChimeraAlphaPsyche>());

            Object.Destroy(body.gameObject);
            Object.DestroyImmediate(human);
            Object.DestroyImmediate(wolf);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Dispatch_BlurredIdentity_FallbackToAlpha()
        {
            string[] slots = new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги" };
            var human = MakeSpecies("Человек", Color.gray, slots);
            var wolf = MakeSpecies("Волк", Color.gray, slots);
            var snake = MakeSpecies("Змея", Color.green, slots);
            var body = MakeBody(human, new[] { wolf, snake }, "Dispatch_Blurred");
            yield return null;
            // ставим по одному слоту каждого зверя → размытие, ни один не дотягивает до Medium
            int s0 = FindSlot(body, "Пасть");
            int wIdx = FindVariant(body, s0, "Волк");
            if (wIdx >= 0) body.Install(s0, wIdx);
            int s1 = FindSlot(body, "Чутьё");
            int snIdx = FindVariant(body, s1, "Змея");
            if (snIdx >= 0) body.Install(s1, snIdx);
            yield return null;
            var dom = body.MostKin(out var tier);
            // tier может быть Weak или None — в обоих случаях диспатч должен дать альфу (т.к. Recompute мапит Weak→null, а Dispatch даёт альфу для Человек/размытия)
            PsycheDispatch.Attach(body);
            yield return null;
            // если tier < Medium, то по логике Metamorph/Recompute это химера; PsycheDispatch при dom==Человек тоже даёт альфу
            // проверяем что альфа получена когда доминанта не Wolf/Moose/Hedge/Snake
            if (dom == null || dom.speciesName == "Человек" || tier < KinTier.Medium)
                Assert.IsNotNull(body.GetComponent<ChimeraAlphaPsyche>(), "Размытая/человек доминанта → химера-альфа");
            else
                Assert.IsNotNull(body.GetComponent<WolfPsyche>(), "Если волк всё же Medium — WolfPsyche");

            Object.Destroy(body.gameObject);
            Object.DestroyImmediate(human);
            Object.DestroyImmediate(wolf);
            Object.DestroyImmediate(snake);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Dispatch_SnakeDominant_GivesSnakePsyche()
        {
            string[] slots = new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги" };
            var human = MakeSpecies("Человек", Color.gray, slots);
            var snake = MakeSpecies("Змея", Color.green, slots);
            var body = MakeBody(human, new[] { snake }, "Dispatch_Snake");
            yield return null;
            foreach (var slotName in slots)
            {
                int sIdx = FindSlot(body, slotName);
                int snIdx = FindVariant(body, sIdx, "Змея");
                if (snIdx >= 0) body.Install(sIdx, snIdx);
            }
            yield return null;
            PsycheDispatch.Attach(body);
            yield return null;
            Assert.IsNotNull(body.GetComponent<SnakePsyche>(), "Доминанта Змея → SnakePsyche");

            Object.Destroy(body.gameObject);
            Object.DestroyImmediate(human);
            Object.DestroyImmediate(snake);
            yield return null;
        }
    }
}
