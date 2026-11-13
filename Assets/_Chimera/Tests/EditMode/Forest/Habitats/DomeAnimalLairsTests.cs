using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Адреса логов волка, лося, ежа (спека `2026-11-13-forest-logovo-po-zakonam` §4.4, §5 п. 1–3) на синтетическом
    /// мире: ровная земля, озеро, лесной массив с кромкой. Проверяется закон адреса, а не конкретные числа мира.
    /// </summary>
    public class DomeAnimalLairsTests
    {
        static readonly Vector2 Lab = Vector2.zero;
        static readonly Vector2 Entry = new Vector2(0f, -850f);

        static DomeAnimalLairs.World World(long seed, List<Vector2> avoid = null)
        {
            var trees = new List<Vector3>();
            // лесной массив западнее лаборатории: стволы через 8 м, кромка по x = −150
            for (float x = -700f; x <= -150f; x += 8f)
                for (float z = -550f; z <= 550f; z += 8f)
                    trees.Add(new Vector3(x, 0f, z));
            // озеро как у павильона (радиус до 150 м): пояс берега 15–60 м вмещает две лёжки с разносом 300 м
            var hydro = new DomeHydro.State { lakeCenter = new Vector2(450f, 0f), lakeRadius = 150f };
            return new DomeAnimalLairs.World
            {
                diameter = 2000f,
                seed = seed,
                ground = (x, z) => 0f,
                hydro = hydro,
                trees = trees,
                zones = new List<ForestSpace.Zone>(),
                lab = Lab,
                entry = Entry,
                avoid = avoid ?? new List<Vector2>(),
            };
        }

        static int CountTrees(DomeAnimalLairs.World w, Vector2 p, float r) =>
            w.trees.Count(t => (new Vector2(t.x, t.z) - p).sqrMagnitude <= r * r);

        [Test]
        public void SameSeed_SameAddresses_OtherSeed_Differs()
        {
            var a = DomeAnimalLairs.Find(World(1337));
            var b = DomeAnimalLairs.Find(World(1337));
            var c = DomeAnimalLairs.Find(World(777));
            CollectionAssert.AreEqual(a.Select(s => s.pos).ToList(), b.Select(s => s.pos).ToList(), "тот же сид — те же адреса");
            CollectionAssert.AreNotEqual(a.Select(s => s.pos).ToList(), c.Select(s => s.pos).ToList(), "другой сид — другие адреса");
        }

        [Test]
        public void EveryKind_GetsItsCount_OnASuitableWorld()
        {
            var spots = DomeAnimalLairs.Find(World(1337));
            Assert.AreEqual(DomeAnimalLairs.WolfCount, spots.Count(s => s.kind == DomeAnimalLairs.Kind.Wolf), "волчьи логова");
            Assert.AreEqual(DomeAnimalLairs.MooseCount, spots.Count(s => s.kind == DomeAnimalLairs.Kind.Moose), "лёжки лося");
            Assert.AreEqual(DomeAnimalLairs.HedgehogCount, spots.Count(s => s.kind == DomeAnimalLairs.Kind.Hedgehog), "норы ежа");
        }

        [Test]
        public void EverySpot_PassesItsKindFilter()
        {
            var w = World(1337);
            foreach (var s in DomeAnimalLairs.Find(w))
            {
                float water = DomeAnimalLairs.WaterDistance(w.hydro, s.pos);
                Assert.LessOrEqual(s.pos.magnitude, w.diameter * 0.5f - DomeAnimalLairs.RingMargin, $"{s.kind}: внутри чаши");
                switch (s.kind)
                {
                    case DomeAnimalLairs.Kind.Wolf:
                        Assert.GreaterOrEqual(CountTrees(w, s.pos, DomeAnimalLairs.WolfTreeRadius), DomeAnimalLairs.WolfMinTrees, "волк — в лесу");
                        Assert.GreaterOrEqual(water, DomeAnimalLairs.WolfMinFromWater, "волк — вдали от воды");
                        Assert.GreaterOrEqual(Vector2.Distance(s.pos, Lab), DomeAnimalLairs.WolfMinFromPeople, "волк — вдали от лаборатории");
                        Assert.GreaterOrEqual(Vector2.Distance(s.pos, Entry), DomeAnimalLairs.WolfMinFromPeople, "волк — вдали от входа");
                        break;
                    case DomeAnimalLairs.Kind.Moose:
                        Assert.That(water, Is.InRange(DomeAnimalLairs.MooseShoreMin, DomeAnimalLairs.MooseShoreMax), "лось — на берегу");
                        break;
                    case DomeAnimalLairs.Kind.Hedgehog:
                        Assert.AreEqual(0, CountTrees(w, s.pos, DomeAnimalLairs.HedgehogOpenRadius), "ёж — на открытом месте");
                        Assert.GreaterOrEqual(CountTrees(w, s.pos, DomeAnimalLairs.HedgehogEdgeRadius), DomeAnimalLairs.HedgehogEdgeTrees, "ёж — у кромки леса");
                        break;
                }
            }
        }

        [Test]
        public void Spacing_WithinKind_AndBetweenKinds()
        {
            var spots = DomeAnimalLairs.Find(World(1337));
            for (int i = 0; i < spots.Count; i++)
                for (int j = i + 1; j < spots.Count; j++)
                {
                    float d = Vector2.Distance(spots[i].pos, spots[j].pos);
                    float need = spots[i].kind != spots[j].kind ? DomeAnimalLairs.OtherSpacing
                        : spots[i].kind == DomeAnimalLairs.Kind.Wolf ? DomeAnimalLairs.WolfSpacing
                        : spots[i].kind == DomeAnimalLairs.Kind.Moose ? DomeAnimalLairs.MooseSpacing
                        : DomeAnimalLairs.HedgehogSpacing;
                    Assert.GreaterOrEqual(d, need, $"{spots[i].kind}–{spots[j].kind}: разнос");
                }
        }

        [Test]
        public void ForeignAddresses_AreAvoided()
        {
            var free = DomeAnimalLairs.Find(World(1337));
            var avoid = free.Select(s => s.pos).ToList(); // чужие адреса ровно там, где логова встали бы
            var spots = DomeAnimalLairs.Find(World(1337, avoid));
            foreach (var s in spots)
                foreach (var a in avoid)
                    Assert.GreaterOrEqual(Vector2.Distance(s.pos, a), DomeAnimalLairs.OtherSpacing, "чужой адрес обойдён");
        }

        [Test]
        public void NoWater_NoMoose_OthersStillPlaced()
        {
            var w = World(1337);
            w.hydro = null;
            var spots = DomeAnimalLairs.Find(w);
            Assert.AreEqual(0, spots.Count(s => s.kind == DomeAnimalLairs.Kind.Moose), "без воды лосю лёжки нет");
            Assert.Greater(spots.Count(s => s.kind == DomeAnimalLairs.Kind.Wolf), 0, "волк от воды не зависит");
        }
    }
}
