using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Chimera.Tests.EditMode
{
    public class SlotDictTests
    {
        [Test]
        public void All_ContainsEveryOrganSlot()
        {
            // любой Organ.slot из конструктора должен быть в словаре
            string[] probed = new[] { "Пасть", "Чутьё", "Сердце", "Шкура", "Руки", "Ноги", "Хвост", "Рога", "Игломёт", "хребет", "Тело", "Погремушка" };
            foreach (var s in probed)
                Assert.IsTrue(BodySlots.All.ContainsKey(s), $"BodySlots.All должен содержать слот '{s}'");
        }

        [Test]
        public void All_HasNoDuplicates()
        {
            var keys = BodySlots.All.Keys.ToList();
            var distinct = keys.Distinct().Count();
            Assert.AreEqual(keys.Count, distinct, "BodySlots.All не должен содержать дублей");
            // словарь технически не может иметь дублей, но проверяем инвариант размера
            Assert.AreEqual(12, keys.Count, "Ожидается 12 слотов (Chassis 3 + Base 6 + Appendage 3)");
        }

        [Test]
        public void OrganSlot_EqualsSocketName()
        {
            // имя сокета = Organ.slot — проверяем что BodySlots знает оба
            var organ = new Organ { organName = "Тест", slot = "Пасть" };
            Assert.IsTrue(BodySlots.IsSlot(organ.slot), "Organ.slot должен быть IsSlot");
            var socket = new BodySocket { name = organ.slot, baseSize = Vector3.one };
            Assert.AreEqual(organ.slot, socket.name, "Имя сокета должно совпадать с Organ.slot");
        }

        [Test]
        public void All_CoversBodySocketNames_FromSampleSpecies()
        {
            var so = ScriptableObject.CreateInstance<SpeciesSO>();
            try
            {
                so.speciesName = "Проба";
                so.sockets = new[]
                {
                    new BodySocket { name = "Пасть", baseSize = Vector3.one },
                    new BodySocket { name = "голова", baseSize = Vector3.one },
                    new BodySocket { name = "хребет", baseSize = Vector3.one },
                };
                foreach (var sk in so.sockets)
                    Assert.IsTrue(BodySlots.IsKnown(sk.name), $"Сокет '{sk.name}' должен быть известен словарю (слот или место)");
                // орган на несуществующий слот — словарь должен ответить false
                var badOrgan = new Organ { organName = "Бад", slot = "НесуществующийСлот" };
                Assert.IsFalse(BodySlots.IsSlot(badOrgan.slot));
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void SpeciesBootstrap_DoesNotIntroduceVisualPartField()
        {
            // отдельного поля-адреса visualPart не должно быть ни у BodySocket ни у Organ
            var socketField = typeof(BodySocket).GetField("visualPart", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(socketField, "BodySocket не должен иметь поля visualPart — адрес = Organ.slot / сокет name");

            var organField = typeof(Organ).GetField("visualPart", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(organField, "Organ не должен иметь поля visualPart");

            // SpeciesBootstrap не должен упоминать visualPart в исходнике (проверка через рефлексию наличия члена не нужна — поля нет)
            var bootType = typeof(SpeciesBootstrap);
            var members = bootType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            bool hasVisualPartMember = members.Any(m => m.Name.ToLower().Contains("visualpart"));
            Assert.IsFalse(hasVisualPartMember, "SpeciesBootstrap не должен заводить visualPart");
        }

        [Test]
        public void BodySlots_PlaceAndSlotAreDisjoint()
        {
            foreach (var kv in BodySlots.All)
                Assert.IsFalse(BodySlots.IsPlace(kv.Key), $"Слот '{kv.Key}' не должен считаться телесным местом");
        }
    }
}
