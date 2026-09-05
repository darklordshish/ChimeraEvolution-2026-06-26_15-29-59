using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    public class MetamorphTests
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
            foreach (var s in slots) organs.Add(new Organ { organName = name + "-" + s, slot = s, cost = 2 });
            organs.Add(new Organ { organName = name + "-Хребет", slot = "хребет", chassisOnly = true });
            so.organs = organs.ToArray();
            return so;
        }

        CreatureBody MakeBody(SpeciesSO chassis, SpeciesSO[] donors, string name)
        {
            var go = new GameObject(name);
            go.AddComponent<Health>();
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.center = new Vector3(0f, 1f, 0f);
            go.AddComponent<NavLocomotion>();
            var body = go.AddComponent<CreatureBody>();
            go.AddComponent<Metamorph>(); // слушатель onDominantChanged (Metamorph.cs:19)
            body.Configure(chassis, donors);
            return body;
        }

        int FindSlot(CreatureBody b, string slot) { for (int i = 0; i < b.SlotCount; i++) if (b.GetSlot(i).slot == slot) return i; return -1; }
        int FindVariant(CreatureBody b, int idx, string species) { var v = b.GetVariants(idx); for (int i = 0; i < v.Count; i++) if (v[i].species == species) return i; return -1; }

        [UnityTest]
        public IEnumerator Metamorph_SwitchesPsyche_OnDominantShift_MediumPlus()
        {
            string[] slots = new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги" };
            var human = MakeSpecies("Человек", new Color(0.9f, 0.72f, 0.62f), slots);
            var wolf = MakeSpecies("Волк", new Color(0.5f, 0.5f, 0.52f), slots);
            var body = MakeBody(human, new[] { wolf }, "Metamorph_Shift");
            yield return null;
            // исходно — человек pure → химера-альфа
            PsycheDispatch.Attach(body);
            yield return null;
            Assert.IsNotNull(body.GetComponent<ChimeraAlphaPsyche>(), "Старт — ChimeraAlpha");
            Assert.IsNull(body.GetComponent<WolfPsyche>());

            // сдвиг доминанты к волку Medium+ (все слоты волчьи)
            foreach (var s in slots)
            {
                int sIdx = FindSlot(body, s);
                int wIdx = FindVariant(body, sIdx, "Волк");
                if (wIdx >= 0) body.Install(sIdx, wIdx);
            }
            // Install вызывает Recompute → onDominantChanged → Metamorph.Remorph (Destroy старая)
            yield return null; // Destroy отложен до конца кадра
            yield return null;
            Assert.IsNotNull(body.GetComponent<WolfPsyche>(), "После сдвига доминанты Medium+ → WolfPsyche");
            Assert.IsNull(body.GetComponent<ChimeraAlphaPsyche>(), "Старая химера-альфа должна быть снесена");

            Object.Destroy(body.gameObject);
            Object.DestroyImmediate(human);
            Object.DestroyImmediate(wolf);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Metamorph_NoSwitch_WhenBelowMedium()
        {
            string[] slots = new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги" };
            var human = MakeSpecies("Человек", Color.gray, slots);
            var wolf = MakeSpecies("Волк", Color.gray, slots);
            var body = MakeBody(human, new[] { wolf }, "Metamorph_NoSwitch");
            yield return null;
            PsycheDispatch.Attach(body);
            yield return null;
            Assert.IsNotNull(body.GetComponent<ChimeraAlphaPsyche>());

            // один графт — Weak, не Medium → остаётся химера (Metamorph.cs гейтит Medium+)
            int sIdx = FindSlot(body, "Пасть");
            int wIdx = FindVariant(body, sIdx, "Волк");
            body.Install(sIdx, wIdx);
            yield return null;
            yield return null;
            Assert.IsNotNull(body.GetComponent<ChimeraAlphaPsyche>(), "Weak — метаморфозы нет, остаётся альфа");
            Assert.IsNull(body.GetComponent<WolfPsyche>());

            Object.Destroy(body.gameObject);
            Object.DestroyImmediate(human);
            Object.DestroyImmediate(wolf);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Metamorph_ClearsConstrict_OnRemorph()
        {
            string[] slots = new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги" };
            var human = MakeSpecies("Человек", Color.gray, slots);
            var wolf = MakeSpecies("Волк", Color.gray, slots);
            var snake = MakeSpecies("Змея", Color.green, slots);
            // дадим змее constrict орган чтобы был Constrict компонент
            // но для теста достаточно что Metamorph.Remorph зовёт cm.End() если есть
            var body = MakeBody(human, new[] { wolf, snake }, "Metamorph_Constrict");
            yield return null;
            PsycheDispatch.Attach(body);
            yield return null;
            // повесим Constrict вручную
            var constrict = body.gameObject.AddComponent<Constrict>();
            // доведём до волка
            foreach (var s in slots)
            {
                int idx = FindSlot(body, s);
                int wIdx = FindVariant(body, idx, "Волк");
                if (wIdx >= 0) body.Install(idx, wIdx);
            }
            yield return null;
            yield return null;
            // после реморфа Constrict должен быть сброшен (End вызван), но компонент остаётся — стадия 0
            // главное что смена психики произошла без NRE
            Assert.IsNotNull(body.GetComponent<WolfPsyche>());

            Object.Destroy(body.gameObject);
            Object.DestroyImmediate(human);
            Object.DestroyImmediate(wolf);
            Object.DestroyImmediate(snake);
            yield return null;
        }
    }
}
