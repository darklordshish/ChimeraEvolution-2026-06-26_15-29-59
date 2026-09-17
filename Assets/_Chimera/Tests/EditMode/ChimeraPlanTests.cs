using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>ХИМЕРА МЕРЯЕТСЯ ТЕМ ЖЕ ДЕТЕКТОРОМ, ЧТО И ЧИСТЫЙ ВИД (п.4 спеки идентичности 14.08).
    /// Сторожит три инварианта закона, каждый из которых ломается молча:
    ///  • ТОПОЛОГИЯ НОСИТЕЛЯ — графт не добавляет и не убирает деталей;
    ///  • КОРЕНЬ НЕ СМЕШИВАЕТСЯ (И3) — место `хребет` в смешанном плане совпадает с шассийным;
    ///  • СМЕШЕНИЕ ВООБЩЕ ПРОИСХОДИТ — хоть одно место обязано поехать, иначе закон мёртв и мы этого
    ///    не заметим (именно так «включённое смешение» полтора месяца числилось отложенным).</summary>
    public class ChimeraPlanTests
    {
        GameObject go;

        SpeciesSO Load(string name)
        {
            var sp = AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/" + name + ".asset");
            Assert.IsNotNull(sp, "нет ассета вида «" + name + "» — прогнать «Chimera → Создать дефолтные виды»");
            return sp;
        }

        /// <summary>Собрать химеру ЧЕРЕЗ ПУБЛИЧНЫЙ API конструктора — тем же путём, что карта тел.</summary>
        BodySocket[] Plan(SpeciesSO chassis, SpeciesSO donor)
        {
            go = new GameObject("~ТестХимера");
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.center = new Vector3(0f, 1f, 0f);

            var body = go.AddComponent<CreatureBody>();
            body.Configure(chassis, new[] { donor });
            body.ExpandPool(500);      // экономика здесь не предмет: проверяется форма

            for (int i = 0; i < body.SlotCount; i++)
            {
                var variants = body.GetVariants(i);
                for (int v = 0; v < variants.Count; v++)
                {
                    if (variants[v].native || variants[v].species != donor.speciesName) continue;
                    if (!body.Install(i, v)) continue;
                    var plan = body.GetBlendedPlan();
                    if (plan != null) return plan;
                    break;
                }
            }
            return null;
        }

        [TearDown]
        public void TearDown() { if (go != null) Object.DestroyImmediate(go); }

        [Test]
        public void Appendage_WithoutNativeSlot_GraftsIntoChimeraSlot()
        {
            // ПРИДАТОК ИДЁТ В ХИМЕРНЫЙ СЛОТ (17.09, поставка 5). У волка родного слота «Рога» нет — рога встают в выданный
            // химерный слот. Помощник перебирал только родные слоты, и карта тел с инструментами кадра не могли
            // собрать ни рогов, ни хвоста, ни игломёта на чужом шасси: «графт не встал» при годном доноре
            var wolf = Load("Волк");
            var moose = Load("Лось");
            BodyProbe.ChimeraPlan(wolf, moose, "Рога", out var worn, out var grafted);

            Assert.IsNotNull(worn, "лосиные рога на волка не встали — придаток без родного слота не собирается");
            var antlers = moose.organs.First(o => o != null && o.slot == "Рога");
            Assert.IsTrue(worn.Contains(antlers), "в надетом нет лосиных рогов: " + grafted);
            Assert.AreEqual(wolf.organs.Count(o => o != null) + 1, worn.Count, "придаток добавляется к родному составу, а не вытесняет орган");
        }

        [Test]
        public void Chimera_KeepsCarrierTopology_AndMovesSomething()
        {
            var chassis = Load("Человек");
            var plan = Plan(chassis, Load("Волк"));
            Assert.IsNotNull(plan, "человек с волчьим графтом не дал смешанного плана — смешение мертво");

            var native = BodyProbe.Measure(chassis);
            var mixed = BodyProbe.Measure(chassis, plan);
            Assert.AreEqual(native.Count, mixed.Count, "графт изменил ЧИСЛО деталей: топология обязана быть носителя");

            // МЕРИМ МЕСТА, А НЕ ДЕТАЛИ: имён деталей по нескольку на место (части органа, зеркальные пары)
            var was = BodyProbe.Group(chassis, native).whole;
            var now = BodyProbe.Group(chassis, mixed).whole;
            int moved = now.Count(kv => was.TryGetValue(kv.Key, out var b)
                                        && (kv.Value.size - b.size).magnitude / Mathf.Max(0.0001f, b.size.magnitude) > 0.001f);
            Assert.Greater(moved, 0, "ни одно место не поехало — смешение пропорций не доходит до сборки");
        }

        [Test]
        public void Chimera_RootStaysChassis()
        {
            var chassis = Load("Человек");
            var plan = Plan(chassis, Load("Волк"));
            Assert.IsNotNull(plan);

            var mine = plan.FirstOrDefault(s => s != null && s.name == BodySlots.Spine);
            var theirs = chassis.sockets.FirstOrDefault(s => s != null && s.name == BodySlots.Spine);
            Assert.IsNotNull(mine, "в смешанном плане нет корня");
            Assert.IsNotNull(theirs, "у шасси нет корня");

            // И3: корень — ЗНАМЕНАТЕЛЬ, у него пропорция не выражается вовсе, и смешивать его нельзя
            Assert.AreEqual(theirs.baseSize, mine.baseSize, "корень сменил габарит");
            Assert.AreEqual(theirs.sizeRel, mine.sizeRel, "корень сменил долю от родителя");
            Assert.AreEqual(theirs.baseEuler, mine.baseEuler, "корень сменил наклон");
            Assert.AreEqual(theirs.attach, mine.attach, 1e-6f, "корень сменил крепление");
        }
    }
}
