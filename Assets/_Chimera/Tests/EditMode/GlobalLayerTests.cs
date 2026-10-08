using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>ГЛОБАЛЬНЫЙ СЛОЙ, шаг 3 (спека `2026-10-07-globalnyj-sloj-shablon-plana.md` §4): тождество при g = 0, числа
    /// цели при g = 1, логарифмическое смешение радиусов, положение в долях сегмента, угасание и рост групп без пары,
    /// ступень по порогам признания, подключение в сборку. Пока виды не переведены на шаблон — синтетические группы.</summary>
    public class GlobalLayerTests
    {
        readonly List<Object> trash = new();
        [SetUp] public void SetUp() => ChainSwap.ResetCache();
        float kHead;
        [SetUp] public void KeepKHead() => kHead = GlobalLayer.KHead;
        [TearDown] public void TearDown() { foreach (var o in trash) if (o != null) Object.DestroyImmediate(o); trash.Clear(); ChainSwap.ResetCache(); GlobalLayer.KHead = kHead; }

        static SpeciesSO Load(string n) => AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{n}.asset");

        SpeciesSO WithGroup(string species, string group, float at, float r)
        {
            var sp = Object.Instantiate(Load(species)); sp.speciesName = species + "·проба"; trash.Add(sp);
            foreach (var b in sp.bones) b.group = null;   // синтетика: настоящие группы шаблона не мешают пробе
            var arm = sp.bones.First(b => b.limb == "перед" && b.mark?.b == "локоть");
            var n = new Bone { name = "м." + group, parent = arm.name, limb = arm.limb, group = group, rel = new BoneRel { at = at, len = 0.3f, r0 = r, r1 = r * 0.8f } };
            sp.bones = sp.bones.Append(n).ToArray();
            BodyChains.ResolveRel(sp.bones);
            return sp;
        }

        static Bone Group(Bone[] bones, string g) => bones.First(b => b.group == g);

        [Test]
        public void ZeroStep_IsIdentity()
        {
            var a = WithGroup("Волк", "бицепс", 0.3f, 0.5f);
            var b = WithGroup("Волк", "бицепс", 0.6f, 1.0f);
            var blended = GlobalLayer.Blend(a, b, 0f);
            Assert.AreEqual(JsonUtility.ToJson(Group(a.bones, "бицепс")), JsonUtility.ToJson(Group(blended, "бицепс")));
        }

        [Test]
        public void FullStep_TakesTargetNumbers_HalfStep_IsGeometricMean()
        {
            var a = WithGroup("Волк", "бицепс", 0.3f, 0.5f);
            var b = WithGroup("Волк", "бицепс", 0.6f, 2.0f);
            var full = Group(GlobalLayer.Blend(a, b, 1f), "бицепс").rel;
            Assert.AreEqual(0.6f, full.at, 1e-4f);
            Assert.AreEqual(2.0f, full.r0, 1e-4f);
            var half = Group(GlobalLayer.Blend(a, b, 0.5f), "бицепс");
            float ma = Group(a.bones, "бицепс").r0, mb = Group(b.bones, "бицепс").r0;
            Assert.AreEqual(Mathf.Sqrt(ma * mb), half.r0, 1e-5f, "радиус в метрах смешивается в логарифмах: √(a·b)");
            Assert.AreEqual(0.45f, half.rel.at, 1e-4f, "положение — линейно");
        }

        [Test]
        public void UnpairedGroups_FadeAndGrow()
        {
            var a = WithGroup("Волк", "бицепс", 0.3f, 0.5f);
            var b = WithGroup("Волк", "трицепс", 0.4f, 0.6f);
            var blended = GlobalLayer.Blend(a, b, 0.25f);
            Assert.AreEqual(0.5f * 0.75f, Group(blended, "бицепс").rel.r0, 1e-4f, "группа шасси без пары гаснет");
            Assert.AreEqual(0.6f * 0.25f, Group(blended, "трицепс").rel.r0, 1e-4f, "группа цели без пары растёт из нуля");
        }

        [Test]
        public void Step_FollowsRecognitionThresholds()
        {
            var human = Load("Человек"); var wolf = Load("Волк");
            Assert.AreEqual(0, GlobalLayer.Step(human, human.organs.ToList()).step, "чистый вид — ступени нет");
            var worn = human.organs.ToList();
            foreach (var w in wolf.organs.Where(o => o != null && !o.chassisOnly))
            {
                int i = worn.FindIndex(o => o != null && o.slot == w.slot);
                if (i >= 0) worn[i] = w; else worn.Add(w);
            }
            var id = GlobalLayer.Identity(human, worn);
            Assert.AreEqual(1f, id.Values.Sum(), 1e-4f, "доли в сумме — 1");
            var (target, step) = GlobalLayer.Step(human, worn);
            float share = id[wolf];
            int expect = share >= GlobalLayer.MediumAt ? 2 : share >= GlobalLayer.WeakAt ? 1 : 0;
            Assert.AreEqual(expect, step);
            if (step > 0) Assert.AreSame(wolf, target);
        }

        /// <summary>ДОЛИ ХОЗЯИНА НЕ СРАВНИМЫ МЕЖДУ ВИДАМИ (кадр ступеней 07.10): хребет человека тонкий, волка толстый, и та же
        /// грудная клетка в метрах записана как 1.99 и 0.91 радиуса хозяина. Смешение идёт в долях масштаба цепи: радиус
        /// группы на ступени — между своим и волчьим, приведённым к торсу человека, а не вдвое меньше обоих.</summary>
        [Test]
        public void CrossSpecies_MassStaysBetween_InChainScale()
        {
            var human = Load("Человек");
            var wolf = Object.Instantiate(Load("Волк")); wolf.planBodies = null; trash.Add(wolf);   // родные числа волка: сторож единиц, не поставки
            Assume.That(GlobalLayer.HasGroups(human) && GlobalLayer.HasGroups(wolf), "виды не на шаблоне");
            float torsoH = Torso(human), torsoW = Torso(wolf);
            var blended = GlobalLayer.Blend(human, wolf, 0.5f);
            foreach (var g in new[] { "грудная клетка", "живот", "таз" })
            {
                float h = Group(human.bones, g).r1, w = Group(wolf.bones, g).r1 * torsoH / torsoW, m = Group(blended, g).r1;
                Assert.That(m, Is.InRange(Mathf.Min(h, w) * 0.98f, Mathf.Max(h, w) * 1.02f), $"«{g}»: человек {h:0.000}, волк в торсе человека {w:0.000}, ступень {m:0.000}");
            }
        }

        /// <summary>ТЕЛО НА ЧУЖОМ ПЛАНЕ (спека 08.10 §4.2): числа шасси, выгруженные `BodyOn` и поданные как тело цели на его
        /// плане, при g = 1 возвращают шасси до микрона — формат поставки и смешение говорят на одном языке.</summary>
        [Test]
        public void ForeignBody_OfChassisNumbers_IsIdentity()
        {
            var human = Load("Человек");
            Assume.That(GlobalLayer.HasGroups(human), "человек не на шаблоне");
            var wolf = WithBody("Волк", GlobalLayer.BodyOn(human, human.Plan).Values.ToArray(), human.Plan);
            wolf.planBodies[0].head = GlobalLayer.HeadOn(human, human.Plan);
            GlobalLayer.KHead = 1f;
            var blended = GlobalLayer.Blend(human, wolf, 1f);
            var hBy = human.bones.ToDictionary(b => b.name); var mBy = blended.ToDictionary(b => b.name);
            foreach (var n in new[] { "шея", "голова" })
            {
                var (hp, hr) = PoseOf(hBy, hBy[n]); var (mp, mr) = PoseOf(mBy, mBy[n]);
                Assert.That((hp - mp).magnitude, Is.LessThan(1e-4f), n + ": начало");
                Assert.That(Quaternion.Angle(hr, mr), Is.LessThan(0.01f), n + ": поворот");
                Assert.AreEqual(hBy[n].length, mBy[n].length, 1e-5f, n + ": длина");
            }
            foreach (var h in human.bones.Where(b => !string.IsNullOrEmpty(b.group)))
            {
                var m = Group(blended, h.group);
                Assert.AreEqual(h.r0, m.r0, 1e-5f, h.group + ": r0");
                Assert.AreEqual(h.r1, m.r1, 1e-5f, h.group + ": r1");
                Assert.AreEqual(h.length, m.length, 1e-5f, h.group + ": длина");
                Assert.That((h.origin - m.origin).magnitude, Is.LessThan(1e-5f), h.group + ": начало");
            }
        }

        /// <summary>Цель пары разных планов — ТЕЛО ВИДА НА ПЛАНЕ ШАССИ, а не его родные числа (спека 08.10 §2 п.1): поставка
        /// «грудная клетка вдвое толще» даёт при g = 1 грудную клетку вдвое толще человеческой.</summary>
        [Test]
        public void ForeignBody_IsTheTarget()
        {
            var human = Load("Человек");
            Assume.That(GlobalLayer.HasGroups(human), "человек не на шаблоне");
            var nums = GlobalLayer.BodyOn(human, human.Plan).Values.Select(n => JsonUtility.FromJson<GroupNumbers>(JsonUtility.ToJson(n))).ToArray();
            nums.First(n => n.name == "грудная клетка").r1 *= 2f;
            var wolf = WithBody("Волк", nums, human.Plan);
            GlobalLayer.MissingBodies.Clear();
            var blended = GlobalLayer.Blend(human, wolf, 1f);
            Assert.AreEqual(2f * Group(human.bones, "грудная клетка").r1, Group(blended, "грудная клетка").r1, 1e-5f);
            Assert.IsEmpty(GlobalLayer.MissingBodies, "тело есть — долга быть не должно");
        }

        /// <summary>ДОЛГ ТЕЛ НА ЧУЖИХ ПЛАНАХ (спека 08.10 §4.5): пары «вид на плане шасси», которые нужны сейчас, — четыре вида
        /// на двуногом. Пришла поставка — строку убрать; список сверяется в обе стороны, как долг матрицы.</summary>
        [Test]
        public void ForeignBodies_Debt()
        {
            var expected = new[] { "Волк", "Лось", "Ёж", "Змея" };   // ждут поставки модельной линии (срезы 1 и 3)
            var missing = new[] { "Волк", "Лось", "Ёж", "Змея" }.Where(n => GlobalLayer.BodyOn(Load(n), "двуногий") == null).ToArray();
            CollectionAssert.AreEquivalent(expected, missing, "долг тел на двуногом изменился — поправь список (пришла поставка или пропала)");
        }

        /// <summary>ПОСАДКА ГОЛОВЫ (спека 08.10 §2 п.3): при g = 1 и `KHead` = 1 шея и голова встают ровно под наклоны и в
        /// пропорции цели; голова остаётся на конце шеи; при `KHead` = 0 шея и голова шасси не тронуты.</summary>
        [Test]
        public void Head_TakesTargetPosture_ScaledByKHead()
        {
            var human = Load("Человек");
            var h = GlobalLayer.HeadOn(human, human.Plan);
            Assume.That(h != null && h.On, "у человека не нашлось шеи и головы");
            var t = JsonUtility.FromJson<HeadNumbers>(JsonUtility.ToJson(h));
            t.neckPitch += 30f; t.headPitch -= 15f; t.neckLen *= 1.5f; t.headR0 *= 1.3f;
            var wolf = WithBody("Волк", GlobalLayer.BodyOn(human, human.Plan).Values.ToArray(), human.Plan);
            wolf.planBodies[0].head = t;

            GlobalLayer.KHead = 1f;
            var shifted = Object.Instantiate(human); shifted.speciesName = "Человек·голова"; trash.Add(shifted);
            shifted.bones = GlobalLayer.Blend(human, wolf, 1f);
            var m = GlobalLayer.HeadOn(shifted, shifted.Plan);
            Assert.AreEqual(t.neckPitch, m.neckPitch, 0.05f, "наклон шеи");
            Assert.AreEqual(t.headPitch, m.headPitch, 0.05f, "наклон головы от шеи");
            Assert.AreEqual(t.neckLen, m.neckLen, 1e-4f, "длина шеи");
            Assert.AreEqual(t.headR0, m.headR0, 1e-4f, "радиус головы");

            // голова на конце шеи: зазор между концом шеи и началом головы тот же, что у шасси
            float Gap(Bone[] bones)
            {
                var by = bones.ToDictionary(b => b.name);
                var (np, nr) = PoseOf(by, by["шея"]); var (hp, _) = PoseOf(by, by["голова"]);
                return (np + nr * Vector3.up * by["шея"].length - hp).magnitude;
            }
            Assert.AreEqual(Gap(human.bones), Gap(shifted.bones), 1e-4f, "голова оторвалась от шеи или вошла в неё");

            GlobalLayer.KHead = 0f;
            var untouched = GlobalLayer.Blend(human, wolf, 1f).ToDictionary(b => b.name);
            var hBy = human.bones.ToDictionary(b => b.name);
            foreach (var n in new[] { "шея", "голова" })
                Assert.That(Quaternion.Angle(PoseOf(hBy, hBy[n]).Item2, PoseOf(untouched, untouched[n]).Item2), Is.LessThan(0.01f), n + ": KHead = 0, а поворот изменился");
        }

        SpeciesSO WithBody(string species, GroupNumbers[] groups, string plan)
        {
            var sp = Object.Instantiate(Load(species)); sp.speciesName = species + "·тело"; trash.Add(sp);
            sp.planBodies = new[] { new PlanBody { species = species, plan = plan, groups = groups } };
            return sp;
        }

        static float Torso(SpeciesSO sp)
        {
            var by = sp.bones.ToDictionary(b => b.name);
            Vector3 Root(string limb)
            {
                var r = sp.bones.First(b => b.limb == limb && string.IsNullOrEmpty(b.group) && (!by.TryGetValue(b.parent ?? "", out var p) || p.limb != limb));
                var (pos, _) = PoseOf(by, r); pos.x = 0; return pos;
            }
            return (Root("перед") - Root("зад")).magnitude;
        }

        static (Vector3, Quaternion) PoseOf(System.Collections.Generic.Dictionary<string, Bone> by, Bone b)
        {
            if (string.IsNullOrEmpty(b.parent) || !by.TryGetValue(b.parent, out var p)) return SkeletonBuilder.Root(b);
            var (pp, pr) = PoseOf(by, p);
            return SkeletonBuilder.Child(p, pp, pr, b);
        }

        [Test]
        public void Compose_ShiftsGroupsBeforeAugments()
        {
            var human = WithGroup("Человек", "проба.глоб", 0.3f, 0.5f);
            var wolf = Load("Волк");   // такой группы у волка нет — у человека она гаснет по ступени
            var worn = human.organs.ToList();
            foreach (var w in wolf.organs.Where(o => o != null && !o.chassisOnly))
            {
                int i = worn.FindIndex(o => o != null && o.slot == w.slot);
                if (i >= 0) worn[i] = w; else worn.Add(w);
            }
            var (_, step) = GlobalLayer.Step(human, worn);
            Assume.That(step, Is.GreaterThan(0), "состав не дотянул до ступени — проверять нечего");
            var body = ChainSwap.Compose(human, worn);
            float r = Group(body.bones, "проба.глоб").rel.r0;
            Assert.AreEqual(0.5f * (1f - GlobalLayer.G(step)), r, 1e-4f, "глобальный слой не дошёл до сборки");
        }
    }
}
