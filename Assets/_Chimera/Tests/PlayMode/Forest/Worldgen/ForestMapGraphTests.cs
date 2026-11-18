using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// КАРТА КАК ИСТОЧНИК, шаг 6 (спека §7, §9): растр, граф путей и картинка — ИЗ СЛОЁВ КАРТЫ (ГеймБосс 18.11). Главный
    /// сторож: растр из карты, прошедшей JSON, тот же, что из живой карты, — значит, он не читает мир. Ребро несёт факты
    /// (длина, наибольший уклон, переправа, поверхность), весов видов в нём нет.
    /// </summary>
    public class ForestMapGraphTests
    {
        static GameObject host;
        static ForestBuilder builder;
        static ForestMap jsonMap;
        static ForestMapRaster raster;
        static ForestMapGraph graph;

        [OneTimeSetUp]
        public void BuildWorld()
        {
            host = new GameObject("~ForestMapGraphProbe");
            host.SetActive(false);
            builder = ForestProfiles.PavilionV2Zone(host, 1337L);
            builder.buildOnAwake = false;
            host.SetActive(true);
            builder.Build();
            jsonMap = ForestMap.FromJson(builder.Map.ToJson());
            jsonMap.Restore();
            raster = ForestMapRaster.Build(jsonMap);
            graph = ForestMapGraph.Build(jsonMap, raster);
        }

        [OneTimeTearDown]
        public void TearDownWorld()
        {
            if (builder != null) builder.Clear();
            if (host != null) Object.Destroy(host);
            builder = null; host = null; jsonMap = null; raster = null; graph = null;
        }

        [UnityTest]
        public IEnumerator Raster_FromJsonMap_EqualsFromLiveMap()
        {
            var live = ForestMapRaster.Build(builder.Map);
            Assert.AreEqual(500, live.n, "растр 4 м на Ø2000 — 500×500");
            Assert.AreEqual(live.key, raster.key, "ключ растра — хеш карты и шаг");
            int bad = 0;
            for (int i = 0; i < live.height.Length; i++)
                if (BitConverter.SingleToInt32Bits(live.height[i]) != BitConverter.SingleToInt32Bits(raster.height[i])
                    || live.cls[i] != raster.cls[i] || live.surface[i] != raster.surface[i]) bad++;
            Assert.AreEqual(0, bad, "растр из JSON-карты = растр из живой: он читает только карту");
            yield break;
        }

        [UnityTest]
        public IEnumerator Raster_ClassesAreSane()
        {
            int water = raster.cls.Count(c => c == ForestMapRaster.Cls.Water);
            int walk = raster.cls.Count(c => c == ForestMapRaster.Cls.Walk);
            int outside = raster.cls.Count(c => c == ForestMapRaster.Cls.Outside);
            Assert.Greater(water, 0, "вода есть");
            Assert.Greater(walk, water, "ходьбы больше, чем воды");
            Assert.Greater(outside, 0, "за кольцом — вне карты");
            Assert.Greater(raster.surface.Count(s => s == ForestMapRaster.Surface.Road), 0, "дороги в растре");
            Assert.Greater(raster.surface.Count(s => s == ForestMapRaster.Surface.Forest), 0, "лес в растре");
            yield break;
        }

        [UnityTest]
        public IEnumerator Graph_VerticesFromMapLayers()
        {
            var m = jsonMap;
            int expected = m.zones.Count + 1 + m.lairs.Count + m.waterholes.Count + (m.lab.has ? 1 : 0) + m.forage.Count;
            Assert.AreEqual(expected, graph.vertices.Count, "вершины: зоны, вход, логова, водопои, лаборатория, корм (шаг 7)");
            Assert.AreEqual(m.forage.Count, graph.vertices.Count(v => v.kind == ForestMapGraph.VertexKind.Forage));
            Assert.AreEqual(m.zones.Count, graph.vertices.Count(v => v.kind == ForestMapGraph.VertexKind.Zone));
            Assert.AreEqual(m.lairs.Count, graph.vertices.Count(v => v.kind == ForestMapGraph.VertexKind.Lair));
            Assert.IsTrue(graph.vertices.All(v => v.kind != ForestMapGraph.VertexKind.Zone || v.zone >= 0), "вершина зоны — в своей зоне");
            Assert.AreEqual(m.hash, graph.mapHash, "граф помнит, с какой карты он снят");
            yield break;
        }

        [UnityTest]
        public IEnumerator Graph_EdgesCarryFacts()
        {
            Assert.Greater(graph.edges.Count, graph.vertices.Count, "рёбер больше, чем вершин");
            var at = graph.vertices.ToDictionary(v => v.id, v => new Vector2(v.x, v.z));
            float snap = ForestMapGraph.SnapCells * raster.step * 1.5f;
            foreach (var e in graph.edges)
            {
                float straight = Vector2.Distance(at[e.a], at[e.b]);
                Assert.GreaterOrEqual(e.length, straight - 2f * snap, $"{e.a}–{e.b}: путь не короче прямой");
                Assert.LessOrEqual(e.maxSlope, raster.wallMinDegrees + 1e-3f, $"{e.a}–{e.b}: по стене не ходят");
                Assert.AreEqual(e.waterLength > 0f, e.crossesWater, $"{e.a}–{e.b}: переправа = длина по воде");
            }
            var fields = typeof(ForestMapGraph.Edge).GetFields().Select(f => f.Name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "a", "b", "length", "maxSlope", "crossesWater", "waterLength", "surface" }, fields,
                "ребро — только факты ландшафта, весов видов нет");
            yield break;
        }

        /// <summary>
        /// Через воду — только если сухого пути нет (найдено 18.11: ребро логово–водопой переплывало озеро 151 м, хотя
        /// берегом можно обойти; в игре в озере не плавают — навмеш воду обходит). Иначе граф прячет сухой обход.
        /// </summary>
        [UnityTest]
        public IEnumerator Graph_WetEdgeOnlyWhenNoDryPath()
        {
            var dry = new ForestMapRaster
            {
                step = raster.step, origin = raster.origin, n = raster.n, height = raster.height, slopeDeg = raster.slopeDeg,
                cls = raster.cls.Select(c => c == ForestMapRaster.Cls.Water ? ForestMapRaster.Cls.Wall : c).ToArray(),
                surface = raster.surface, key = raster.key, walkMaxDegrees = raster.walkMaxDegrees, wallMinDegrees = raster.wallMinDegrees,
            };
            var dryGraph = ForestMapGraph.Build(jsonMap, dry);
            var dryPairs = dryGraph.edges.Select(e => e.a + "|" + e.b).ToHashSet();
            foreach (var e in graph.edges.Where(e => e.crossesWater))
                Assert.IsFalse(dryPairs.Contains(e.a + "|" + e.b), $"{e.a}–{e.b}: идёт по воде {e.waterLength:0} м, хотя есть сухой путь");
            yield break;
        }

        /// <summary>
        /// Связность — факт растра, а не соседства по прямой (найдено 18.11 рядом сидов: на сиде 17 от входа по графу
        /// достижимо 7 вершин из 22, хотя по растру — все). Вершина, до которой по растру дойти можно, достижима и по графу.
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator Graph_ReachabilityEqualsRaster_Seed17()
        {
            var h = new GameObject("~Seed17Graph");
            h.SetActive(false);
            var b = ForestProfiles.PavilionV2Zone(h, 17L);
            b.buildOnAwake = false;
            h.SetActive(true);
            try
            {
                b.Build();
                Assume.That(b.Map, Is.Not.Null, "у мира сида 17 есть карта");
                var r = ForestMapRaster.Build(b.Map);
                var g = ForestMapGraph.Build(b.Map, r);
                var truth = ForestMapGraph.RasterReachable(b.Map, r, g, "entry");
                var byGraph = g.Reachable("entry");
                CollectionAssert.AreEquivalent(truth, byGraph, "по графу достижимо ровно то, что достижимо по растру");
            }
            finally { b.Clear(); Object.Destroy(h); }
            yield break;
        }

        [UnityTest]
        public IEnumerator Graph_EntryReachesLab_AndIsDeterministic()
        {
            var reach = graph.Reachable("entry");
            Assert.IsTrue(reach.Contains("lab"), "от входа до лаборатории есть путь");
            var again = ForestMapGraph.Build(jsonMap, raster);
            Assert.AreEqual(graph.ToJson(), again.ToJson(), "граф детерминирован");
            yield break;
        }

        [UnityTest]
        public IEnumerator Painter_NoSceneObjects()
        {
            int before = Object.FindObjectsByType<Transform>().Length;
            var tex = ForestMapPainter.Paint(jsonMap, raster, graph);
            int after = Object.FindObjectsByType<Transform>().Length;
            Assert.AreEqual(raster.n, tex.width);
            Assert.AreEqual(before, after, "картинка карты не создаёт объектов сцены");
            Object.Destroy(tex);
            yield break;
        }

        /// <summary>
        /// РЯД СИДОВ (спека §7): карты павильона v2 на сидах 1–20 (те же, что у G6) — сетка 4×5, по 250 px. Для выбора сида и
        /// приёмки на глаз. До шага 5б карта — снимок мира, поэтому каждый сид собирает мир (≈8 с). Съёмка, не сторож.
        /// </summary>
        [UnityTest, Explicit("съёмка ряда — вручную, ≈3 минуты"), Timeout(1200000)]
        public IEnumerator Write_SeedRow_4x5()
        {
            const int cell = 250, cols = 4, rows = 5;
            var sheet = new Texture2D(cell * cols, cell * rows, TextureFormat.RGB24, false);
            var sb = new StringBuilder();
            sb.AppendLine("# Лес: ряд сидов — карты павильона v2");
            sb.AppendLine();
            sb.AppendLine("**Генерируемый отчёт** — `ForestMapGraphTests.Write_SeedRow_4x5` ([Explicit]); руками не правится. Картинка — `КАРТЫ-СИДЫ.png`,");
            sb.AppendLine("сид 1 — верхний левый, по строкам слева направо. Профиль `PavilionV2Zone`.");
            sb.AppendLine();
            sb.AppendLine("| сид | суша, км² | логов | вершин | рёбер | от входа достижимо | рёбер через воду |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            for (int seed = 1; seed <= cols * rows; seed++)
            {
                var h = new GameObject("~SeedRow" + seed);
                h.SetActive(false);
                var b = ForestProfiles.PavilionV2Zone(h, seed);
                b.buildOnAwake = false;
                h.SetActive(true);
                b.Build();
                yield return null;
                if (b.Map == null) { sb.AppendLine($"| {seed} | — карты нет (см. трассу) | | | | | |"); b.Clear(); Object.Destroy(h); continue; }
                var r = ForestMapRaster.Build(b.Map);
                var g = ForestMapGraph.Build(b.Map, r);
                var tex = ForestMapPainter.Paint(b.Map, r, g);
                int col = (seed - 1) % cols, row = (seed - 1) / cols;
                for (int y = 0; y < cell; y++)
                    for (int x = 0; x < cell; x++)
                        sheet.SetPixel(col * cell + x, (rows - 1 - row) * cell + y, tex.GetPixelBilinear((x + 0.5f) / cell, (y + 0.5f) / cell));
                sb.AppendLine($"| {seed} | {b.Map.areas.landKm2:0.00} | {b.Map.lairs.Count} | {g.vertices.Count} | {g.edges.Count} | " +
                              $"{g.Reachable("entry").Count}/{g.vertices.Count} | {g.edges.Count(e => e.crossesWater)} |");
                Object.Destroy(tex);
                b.Clear();
                Object.Destroy(h);
                yield return null;
            }
            sheet.Apply();
            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs", "Диаграммы", "Лес");
            File.WriteAllBytes(Path.Combine(dir, "КАРТЫ-СИДЫ.png"), sheet.EncodeToPNG());
            File.WriteAllText(Path.Combine(dir, "КАРТЫ-СИДЫ.md"), sb.ToString(), new UTF8Encoding(false));
            Object.Destroy(sheet);
        }

        /// <summary>Картинка и сводка графа павильона v2 — в `Docs/Диаграммы/Лес/` (съёмка, не сторож).</summary>
        [UnityTest, Explicit("съёмка карты — вручную")]
        public IEnumerator Write_MapPicture_AndGraphSummary()
        {
            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs", "Диаграммы", "Лес");
            var tex = ForestMapPainter.Paint(jsonMap, raster, graph);
            File.WriteAllBytes(Path.Combine(dir, "КАРТА.png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            var sb = new StringBuilder();
            sb.AppendLine("# Лес: карта павильона v2 — граф путей");
            sb.AppendLine();
            sb.AppendLine("**Генерируемый отчёт** — `ForestMapGraphTests.Write_MapPicture_AndGraphSummary` ([Explicit]); руками не правится.");
            sb.AppendLine($"Профиль `PavilionV2Zone`, сид 1337, карта `{jsonMap.hash}`, растр `{raster.key}` ({raster.n}×{raster.n}). Картинка — `КАРТА.png`.");
            sb.AppendLine();
            sb.AppendLine($"Площадь внутри кольца {jsonMap.areas.totalKm2:0.00} км², суши {jsonMap.areas.landKm2:0.00} км². Вершин {graph.vertices.Count}, рёбер {graph.edges.Count}; " +
                          $"от входа достижимо {graph.Reachable("entry").Count} из {graph.vertices.Count}.");
            sb.AppendLine();
            sb.AppendLine("| ребро | длина, м | прямая, м | макс. уклон, ° | переправа, м | поверхность |");
            sb.AppendLine("|---|---|---|---|---|---|");
            var at = graph.vertices.ToDictionary(v => v.id, v => new Vector2(v.x, v.z));
            foreach (var e in graph.edges)
                sb.AppendLine($"| {e.a}–{e.b} | {e.length:0} | {Vector2.Distance(at[e.a], at[e.b]):0} | {e.maxSlope:0.0} | " +
                              $"{(e.crossesWater ? e.waterLength.ToString("0") : "—")} | {e.surface} |");
            File.WriteAllText(Path.Combine(dir, "КАРТА.md"), sb.ToString(), new UTF8Encoding(false));
            yield break;
        }
    }
}
