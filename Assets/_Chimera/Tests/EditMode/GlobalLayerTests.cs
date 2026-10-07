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
        [TearDown] public void TearDown() { foreach (var o in trash) if (o != null) Object.DestroyImmediate(o); trash.Clear(); ChainSwap.ResetCache(); }

        static SpeciesSO Load(string n) => AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{n}.asset");

        SpeciesSO WithGroup(string species, string group, float at, float r)
        {
            var sp = Object.Instantiate(Load(species)); sp.speciesName = species + "·проба"; trash.Add(sp);
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
            var half = Group(GlobalLayer.Blend(a, b, 0.5f), "бицепс").rel;
            Assert.AreEqual(1.0f, half.r0, 1e-4f, "радиус смешивается в логарифмах: √(0.5·2) = 1");
            Assert.AreEqual(0.45f, half.at, 1e-4f, "положение — линейно");
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

        [Test]
        public void Compose_ShiftsGroupsBeforeAugments()
        {
            var human = WithGroup("Человек", "бицепс", 0.3f, 0.5f);
            var wolf = Load("Волк");   // массы шаблона у волка ещё нет — группа человека гаснет по ступени
            var worn = human.organs.ToList();
            foreach (var w in wolf.organs.Where(o => o != null && !o.chassisOnly))
            {
                int i = worn.FindIndex(o => o != null && o.slot == w.slot);
                if (i >= 0) worn[i] = w; else worn.Add(w);
            }
            var (_, step) = GlobalLayer.Step(human, worn);
            Assume.That(step, Is.GreaterThan(0), "состав не дотянул до ступени — проверять нечего");
            var body = ChainSwap.Compose(human, worn);
            float r = Group(body.bones, "бицепс").rel.r0;
            Assert.AreEqual(0.5f * (1f - GlobalLayer.G(step)), r, 1e-4f, "глобальный слой не дошёл до сборки");
        }
    }
}
