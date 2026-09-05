using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// CreatureBody.Expression: дубль слота Sup=max (не сумма), родной×m, химерный от 0, ApplyVitals.
    /// Проверяем что 2 сердца не удваивают HP (CreatureBody.Expression.cs:68, CreatureBody.cs:390).
    /// </summary>
    public class ExpressionTests
    {
        readonly List<Object> toDestroy = new List<Object>();
        readonly List<GameObject> goToDestroy = new List<GameObject>();

        SpeciesSO MakeSpecies(string name, int baseHp, Color tint, Organ[] organs, BodySocket[] sockets = null)
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            toDestroy.Add(so);
            so.speciesName = name;
            so.tint = tint;
            so.mutagenPool = 20;
            so.baseHp = baseHp;
            so.sockets = sockets ?? new BodySocket[0];
            so.organs = organs;
            so.bones = new Bone[0];
            return so;
        }

        CreatureBody MakeBody(SpeciesSO chassis, SpeciesSO[] donors, int baseHpOverride = 0)
        {
            var go = new GameObject("ExprBody_" + chassis.speciesName);
            goToDestroy.Add(go);
            var h = go.AddComponent<Health>();
            // health Awake sets Current = maxHealth (30). Recompute will override.
            var body = go.AddComponent<CreatureBody>();
            body.Configure(chassis, donors);
            return body;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in goToDestroy) Object.DestroyImmediate(go);
            goToDestroy.Clear();
            foreach (var o in toDestroy) Object.DestroyImmediate(o);
            toDestroy.Clear();
        }

        // ---------------------------------------------------------------------
        // Sup = max, не сумма
        // ---------------------------------------------------------------------

        [Test]
        public void Contribution_Sup_IsMax_NotSum_ForScalars()
        {
            // Достаём внутренний тип Contribution и метод Sup через рефлексию
            var bodyType = typeof(CreatureBody);
            var contribType = bodyType.GetNestedType("Contribution", BindingFlags.NonPublic);
            Assert.IsNotNull(contribType, "CreatureBody.Contribution должен существовать (Expression.cs:12)");
            var sup = contribType.GetMethod("Sup", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(sup, "Contribution.Sup должен существовать (Expression.cs:26)");

            object a = System.Activator.CreateInstance(contribType);
            object b = System.Activator.CreateInstance(contribType);
            // ставим hpBonus 0.3 и 0.6
            contribType.GetField("hpBonus").SetValue(a, 0.3f);
            contribType.GetField("hpBonus").SetValue(b, 0.6f);
            contribType.GetField("dmg").SetValue(a, 10f);
            contribType.GetField("dmg").SetValue(b, 5f);
            contribType.GetField("atkCd").SetValue(a, 0.5f);
            contribType.GetField("atkCd").SetValue(b, 0.3f);

            var res = sup.Invoke(null, new[] { a, b });
            float hp = (float)contribType.GetField("hpBonus").GetValue(res);
            float dmg = (float)contribType.GetField("dmg").GetValue(res);
            float cd = (float)contribType.GetField("atkCd").GetValue(res);

            Assert.AreEqual(0.6f, hp, 1e-5f, "Sup hpBonus должен быть max, не сумма");
            Assert.AreEqual(10f, dmg, 1e-5f, "Sup dmg = max");
            Assert.AreEqual(0.3f, cd, 1e-5f, "Sup atkCd = min (быстрее = лучше)");
            // сумма дала бы 0.9 / 15 / не min — ловим регрессию
            Assert.AreNotEqual(0.9f, hp, "Дубль не должен суммировать");
        }

        [Test]
        public void TwoHearts_DoNotDoubleHp_Integration()
        {
            // Один слот Сердце, но два органа одного слота через химерный слот => Sup
            var humanHeart = new Organ { organName = "ЧелСердце", slot = BodySlots.Heart, cost = 2, hpBonus = 0.4f };
            var wolfHeart = new Organ { organName = "ВолкСердце", slot = BodySlots.Heart, cost = 3, hpBonus = 0.6f };
            var chassisOnly = new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true };
            var human = MakeSpecies("Человек", 100, Color.white,
                new[] { chassisOnly, humanHeart }, new BodySocket[0]);
            var wolf = MakeSpecies("Волк", 100, Color.gray,
                new[] { new Organ { organName = "ВолкСердце", slot = BodySlots.Heart, cost = 3, hpBonus = 0.6f }, new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true } },
                new BodySocket[0]);

            var body = MakeBody(human, new[] { wolf });
            // даём химерный слот и кладём второе сердце туда
            body.GrantChimeraSlot();
            // найти химерный слот
            int chimeraIdx = -1;
            for (int i = 0; i < body.SlotCount; i++) if (body.GetSlot(i).chimera) chimeraIdx = i;
            Assert.AreNotEqual(-1, chimeraIdx, "химерный слот должен появиться после Grant");

            // в химерном слоте найти вариант ВолкСердце
            var vars = body.GetVariants(chimeraIdx);
            int vWolf = -1;
            for (int i = 0; i < vars.Count; i++) if (vars[i].organName == "ВолкСердце" && vars[i].species == "Волк") vWolf = i;
            Assert.AreNotEqual(-1, vWolf, "химерный слот должен содержать волчье сердце");
            Assert.IsTrue(body.Install(chimeraIdx, vWolf), "установка второго сердца в химерный слот");

            var health = body.GetComponent<Health>();
            // Sup: max 0.6 => hp = 100 * (1+0.6)=160. Сумма дала бы 100*(1+1.0)=200
            Assert.AreEqual(160, health.Max, "Два сердца должны дать max(0.4,0.6)=0.6, а не сумму 1.0");
        }

        // ---------------------------------------------------------------------
        // Родной × m
        // ---------------------------------------------------------------------

        [Test]
        public void NativeOrgan_ScalesWithBonusMultiplier()
        {
            var heart = new Organ { organName = "ЧелСердце", slot = BodySlots.Heart, cost = 2, hpBonus = 0.5f };
            var spine = new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true };
            var human = MakeSpecies("Человек", 100, Color.white, new[] { spine, heart }, new BodySocket[0]);
            var body = MakeBody(human, new SpeciesSO[0]);

            // Affinity 0 => m=1 => hpBonus 0.5 => Max 150
            // кости не влияют, но проверяем через Health.Max
            var h = body.GetComponent<Health>();
            Assert.AreEqual(150, h.Max, "родной при 0 родства: 100*(1+0.5)=150");

            body.SetAffinity("Человек", 100);
            // BonusMult для игрока не выпукло? body.Power у не-игрока = expression (0) => BonusMultiplier = 1..2
            // Но chassis=Человек, donors пуст => BonusMult возвращает 1 (нет донора). Нужно дать донора чтобы BonusMultiplier читал.
            // Поэтому создаём донора-волка чтобы BonusMult брал его кривую? Actually BonusMultiplier берет species флага.
            // Для родного органа species = шасси => читает affinity к шасси.
            // Donors пуст => BonusMult =1 всегда (Affinity.cs:54). Значит родной не растёт без донора в списке.
            // Проверим напрямую через рефлексию BonusMultiplier
            var bm = typeof(CreatureBody).GetMethod("BonusMultiplier", BindingFlags.NonPublic | BindingFlags.Instance);
            // создаём тело с донором чтобы аффинити к шасси читалось через второй путь? Но Express берёт BonusMultiplier(pick.species) где pick.species=шасси
            // Давайте прямо проверим Express через два организма: с 0 и 100 родства при наличии donors[0]=human-дубль
            // Проще: создать donors = [humanClone] чтобы BonusMult не был 1
            var humanClone = MakeSpecies("Человек", 100, Color.white,
                new[] { new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true } }, new BodySocket[0]);
            // пересоздаём тело с донором
            var go2 = new GameObject("NativeMult");
            goToDestroy.Add(go2);
            go2.AddComponent<Health>();
            var body2 = go2.AddComponent<CreatureBody>();
            // human как шасси, humanClone как донор (тот же вид, но донор даст ветку 1..2)
            // Но donors[0].speciesName = "Человек" — BonusMult возьмёт его же аффинити к человеку
            // Однако code: BonusMult => donors[0].speciesName => "Человек" — совпадает с шасси
            body2.Configure(human, new[] { humanClone });
            body2.SetAffinity("Человек", 0);
            // форсим Recompute (Configure уже сделал)
            var health2 = go2.GetComponent<Health>();
            int max0 = health2.Max; // ~150

            body2.SetAffinity("Человек", 100);
            // Update -> AffinitySum изменится => Recompute в следующем Update, форсируем через Refeed?
            // Body не имеет публичного Recompute, но есть Refeed
            body2.Refeed();
            int max100 = health2.Max;

            // при 100 родства m=2 => hpBonus 1.0 => 200
            Assert.Greater(max100, max0, "родной бонус должен расти с родством");
            Assert.AreEqual(200, max100, "родной ×2: 100*(1+0.5*2)=200");
        }

        // ---------------------------------------------------------------------
        // Химерный от 0
        // ---------------------------------------------------------------------

        [Test]
        public void ChimeraSlot_BlendsFromZero()
        {
            // Химерный орган вытеснять нечего => база 0 (Expression.cs:73 EmptyOrgan)
            // Blend(0, wv, m) => при m=1 => wv, при m=2 => wv*2 (овершен)
            // Проверим через тело: химерный волчье сердце в химерном слоте при 0 и 100 родства
            var wolfHeart = new Organ { organName = "ВолкСердце", slot = BodySlots.Heart, cost = 3, hpBonus = 0.4f };
            var spineHuman = new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true };
            var human = MakeSpecies("Человек", 100, Color.white,
                new[] { spineHuman, new Organ { organName = "ЧелСердце", slot = BodySlots.Heart, cost = 2, hpBonus = 0f } },
                new BodySocket[0]);
            var wolf = MakeSpecies("Волк", 100, Color.gray,
                new[] { new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true }, wolfHeart },
                new BodySocket[0]);

            var go = new GameObject("ChimeraZero");
            goToDestroy.Add(go);
            go.AddComponent<Health>();
            var body = go.AddComponent<CreatureBody>();
            body.Configure(human, new[] { wolf });

            body.GrantChimeraSlot();
            int chim = -1;
            for (int i = 0; i < body.SlotCount; i++) if (body.GetSlot(i).chimera) chim = i;
            int v = -1;
            var vars = body.GetVariants(chim);
            for (int i = 0; i < vars.Count; i++) if (vars[i].organName == "ВолкСердце") v = i;
            Assert.AreNotEqual(-1, v);

            // снять родное сердце чтобы изолировать вклад химерного? Оставим родное 0, вклад только химерного
            body.SetAffinity("Волк", 0);
            body.Install(chim, v);
            body.Refeed();
            int maxAt0 = go.GetComponent<Health>().Max; // base 100*(1+0.4)=140

            body.SetAffinity("Волк", 100);
            body.Refeed();
            int maxAt100 = go.GetComponent<Health>().Max; // 100*(1+0.8)=180 (0.4*2)

            Assert.AreEqual(140, maxAt0, "химерный от 0 при m=1: 100*(1+0.4)=140");
            Assert.AreEqual(180, maxAt100, "химерный от 0 при m=2: 100*(1+0.8)=180");
        }

        // ---------------------------------------------------------------------
        // ApplyVitals
        // ---------------------------------------------------------------------

        [Test]
        public void ApplyVitals_False_DoesNotTouchHealth()
        {
            var heart = new Organ { organName = "Сердце+", slot = BodySlots.Heart, cost = 2, hpBonus = 1f, damageReduction = 0.3f, regen = 5f };
            var spine = new Organ { organName = "Хребет", slot = BodySlots.Spine, chassisOnly = true };
            var human = MakeSpecies("Человек", 80, Color.white, new[] { spine, heart }, new BodySocket[0]);
            var go = new GameObject("VitalsOff");
            goToDestroy.Add(go);
            var health = go.AddComponent<Health>();
            health.SetMaxHealth(10);
            var body = go.AddComponent<CreatureBody>();
            // applyVitals — сериализованное поле, выставляем через рефлексию до Configure
            typeof(CreatureBody).GetField("applyVitals", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(body, false);
            body.Configure(human, new SpeciesSO[0]);

            // при applyVitals=false тело не должно менять Max (останется 10) и regen
            Assert.AreEqual(10, health.Max, "applyVitals=false: Health.Max не должен меняться");
            Assert.AreEqual(0f, health.RegenPerSecond, 1e-5f, "regen тоже не трогаем");
            Assert.AreEqual(0f, health.DamageReduction, 1e-5f);

            // включили — перекомпут должен применить
            typeof(CreatureBody).GetField("applyVitals", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(body, true);
            body.Refeed();
            Assert.Greater(health.Max, 10, "после включения vitals Max должен вырасти");
        }
    }
}
