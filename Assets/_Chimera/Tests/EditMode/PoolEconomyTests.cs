using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Slots экономика: PoolUsed ≤ Pool, WornElsewhere блокирует дубль,
    /// CanInstall/Install/Remove, химерный слот старт -1 Grant/RemoveChimeraSlot
    /// (CreatureBody.Slots.cs:220)
    /// </summary>
    public class PoolEconomyTests
    {
        readonly List<Object> toDestroy = new List<Object>();
        readonly List<GameObject> goToDestroy = new List<GameObject>();

        SpeciesSO MakeSpecies(string name, int pool, Organ[] organs)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            toDestroy.Add(so);
            so.speciesName = name;
            so.mutagenPool = pool;
            so.baseHp = 50;
            so.sockets = new BodySocket[0];
            so.bones = new Bone[0];
            so.organs = organs;
            return so;
        }

        CreatureBody MakeBody(SpeciesSO chassis, SpeciesSO[] donors)
        {
            var go = new GameObject("PoolBody_" + chassis.speciesName);
            goToDestroy.Add(go);
            go.AddComponent<Health>();
            var b = go.AddComponent<CreatureBody>();
            b.Configure(chassis, donors);
            return b;
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
        public void PoolUsed_NeverExceedsPool_Initially()
        {
            var human = MakeSpecies("Человек", 10, new[]
            {
                new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true },
                new Organ { organName = "Пасть", slot = BodySlots.Maw, hotkey = "1", cost = 3 },
                new Organ { organName = "Сердце", slot = BodySlots.Heart, hotkey = "2", cost = 4 },
            });
            var wolf = MakeSpecies("Волк", 10, new[]
            {
                new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true },
                new Organ { organName = "Клык", slot = BodySlots.Maw, cost = 8 },
            });
            var body = MakeBody(human, new[] { wolf });
            Assert.LessOrEqual(body.PoolUsed, body.Pool, "из коробки PoolUsed ≤ Pool (родной набор со скидкой родства к шасси 100 => -80%)");
        }

        [Test]
        public void CanInstall_RespectsPoolLimit()
        {
            // Пул 5, родной пасть 3, волчий клык 8 => влезает только после скидки
            var human = MakeSpecies("Человек", 5, new[]
            {
                new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true },
                new Organ { organName = "Пасть", slot = BodySlots.Maw, hotkey = "1", cost = 1 },
            });
            var wolf = MakeSpecies("Волк", 5, new[]
            {
                new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true },
                new Organ { organName = "Клык", slot = BodySlots.Maw, cost = 20 },
            });
            var body = MakeBody(human, new[] { wolf });
            int slot = 1; // Пасть (0=хребет,1=Пасть)
            // пытаемся поставить дорогой вариант — не влезает
            var vars = body.GetVariants(slot);
            int expensive = -1;
            for (int i = 0; i < vars.Count; i++) if (vars[i].organName == "Клык") expensive = i;
            Assert.AreNotEqual(-1, expensive);
            Assert.IsFalse(vars[expensive].affordable, "дорогой вариант не должен быть affordable");
            Assert.IsFalse(body.Install(slot, expensive), "Install дорогого должен вернуть false");
            Assert.LessOrEqual(body.PoolUsed, body.Pool);
        }

        [Test]
        public void WornElsewhere_BlocksDuplicateOrgan()
        {
            // Два слота одного типа невозможны без химерного; но химерный принимает любой орган.
            // Кладём один и тот же волчий орган в родной слот и пытаемся в химерный — дубль должен блокироваться.
            var spine = new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true };
            var humanMaw = new Organ { organName = "Пасть", slot = BodySlots.Maw, cost = 2 };
            var wolfMaw = new Organ { organName = "Клык", slot = BodySlots.Maw, cost = 2 };
            var human = MakeSpecies("Человек", 20, new[] { spine, humanMaw });
            var wolf = MakeSpecies("Волк", 20, new[] { new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true }, wolfMaw });

            var body = MakeBody(human, new[] { wolf });

            // ставим волчий клык в родной слот
            int mawSlot = -1;
            for (int i = 0; i < body.SlotCount; i++) if (body.GetSlot(i).slot == BodySlots.Maw) mawSlot = i;
            Assert.AreNotEqual(-1, mawSlot);
            var vars = body.GetVariants(mawSlot);
            int wi = -1; for (int i = 0; i < vars.Count; i++) if (vars[i].organName == "Клык") wi = i;
            Assert.IsTrue(body.Install(mawSlot, wi));

            // даём химерный и пробуем тот же орган туда — WornElsewhere => Available false
            body.GrantChimeraSlot();
            int chim = -1;
            for (int i = 0; i < body.SlotCount; i++) if (body.GetSlot(i).chimera) chim = i;
            var chimVars = body.GetVariants(chim);
            int chimWi = -1;
            for (int i = 0; i < chimVars.Count; i++)
                if (chimVars[i].organName == "Клык" && chimVars[i].species == "Волк") chimWi = i;
            Assert.AreNotEqual(-1, chimWi);
            Assert.IsTrue(chimVars[chimWi].duplicate, "тот же орган уже надет — duplicate=true (Slots.cs:230 WornElsewhere)");
            Assert.IsFalse(chimVars[chimWi].affordable, "duplicate считается недоступным через Available (Slots.cs:243)");
            Assert.IsFalse(body.Install(chim, chimWi), "дубль органа в другом слоте должен быть отклонён");
        }

        [Test]
        public void Install_And_Remove_RoundTrip()
        {
            var spine = new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true };
            var humanMaw = new Organ { organName = "Пасть", slot = BodySlots.Maw, cost = 2 };
            var wolfMaw = new Organ { organName = "Клык", slot = BodySlots.Maw, cost = 2 };
            var human = MakeSpecies("Человек", 20, new[] { spine, humanMaw });
            var wolf = MakeSpecies("Волк", 20, new[] { new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true }, wolfMaw });
            var body = MakeBody(human, new[] { wolf });

            int mawSlot = -1;
            for (int i = 0; i < body.SlotCount; i++) if (body.GetSlot(i).slot == BodySlots.Maw) mawSlot = i;
            int wi = -1;
            var vars = body.GetVariants(mawSlot);
            for (int i = 0; i < vars.Count; i++) if (vars[i].organName == "Клык") wi = i;

            Assert.IsTrue(body.Install(mawSlot, wi));
            Assert.IsTrue(body.GetSlot(mawSlot).installed, "после Install слот должен быть installed (звериный)");
            Assert.AreEqual("Клык", body.GetSlot(mawSlot).organName);

            // снять звериный — поставить родной обратно (у не-химерного Remove не снимает, а Install родного)
            int native = -1;
            for (int i = 0; i < vars.Count; i++) if (vars[i].native) native = i;
            Assert.IsTrue(body.Install(mawSlot, native));
            Assert.IsFalse(body.GetSlot(mawSlot).installed);
        }

        [Test]
        public void ChimeraSlot_StartsMinusOne_GrantAndRemove()
        {
            var spine = new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true };
            var humanMaw = new Organ { organName = "Пасть", slot = BodySlots.Maw, cost = 2 };
            var human = MakeSpecies("Человек", 20, new[] { spine, humanMaw });
            var wolf = MakeSpecies("Волк", 20, new[] { new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true }, new Organ { organName = "Клык", slot = BodySlots.Maw, cost = 2 } });
            var body = MakeBody(human, new[] { wolf });

            Assert.AreEqual(0, body.ChimeraSlots, "из коробки химерных слотов 0");
            int before = body.SlotCount;

            body.GrantChimeraSlot();
            Assert.AreEqual(1, body.ChimeraSlots);
            Assert.AreEqual(before + 1, body.SlotCount, "Grant добавляет слот");
            int chim = -1;
            for (int i = 0; i < body.SlotCount; i++) if (body.GetSlot(i).chimera) chim = i;
            Assert.AreNotEqual(-1, chim);
            var sv = body.GetSlot(chim);
            Assert.AreEqual("—", sv.organName, "химерный стартует пустым (-1) с прочерком");
            Assert.AreEqual(0, sv.cost, "пустой химерный = 0 в пуле (Slots.cs:47)");
            Assert.IsFalse(sv.installed);
            Assert.IsFalse(body.Remove(chim), "Remove пустого = false");

            // надеть что-то
            var vars = body.GetVariants(chim);
            int any = -1;
            for (int i = 0; i < vars.Count; i++) if (!vars[i].native && vars[i].affordable) { any = i; break; }
            if (any == -1) for (int i = 0; i < vars.Count; i++) if (vars[i].affordable) { any = i; break; }
            Assert.AreNotEqual(-1, any, "должен быть доступный вариант в химерном");
            Assert.IsTrue(body.Install(chim, any));
            Assert.AreNotEqual("—", body.GetSlot(chim).organName);
            Assert.IsTrue(body.Remove(chim), "Remove заполненного химерного опустошает");
            Assert.AreEqual("—", body.GetSlot(chim).organName, "после Remove снова прочерк");
            Assert.LessOrEqual(body.PoolUsed, body.Pool);

            body.RemoveChimeraSlot();
            Assert.AreEqual(0, body.ChimeraSlots);
            Assert.AreEqual(before, body.SlotCount, "RemoveChimeraSlot убрал последний химерный");
        }

        [Test]
        public void EffectiveCost_ZeroAffinity_FullPrice()
        {
            var spine = new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true };
            var maw = new Organ { organName = "Пасть", slot = BodySlots.Maw, cost = 10 };
            var wolfMaw = new Organ { organName = "Клык", slot = BodySlots.Maw, cost = 10 };
            var human = MakeSpecies("Человек", 20, new[] { spine, maw });
            var wolf = MakeSpecies("Волк", 20, new[] { new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true }, wolfMaw });
            var body = MakeBody(human, new[] { wolf });
            body.SetAffinity("Волк", 0);
            // эффективная цена при 0 родства = cost*1
            var vars = body.GetVariants(1); // Пасть
            int wi = -1; for (int i = 0; i < vars.Count; i++) if (vars[i].organName == "Клык") wi = i;
            Assert.AreEqual(10, vars[wi].cost, "при 0 родства цена без скидки");
        }
    }
}
