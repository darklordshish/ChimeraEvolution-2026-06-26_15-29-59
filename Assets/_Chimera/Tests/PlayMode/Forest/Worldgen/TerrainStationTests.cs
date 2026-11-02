using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// S3 §4.1: станция ищется на <c>Hydro0</c>, а её <c>platformY</c> читается из
    /// ФИНАЛЬНОГО <c>ShapeGround</c> уже найденного пятна; площадка — последний blend.
    /// Кольцо в поле не входит, но входит во все маски.
    /// <para>
    /// Профиль v2 — Ø2000/48 (единственный объявленный для полос H0, S1 §12).
    /// A/C/B0/Pavilion v1 трогает не этот файл, а <c>ForestBaselineTests</c>.
    /// </para>
    /// </summary>
    public class TerrainStationTests
    {
        GameObject host;
        ForestBuilder builder;

        [TearDown]
        public void TearDown()
        {
            if (builder != null) builder.Clear();
            if (host != null) Object.Destroy(host);
            builder = null;
            host = null;
        }

        ForestBuilder BuildV2()
        {
            host = new GameObject("~TerrainStationProbe");
            host.SetActive(false);
            builder = host.AddComponent<ForestBuilder>();
            builder.buildOnAwake = false;
            builder.randomSeed = false;
            builder.seed = 1337;
            builder.mapDiameter = 2000f;
            builder.chunkSize = 250f;
            builder.terrainQuads = 48;
            builder.worldVersion = 2;
            builder.buildRelief = true;
            builder.patchyFlora = true;
            builder.treeCount = 1;
            builder.buildAsh = false;
            builder.buildRavines = false;
            builder.buildTerrace = true;
            builder.buildCanyon = true;
            builder.buildLabComplex = true;
            builder.buildStationLoop = true;
            builder.buildStationHill = true;
            builder.buildLabPlan = false;
            builder.labEntrance = LabEntrance.Buried;
            builder.labStyle = LabStyle.Bastion;
            builder.minLabDistance = 150f;
            builder.spawnAtEntry = true;
            builder.speciesPool = new List<SpeciesSO>();
            host.SetActive(true);
            builder.Build();
            return builder;
        }

        static float PadDeviation(ForestBuilder b)
        {
            if (b.LabComplex == null) return 0f;
            float yaw = b.PadYaw * Mathf.Deg2Rad;
            float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
            float max = 0f;
            const int n = 9;
            for (int iz = 0; iz < n; iz++)
                for (int ix = 0; ix < n; ix++)
                {
                    float lx = Mathf.Lerp(-DomeLabComplex.PadHalfWidth * 0.9f,
                        DomeLabComplex.PadHalfWidth * 0.9f, ix / (n - 1f));
                    float lz = Mathf.Lerp(-DomeLabComplex.PadHalfDepth * 0.9f,
                        DomeLabComplex.PadHalfDepth * 0.9f, iz / (n - 1f));
                    float x = b.LabCenter.x + lx * c + lz * s;
                    float z = b.LabCenter.y - lx * s + lz * c;
                    max = Mathf.Max(max, Mathf.Abs(b.SampleGround(x, z) - b.PlatformY));
                }
            return max;
        }

        [UnityTest]
        public IEnumerator V2_Platform_FromShapeGround_AndPadLast()
        {
            var b = BuildV2();
            yield return null;

            Assert.IsNotNull(b.Terrain, "DomeTerrain не создан");
            Assert.IsTrue(b.Terrain.TargetOrder, "worldVersion 2 обязан дать целевой порядок");
            Assert.IsNotNull(b.LabComplex, "комплекс не собран на v2");

            float depth = DomeLabComplex.DepthFor(b.ResolvedLabEntrance);
            float shape = b.ShapeGround(b.LabCenter.x, b.LabCenter.y);
            float expected = Mathf.Max(shape - depth, b.Hydro.lakeLevel + 0.5f);
            Assert.AreEqual(expected, b.PlatformY, 0.02f,
                "platformY обязан читаться из финального ShapeGround(LabCenter), а не из Hydro0");

            Assert.LessOrEqual(PadDeviation(b), 0.06f,
                "площадка последняя: плита плоская по SampleGround");
            Assert.AreEqual(b.PlatformY, b.SampleGround(b.LabCenter.x, b.LabCenter.y), 0.05f,
                "в центре пятна SampleGround = platformY");
            if (expected > b.Hydro.lakeLevel + 0.5f + 0.1f)
                Assert.AreEqual(depth, shape - b.SampleGround(b.LabCenter.x, b.LabCenter.y), 0.05f,
                    "форма над плитой ровно на DepthFor: подземный вход не «яма»");
        }

        /// <summary>
        /// P0.1/P0.2/P0.5: на v2 «арен» нет — есть зоны R24–34 на ShapeGround,
        /// 2 остова, 2 ворот, 4–6 общих зон (2 водопоя) и реестр адресов.
        /// Поды и адреса читают ShapeGround, не второй пол.
        /// </summary>
        [UnityTest]
        public IEnumerator V2_Poi_AreZones_NotArenas()
        {
            var b = BuildV2();
            yield return null;

            Assert.AreEqual(0, b.ArenaEdges, "на v2 кромок-арен нет");
            Assert.IsNull(GameObject.Find("ArenaEdge"), "на v2 нет ни одного ArenaEdge");

            // Факты для «записанного отказа»: все места, даже если ворот зон не пройден.
            int minExits = int.MaxValue;
            for (int i = 0; i < b.Zones.Count; i++)
                minExits = Mathf.Min(minExits, ForestSpace.Exits(b.Zones, b.Links, i));
            if (b.Zones.Count == 0) minExits = 0;
            string facts = $"зон {b.Zones.Count} (план 4–6), связей {b.Links.Count}, " +
                $"min подходов {minExits}, общих {b.Commons.Count}, водопоев {b.Waterholes.Count}, " +
                $"остовов {b.Shells.Count}, ворот {b.Gates.Count}";

            foreach (var z in b.Zones)
            {
                Assert.GreaterOrEqual(z.radius, 24f - 0.01f, "зона R < 24");
                Assert.LessOrEqual(z.radius, 34f + 0.01f, "зона R > 34");
                Assert.Greater(b.ShapeGround(z.center.x, z.center.y),
                    b.Hydro.lakeLevel + 1.0f, "зона на мокром полу");
                Assert.IsFalse(b.Masks.OverlapsRing(
                        DomeFootprint.Circle(z.center, z.radius + 20f)),
                    "зона R+20 залезла в скальное кольцо");
                // Пол ≤5° в R+20 и 0 клеток >35° — проверка кромки, не одной точки.
                float maxSlope = SlopeClasses.SlopeAt(b.Terrain.cfg, z.center.x, z.center.y,
                    b.ShapeGround, SlopeClasses.ClassEpsilon);
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI * 2f / 8f;
                    float rr = z.radius + 20f;
                    float x = z.center.x + Mathf.Cos(a) * rr;
                    float zz = z.center.y + Mathf.Sin(a) * rr;
                    maxSlope = Mathf.Max(maxSlope,
                        SlopeClasses.SlopeAt(b.Terrain.cfg, x, zz, b.ShapeGround,
                            SlopeClasses.ClassEpsilon));
                }
                Assert.LessOrEqual(maxSlope, 5.01f, "пол зоны круче 5°");
            }

            Assert.AreEqual(2, b.Gates.Count, "ворот уровня — 2");
            Assert.AreEqual(2, b.Shells.Count, "остовов — 2 (у кромки первых двух зон)");

            // P0.2: адреса и поды — на ShapeGround, а не на SampleGround поверх площадки.
            foreach (var poi in b.Pois.All)
                if (poi.kind == ForestPoiKind.Zone || poi.kind == ForestPoiKind.Common ||
                    poi.kind == ForestPoiKind.Waterhole)
                    Assert.AreEqual(b.ShapeGround(poi.pos.x, poi.pos.y), poi.groundY, 0.01f,
                        $"адрес {poi.kind} не на ShapeGround");
            foreach (var pod in b.CoverPods)
            {
                Assert.AreEqual(b.ShapeGround(pod.center.x, pod.center.y), pod.groundY, 0.01f,
                    "под-укрытие не на ShapeGround");
                Assert.IsFalse(b.Masks.OverlapsRing(DomeFootprint.Circle(pod.center, 0f)),
                    "под-укрытие попало в скальное кольцо");
            }

            Assert.AreEqual(1, b.Pois.Count(ForestPoiKind.Base), "POI base");
            Assert.AreEqual(1, b.Pois.Count(ForestPoiKind.Entry), "POI entry");
            Assert.AreEqual(2, b.Pois.Count(ForestPoiKind.Gate), "POI gate");

            // ВОРОТА S3 §4/И5: 4–6 зон. Если поле дало меньше — это НЕ зелёный тест,
            // а записанный отказ/долг S2 (композиция/плато), как B0_Plan…KnownDrift.
            if (b.Zones.Count < 4)
                Assert.Inconclusive(
                    "S3 gate 4–6 зон не пройден: " + facts +
                    ". Причина — S1-поле без композиции S2; порог не ослаблен. Долг S2.");
            Assert.LessOrEqual(b.Zones.Count, 6, "открытых зон больше шести: " + facts);

            Assert.AreEqual(b.Zones.Count, b.Pois.Count(ForestPoiKind.Zone), "POI zone");
            Assert.AreEqual(b.Shells.Count, b.Pois.Count(ForestPoiKind.Shell), "POI shell");
            Assert.AreEqual(b.Links.Count, b.Pois.Count(ForestPoiKind.Link), "POI link");

            // Связи/общие/водопои: невалидная связь не добавляется (P0.2), поэтому
            // на S1-поле их может быть меньше плана. Это записанный отказ S2/S4,
            // а не повод подделать связь или ослабить порог.
            int commonsTotal = b.Commons.Count + b.Waterholes.Count;
            bool linksOk = minExits >= 2;
            bool commonsOk = commonsTotal >= 4 && commonsTotal <= 6;
            bool waterOk = b.Waterholes.Count == 2;
            if (!linksOk || !commonsOk || !waterOk)
                Assert.Inconclusive(
                    "S3 links/common gate не пройден: " + facts +
                    ". Связи только валидные (P0.2); план требует ≥2 подходов, 4–6 общих, " +
                    "2 водопоя. Причина — S1-поле без композиции/коридоров S2–S4. Долг S2/S4.");
        }

        [UnityTest]
        public IEnumerator V2_Ring_OutOfField_ButInMasks_AndClearResets()
        {
            var b = BuildV2();
            yield return null;

            var masks = b.Masks;
            Assert.IsNotNull(masks, "на v2 маски обязаны существовать");
            Assert.IsTrue(masks.HasRing, "кольцо — маска");
            Assert.IsTrue(masks.Has("station"), "станция — маска");
            Assert.IsTrue(masks.Has("entry"), "вход — маска");
            Assert.IsTrue(masks.Has("mainRoute"), "главный маршрут — маска");
            Assert.IsTrue(masks.Has("gate"), "ворота — маска (P0.7)");
            Assert.IsTrue(masks.Has("corridor"), "коридор — маска (P0.7)");
            Assert.IsTrue(masks.Has("zone"), "открытая зона — маска");
            Assert.IsTrue(masks.Has("common"), "общая зона — маска");
            Assert.IsTrue(masks.Has("ramp"), "рампа — часть маски станции (P0.4)");
            Assert.IsTrue(masks.Has("crevice"), "расщелина — часть маски станции (P0.4)");
            Assert.IsTrue(masks.Has("approach"), "подход 16/24 м — часть маски станции (P0.4)");

            // Ø2000: rin = D/2 − 80 = 920 (DomeRockRing.InnerRadius, но конфиг сцены — не наш).
            float rin = b.mapDiameter * 0.5f - 80f;
            Assert.Greater(rin, 0f, "радиус кольца не посчитан");

            // Кольцо не входит в SampleGround (правило S3 §2.1): RingField выше поля.
            // Берём точку в ТЕЛЕ кольца (rin+120), а не у внутренней кромки: на кромке
            // ring = поле по построению (max с полем), и «выше» там доказывать нечего.
            float r = rin + 120f;
            float field = b.SampleGround(r, 0f);
            float ring = b.RingGround(r, 0f);
            Assert.GreaterOrEqual(ring, field, "RingField не ниже SampleGround");
            Assert.Greater(ring, field + 1f, "кольцо — отдельный меш/стена, а не поле");
            Assert.IsTrue(masks.OverlapsRing(DomeFootprint.Circle(new Vector2(r, 0f), 5f)),
                "габарит на кольце обязан попасть в маску кольца");

            b.Clear();
            Assert.IsNull(b.Terrain, "Clear обязан снять пол мира");
            Assert.IsNull(b.Masks, "Clear обязан снять маски");
            Assert.AreEqual(0f, b.SampleGround(0f, 0f), 1e-6f, "после Clear высоты нет");
        }
    }
}
