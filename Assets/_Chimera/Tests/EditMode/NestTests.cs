using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>ГНЁЗДА (спека `2026-09-26-adresaciya-detaley-himery.md`, П1–П3, П7). Аугмент, адресованный гнездом, встаёт
    /// в гнездо НОСИТЕЛЯ — в его кадре и единицах, и висит на кости-хозяине. Сторожат ровно то, что 26.09 было сломано
    /// у всех химер: деталь донора на чужом шасси висела в воздухе, потому что адрес был записан в координатах донора.</summary>
    public class NestTests
    {
        GameObject root;
        SpeciesSO a, b;

        // Одна парная кость «лапа» сверху вниз: начало (x, top, z), длина 0.5, конец — запястье, туда и ставится гнездо.
        // Кость НЕ скрывает место, и у вида нет органа на нём, кроме подаваемого тестом: мерим только гнездо
        static SpeciesSO Chassis(string name, Vector3 wrist, float unit)
        {
            var s = ScriptableObject.CreateInstance<SpeciesSO>();
            s.speciesName = name;   // своё имя: кэш оболочки поля ключуется именем вида
            s.sockets = new[] { new BodySocket { name = "Руки", baseSize = new Vector3(0.1f, 0.5f, 0.1f) } };
            s.bones = new[]
            {
                new Bone { name = "лапа", socket = "Руки", origin = wrist + Vector3.up * 0.5f, dir = new Vector3(180f, 0f, 0f),
                           length = 0.5f, r0 = 0.05f, r1 = 0.05f, mirrorX = true },
            };
            string json = "{\"species\":\"" + name + "\",\"places\":[{\"name\":\"Руки\",\"host\":\"лапа\"," +
                          $"\"pos\":[{F(wrist.x)},{F(wrist.y)},{F(wrist.z)}],\"dir\":[0,-1,0],\"unit\":{F(unit)},\"mirror\":true}}]}}";
            s.nests = SpeciesHandoff.ReadNests(s, json, out var problems);
            Assert.IsEmpty(problems, string.Join("\n", problems));
            return s;
        }

        static string F(float v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // АУГМЕНТ ДОНОРА: один кусок в гнезде — на пол-единицы вдоль гнезда (вниз от запястья), габарит 1×2×3 единицы
        static Organ Paw() => new Organ
        {
            organName = "лапа донора", slot = "Руки",
            visualParts = new[] { new OrganPart { nest = true, offset = new Vector3(0f, 0f, 0.5f), scale = new Vector3(1f, 2f, 3f) } },
        };

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("~гнёзда");
            a = Chassis("~гнездо-А", new Vector3(0.2f, 0.3f, 0.4f), 0.1f);
            b = Chassis("~гнездо-Б", new Vector3(0.3f, 0.9f, -0.2f), 0.3f);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            MorphBuilder.ResetCatalog();
        }

        Transform[] Paws() => root.GetComponentsInChildren<MeshRenderer>().Where(r => r.name == "Руки")
                                  .Select(r => r.transform).OrderByDescending(t => t.position.x).ToArray();

        [Test]
        public void Handoff_BodyMetres_RoundTripThroughHostBone()
        {
            var n = a.nests.Single();
            Assert.AreEqual("лапа", n.host);
            // запястье — конец кости: в её кадре это (0, длина, 0)
            Assert.That(Vector3.Distance(n.localPos, new Vector3(0f, 0.5f, 0f)), Is.LessThan(1e-4f), "гнездо не легло на конец кости: " + n.localPos);
        }

        [Test]
        public void SameAugment_LandsInEachCarriersNest_AtItsCalibre()
        {
            MorphBuilder.Build(root.transform, a, new[] { Paw() });
            var onA = Paws();
            Assert.AreEqual(2, onA.Length, "пара гнёзд — две лапы");
            // вдоль гнезда (вниз) на 0.5 единицы: 0.3 − 0.05
            Assert.That(Vector3.Distance(onA[0].position, new Vector3(0.2f, 0.25f, 0.4f)), Is.LessThan(1e-3f), "правая на А: " + onA[0].position);
            Assert.That(Vector3.Distance(onA[1].position, new Vector3(-0.2f, 0.25f, 0.4f)), Is.LessThan(1e-3f), "левая на А — зеркало: " + onA[1].position);
            Assert.That(Vector3.Distance(onA[0].lossyScale, new Vector3(0.1f, 0.2f, 0.3f)), Is.LessThan(1e-3f), "габарит в единицах А: " + onA[0].lossyScale);

            MorphBuilder.Build(root.transform, b, new[] { Paw() });
            var onB = Paws();
            // тот же аугмент — калибр носителя Б: 0.9 − 0.15, габарит втрое крупнее
            Assert.That(Vector3.Distance(onB[0].position, new Vector3(0.3f, 0.75f, -0.2f)), Is.LessThan(1e-3f), "правая на Б: " + onB[0].position);
            Assert.That(Vector3.Distance(onB[0].lossyScale, new Vector3(0.3f, 0.6f, 0.9f)), Is.LessThan(1e-3f), "габарит в единицах Б: " + onB[0].lossyScale);
        }

        [Test]
        public void NestedDetail_IsChildOfHostBone_AndFollowsIt()
        {
            MorphBuilder.Build(root.transform, a, new[] { Paw() });
            var paws = Paws();
            Assert.AreEqual("лапа", paws[0].parent.name, "правая лапа — ребёнок кости-хозяина");
            Assert.AreEqual("лапа.L", paws[1].parent.name, "левая — ребёнок зеркальной кости");

            // ПОВЕРНУЛИ КОСТЬ — ДЕТАЛЬ ПОЕХАЛА: ради этого П7 и нужен (анимация)
            var bone = paws[0].parent;
            var before = paws[0].position;
            bone.Rotate(Vector3.right, 90f, Space.Self);
            Assert.That(Vector3.Distance(paws[0].position, before), Is.GreaterThan(0.1f), "кость повернулась, а деталь осталась на месте");
        }

        [Test]
        public void NoNest_NoDetail_AndTheMissIsVisible()
        {
            a.nests = new PlaceNest[0];
            MorphBuilder.Build(root.transform, a, new[] { Paw() });
            Assert.IsEmpty(Paws(), "без гнезда деталь не ставится наугад");
            CollectionAssert.Contains(MorphBuilder.MissingNests.ToList(), "~гнездо-А.Руки");
        }

        // ГОЛОВА ДЛЯ ТЕСТА НОСА: гнездо Пасти смотрит вперёд из точки (0, 1, 0.5), единица 0.2; своё гнездо носа у шасси
        // нарочно далеко (0, 1, 3) — чтобы было видно, из какого гнезда нос встал
        static SpeciesSO Head(string name)
        {
            var s = ScriptableObject.CreateInstance<SpeciesSO>();
            s.speciesName = name;
            s.sockets = new[]
            {
                new BodySocket { name = "Пасть" },
                new BodySocket { name = "Чутьё", inner = true },
                new BodySocket { name = "нос", parent = "Пасть", formFrom = "Чутьё", formRole = PartRole.Nose, graft = true },
            };
            s.bones = new[] { new Bone { name = "голова", origin = new Vector3(0f, 1f, 0f), length = 0.2f } };
            s.nests = SpeciesHandoff.ReadNests(s, "{\"places\":[" +
                "{\"name\":\"Пасть\",\"host\":\"голова\",\"pos\":[0,1,0.5],\"dir\":[0,0,1],\"unit\":0.2}," +
                "{\"name\":\"Чутьё\",\"host\":\"голова\",\"pos\":[0,1,0],\"dir\":[0,0,1],\"unit\":0.2}," +
                "{\"name\":\"нос\",\"host\":\"голова\",\"pos\":[0,1,3],\"dir\":[0,0,1],\"unit\":0.2}]}", out var problems);
            Assert.IsEmpty(problems, string.Join("\n", problems));
            return s;
        }

        [Test]
        public void Nose_SitsOnTheTipOfTheWornMuzzle_NotOnTheChassisNest()
        {
            var head = Head("~гнездо-нос");
            var maw = new Organ
            {
                organName = "чужая морда", slot = "Пасть",
                visualParts = new[] { new OrganPart { nest = true, offset = new Vector3(0f, 0f, 0.5f) } },
                // кончик морды — одна единица гнезда Пасти вперёд; нос вдвое мельче её единицы
                carries = new[] { new PlaceNest { name = "нос", localPos = new Vector3(0f, 0f, 1f), unit = 0.5f } },
            };
            var sense = new Organ
            {
                organName = "нюх носителя", slot = "Чутьё",
                visualParts = new[] { new OrganPart { nest = true, role = PartRole.Nose } },
            };
            try
            {
                MorphBuilder.Build(root.transform, head, new[] { maw, sense });
                var nose = root.GetComponentsInChildren<MeshRenderer>().Single(r => r.name == "нос").transform;
                Assert.That(Vector3.Distance(nose.position, new Vector3(0f, 1f, 0.7f)), Is.LessThan(1e-3f), "нос не на кончике морды: " + nose.position);
                Assert.That(Mathf.Abs(nose.lossyScale.x - 0.1f), Is.LessThan(1e-3f), "калибр носа — доля единицы Пасти: " + nose.lossyScale);

                // морда без гнезда носа — нос берёт гнездо шасси
                maw.carries = new PlaceNest[0];
                MorphBuilder.Build(root.transform, head, new[] { maw, sense });
                nose = root.GetComponentsInChildren<MeshRenderer>().Single(r => r.name == "нос").transform;
                Assert.That(Vector3.Distance(nose.position, new Vector3(0f, 1f, 3f)), Is.LessThan(1e-3f), "без гнезда на морде нос должен встать в гнездо шасси: " + nose.position);
            }
            finally { Object.DestroyImmediate(head); }
        }

        [Test]
        public void Handoff_ReportsPlacesWithoutNest()
        {
            var s = ScriptableObject.CreateInstance<SpeciesSO>();
            s.speciesName = "~гнездо-В";
            s.sockets = new[] { new BodySocket { name = "Руки" }, new BodySocket { name = "Хвост" } };
            s.bones = new[] { new Bone { name = "лапа", length = 0.5f } };
            SpeciesHandoff.ReadNests(s,
                "{\"places\":[{\"name\":\"Руки\",\"host\":\"лапа\",\"pos\":[0,0,0],\"dir\":[0,-1,0],\"unit\":0.1}]}", out var problems);
            Object.DestroyImmediate(s);
            Assert.That(problems.Any(p => p.Contains("Хвост")), "тотальность (П2): у места «Хвост» нет гнезда — должно быть замечание");
        }
    }
}
