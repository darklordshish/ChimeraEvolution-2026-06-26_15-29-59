using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>ЧЬЯ ФОРМА НА ОБЩЕМ МЕСТЕ, когда химерный слот кладёт второй орган того же типа (решение геймдизайнера 29.09,
    /// спека двух слоёв, решение 14): прививка в родном слоте → прививка в химерном → собственный орган шасси.
    /// Сторожит дыру прежнего правила «виден родной слот»: человек со своей рукой и волчьей в химерном рисовался с
    /// человеческой, и контур врал о том, что у него работает.</summary>
    public class StackedSlotDrawOrderTests
    {
        static SpeciesSO Load(string name) => AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{name}.asset");

        GameObject go;
        SpeciesSO chassis;

        [TearDown] public void TearDown() { if (go != null) Object.DestroyImmediate(go); if (chassis != null) Object.DestroyImmediate(chassis); }

        CreatureBody Human()
        {
            chassis = Object.Instantiate(Load("Человек"));
            chassis.sockets = new BodySocket[0]; // тесту нужен состав, не модель
            go = new GameObject("Stacked");
            var body = go.AddComponent<CreatureBody>();
            body.Configure(chassis, new[] { Load("Волк"), Load("Лось") });
            body.ExpandPool(9999);
            return body;
        }

        static int NativeSlot(CreatureBody body, string slot)
        {
            for (int i = 0; i < body.SlotCount; i++)
                if (!body.GetSlot(i).chimera && body.GetSlot(i).slot == slot) return i;
            return -1;
        }

        static void Put(CreatureBody body, int slot, string species, string type)
        {
            var v = body.GetVariants(slot);
            int k = v.FindIndex(x => x.species == species && x.slotType == type);
            Assert.GreaterOrEqual(k, 0, $"нет варианта {species}/{type}");
            Assert.IsTrue(body.Install(slot, k), $"не встал {species}/{type}");
        }

        static string FirstFor(CreatureBody body, string type)
        {
            foreach (var o in body.WornInDrawOrder())
                if (o.slot == type)
                    foreach (var name in new[] { "Человек", "Волк", "Лось" })
                        if (System.Array.IndexOf(Load(name).organs, o) >= 0) return name;
            return null;
        }

        [Test]
        public void OwnOrganInNativeSlot_YieldsToChimericGraft()
        {
            var body = Human();
            body.GrantChimeraSlot();
            Put(body, body.SlotCount - 1, "Волк", BodySlots.Arms);
            Assert.AreEqual("Волк", FirstFor(body, BodySlots.Arms), "химерная волчья рука не видна поверх своей");
        }

        [Test]
        public void NativeSlotGraft_BeatsChimericGraft()
        {
            var body = Human();
            Put(body, NativeSlot(body, BodySlots.Arms), "Лось", BodySlots.Arms);
            body.GrantChimeraSlot();
            Put(body, body.SlotCount - 1, "Волк", BodySlots.Arms);
            Assert.AreEqual("Лось", FirstFor(body, BodySlots.Arms), "выбор игрока в родном слоте перебит химерным");
        }
    }
}
