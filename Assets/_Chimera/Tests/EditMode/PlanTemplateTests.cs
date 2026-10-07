using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>ШАБЛОН ПЛАНА (спека `2026-10-07-globalnyj-sloj-shablon-plana.md` §4.1, §4.4): у каждого вида плана — каждая
    /// группа шаблона ровно одним узлом в долях, на кости своей цепи и сегмента; групп вне шаблона нет; таблица планов —
    /// из имён своих шаблонов, без пропусков. Поставки шаблонов — модельная линия; пока их нет, сторожит самотест.</summary>
    public class PlanTemplateTests
    {
        const string Dir = "Docs/models/handoff/";
        static readonly string[] Species = { "Волк", "Лось", "Ёж", "Змея", "Человек" };
        static SpeciesSO Load(string n) => AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{n}.asset");

        static PlanTemplate[] Templates() => Directory.Exists(Dir)
            ? Directory.GetFiles(Dir, "plan-*.json").Where(f => !Path.GetFileName(f).Contains("__"))
                       .Select(f => PlanTemplate.Parse(File.ReadAllText(f))).ToArray()
            : new PlanTemplate[0];

        [Test]
        public void EverySpecies_FillsItsPlanTemplate()
        {
            var templates = Templates();
            if (templates.Length == 0) Assert.Pass("шаблонов планов ещё нет — сторожит самотест");
            var bad = Species.Select(Load).Where(sp => sp != null)
                             .SelectMany(sp => templates.Where(t => t.plan == sp.Plan).SelectMany(t => t.Check(sp))).ToList();
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void PlanTables_NameOnlyTheirTemplates()
        {
            var templates = Templates();
            var tables = Directory.Exists(Dir) ? Directory.GetFiles(Dir, "plan-*__*.json") : new string[0];
            if (tables.Length == 0) Assert.Pass("таблиц планов ещё нет");
            foreach (var f in tables)
            {
                var t = PlanTable.Parse(File.ReadAllText(f));
                var bad = t.Check(templates.FirstOrDefault(x => x.plan == t.a), templates.FirstOrDefault(x => x.plan == t.b));
                Assert.IsEmpty(bad, Path.GetFileName(f) + ":\n" + string.Join("\n", bad));
            }
        }

        /// <summary>Самотест детектора: синтетическая группа на плече человека проходит; без долей, с лишней группой шаблона,
        /// с группой вне шаблона и на чужом сегменте — ловится.</summary>
        [Test]
        public void Detector_CatchesBrokenTemplateUse()
        {
            var human = Object.Instantiate(Load("Человек"));
            foreach (var b in human.bones) b.group = null;   // проба — на чистом от групп человеке: свои 17 групп у него есть с 07.10
            try
            {
                var arm = human.bones.First(b => b.limb == "перед" && b.mark?.b == "локоть");
                var node = new Bone { name = "проба", parent = arm.name, limb = arm.limb, group = "проба", rel = new BoneRel { at = 0.5f, len = 0.3f, r0 = 0.5f, r1 = 0.4f } };
                human.bones = human.bones.Append(node).ToArray();
                var t = new PlanTemplate { plan = human.Plan, groups = new[] { new PlanGroup { name = "проба", limb = "перед", endMark = "локоть" } } };
                Assert.IsEmpty(t.Check(human), "правильная группа не прошла");

                node.rel = null;
                Assert.IsNotEmpty(t.Check(human), "группа не в долях не поймана");
                node.rel = new BoneRel { at = 0.5f, len = 0.3f, r0 = 0.5f, r1 = 0.4f };

                var extra = new PlanTemplate { plan = human.Plan, groups = t.groups.Append(new PlanGroup { name = "нет", limb = "перед" }).ToArray() };
                Assert.IsNotEmpty(extra.Check(human), "пропуск группы шаблона не пойман");

                var wrongSeg = new PlanTemplate { plan = human.Plan, groups = new[] { new PlanGroup { name = "проба", limb = "перед", endMark = "запястье" } } };
                Assert.IsNotEmpty(wrongSeg.Check(human), "группа на чужом сегменте не поймана");

                // СЕГМЕНТ ИЗ ДВУХ КОСТЕЙ (волк: `предплечье` + риг `пясть`): группа на верхней кости сегмента «до запястья»
                var wolf = Object.Instantiate(Load("Волк"));
                foreach (var b in wolf.bones) b.group = null;
                try
                {
                    var fore = wolf.bones.FirstOrDefault(b => b.limb == "перед" && b.mark?.b == "запястье");
                    var upper = fore != null && fore.layer == BodyLayer.Rig ? wolf.bones.First(b => b.name == fore.parent) : fore;
                    var g = new Bone { name = "проба2", parent = upper.name, limb = upper.limb, group = "сгибатели", rel = new BoneRel { at = 0.3f, len = 0.3f, r0 = 0.5f, r1 = 0.4f } };
                    wolf.bones = wolf.bones.Append(g).ToArray();
                    var tw = new PlanTemplate { plan = wolf.Plan, groups = new[] { new PlanGroup { name = "сгибатели", limb = "перед", endMark = "запястье" } } };
                    Assert.IsEmpty(tw.Check(wolf), "группа на верхней кости сегмента из двух костей не прошла");
                }
                finally { Object.DestroyImmediate(wolf); }

                var notYet = new PlanTemplate { plan = human.Plan, species = new[] { "Другой" }, groups = extra.groups };
                Assert.IsEmpty(notYet.Check(human), "непереведённый вид проверен полностью");
                var notYetWrong = new PlanTemplate { plan = human.Plan, species = new[] { "Другой" }, groups = new PlanGroup[0] };
                Assert.IsNotEmpty(notYetWrong.Check(human), "у непереведённого вида группа вне шаблона не поймана");

                var empty = new PlanTemplate { plan = human.Plan, groups = new PlanGroup[0] };
                Assert.IsNotEmpty(empty.Check(human), "группа вне шаблона не поймана");

                var tb = new PlanTemplate { plan = "четвероногий", groups = new[] { new PlanGroup { name = "плечевая", limb = "перед" } } };
                var table = new PlanTable { a = human.Plan, b = "четвероногий", pairs = new[] { new GroupPair { a = "проба", b = "плечевая" } } };
                Assert.IsEmpty(table.Check(t, tb), "правильная таблица не прошла");
                table.pairs = new GroupPair[0];
                Assert.IsNotEmpty(table.Check(t, tb), "пропуск в таблице не пойман");
            }
            finally { Object.DestroyImmediate(human); }
        }
    }
}
