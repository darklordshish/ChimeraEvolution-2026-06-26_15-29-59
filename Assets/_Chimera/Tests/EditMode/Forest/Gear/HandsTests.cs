using NUnit.Framework;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Руки человека как данные: дефы, прочность/порча/топливо, раскладка, чистота резолвера.
    /// Слайс s4 (ветка forest/s4-hands-tools, спека 2026-09-18-forest-s4).
    /// </summary>
    public class HandsTests
    {
        static HandItemDef Spear()
        {
            return new HandItemDef
            {
                id = "spear", kind = HandItemKind.Spear, tier = 1,
                damage = 8, range = 2.2f, halfAngle = 35f,
                staminaCost = 20f, cooldown = 1.2f, windupTime = 0.5f,
                backstabMult = 3f, durabilityMax = 40
            };
        }

        static HandItemDef Knife()
        {
            return new HandItemDef
            {
                id = "bone-knife", kind = HandItemKind.BoneKnife, tier = 2,
                damage = 6, range = 1.4f, halfAngle = 50f,
                staminaCost = 12f, cooldown = 0.7f, windupTime = 0.3f,
                backstabMult = 10f, durabilityMax = 50
            };
        }

        static HandItemDef Torch()
        {
            return new HandItemDef
            {
                id = "torch", kind = HandItemKind.Torch, tier = 1,
                damage = 2, range = 1.6f, halfAngle = 60f,
                backstabMult = 1f, durabilityMax = 0,
                fuelSeconds = 120f, lightRadius = 8f
            };
        }

        [Test]
        public void Bare_Defaults_MatchLimbStrikeShape()
        {
            var s = HandStrikeResolver.Resolve(null, false, false, 1f);
            Assert.AreEqual(10, s.damage, "голые: урон 10");
            Assert.AreEqual(1.6f, s.range, "голые: досягаемость 1.6");
            Assert.AreEqual(60f, s.halfAngle, "голые: конус 60");
            Assert.AreEqual(1f, s.damageMult, "мощь идёт множителем, не печётся");
        }

        [Test]
        public void Spear_BackstabX3_OnlyFromBehind()
        {
            var plain = HandStrikeResolver.Resolve(Spear(), false, false, 1f);
            Assert.AreEqual(1f, plain.damageMult, "в лоб — без множителя");
            Assert.AreEqual(0.5f, plain.windupTime, "замах проходит в выход (нужен NPC-доставке)");
            var back = HandStrikeResolver.Resolve(Spear(), false, true, 1f);
            Assert.AreEqual(8, back.damage, "база не тронута");
            Assert.AreEqual(3f, back.damageMult, "в спину ×3");
        }

        [Test]
        public void Knife_BackstabX10_AndNormalIsBase()
        {
            var normal = HandStrikeResolver.Resolve(Knife(), false, false, 2f);
            Assert.AreEqual(6, normal.damage, "без засады — базовый урон");
            Assert.AreEqual(2f, normal.damageMult, "без засады — только мощь");
            var back = HandStrikeResolver.Resolve(Knife(), false, true, 2f);
            Assert.AreEqual(20f, back.damageMult, "в спину: мощь × множитель (ваншот-риск — см. спеку)");
        }

        [Test]
        public void Broken_FallsBackToBare()
        {
            var s = HandStrikeResolver.Resolve(Spear(), true, true, 1f);
            Assert.AreEqual(10, s.damage, "сломанное копьё — голые руки");
            Assert.AreEqual(1.6f, s.range);
        }

        [Test]
        public void Durability_Decrements_AndBreaks()
        {
            var item = new HandItemInstance(Spear());
            item.durabilityLeft = 3;
            Assert.IsTrue(item.Use());
            Assert.IsTrue(item.Use());
            Assert.IsFalse(item.Use(), "третий удар ломает (3→0)");
            Assert.IsTrue(item.IsBroken);
        }

        [Test]
        public void Bare_Unbreakable()
        {
            var bare = new HandItemInstance(HandItemDef.Bare());
            for (int i = 0; i < 10; i++) Assert.IsTrue(bare.Use(), "голые не ломаются");
            Assert.IsFalse(bare.IsBroken);
        }

        [Test]
        public void Spoil_Expires_ByTime()
        {
            var trophy = new HandItemDef
            {
                id = "trophy", kind = HandItemKind.ChimeraTrophy, tier = 3,
                damage = 14, range = 1.8f, halfAngle = 50f, backstabMult = 2f,
                spoilSeconds = 10f
            };
            var item = new HandItemInstance(trophy);
            item.Tick(11f);
            Assert.IsTrue(item.IsBroken, "трофей протух по времени, не по ударам");
        }

        [Test]
        public void Torch_FuelBurns_NotHits()
        {
            var torch = new HandItemInstance(Torch());
            torch.Tick(121f);
            Assert.IsTrue(torch.IsBroken, "факел гаснет по топливу");
            var fresh = new HandItemInstance(Torch());
            for (int i = 0; i < 20; i++) fresh.Use();
            Assert.IsFalse(fresh.IsBroken, "удары топливо не жгут");
        }

        [Test]
        public void TwoHanded_BlocksOffhand()
        {
            var loadout = new HandsLoadout();
            var spear = Spear();
            spear.twoHanded = true;
            Assert.IsTrue(loadout.EquipMain(spear), "двуручное в main встаёт");
            Assert.IsFalse(loadout.CanEquip(HandsLoadout.HandSlot.Off, Knife()),
                "при двуручном off занят");
        }

        [Test]
        public void Torch_OffOnly_PlusKnife()
        {
            var loadout = new HandsLoadout();
            Assert.IsFalse(loadout.EquipMain(Torch()), "факел не в main");
            Assert.IsTrue(loadout.EquipMain(Knife()), "нож в main");
            Assert.IsTrue(loadout.EquipOff(Torch()), "факел в off + одноручное");
        }

        [Test]
        public void Validate_CatchesNegatives()
        {
            var bad = Spear();
            bad.damage = -1;
            bad.range = 0f;
            bad.backstabMult = 0.5f;
            Assert.GreaterOrEqual(HandItemRules.Validate(bad).Count, 3,
                "урон −1, досягаемость 0, засада 0.5 — всё ловится");
            Assert.IsEmpty(HandItemRules.Validate(Spear()), "корректное копьё чисто");
            Assert.IsEmpty(HandItemRules.Validate(Torch()), "корректный факел чист");
        }

        [Test]
        public void Validate_TorchNeedsLightAndFuel_TwoHandedBanned()
        {
            var dark = Torch();
            dark.lightRadius = 0f;
            Assert.IsNotEmpty(HandItemRules.Validate(dark), "факел без света ловится");
            var heavy = Torch();
            heavy.twoHanded = true;
            Assert.IsNotEmpty(HandItemRules.Validate(heavy), "двуручный факел запрещён");
        }

        [Test]
        public void Resolver_Pure_SameIn_SameOut()
        {
            var a = HandStrikeResolver.Resolve(Spear(), false, true, 1.5f);
            var b = HandStrikeResolver.Resolve(Spear(), false, true, 1.5f);
            Assert.AreEqual(a.damage, b.damage);
            Assert.AreEqual(a.damageMult, b.damageMult);
            Assert.AreEqual(a.range, b.range);
            Assert.AreEqual(a.windupTime, b.windupTime);
        }
    }
}
