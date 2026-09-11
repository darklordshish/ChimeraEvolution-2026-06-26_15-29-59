using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// СМЕШЕНИЕ ПЛАНА ПО ИДЕНТИЧНОСТИ — живые обещания спеки 14.08: тождественность на родном составе (И5) и
    /// локальность (И3: Пасть тянет голову, конечности висят на исключённом хребте и её не трогают).
    /// До 12.09 файл сторожил и клетку (`CageTable`), и два из четырёх тестов проверяли только её. Клетка
    /// отменена 10.09 (`SPEC-konstruktor-formy.md`) и снята из кода — живые проверки плана остались здесь.
    /// </summary>
    public class ChimeraBlendTests
    {
        readonly List<Object> sos = new List<Object>();
        readonly List<GameObject> gos = new List<GameObject>();

        SpeciesSO MakeSpecies(string name, BodySocket[] sockets, Organ[] organs)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            sos.Add(so);
            so.speciesName = name;
            so.tint = Color.gray;
            so.mutagenPool = 16;
            so.baseHp = 75;
            so.baseStamina = 100;
            so.baseStaminaRegen = 10f;
            so.sockets = sockets;
            so.bones = new Bone[0];
            so.organs = organs;
            return so;
        }

        BodySocket[] Sockets(float headSizeRel)
        {
            return new[]
            {
                new BodySocket { name = "хребет", localPos = Vector3.zero, baseSize = new Vector3(0.33f, 0.48f, 1.29f) },
                new BodySocket { name = "голова", parent = "хребет", attach = 1f, baseSize = new Vector3(0.26f, 0.22f, 0.46f), sizeRel = Vector3.one * headSizeRel },
                new BodySocket { name = "Пасть", parent = "голова", attach = 1f, baseSize = new Vector3(0.13f, 0.09f, 0.22f) },
                new BodySocket { name = "Ноги", parent = "хребет", attach = 0.2f, baseSize = new Vector3(0.14f, 0.74f, 0.19f), sizeRel = new Vector3(0.4f, 1.5f, 0.15f), mirrorX = true },
                new BodySocket { name = "Чутьё", parent = "голова", attach = 0.5f, baseSize = Vector3.one * 0.1f, inner = true },
            };
        }

        Organ[] OrgansFor(string name)
        {
            return new[]
            {
                new Organ { organName = name + "-Хребет", slot = "хребет", chassisOnly = true },
                new Organ { organName = name + "-Пасть", slot = "Пасть", cost = 3 },
                new Organ { organName = name + "-Ноги", slot = "Ноги", cost = 4 },
                new Organ { organName = name + "-Нюх", slot = "Чутьё", cost = 3 },
            };
        }

        CreatureBody MakeBody(SpeciesSO chassis, SpeciesSO[] donors)
        {
            var go = new GameObject("ChimeraBlend_" + chassis.speciesName);
            gos.Add(go);
            go.AddComponent<Health>();
            var body = go.AddComponent<CreatureBody>();
            body.Configure(chassis, donors);
            body.SetAffinity(chassis.speciesName, CreatureBody.AffinityCap);
            return body;
        }

        int FindSlot(CreatureBody body, string slot)
        {
            for (int i = 0; i < body.SlotCount; i++) if (body.GetSlot(i).slot == slot) return i;
            return -1;
        }

        int FindVariant(CreatureBody body, int slotIdx, string species)
        {
            var vars = body.GetVariants(slotIdx);
            for (int i = 0; i < vars.Count; i++) if (vars[i].species == species) return i;
            return -1;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in gos) Object.DestroyImmediate(go);
            gos.Clear();
            foreach (var o in sos) Object.DestroyImmediate(o);
            sos.Clear();
        }

        [Test]
        public void PureComposition_PlanIsNull_IdentityOne()
        {
            // И5: на родном составе вес 1 на себе — плана смешения нет вовсе, сборка тождественна сегодняшней
            var human = MakeSpecies("Человек", Sockets(0.5f), OrgansFor("Человек"));
            var wolf  = MakeSpecies("Волк",    Sockets(1.0f), OrgansFor("Волк"));
            var body = MakeBody(human, new[] { wolf });

            Assert.AreEqual(1f, body.Identity(human), 1e-4f, "чистый человек: Identity = 1");
            Assert.AreEqual(0f, body.Identity(wolf), 1e-4f);
            Assert.IsNull(body.GetBlendedPlan(), "родной состав → план null (тождественность до микрона)");
        }

        [Test]
        public void Locality_MawAffectsHead_LegsExcluded()
        {
            // И3: Ноги висят на хребте, а хребет из смешения исключён — голова от волчьих ног не тянется
            var human = MakeSpecies("Человек", Sockets(0.5f), OrgansFor("Человек"));
            var wolf  = MakeSpecies("Волк",    Sockets(1.0f), OrgansFor("Волк"));
            var body = MakeBody(human, new[] { wolf });

            int legSlot = FindSlot(body, "Ноги");
            Assert.IsTrue(body.Install(legSlot, FindVariant(body, legSlot, "Волк")));
            BodySocket headAfterLegs = null;
            var plan = body.GetBlendedPlan();
            if (plan != null) foreach (var s in plan) if (s != null && s.name == "голова") headAfterLegs = s;
            float headSizeX = headAfterLegs != null ? headAfterLegs.sizeRel.x : 0.5f; // план null — голова как в шасси
            Assert.AreEqual(0.5f, headSizeX, 1e-5f, "голова не тянется от ног (локальность, хребет исключён)");

            // Пасть висит на голове — голова обязана потянуться к волчьей
            var body2 = MakeBody(human, new[] { wolf });
            int mawSlot = FindSlot(body2, "Пасть");
            Assert.IsTrue(body2.Install(mawSlot, FindVariant(body2, mawSlot, "Волк")));
            var plan2 = body2.GetBlendedPlan();
            Assert.IsNotNull(plan2, "Пасть→голова — план должен смешать голову");
            BodySocket headAfterMaw = null;
            foreach (var s in plan2) if (s != null && s.name == "голова") headAfterMaw = s;
            Assert.IsNotNull(headAfterMaw);
            Assert.Greater(headAfterMaw.sizeRel.x, 0.5f, "голова потянулась к волку из-за Пасти");
        }
    }
}
