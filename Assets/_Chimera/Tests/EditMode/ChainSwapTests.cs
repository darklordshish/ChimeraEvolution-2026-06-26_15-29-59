using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>АУГМЕНТ ПОДСТАВЛЯЕТ СВОЮ ЦЕПЬ (спека двух слоёв §3, решение 13). Сторожит: цепь носителя вынута целиком,
    /// донорская встала в его сустав; ось цепи — носителя (стойка за шасси), длина цепи — носителя, толщина — не больше
    /// стыка носителя и своей формы (не раздувается); гнездо конца конечности — донорское; ассет шасси не тронут; ключ оболочки — по составу.</summary>
    public class ChainSwapTests
    {
        static SpeciesSO Load(string name) => AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{name}.asset");

        [SetUp] public void SetUp() => ChainSwap.ResetCache();
        [TearDown] public void TearDown() => ChainSwap.ResetCache();

        static List<Organ> HumanWith(SpeciesSO donor, params string[] slots)
        {
            var worn = Load("Человек").organs.Where(o => !slots.Contains(o.slot)).ToList();
            worn.InsertRange(0, donor.organs.Where(o => slots.Contains(o.slot)));
            return worn;
        }

        static Vector3 Tip(SpeciesSO sp, string name)
        {
            var by = sp.bones.ToDictionary(b => b.name);
            var (p, r) = SkeletonBuilder.Place(by[name], by, new Dictionary<string, (Vector3, Quaternion)>());
            return SkeletonBuilder.Tip(by[name], p, r);
        }

        [Test]
        public void NativeComposition_ReturnsChassisItself()
        {
            var h = Load("Человек");
            Assert.AreSame(h, ChainSwap.Compose(h, h.organs), "без прививок подстановки нет — строится сам ассет");
        }

        [Test]
        public void WolfLegs_ReplaceHumanLegChain_AtHumanStance()
        {
            var h = Load("Человек"); var w = Load("Волк");
            string before = JsonUtility.ToJson(h);
            var c = ChainSwap.Compose(h, HumanWith(w, BodySlots.Legs));

            Assert.AreNotSame(h, c);
            Assert.AreEqual(before, JsonUtility.ToJson(h), "ассет шасси тронут подстановкой");
            Assert.IsNotEmpty(c.meshKey, "у составного тела нет ключа оболочки — химера получит оболочку чистого вида");

            var leg = c.bones.Where(b => b.limb == "зад").Select(b => b.name).ToList();
            CollectionAssert.DoesNotContain(leg, "голень6", "сечения человеческой голени остались — цепь вынута не целиком");
            CollectionAssert.Contains(leg, "ляжка", "волчий бугор не приехал с цепью");
            var femur = c.bones.First(b => b.name == "бедро");
            Assert.AreEqual(h.bones.First(b => b.name == "бедро").parent, femur.parent, "корень цепи не встал в сустав носителя");
            var wf = w.bones.First(b => b.name == "бедро");
            var hf = h.bones.First(b => b.name == "бедро");
            Assert.LessOrEqual(femur.r0, hf.r0 + 1e-4f, "цепь толще стыка носителя — раздуется");
            Assert.LessOrEqual(femur.r0 / femur.length, wf.r0 / wf.length + 1e-4f, "цепь толще своей формы — раздуется");

            // ОСЬ цепи — носителя (стойка за шасси). Конец выше щиколотки: длина по сегментам, а хорда зигзага короче —
            // скакательный встаёт над землёй, ниже него шток конца конечности (пальцеходящая нога, письмо Модельного 29b §1.4)
            Vector3 Axis(SpeciesSO sp)
            {
                var by = sp.bones.ToDictionary(b => b.name);
                var (p0, _) = SkeletonBuilder.Place(by["бедро"], by, new Dictionary<string, (Vector3, Quaternion)>());
                return Tip(sp, "голень") - p0;
            }
            Assert.Less(Vector3.Angle(Axis(h), Axis(c)), 2f, "ось ноги ушла из стойки носителя");
            Assert.Greater(Tip(c, "голень").y, Tip(h, "голень").y, "скакательный не над щиколоткой — зигзаг не донорский");
            // зигзаг — донора: колено выходит вперёд оси «таз → скакательный»
            Assert.Greater(Tip(c, "бедро").z, Tip(h, "бедро").z + 0.1f, "колено не вышло вперёд — сгиб не волчий");

            var nest = c.nests.FirstOrDefault(n => n.name == BodySlots.Legs);
            if (nest != null) CollectionAssert.Contains(leg, nest.host, "гнездо конца ноги осталось на вынутой кости");
        }

        [Test]
        public void SameComposition_SharesOneBody()
        {
            var h = Load("Человек"); var w = Load("Волк");
            Assert.AreSame(ChainSwap.Compose(h, HumanWith(w, BodySlots.Arms)), ChainSwap.Compose(h, HumanWith(w, BodySlots.Arms)),
                           "одинаковый состав — одно тело и одна оболочка");
        }
    }
}
