using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>РАСКЛАДКИ ЧИТАЮТСЯ ИЗ ФАЙЛОВ ПОСТАВКИ, а не переписываются в код (17.09): иначе у формы два источника,
    /// и первая регенерация раскладки у модельной линии разойдётся с кодом молча. Формат — массивы чисел, как их
    /// пишет генератор на Python; лишние поля для людей («metres», «note») разбор пропускает.</summary>
    public class HandoffLayoutTests
    {
        static SpeciesSO Species()
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            so.speciesName = "~раскладка";
            so.sockets = new[]
            {
                new BodySocket { name = "голова", baseSize = Vector3.one },
                new BodySocket { name = "нос", attach = 0.1f },
            };
            so.organs = new[]
            {
                new Organ { organName = "Пасть", slot = "Пасть", visualParts = new[] { new OrganPart(), new OrganPart(), new OrganPart() } },
                new Organ { organName = "Коготь", slot = "Руки", visualParts = new[] { new OrganPart() } },
            };
            return so;
        }

        const string Head = @"{
          ""head"": { ""baseSize"": [0.266, 0.27, 0.375], ""sizeRel"": [1.209, 0.9, 0.987] },
          ""places"": [ { ""name"": ""нос"", ""attach"": 0.5, ""attachOffset"": [0, -0.046, 0.439],
                          ""sizeRel"": [0.177, 0.148, 0.12], ""baseEuler"": [0, 0, 0],
                          ""note"": ""мочка на конце клина"", ""metres"": { ""along"": 0.352 } } ],
          ""muzzle"": { ""block"": ""клин"", ""offset"": [0, 0, 0], ""scale"": [1, 1, 1] },
          ""teeth"": [ { ""name"": ""клык"", ""offset"": [0.257, -0.2, 0.304], ""scale"": [0.129, 0.24, 0.057], ""color"": [0.95, 0.94, 0.9, 1.0] } ]
        }";

        const string Legs = @"{ ""legs"": [ { ""slot"": ""Руки"", ""organ"": ""Коготь"", ""parts"": [
            { ""node"": ""предплечье"", ""block"": ""брусок"", ""offset"": [0, 0.147, 0.067], ""scale"": [0.766, 0.809, 0.44], ""euler"": [-7.6, 0, 0] } ] } ] }";

        [Test]
        public void Head_SetsCalibre_Places_AndReplacesMawParts()
        {
            var so = Species();
            SpeciesHandoff.ApplyLayouts(so, Head, null);

            var head = so.sockets.First(s => s.name == "голова");
            Assert.AreEqual(0.375f, head.baseSize.z, 1e-4f, "калибр головы не записан");
            Assert.AreEqual(1.209f, head.sizeRel.x, 1e-4f, "доля головы от шеи не записана");

            var nose = so.sockets.First(s => s.name == "нос");
            Assert.AreEqual(0.5f, nose.attach, 1e-4f);
            Assert.AreEqual(0.439f, nose.attachOffset.z, 1e-4f);

            var maw = so.organs.First(o => o.slot == "Пасть");
            Assert.AreEqual(2, maw.visualParts.Length, "куски Пасти должны заменяться ЦЕЛИКОМ: морда + один зуб, старые три ушли");
            Assert.AreEqual("клин", maw.visualParts[0].block, "морда — первым куском, ригблоком");
            Assert.AreEqual(0.95f, maw.visualParts[1].color.r, 1e-4f, "цвет зуба не прочитан");
            Assert.AreEqual(1f, maw.visualParts[1].color.a, 1e-4f);
        }

        [Test]
        public void Legs_ReplaceOrganParts_WithNodeParts()
        {
            var so = Species();
            SpeciesHandoff.ApplyLayouts(so, null, Legs);

            var claw = so.organs.First(o => o.organName == "Коготь");
            Assert.AreEqual(1, claw.visualParts.Length);
            Assert.AreEqual("предплечье", claw.visualParts[0].node, "узел куска не прочитан — брусок встал бы по месту");
            Assert.AreEqual("брусок", claw.visualParts[0].block);
            Assert.AreEqual(-7.6f, claw.visualParts[0].euler.x, 1e-4f);
        }

        [Test]
        public void Senses_ReplacePartsOfTheirRole_KeepOthers_AndEyeChannelColour()
        {
            // УШИ И ГЛАЗА РИГБЛОКАМИ (решение геймдизайнера 17.09: «нормальные, а не из примитивов»). Их рисует орган Чутья
            // частями с ролью. Раскладка заменяет части ТОЛЬКО тех ролей, что назвала: нос остаётся. Цвет глаза — канал
            // механики («прозрение», «нюх»), а не форма: блок без цвета не должен гасить канал
            var so = Species();
            var eyeChannel = new Color(0.45f, 0.30f, 0.12f, 1f);
            so.organs = new[]
            {
                new Organ { organName = "Нюх", slot = "Чутьё", visualParts = new[]
                {
                    new OrganPart { role = PartRole.Nose, shape = PartShape.Sphere },
                    new OrganPart { role = PartRole.Ear, shape = PartShape.Sphere },
                    new OrganPart { role = PartRole.Ear, shape = PartShape.Sphere },
                    new OrganPart { role = PartRole.Eye, shape = PartShape.Sphere, color = eyeChannel },
                } },
            };
            SpeciesHandoff.ApplyLayouts(so, @"{ ""senses"": [
                { ""role"": ""Ear"", ""block"": ""ухо"", ""offset"": [0, 0.1, 0], ""scale"": [1, 1, 1], ""euler"": [0, 0, -7] },
                { ""role"": ""Eye"", ""block"": ""глаз"", ""scale"": [1, 1, 1] } ] }", null);

            var parts = so.organs[0].visualParts;
            Assert.AreEqual(1, parts.Count(p => p.role == PartRole.Nose), "нос не назван в раскладке — должен остаться");
            var ears = parts.Where(p => p.role == PartRole.Ear).ToArray();
            Assert.AreEqual(1, ears.Length, "части уха заменяются целиком: три шара → один блок");
            Assert.AreEqual("ухо", ears[0].block);
            var eye = parts.Single(p => p.role == PartRole.Eye);
            Assert.AreEqual("глаз", eye.block);
            Assert.AreEqual(eyeChannel, eye.color, "блок глаза погасил цвет канала");
        }

        [Test]
        public void TeethWithoutMuzzle_DoNotAddCube()
        {
            // JsonUtility не бывает null для вложенного класса: отсутствующая «muzzle» приходит пустым объектом,
            // и без защиты в Пасть лёг бы голый куб
            var so = Species();
            SpeciesHandoff.ApplyLayouts(so, @"{ ""teeth"": [ { ""offset"": [0.1, 0, 0], ""scale"": [0.1, 0.1, 0.1] } ] }", null);
            var maw = so.organs.First(o => o.slot == "Пасть");
            Assert.AreEqual(1, maw.visualParts.Length, "без морды в раскладке кусков должно быть ровно столько, сколько зубов");
        }
    }
}
