using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Chimera.Tests.EditMode
{
    /// <summary>КЛЮЧИ ОБЛАСТЕЙ (спека `2026-10-21-organ-prorastaet-klyuch-oblasti.md`): орган слота прорастает формой своей области
    /// вида-донора; вес — доля вида к порогу ступени 2, с глобальным ключом — супремум (область добирает недостающее).</summary>
    public class RegionKeysTests
    {
        static SpeciesSO Load(string n) => AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{n}.asset");

        static System.Collections.Generic.List<Organ> WolfOn(SpeciesSO human, SpeciesSO wolf, params string[] slots) =>
            human.organs.Where(o => o != null).Select(o => !o.chassisOnly && slots.Contains(o.slot)
                ? wolf.organs.FirstOrDefault(w => w != null && w.slot == o.slot && !w.chassisOnly && !w.nativeOnly) ?? o : o).ToList();

        [Test]
        public void OneOrgan_GrowsItsArea_ByDonorShare()
        {
            var h = Load("Человек"); var w = Load("Волк");
            var r = BodyMesh.RegionKeysFor(h, WolfOn(h, w, "Руки"));
            Assert.AreEqual(1, r.Count, "одна область — один ключ");
            float share = GlobalLayer.Identity(h, WolfOn(h, w, "Руки"))[w];
            Assert.AreEqual(share / GlobalLayer.MediumAt, r["Волк·Руки"], 1e-4f);
        }

        [Test]
        public void WithGlobalKey_AreaTakesOnlyTheRest_Supremum()
        {
            var h = Load("Человек"); var w = Load("Волк");
            var all = WolfOn(h, w, "Руки", "Ноги", "Сердце", "Чутьё", "Пасть", "Шкура");
            Assert.That(BodyMesh.RegionKeysFor(h, all).Values, Has.All.EqualTo(0f).Within(1e-4f), "ступень 2: глобальный ключ уже полный");
            var five = WolfOn(h, w, "Руки", "Ноги", "Сердце", "Чутьё", "Пасть");
            float share = GlobalLayer.Identity(h, five)[w];
            float expect = UnityEngine.Mathf.Clamp01(share / GlobalLayer.MediumAt) - BodyMesh.KeyStep1;
            Assert.AreEqual(expect, BodyMesh.RegionKeysFor(h, five)["Волк·Руки"], 1e-4f, "ступень 1: область добирает до своей доли");
        }

        [Test]
        public void OwnOrgans_GiveNoAreas()
        {
            var h = Load("Человек");
            Assert.IsEmpty(BodyMesh.RegionKeysFor(h, h.organs.Where(o => o != null).ToList()));
        }
    }
}
