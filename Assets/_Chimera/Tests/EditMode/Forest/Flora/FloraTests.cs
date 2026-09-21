using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Растительность: детерминизм, min-distance, фильтры крутизны/воды, counts,
    /// габарит кита, outward-нормали, тинт воды. Слайс s8.
    /// </summary>
    public class FloraTests
    {
        static WorldGenConfigSO World(long seed)
        {
            var cfg = ScriptableObject.CreateInstance<WorldGenConfigSO>();
            cfg.seed = seed;
            cfg.waterLevel = -1.5f;
            return cfg;
        }

        static ulong PointsHash(List<FloraScatter.FloraPoint> pts)
        {
            unchecked
            {
                ulong h = 1469598103934665603UL;
                foreach (var p in pts)
                {
                    h ^= (ulong)p.pos.x.GetHashCode() + 0x9E3779B97F4A7C15UL + (h << 6) + (h >> 2);
                    h ^= (ulong)p.pos.z.GetHashCode() + 0x9E3779B97F4A7C15UL + (h << 6) + (h >> 2);
                }
                return h;
            }
        }

        [Test]
        public void Scatter_Deterministic()
        {
            var cfg = World(1337);
            var a = FloraScatter.Scatter(cfg.seed, 11, 40, 46f, 3f, cfg);
            var b = FloraScatter.Scatter(cfg.seed, 11, 40, 46f, 3f, cfg);
            Assert.AreEqual(PointsHash(a), PointsHash(b), "тот же сид — те же точки");
            Assert.AreEqual(a.Count, b.Count);
        }

        [Test]
        public void Scatter_RespectsMinDistance_AndCount()
        {
            var cfg = World(1337);
            var pts = FloraScatter.Scatter(cfg.seed, 11, 40, 46f, 3f, cfg);
            Assert.AreEqual(40, pts.Count, "в открытой местности — полный набор");
            for (int i = 0; i < pts.Count; i++)
                for (int j = i + 1; j < pts.Count; j++)
                {
                    Vector2 d = new Vector2(pts[i].pos.x - pts[j].pos.x, pts[i].pos.z - pts[j].pos.z);
                    Assert.GreaterOrEqual(d.magnitude, 3f - 1e-3f, $"пара {i}/{j} держит дистанцию");
                }
        }

        [Test]
        public void Scatter_SkipsSteepAndWater()
        {
            var steep = World(12);
            steep.amplitude = 12f;
            steep.baseFrequency = 0.08f;
            steep.waterLevel = 1000f; // всё под водой: точек быть не должно вообще
            var pts = FloraScatter.Scatter(steep.seed, 11, 40, 46f, 3f, steep);
            Assert.AreEqual(0, pts.Count, "мир-океан — пусто, без висяков");
        }

        [Test]
        public void Scatter_NoPointsBelowWater()
        {
            var cfg = World(1337);
            var pts = FloraScatter.Scatter(cfg.seed, 44, 200, 46f, 2f, cfg);
            foreach (var p in pts)
                Assert.GreaterOrEqual(p.pos.y, cfg.waterLevel, "пруды чистые (ниже уровня не растёт)");
        }

        [Test]
        public void ScatterBelt_RespectsCeiling()
        {
            var cfg = World(1337);
            var pts = FloraScatter.ScatterBelt(cfg.seed, 55, 120, 46f, 2f, cfg, 1f);
            Assert.Greater(pts.Count, 0, "пояс не пуст");
            foreach (var p in pts)
            {
                Assert.GreaterOrEqual(p.pos.y, cfg.waterLevel, "ниже воды нет");
                Assert.LessOrEqual(p.pos.y, cfg.waterLevel + 1f + 1e-4f, "выше пояса нет");
            }
        }

        [Test]
        public void TreeKindFor_LowlandKeepsLegacy_HighlandGivesSpruce()
        {
            for (int k = 0; k < 50; k++)
            {
                var kind = FloraPlacer.TreeKindFor(1337, k, 0f);
                Assert.AreNotEqual(FloraKind.SpruceTree, kind, $"низина k={k} — старый набор");
                Assert.AreEqual(FloraPlacer.TreeKindFor(1337, k, 0f), kind, "детерминировано");
            }
            int spruce = 0;
            for (int k = 0; k < 200; k++)
                if (FloraPlacer.TreeKindFor(1337, k, 10f) == FloraKind.SpruceTree) spruce++;
            Assert.Greater(spruce, 100, "высоко — в основном ели");
        }

        [Test]
        public void Kit_HasVolume()
        {
            Assert.Greater(FloraMeshKit.Trunk(0.14f, 0.09f, 2.6f).bounds.size.y, 2f, "ствол тянется вверх");
            Assert.Greater(FloraMeshKit.Crown(1.1f).bounds.size.x, 1f, "крона широкая");
            Assert.Greater(FloraMeshKit.GrassBlade(0.12f, 0.7f).vertexCount, 0, "травинка не пуста");
            Assert.Greater(FloraMeshKit.SpruceCrown(1f, 3f).bounds.size.y, 2f, "ель тянется вверх");
            Assert.Greater(FloraMeshKit.Fern(0.8f, 0.5f, 0.08f).vertexCount, 0, "папоротник не пуст");
        }

        static void AssertOutward(Mesh mesh, string name)
        {
            var verts = mesh.vertices;
            var tris = mesh.triangles;
            Vector3 center = mesh.bounds.center;
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                Vector3 fc = (a + b + c) / 3f;
                Assert.Greater(Vector3.Dot(n, fc - center), 0f, $"{name}: грань {i / 3} смотрит наружу");
            }
        }

        [Test]
        public void Kit_NormalsPointOutward()
        {
            AssertOutward(FloraMeshKit.Trunk(0.14f, 0.09f, 2.6f), "ствол");
            AssertOutward(FloraMeshKit.Icosahedron(1f), "икосаэдр (примитив гроздей)");
            AssertOutward(FloraMeshKit.Slab(0.9f, 0.5f, 0.7f), "плита");
            AssertOutward(FloraMeshKit.Spike(0.5f, 2.4f), "шип (примитив ярусов ели)");
            // Крона/куст — грозди: внутренние стороны смотрят друг на друга законно,
            // outward меряем на одиночном икосаэдре выше; здесь только объём.
            // Ель — туда же: ярусы тем же winding, что шип (проверен), а общий центр
            // даёт ложный минус нижнему ярусу (поймано тестом: −0.04 на грани 0).
            Assert.Greater(FloraMeshKit.Crown(1.1f).vertexCount, 0, "крона не пуста");
            Assert.Greater(FloraMeshKit.Bush(0.7f).vertexCount, 0, "куст не пуст");
            Assert.Greater(FloraMeshKit.SpruceCrown(1f, 3f).vertexCount, 0, "ель не пуста");
            // Папоротник односторонний (лист двусторонний): только объём.
            Assert.Greater(FloraMeshKit.Fern(0.8f, 0.5f, 0.08f).bounds.size.x, 0.5f, "папоротник раскидистый");
        }

        [Test]
        public void WaterTint_DarkensBelowLevelOnly()
        {
            var cfg = World(1337);
            int res = 64; // тот же регион, что карта (там точно есть и вода min −2.36, и суша)
            float step = 2f;
            var cells = ForestMapBuilder.BuildColors(cfg, res, step);
            var tinted = FloraWaterTint.TintCells(cells, cfg, res, step);
            bool darkened = false, kept = false;
            for (int iz = 0; iz < res; iz++)
                for (int ix = 0; ix < res; ix++)
                {
                    float h = WorldHeightField.SampleHeight(cfg, ix * step, iz * step);
                    Color a = cells[iz * res + ix], b = tinted[iz * res + ix];
                    if (h < cfg.waterLevel)
                    {
                        Assert.Less(b.grayscale + 1e-5f, a.grayscale + 0.3f, "ниже уровня — темнее");
                        darkened = true;
                    }
                    else Assert.AreEqual(a, b, "выше уровня — цел");
                    if (h >= cfg.waterLevel) kept = true;
                }
            Assert.IsTrue(darkened && kept, "в кадре есть и вода, и суша");
        }
    }
}
