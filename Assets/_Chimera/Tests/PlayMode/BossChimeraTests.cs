using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>БОСС-ХИМЕРА (#4b-4): босс собирается ТЕМ ЖЕ конструктором, что все, а боссовость — отдельный модуль.
    /// Сторожим четыре обещания: рецепт идёт через `Install` (оборотень = человек + весь набор одного вида), расширение — химерными
    /// слотами, множитель HP модуля ПЕРЕЖИВАЕТ пересчёт тела (иначе первая же прививка стирала бы его молча), и босс
    /// ШТУЧНЫЙ — разброс особи, который тело вешает каждому NPC, снят (решение Ф6).</summary>
    public class BossChimeraTests
    {
        readonly List<Object> trash = new();

        SpeciesSO MakeSpecies(string name, params string[] slots)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = name;
            so.tint = Color.gray;
            so.mutagenPool = 100;
            so.baseHp = 60;
            so.baseStamina = 100;
            so.baseStaminaRegen = 10f;
            so.sockets = new BodySocket[0];
            so.bones = new Bone[0];
            var organs = new List<Organ>();
            foreach (var s in slots) organs.Add(new Organ { organName = name + "-" + s, slot = s, cost = 2 });
            so.organs = organs.ToArray();
            trash.Add(so);
            return so;
        }

        static Bossness.Settings Plain(int slots = 0, float hp = 1f) => new Bossness.Settings
        {
            chimeraSlots = slots, expression = 0f, hpMult = hp, damageMult = 1f, sizeScale = 1f,
            massive = false, eternalRage = false, poolReward = 0, grantsChimeraSlot = false,
        };

        CreatureBody SpawnBoss(SpeciesSO human, SpeciesSO[] donors, Bossness.Settings s, Vector3 pos, string name)
        {
            Bossness module = null;
            var body = ChimeraFactory.Spawn(human, donors, pos, name,
                beforeBody: go => module = Bossness.AttachBefore(go, s),
                compose: b => { ChimeraFactory.InstallAllFrom(b, "Волк"); module.Extend(b); });
            module.Finish(body, name);
            trash.Add(body.gameObject);
            return body;
        }

        [TearDown]
        public void Clean()
        {
            foreach (var o in trash) if (o != null) Object.Destroy(o);
            trash.Clear();
        }

        [UnityTest]
        public IEnumerator Werebeast_Recipe_GoesThroughConstructor_AllOrgansOfOneDonor()
        {
            // оборотень = человек + весь набор ОДНОГО донора; здесь донор — волк (тот исход, что раньше звался вервольфом)
            var human = MakeSpecies("Человек", "Пасть", "Руки", "Сердце");
            var wolf = MakeSpecies("Волк", "Пасть", "Руки", "Сердце");
            var snake = MakeSpecies("Змея", "Пасть");
            var body = ChimeraFactory.Spawn(human, new[] { wolf, snake }, new Vector3(0f, 0.5f, 0f), "Оборотень",
                compose: b => ChimeraFactory.InstallAllFrom(b, "Волк"));
            trash.Add(body.gameObject);
            yield return null;

            int wolfSlots = 0;
            for (int s = 0; s < body.SlotCount; s++)
            {
                var v = body.GetSlot(s);
                if (v.chimera) continue;
                Assert.IsTrue(v.installed, $"слот «{v.slot}» должен нести чужой орган — рецепт ставит волчий всюду, где он есть");
                Assert.AreEqual("Волк", v.species, $"в слоте «{v.slot}» орган не волчий: {v.organName} ({v.species})");
                wolfSlots++;
            }
            Assert.AreEqual(3, wolfSlots, "Пасть, Руки, Сердце — все три волчьи");
            Assert.AreEqual("Человек", body.Chassis.speciesName, "шасси босса — человек (сценарный закон)");
        }

        [UnityTest]
        public IEnumerator Bossness_Extends_WithChimeraSlots_ThroughConstructor()
        {
            var human = MakeSpecies("Человек", "Пасть", "Руки", "Сердце");
            var wolf = MakeSpecies("Волк", "Пасть", "Руки", "Сердце");
            var snake = MakeSpecies("Змея", "Пасть", "Хвост");
            var s = Plain(slots: 2);
            s.massive = true;
            s.grantsChimeraSlot = true;
            var body = SpawnBoss(human, new[] { wolf, snake }, s, new Vector3(5f, 0.5f, 0f), "Босс");
            yield return null;

            Assert.AreEqual(2, body.ChimeraSlots, "модуль расширяет тело химерными слотами через GrantChimeraSlot");
            int filled = 0;
            for (int i = 0; i < body.SlotCount; i++)
            {
                var v = body.GetSlot(i);
                if (!v.chimera || !v.installed) continue;
                filled++;
                Assert.AreEqual("Змея", v.species,
                    "волчьи органы уже носятся — второй экземпляр конструктор не пускает, химерный слот берёт чужое");
            }
            Assert.AreEqual(2, filled, "оба химерных слота заполнены конструктором");
            Assert.IsNotNull(body.GetComponent<Massive>(), "масса — черта модуля");
            Assert.IsNotNull(body.GetComponent<SuperBossReward>(), "награда за первое убийство навешана");
        }

        [UnityTest]
        public IEnumerator Bossness_HpMult_SurvivesRecompute()
        {
            var human = MakeSpecies("Человек", "Пасть", "Сердце");
            var wolf = MakeSpecies("Волк", "Пасть", "Сердце");
            var plain = SpawnBoss(human, new[] { wolf }, Plain(hp: 1f), new Vector3(0f, 0.5f, 10f), "Обычный");
            var boss = SpawnBoss(human, new[] { wolf }, Plain(hp: 3f), new Vector3(10f, 0.5f, 10f), "Босс");
            yield return null;

            // сторож не пустой: разброс особи тело вешает каждому NPC — модуль обязан его снять
            Assert.IsTrue(boss.TryGetComponent<SpawnVariance>(out var v), "тело не повесило разброс особи — сторож штучности пуст");
            Assert.AreEqual(1f, v.HpMult, 1e-4f, "босс штучный (Ф6): разброс HP снят");
            Assert.AreEqual(1f, v.DamageMult, 1e-4f, "босс штучный (Ф6): разброс урона снят");
            Assert.AreEqual(1f, v.SpeedMult, 1e-4f, "босс штучный (Ф6): разброс скорости снят");

            int baseMax = plain.GetComponent<Health>().Max;
            Assert.Greater(baseMax, 1, "у химеры без модуля должно быть осмысленное HP");
            Assert.AreEqual(baseMax * 3f, boss.GetComponent<Health>().Max, 1.5f,
                "HP босса = HP той же химеры без модуля × множитель");

            boss.GrantChimeraSlot(); // любой вызов конструктора пересчитывает тело
            yield return null;
            Assert.AreEqual(baseMax * 3f, boss.GetComponent<Health>().Max, 1.5f,
                "множитель модуля переживает Recompute — не стирается прививкой или выданным слотом");
        }
    }
}
