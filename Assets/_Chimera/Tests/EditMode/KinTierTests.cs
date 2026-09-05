using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    public class KinTierTests
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
            so.sockets = new BodySocket[0];
            so.bones = new Bone[0];
            var organs = new List<Organ>();
            foreach (var s in slots) organs.Add(new Organ { organName = name + "-" + s, slot = s, cost = 3 });
            organs.Add(new Organ { organName = name + "-Хребет", slot = "хребет", chassisOnly = true });
            so.organs = organs.ToArray();
            return so;
        }

        CreatureBody MakeBody(SpeciesSO chassis, SpeciesSO[] donors)
        {
            var go = new GameObject("KinBody_" + chassis.speciesName);
            goToDestroy.Add(go);
            go.AddComponent<Health>();
            var b = go.AddComponent<CreatureBody>();
            b.Configure(chassis, donors);
            // Аналогично IdentitySumTests: без капа родства шасси PoolUsed=19>16 и Install
            // молча отказывает (PoolUsed-SlotCost+newCost > Pool). В игре Awake ставит кап
            // до Configure, в тестах Configure после Awake — ставим вручную.
            b.SetAffinity(chassis.speciesName, CreatureBody.AffinityCap);
            return b;
        }

        int FindSlot(CreatureBody b, string name)
        {
            for (int i = 0; i < b.SlotCount; i++) if (b.GetSlot(i).slot == name) return i;
            return -1;
        }

        int FindVariant(CreatureBody b, int slotIdx, string species)
        {
            var vars = b.GetVariants(slotIdx);
            for (int i = 0; i < vars.Count; i++) if (vars[i].species == species) return i;
            return -1;
        }

        void InstallN(CreatureBody body, string donorSpecies, int n)
        {
            int installed = 0;
            for (int i = 0; i < body.SlotCount && installed < n; i++)
            {
                var slot = body.GetSlot(i);
                if (slot.slot == "хребет") continue;
                int vi = FindVariant(body, i, donorSpecies);
                if (vi >= 0 && body.Install(i, vi)) installed++;
            }
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
        public void Tier_PureSpecies_IsStrong()
        {
            var slots = new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги" };
            var human = MakeSpecies("Человек", slots);
            var wolf = MakeSpecies("Волк", slots);
            var body = MakeBody(human, new[] { wolf });
            Assert.AreEqual(KinTier.Strong, body.Tier(human), "Чистый вид должен быть Strong (>=0.999)");
            var kin = body.MostKin(out var tier);
            Assert.AreEqual(human, kin);
            Assert.AreEqual(KinTier.Strong, tier);
        }

        [Test]
        public void Tier_MediumAtBoundary_OneGraftOfSix()
        {
            // H=6: 1 графт => human 0.85 => Medium
            var slots = new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги" };
            var human = MakeSpecies("Человек", slots);
            var wolf = MakeSpecies("Волк", slots);
            var body = MakeBody(human, new[] { wolf });
            InstallN(body, "Волк", 1);
            float id = body.Identity(human);
            Assert.AreEqual(0.85f, id, 1e-4f);
            Assert.AreEqual(KinTier.Medium, body.Tier(human), $"Identity {id} должен дать Medium (>=0.85)");
        }

        [Test]
        public void Tier_WeakAtBoundary_TwoGraftsOfSix()
        {
            // H=6: 2 графте => human 0.70 => Weak (>=0.65 <0.85)
            var slots = new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги" };
            var human = MakeSpecies("Человек", slots);
            var wolf = MakeSpecies("Волк", slots);
            var body = MakeBody(human, new[] { wolf });
            InstallN(body, "Волк", 2);
            float id = body.Identity(human);
            Assert.AreEqual(0.70f, id, 1e-4f);
            Assert.AreEqual(KinTier.Weak, body.Tier(human), $"Identity {id} должен дать Weak");
        }

        [Test]
        public void Tier_None_BelowWeak()
        {
            // H=6: 3 графте => human 0.55 => None (<0.65)
            var slots = new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги" };
            var human = MakeSpecies("Человек", slots);
            var wolf = MakeSpecies("Волк", slots);
            var body = MakeBody(human, new[] { wolf });
            InstallN(body, "Волк", 3);
            float id = body.Identity(human);
            Assert.AreEqual(0.55f, id, 1e-4f);
            Assert.AreEqual(KinTier.None, body.Tier(human));
            Assert.AreEqual(KinTier.None, body.Tier(wolf));
        }

        [Test]
        public void MostKin_HybridChimera_WhenHalfAndHalf_ReturnsNull()
        {
            var slots = new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги" };
            var human = MakeSpecies("Человек", slots);
            var wolf = MakeSpecies("Волк", slots);
            var body = MakeBody(human, new[] { wolf });
            InstallN(body, "Волк", 3); // 0.55/0.45 — оба <0.65
            var kin = body.MostKin(out var tier);
            Assert.IsNull(kin, "При <0.65 обе идентичности — химера, MostKin null");
            Assert.AreEqual(KinTier.None, tier);
        }

        [Test]
        public void Homogeneity_PureIsOne_HybridLessThanOne()
        {
            var slots = new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги" };
            var human = MakeSpecies("Человек", slots);
            var wolf = MakeSpecies("Волк", slots);
            var pure = MakeBody(human, new[] { wolf });
            Assert.AreEqual(1f, pure.Homogeneity, 1e-4f);

            var hybrid = MakeBody(human, new[] { wolf });
            InstallN(hybrid, "Волк", 2);
            float h = hybrid.Homogeneity;
            Assert.Greater(h, 0f);
            Assert.Less(h, 1f);
            // Homogeneity == max Identity
            float maxId = Mathf.Max(hybrid.Identity(human), hybrid.Identity(wolf));
            Assert.AreEqual(maxId, h, 1e-4f);
        }
    }
}
