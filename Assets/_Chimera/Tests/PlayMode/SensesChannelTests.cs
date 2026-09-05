using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// Senses каналы Sight/Hearing/Scent/Thermal от сборки, ScentTrail цвет состава
    /// (Senses/*, Perception.cs, CreatureBody.cs:360)
    /// </summary>
    public class SensesChannelTests
    {
        SpeciesSO MakeSpecies(string name, Color tint, Organ[] organs)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = name;
            so.tint = tint;
            so.mutagenPool = 10;
            so.baseHp = 50;
            so.sockets = new BodySocket[0];
            so.bones = new Bone[0];
            so.organs = organs;
            return so;
        }

        [UnityTest]
        public IEnumerator Sight_Always_On_HearingBoost_ScentAndThermal_FromAssembly()
        {
            var spine = new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true };
            var maw = new Organ { organName = "Пасть", slot = BodySlots.Maw, cost = 2 };
            var senseNose = new Organ { organName = "Нюх", slot = BodySlots.Sense, cost = 3, enablesScent = true, keenHearing = false, hearingMult = 0f };
            var sensePit = new Organ { organName = "Пит", slot = BodySlots.Sense, cost = 3, enablesScent = false, enablesThermal = true, thermalRange = 25f };
            var senseEar = new Organ { organName = "Ухо", slot = BodySlots.Sense, cost = 3, enablesScent = false, keenHearing = true, hearingMult = 2f };

            var human = MakeSpecies("Человек", Color.white, new[] { spine, maw, senseNose });
            var wolf = MakeSpecies("Волк", new Color(0.5f,0.5f,0.52f), new[] { new Organ { organName="Хребет", slot=BodySlots.Spine, chassisOnly=true }, new Organ { organName="Пит", slot=BodySlots.Sense, cost=3, enablesThermal=true, thermalRange=25f } });
            var moose = MakeSpecies("Лось", new Color(0.55f,0.42f,0.25f), new[] { new Organ { organName="Хребет", slot=BodySlots.Spine, chassisOnly=true }, senseEar });

            // игрок: chassis человек, доноры волк+лось — можем надеть любой нюх
            var go = new GameObject("SensesAssembly");
            go.AddComponent<CharacterController>().height = 2f;
            go.GetComponent<CharacterController>().center = new Vector3(0f,1f,0f);
            go.AddComponent<PlayerController>();
            go.AddComponent<Health>().SetMaxHealth(100);
            var body = go.AddComponent<CreatureBody>();
            body.Configure(human, new[] { wolf, moose });
            yield return null;

            var senses = go.GetComponent<Senses>();
            Assert.IsNotNull(senses, "игрок должен иметь Senses (CreatureBody.Awake)");

            // Sight всегда >0 (глаза при нём всегда) — даже без органа Чутья
            Assert.Greater(senses.Range(SenseKind.Sight), 0f, "Sight должен быть >0 всегда (Perception: глаза при нём)");

            // Hearing всегда >0 (уши при нём всегда), базовый ~20 (CreatureBody.cs:33)
            float hearingBase = senses.Range(SenseKind.Hearing);
            Assert.Greater(hearingBase, 0f, "Hearing должен быть >0 всегда");

            // Scent включен: у human старта Нюх с enablesScent => Scent >0
            Assert.Greater(senses.Range(SenseKind.Scent), 0f, "Scent должен быть >0 когда надето волчье Чутьё (enablesScent)");

            // Thermal выключен: нет Пит-органа => 0
            Assert.AreEqual(0f, senses.Range(SenseKind.Thermal), 1e-5f, "Thermal должен быть 0 без Пит-органа");

            // надеть Пит вместо Нюха: снять enablesScent, включить thermal
            int senseSlot = -1;
            for (int i=0;i<body.SlotCount;i++) if (body.GetSlot(i).slot==BodySlots.Sense) senseSlot=i;
            Assert.AreNotEqual(-1, senseSlot);
            var vars = body.GetVariants(senseSlot);
            int vPit=-1, vNose=-1;
            for (int i=0;i<vars.Count;i++) { if (vars[i].organName=="Пит") vPit=i; if (vars[i].organName=="Нюх") vNose=i; }
            Assert.AreNotEqual(-1, vPit);
            Assert.IsTrue(body.Install(senseSlot, vPit));
            yield return null;
            Assert.AreEqual(0f, senses.Range(SenseKind.Scent), 1e-5f, "после снятия Нюха Scent должен закрыться (0)");
            Assert.Greater(senses.Range(SenseKind.Thermal), 0f, "после Пит-органа Thermal >0");

            // надеть лосиное ухо: hearing ×2
            int vEar=-1;
            vars = body.GetVariants(senseSlot);
            for (int i=0;i<vars.Count;i++) if (vars[i].organName=="Ухо") vEar=i;
            if (vEar!=-1)
            {
                Assert.IsTrue(body.Install(senseSlot, vEar));
                yield return null;
                float hearingBoosted = senses.Range(SenseKind.Hearing);
                Assert.AreEqual(hearingBase*2f, hearingBoosted, 0.01f, "лосиное ухо hearingMult=2 должно удвоить дальность слуха (CreatureBody.cs:374)");
            }

            Object.Destroy(go);
            Object.DestroyImmediate(human);
            Object.DestroyImmediate(wolf);
            Object.DestroyImmediate(moose);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ScentTrail_Color_IsCompositionTint()
        {
            // Проверяем что ScentTrail.Configure получает смесь тинтов по составу (CreatureBody.cs:449)
            var spine = new Organ { organName="Хребет", slot=BodySlots.Spine, chassisOnly=true };
            var mawHuman = new Organ { organName="ПастьЧ", slot=BodySlots.Maw, cost=2 };
            var mawWolf = new Organ { organName="ПастьВ", slot=BodySlots.Maw, cost=2 };
            var human = MakeSpecies("Человек", new Color(0.9f,0.85f,0.8f), new[] { spine, mawHuman });
            var wolf = MakeSpecies("Волк", new Color(0.5f,0.5f,0.52f), new[] { new Organ{organName="Хребет",slot=BodySlots.Spine,chassisOnly=true}, mawWolf });

            var go = new GameObject("ScentComposition");
            go.AddComponent<CharacterController>().height=2f;
            go.GetComponent<CharacterController>().center=new Vector3(0f,1f,0f);
            go.AddComponent<Health>().SetMaxHealth(100);
            go.AddComponent<PlayerController>();
            var body = go.AddComponent<CreatureBody>();
            body.Configure(human, new[] { wolf });
            yield return null;

            // чистый человек: состав = тинт человека
            var trail = go.GetComponent<ScentTrail>();
            Assert.IsNotNull(trail);
            // достаём tint через сериализованное поле
            var fi = typeof(ScentTrail).GetField("tint", System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            Color tintPure = (Color)fi.GetValue(trail);
            Assert.AreEqual(human.tint.r, tintPure.r, 0.02f, "чистый шасси => след красится тинтом шасси");
            Assert.AreEqual(human.tint.g, tintPure.g, 0.02f);
            Assert.AreEqual(human.tint.b, tintPure.b, 0.02f);

            // ставим волчью пасть => состав 50/50 => средний цвет
            int slot=-1; for(int i=0;i<body.SlotCount;i++) if(body.GetSlot(i).slot==BodySlots.Maw) slot=i;
            var vars = body.GetVariants(slot);
            int wi=-1; for(int i=0;i<vars.Count;i++) if(vars[i].organName=="ПастьВ") wi=i;
            Assert.IsTrue(body.Install(slot, wi));
            yield return null;
            // Recompute уже покрасил след: вычитываем снова
            Color tintMixed = (Color)fi.GetValue(trail);
            Color expected = new Color((human.tint.r+wolf.tint.r)/2f, (human.tint.g+wolf.tint.g)/2f, (human.tint.b+wolf.tint.b)/2f);
            // alpha у следа 0.65 из Configure (CreatureBody.cs:452)
            Assert.AreEqual(expected.r, tintMixed.r, 0.03f, "химера 50/50 => след средний цвет");
            Assert.AreEqual(expected.g, tintMixed.g, 0.03f);
            Assert.AreEqual(expected.b, tintMixed.b, 0.03f);
            Assert.AreEqual(0.65f, tintMixed.a, 0.02f, "alpha следа 0.65 (ScentTrail.Configure)");

            Object.Destroy(go);
            Object.DestroyImmediate(human);
            Object.DestroyImmediate(wolf);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Senses_ZeroRange_ChannelClosed()
        {
            var go = new GameObject("SensesZero");
            var senses = go.AddComponent<Senses>();
            // через Set нулим канал
            senses.Set(SenseKind.Sight, 0f);
            senses.Set(SenseKind.Scent, 0f);
            senses.Set(SenseKind.Thermal, 0f);
            senses.Set(SenseKind.Hearing, 0f);
            Assert.AreEqual(0f, senses.Range(SenseKind.Sight));
            Assert.AreEqual(0f, senses.Range(SenseKind.Scent));
            Assert.AreEqual(0f, senses.Range(SenseKind.Thermal));
            Assert.AreEqual(0f, senses.Range(SenseKind.Hearing));

            // Seed не перетирает уже заданный >0, но 0 остаётся 0 — Set закрывает канал вместе со снятым органом (Senses.cs:43)
            senses.Set(SenseKind.Scent, 22f);
            Assert.AreEqual(22f, senses.Range(SenseKind.Scent));
            senses.Set(SenseKind.Scent, 0f);
            Assert.AreEqual(0f, senses.Range(SenseKind.Scent));
            Object.Destroy(go);
            yield return null;
        }
    }
}
