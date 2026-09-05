using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    public class IdentitySumTests
    {
        readonly List<Object> toDestroy = new List<Object>();
        readonly List<GameObject> goToDestroy = new List<GameObject>();

        SpeciesSO MakeSpecies(string name, string[] slots)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            toDestroy.Add(so);
            so.speciesName = name;
            so.mutagenPool = 30;
            so.baseHp = 50;
            so.baseStamina = 100;
            so.baseStaminaRegen = 10f;
            so.tint = Color.gray;
            // sockets пустые чтобы MorphBuilder не строил геометрию
            so.sockets = new BodySocket[0];
            so.bones = new Bone[0];
            var organs = new List<Organ>();
            foreach (var s in slots)
            {
                organs.Add(new Organ { organName = name + "-" + s, slot = s, cost = 3 });
            }
            // chassisOnly несущий хребет — не участвует в массе но нужен для полноты
            organs.Add(new Organ { organName = name + "-Хребет", slot = "хребет", chassisOnly = true });
            so.organs = organs.ToArray();
            return so;
        }

        CreatureBody MakeBody(SpeciesSO chassis, SpeciesSO[] donors)
        {
            var go = new GameObject("IdentityBody_" + chassis.speciesName);
            goToDestroy.Add(go);
            go.AddComponent<Health>();
            var body = go.AddComponent<CreatureBody>();
            body.Configure(chassis, donors);
            // Configure не ставит родство шасси (Awake было до Configure с null chassis) — без этого
            // PoolUsed = 6*3+1 =19 >16 блокирует Install (CanInstall/Available) и графты не ставятся:
            // Identity остаётся 1.0 вместо 0.85, Homogeneity 1.0, MostKin не null. Ставим кап как в рантайме.
            body.SetAffinity(chassis.speciesName, CreatureBody.AffinityCap);
            return body;
        }

        int FindSlot(CreatureBody body, string slotName)
        {
            for (int i = 0; i < body.SlotCount; i++)
                if (body.GetSlot(i).slot == slotName) return i;
            return -1;
        }

        int FindVariant(CreatureBody body, int slotIdx, string speciesName)
        {
            var vars = body.GetVariants(slotIdx);
            for (int i = 0; i < vars.Count; i++)
                if (vars[i].species == speciesName) return i;
            return -1;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in goToDestroy) Object.DestroyImmediate(go);
            goToDestroy.Clear();
            foreach (var o in toDestroy) Object.DestroyImmediate(o);
            toDestroy.Clear();
        }

        [Test]
        public void Identity_SumEqualsOne_WithOneGraft()
        {
            string[] slots = new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги" };
            var human = MakeSpecies("Человек", slots);
            var wolf = MakeSpecies("Волк", slots);
            var snake = MakeSpecies("Змея", slots);
            var body = MakeBody(human, new[] { wolf, snake });

            int sIdx = FindSlot(body, "Пасть");
            Assert.AreNotEqual(-1, sIdx, "Слот Пасть не найден");
            int wIdx = FindVariant(body, sIdx, "Волк");
            Assert.AreNotEqual(-1, wIdx);
            Assert.IsTrue(body.Install(sIdx, wIdx));

            float h = body.Identity(human);
            float w = body.Identity(wolf);
            float sn = body.Identity(snake);
            float sum = h + w + sn;
            Assert.AreEqual(1f, sum, 1e-4f, $"Σ Identity должно быть 1, получено {sum} (h={h} w={w} sn={sn})");
        }

        [Test]
        public void Identity_EachNonNegative()
        {
            string[] slots = new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги" };
            var human = MakeSpecies("Человек", slots);
            var wolf = MakeSpecies("Волк", slots);
            var snake = MakeSpecies("Змея", slots);
            var body = MakeBody(human, new[] { wolf, snake });
            int sIdx = FindSlot(body, "Пасть");
            int wIdx = FindVariant(body, sIdx, "Волк");
            body.Install(sIdx, wIdx);

            Assert.GreaterOrEqual(body.Identity(human), 0f);
            Assert.GreaterOrEqual(body.Identity(wolf), 0f);
            Assert.GreaterOrEqual(body.Identity(snake), 0f);
        }

        [Test]
        public void Identity_ConvexSum_IsOne()
        {
            string[] slots = new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги" };
            var human = MakeSpecies("Человек", slots);
            var wolf = MakeSpecies("Волк", slots);
            var snake = MakeSpecies("Змея", slots);
            // без графтов — сумма тоже 1
            var body = MakeBody(human, new[] { wolf, snake });
            float sum = body.Identity(human) + body.Identity(wolf) + body.Identity(snake);
            Assert.AreEqual(1f, sum, 1e-4f);
            // каждый >=0 и <=1
            Assert.GreaterOrEqual(body.Identity(human), 0f);
            Assert.LessOrEqual(body.Identity(human), 1f);
        }

        [Test]
        public void Identity_PureHuman_IsOne()
        {
            string[] slots = new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги" };
            var human = MakeSpecies("Человек", slots);
            var wolf = MakeSpecies("Волк", slots);
            var body = MakeBody(human, new[] { wolf });
            Assert.AreEqual(1f, body.Identity(human), 1e-4f, "Чистый человек должен давать Identity=1");
        }

        [Test]
        public void Identity_PureHuman_SecondSpeciesZero()
        {
            string[] slots = new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги" };
            var human = MakeSpecies("Человек", slots);
            var wolf = MakeSpecies("Волк", slots);
            var body = MakeBody(human, new[] { wolf });
            Assert.AreEqual(0f, body.Identity(wolf), 1e-4f, "Чистый человек: волчья идентичность 0");
        }
    }
}
