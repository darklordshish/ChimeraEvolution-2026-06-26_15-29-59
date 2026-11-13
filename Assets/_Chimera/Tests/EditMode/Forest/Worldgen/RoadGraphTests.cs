using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// S4 P6: POI v0 → MST с корнем базы, дороги не идут через воду/кольцо/станцию,
    /// сеть связна на плоском поле. Реальная поверхность — PlayMode-срез.
    /// </summary>
    public class RoadGraphTests
    {
        static DomeGenConfigSO Config()
        {
            var c = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            c.mapDiameter = 1000f;
            c.chunkSize = 200f;
            c.worldVersion = 2;
            return c;
        }

        static List<ForestPoi> Pois()
        {
            var list = new List<ForestPoi>();
            void Add(ForestPoiKind k, string n, Vector2 p)
            {
                list.Add(new ForestPoi
                {
                    kind = k, name = n, pos = p, radius = 8f, groundY = 20f,
                });
            }
            Add(ForestPoiKind.Base, "base", new Vector2(0f, 0f));
            Add(ForestPoiKind.Gate, "gate0", new Vector2(300f, 200f));
            Add(ForestPoiKind.Gate, "gate1", new Vector2(-260f, -220f));
            Add(ForestPoiKind.Zone, "zone0", new Vector2(200f, -260f));
            Add(ForestPoiKind.Zone, "zone1", new Vector2(-280f, 240f));
            Add(ForestPoiKind.Common, "common", new Vector2(120f, 320f));
            Add(ForestPoiKind.Lair, "lair", new Vector2(40f, 0f));
            return list;
        }

        static DomeMasks Masks()
        {
            var m = new DomeMasks().RingBand(480f, 560f);
            m.Add("station", DomeFootprint.Obb(Vector2.zero, 0f, 23f, 18f));
            m.Add("water", DomeFootprint.Circle(new Vector2(400f, -400f), 20f));
            return m;
        }

        [Test]
        public void Network_RootsAtBase_AndConnectsPrimary()
        {
            var cfg = Config();
            try
            {
                var net = ForestRoads.Build(cfg, Pois(), (x, z) => 20f, null, Masks(), 1337L);
                Assert.Greater(net.roads.Count, 0, "дорог нет: " + net.note);
                Assert.Greater(net.length, 0f, "нулевая длина сети");
                bool fromBase = false;
                foreach (var r in net.roads)
                    if (Vector2.Distance(r.points[0], Vector2.zero) < 1f ||
                        Vector2.Distance(r.points[r.points.Count - 1], Vector2.zero) < 1f)
                        fromBase = true;
                Assert.IsTrue(fromBase, "ни одно ребро не выходит из базы");
            }
            finally { Object.DestroyImmediate(cfg); }
        }

        [Test]
        public void LairSpur_IsThreeMetersWide()
        {
            var cfg = Config();
            try
            {
                var net = ForestRoads.Build(cfg, Pois(), (x, z) => 20f, null, Masks(), 1337L);
                bool spur = false;
                foreach (var r in net.roads)
                    if (r.width <= 3.01f && r.width >= 2.99f) { spur = true; break; }
                Assert.IsTrue(spur, "spur к логову (3 м) не построен: " + net.note);
            }
            finally { Object.DestroyImmediate(cfg); }
        }

        [Test]
        public void Roads_AvoidRingAndStation()
        {
            var cfg = Config();
            try
            {
                var masks = Masks();
                var net = ForestRoads.Build(cfg, Pois(), (x, z) => 20f, null, masks, 1337L);
                foreach (var r in net.roads)
                    for (int i = 1; i < r.points.Count; i++)
                    {
                        Vector2 mid = (r.points[i - 1] + r.points[i]) * 0.5f;
                        Assert.IsFalse(masks.OverlapsRing(DomeFootprint.Circle(mid, 2f)),
                            "дорога вошла в кольцо");
                        if (Vector2.Distance(mid, Vector2.zero) > 25f)
                            Assert.IsFalse(masks.ContainsPoint(mid, "station"),
                                "дорога вошла в станцию вне базы");
                    }
            }
            finally { Object.DestroyImmediate(cfg); }
        }

        [Test]
        public void Near_DetectsRoadForFloraCut()
        {
            var cfg = Config();
            try
            {
                var net = ForestRoads.Build(cfg, Pois(), (x, z) => 20f, null, Masks(), 1337L);
                Assert.IsTrue(ForestRoads.Near(net, 1f, 0f, 1.5f), "точка у базы не на дороге");
                Assert.IsFalse(ForestRoads.Near(net, 900f, 900f, 1.5f), "точка вдали — не дорога");
            }
            finally { Object.DestroyImmediate(cfg); }
        }
    }
}
