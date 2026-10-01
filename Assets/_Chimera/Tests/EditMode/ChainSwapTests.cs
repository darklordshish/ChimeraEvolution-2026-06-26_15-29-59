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

        static SpeciesSO With(SpeciesSO chassis, SpeciesSO donor, params string[] slots)
        {
            var worn = chassis.organs.Where(o => !slots.Contains(o.slot)).ToList();
            worn.InsertRange(0, donor.organs.Where(o => slots.Contains(o.slot)));
            return ChainSwap.Compose(chassis, worn);
        }

        static Vector3 Start(SpeciesSO sp, string name)
        {
            var by = sp.bones.ToDictionary(b => b.name);
            return SkeletonBuilder.Place(by[name], by, new Dictionary<string, (Vector3, Quaternion)>()).pos;
        }

        [Test]
        public void WolfHeart_ReplacesHumanChest_NeckAndShouldersStay()
        {
            var h = Load("Человек"); var w = Load("Волк");
            var c = With(h, w, BodySlots.Heart);
            var names = c.bones.Select(b => b.name).ToList();
            CollectionAssert.DoesNotContain(names, "грудь2", "человеческая грудь не вынута");
            Assert.IsTrue(c.bones.Any(b => b.socket == BodySlots.Heart), "волчья грудь не встала");
            // СИРОТЫ: шея и лопатки висели на груди человека — переехали на хребет, но в мире не сдвинулись
            foreach (var n in new[] { "шея", "лопатка" })
                Assert.Less(Vector3.Distance(Start(h, n), Start(c, n)), 1e-3f, $"«{n}» сдвинулась при смене груди");
        }

        [Test]
        public void HumanMaw_ReplacesWolfHead_SensesRideWithIt()
        {
            var w = Load("Волк"); var h = Load("Человек");
            var c = With(w, h, BodySlots.Maw);
            var head = c.bones.Where(b => b.limb == "голова").Select(b => b.name).ToList();
            CollectionAssert.Contains(head, "челюсть", "человеческая голова не встала");
            Assert.AreEqual(w.bones.First(b => b.name == "голова").parent, c.bones.First(b => b.name == "голова").parent,
                            "голова ушла с конца шеи");
            // гнёзда, которые у донора сидят на его голове, приезжают с ней
            foreach (var n in h.nests.Where(n => h.bones.Any(b => b.name == n.host && b.limb == "голова")))
                CollectionAssert.Contains(head, c.nests.First(x => x.name == n.name).host, $"гнездо «{n.name}» осталось на вынутой голове");
        }

        [Test]
        public void WolfMaw_OnHuman_LooksForward_AtHeadWidthCalibre()
        {
            // ГОЛОВА — ВЗГЛЯД (кадр 01.10): выравнивание по оси человеческой головы (она идёт к макушке) задирало волчью
            // морду в небо. Морда смотрит вперёд у любого носителя, калибр — ширина головы
            var h = Load("Человек"); var w = Load("Волк");
            var c = With(h, w, BodySlots.Maw);
            var axis = Tip(c, "голова") - Start(c, "голова");
            Assert.Greater(axis.z, Mathf.Abs(axis.y), "волчья голова на человеке смотрит не вперёд");
            float s = h.nests.First(n => n.name == "голова").unit / w.nests.First(n => n.name == "голова").unit;
            Assert.AreEqual(w.bones.First(b => b.name == "голова").length * s, c.bones.First(b => b.name == "голова").length, 1e-4f,
                            "голова мерена не шириной");
        }

        [Test]
        public void SnakeMaw_OnWolf_AndWolfMaw_OnSnake_KeepOneRoot()
        {
            var w = Load("Волк"); var z = Load("Змея");
            foreach (var c in new[] { With(w, z, BodySlots.Maw), With(z, w, BodySlots.Maw) })
                Assert.AreEqual(1, c.bones.Count(b => string.IsNullOrEmpty(b.parent)), $"{c.name}: корней не один");
        }

        static IEnumerable<string[]> Permutations(string[] xs)
        {
            if (xs.Length <= 1) { yield return xs; yield break; }
            for (int i = 0; i < xs.Length; i++)
                foreach (var rest in Permutations(xs.Where((_, j) => j != i).ToArray()))
                    yield return new[] { xs[i] }.Concat(rest).ToArray();
        }

        [Test]
        public void SlotOrder_DoesNotChangeBody()
        {
            // СТОРОЖ ПОРЯДКА (спека конструктора §5, находка математика консилиума): подстановки слотов коммутируют —
            // все перестановки дают одно тело. Сравнение — множеством костей и гнёзд: порядок списка не важен, важна форма
            var slots = new[] { BodySlots.Arms, BodySlots.Legs, BodySlots.Maw, BodySlots.Heart };
            var names = new[] { "Волк", "Лось", "Ёж", "Змея", "Человек" };
            foreach (var cn in names)
                foreach (var dn in names)
                {
                    if (cn == dn) continue;
                    var c = Load(cn); var d = Load(dn);
                    var worn = c.organs.Where(o => !slots.Contains(o.slot)).ToList();
                    worn.InsertRange(0, d.organs.Where(o => slots.Contains(o.slot)));
                    string Print(string[] order)
                    {
                        var (bones, nests) = ChainSwap.AssembleInOrder(c, worn, order);
                        var lines = bones.Select(b => JsonUtility.ToJson(b))
                            .Concat(nests.Select(n => $"{n.name}@{n.host} {n.localPos} {n.localRot} {n.unit}")).ToList();
                        lines.Sort(System.StringComparer.Ordinal);
                        return string.Join("|", lines);
                    }
                    string first = null;
                    foreach (var order in Permutations(slots))
                    {
                        var p = Print(order);
                        first ??= p;
                        Assert.AreEqual(first, p, $"{cn}+{dn}: порядок {string.Join(",", order)} дал другое тело");
                    }
                }
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
