using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// S5 шаг 1: проверяется ИЗМЕРИТЕЛЬ на синтетике, а не мир. Пока ворота стоят на
    /// непроверенном инструменте, «зелёный» ничего не значит: детектор может мерить
    /// неизвестно что. Числа порогов здесь — S1/S2/S4/решения 01.11; S5 владеет
    /// только M3b ≥60 % и M14 (пятна), и они проверяются здесь как инварианты.
    /// </summary>
    public class ForestLandscapeMetricsTests
    {
        readonly List<DomeGenConfigSO> made = new List<DomeGenConfigSO>();

        [TearDown]
        public void TearDown()
        {
            foreach (var cfg in made)
                if (cfg != null) UnityEngine.Object.DestroyImmediate(cfg);
            made.Clear();
        }

        DomeGenConfigSO Config(int worldVersion = 2, float diameter = 2000f)
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.seed = 1337;
            cfg.randomSeed = false;
            cfg.mapDiameter = diameter;
            cfg.chunkSize = diameter / 8f;
            cfg.worldVersion = worldVersion;
            made.Add(cfg);
            return cfg;
        }

        static float Radius(DomeGenConfigSO cfg) => DomeRockRing.InnerRadius(cfg);

        // ------------------------------------------------------------------
        // M1: плоскость, три класса, монотонность и совместность пары
        // ------------------------------------------------------------------

        [Test]
        public void Plane_AllMetricsAreZero()
        {
            var cfg = Config();
            float r = Radius(cfg);
            Func<float, float, float> plane = (x, z) => 0f;

            var slope = ForestLandscapeMetrics.Slope(plane, cfg, r, 64, 5.2f);
            Assert.AreEqual(0f, slope.p50Field, 1e-6f, "плоскость: p50");
            Assert.AreEqual(0f, slope.p95Field, 1e-6f, "плоскость: p95");
            Assert.AreEqual(0f, slope.p99Field, 1e-6f, "плоскость: p99");
            Assert.AreEqual(1f, slope.passableField, 1e-6f, "плоскость: всё проходимо");
            Assert.AreEqual(0f, slope.climbableField, 1e-6f, "плоскость: лазаемого нет");
            Assert.AreEqual(0f, slope.wallField, 1e-6f, "плоскость: стен нет");
            Assert.IsTrue(slope.threeClassesDistinct, "три класса разбивают семплы");
            Assert.IsTrue(slope.pairCompatible, "плоскость совместна");

            Assert.AreEqual(0f, ForestLandscapeMetrics.LongEnergy(plane, r, 64), 1e-6f,
                "плоскость: E_λ");
            Assert.AreEqual(0f, ForestLandscapeMetrics.MesoCoverage(plane, r, 64,
                out int windows, out int good), 1e-6f, "плоскость: мезо");
            Assert.AreEqual(0, good, "плоскость: годных окон нет");
            Assert.Greater(windows, 0, "окна вообще считались");
            Assert.AreEqual(0f, ForestLandscapeMetrics.LocalRelief(plane, r, 96), 1e-6f,
                "плоскость: локальный охват");
        }

        [Test]
        public void ThreeClasses_DoNotMix()
        {
            var cfg = Config();
            float r = Radius(cfg);
            float tan30 = Mathf.Tan(30f * Mathf.Deg2Rad);
            float tan45 = Mathf.Tan(45f * Mathf.Deg2Rad);

            var ramp = ForestLandscapeMetrics.Slope((x, z) => x * tan30, cfg, r, 64, 5.2f);
            Assert.Greater(ramp.shareClimbableForTest(), 0.95f, "рампа 30° — лазаемая");
            Assert.Less(ramp.wallField, 0.01f, "рампа 30° не стена");

            var wall = ForestLandscapeMetrics.Slope((x, z) => x * tan45, cfg, r, 64, 5.2f);
            Assert.Greater(wall.wallField, 0.95f, "45° — стена");
            Assert.Less(wall.climbableField, 0.01f, "45° не лазаемая");
        }

        [Test]
        public void Shares_AreMonotonic_AndPairIsCompatible()
        {
            var cfg = Config();
            float r = Radius(cfg);
            var hill = DomeForms.Disc(Vector2.zero, 600f, 30f);
            Func<float, float, float> field = (x, z) => hill.Apply(0f, x, z);
            var slopes = ForestLandscapeMetrics.SlopeSamples(field, cfg, r, 96,
                SlopeClasses.ClassEpsilon);
            Assert.Greater(slopes.Count, 0, "семплы есть");
            float prev = 1f;
            foreach (float t in new[] { 5f, 10f, 15f, 20f, 25f, 30f })
            {
                float share = ForestLandscapeMetrics.ShareAbove(slopes, t);
                Assert.LessOrEqual(share, prev + 1e-6f,
                    $"доля >{t}° выросла: доля выше порога не может расти с порогом");
                prev = share;
            }
            float p95 = ForestLandscapeMetrics.Percentile(slopes, 0.95f);
            if (p95 <= 20f)
                Assert.LessOrEqual(ForestLandscapeMetrics.ShareAbove(slopes, 22f), 0.05f + 1e-6f,
                    "при p95 ≤ 20° доля >22° обязана быть ≤ 5 % (S5 §3.1 п. 4)");
        }

        // ------------------------------------------------------------------
        // M2–M4: энергия, мезо, аспект, баланс
        // ------------------------------------------------------------------

        [Test]
        public void LongWave2000_HasLongEnergy()
        {
            var cfg = Config();
            float r = Radius(cfg);
            Func<float, float, float> sine = (x, z) => 10f * Mathf.Sin(2f * Mathf.PI * x / 2000f);
            float e = ForestLandscapeMetrics.LongEnergy(sine, r, 160);
            Assert.GreaterOrEqual(e, 0.60f, $"E_λ={e:P1} < 60 %: окно 250 м не держит волну 2000 м");
        }

        /// <summary>
        /// План S5 шаг 1 обещал «синус λ = 500 → E_λ ≈ 1». Это арифметически неверно для
        /// ОПРЕДЕЛЕНИЯ M2: `Var(box250)/Var` у синуса даёт `(sinc(π/2))² ≈ 0.405`.
        /// Порог 0.60 относится к H0 (макро+мезо), а не к чистой волне 500 м. Держим
        /// расхождение явным тестом, а не подгоняем порог под обещание плана.
        /// </summary>
        [Test]
        public void Sine500_Box250_IsAboutFortyPercent()
        {
            var cfg = Config();
            float r = Radius(cfg);
            Func<float, float, float> sine = (x, z) => 10f * Mathf.Sin(2f * Mathf.PI * x / 500f);
            float e = ForestLandscapeMetrics.LongEnergy(sine, r, 160);
            Assert.AreEqual(0.405f, e, 0.05f,
                "M2 = Var(box250)/Var: для λ=500 окно 250 м обязано дать ≈0.405");
            Assert.Less(e, 0.60f,
                "чистая λ=500 не проходит порог 0.60 — это факт формулы, а не красное мира");
        }

        [Test]
        public void WhiteNoise_HasNoLongEnergy()
        {
            var cfg = Config();
            float r = Radius(cfg);
            Func<float, float, float> noise = (x, z) =>
                SeededHash.ToFloat01(SeededHash.Hash(1337, 11,
                    Mathf.RoundToInt(x * 10f) * 7919 + Mathf.RoundToInt(z * 10f))) - 0.5f;
            Assert.LessOrEqual(ForestLandscapeMetrics.LongEnergy(noise, r, 160), 0.20f,
                "белый шум не должен держать окно 250 м");
        }

        [Test]
        public void Comb40_HasNoLongEnergy_AndSeamGrows()
        {
            var cfg = Config();
            float r = Radius(cfg);
            Func<float, float, float> comb = (x, z) => 2f * Mathf.Sin(2f * Mathf.PI * x / 40f);
            Assert.LessOrEqual(ForestLandscapeMetrics.LongEnergy(comb, r, 160), 0.10f,
                "гребёнка λ=40 м не макро-энергия");
            var seam = ForestLandscapeMetrics.Seams(comb, cfg, r, null, 8, 1f);
            Assert.Greater(seam.maxSlopeJump, 0.5f,
                "гребёнка даёт излом поля — измеритель его видит");
        }

        [Test]
        public void Hill_HasKnownAspect_AndRadius()
        {
            var cfg = Config();
            var hill = DomeForms.Disc(Vector2.zero, 100f, 9f);
            Func<float, float, float> field = (x, z) => hill.Apply(0f, x, z);
            var forms = new List<DomeForm> { hill };
            var info = ForestLandscapeMetrics.Landforms(forms);
            Assert.AreEqual(1, info.Count, "одна форма");
            Assert.AreEqual(0.09f, info[0].aspect, 0.002f,
                "H/D = H/(D − бровка) = 9/100");
            Assert.LessOrEqual(info[0].maxSlope, 19.8f + 0.1f, "холм ≤ 19.8°");
            Assert.GreaterOrEqual(info[0].maxSlope, 15f, "холм не плоский");

            float numeric = ForestLandscapeMetrics.MaxFormSlope(field, cfg, forms);
            Assert.LessOrEqual(numeric, info[0].maxSlope + 0.5f,
                "числовой max не выше аналитического + 0.5°");
            Assert.Greater(numeric, 15f, "числовой max видит форму");

            var seam = ForestLandscapeMetrics.Seams(field, cfg, Radius(cfg), forms);
            Assert.GreaterOrEqual(seam.minCurvatureRadius, 5f,
                "R = run²/(6H) ≥ 5 м");
        }

        [Test]
        public void Ridge35x4_2_IsWalkable_NotWall()
        {
            var cfg = Config();
            float r = Radius(cfg);
            var ridge = DomeForms.Capsule(Vector2.zero, Vector2.right, 100f, 35f, 4.2f);
            Func<float, float, float> field = (x, z) => ridge.Apply(0f, x, z);
            var forms = new List<DomeForm> { ridge };
            var slope = ForestLandscapeMetrics.Slope(field, cfg, r, 96, 5.2f, forms);
            Assert.LessOrEqual(slope.maxGradPerForm, 19.8f + 0.5f,
                $"гряда W=35 H=4.2 даёт {slope.maxGradPerForm:F1}°, а не 70°");
            Assert.Less(slope.wallField, 0.01f, "гряда не стена");
        }

        [Test]
        public void Balance_UsesVolume_NotHeightSum()
        {
            var forms = new List<DomeForm>
            {
                DomeForms.Disc(new Vector2(-420f, 0f), 300f, 20f, DomeFormSign.Lift, "P"),
                DomeForms.Disc(new Vector2(420f, 0f), 300f, 20f, DomeFormSign.Carve, "N"),
            };
            var balance = ForestLandscapeMetrics.Balance(forms);
            Assert.IsTrue(balance.measurable, "обе стороны измеримы");
            Assert.AreEqual(1f, balance.ratio, 0.02f,
                "равные по объёму P и N дают 1.00; Σ высот дала бы другое число");
        }

        // ------------------------------------------------------------------
        // M3b, M13
        // ------------------------------------------------------------------

        [Test]
        public void LocalRelief_ReturnsShareOfDisk()
        {
            var cfg = Config();
            float r = Radius(cfg);
            var a = DomeForms.Disc(new Vector2(-300f, 0f), 80f, 6f);
            var b = DomeForms.Disc(new Vector2(300f, 0f), 80f, 6f);
            Func<float, float, float> field = (x, z) => a.Apply(b.Apply(0f, x, z), x, z);
            float share = ForestLandscapeMetrics.LocalRelief(field, r, 128, 40f, 3f);
            Assert.Greater(share, 0f, "холмы дают локальный перепад");
            Assert.LessOrEqual(share, 1f, "доля не больше диска");
            // Два диска D=80 на карте R≈920 — это ~0.38 % площади; окно 40 м ловит
            // только их окрестность. Ожидание «>1 %» было ошибкой теста, не метрики.
            Assert.Greater(share, 0.001f, "пятно холмов заметно на диске");
            Assert.Less(share, 0.05f, "доля не раздувается до всего диска");
        }

        [Test]
        public void Climb_SeesBandAndBypass()
        {
            var cfg = Config();
            float r = Radius(cfg);
            Func<float, float, float> plane = (x, z) => 0f;
            var climb = ForestLandscapeMetrics.Climb(plane, cfg, r, 64,
                new Vector2(-r * 0.8f, 0f), new Vector2(r * 0.8f, 0f));
            Assert.Greater(climb.shareClimbable, -1e-6f, "доля 22–35° посчитана");
            Assert.IsTrue(climb.bypass, "на плоскости обход есть");
            Assert.AreEqual(0, climb.ledgesHigh, "на плоскости нет уступов > 0.5 м");
        }

        // ------------------------------------------------------------------
        // M14, M15, M16
        // ------------------------------------------------------------------

        [Test]
        public void Patches_LocalDensityDiffersFromGlobal()
        {
            var cfg = Config();
            float r = Radius(cfg);
            var trunks = new List<Vector3>();
            for (int i = 0; i < 60; i++)
                trunks.Add(new Vector3(i % 10 * 8f, 0f, i / 10 * 8f)); // 60 в одной ячейке 100 м
            for (int i = 0; i < 40; i++)
                trunks.Add(new Vector3(-600f + i * 20f, 0f, 600f));
            var patch = ForestLandscapeMetrics.Patches(trunks, r, null, null, null,
                (x, z) => 0f, cfg, 0.10f);
            Assert.GreaterOrEqual(patch.patchCount, 1, "пятно найдено");
            Assert.Greater(patch.localDensityPerKm2, patch.globalDensityPerKm2,
                "локальная плотность выше глобальной — единица «внутри пятна» работает");
            Assert.GreaterOrEqual(patch.localDensityPerKm2, 2000f, "плотное пятно выше рабочего порога");
            Assert.AreEqual(0.10f, patch.crownCover, 1e-6f, "крона — отчёт");
        }

        [Test]
        public void Roads_KnownPolyline_ComponentsAndSlope()
        {
            var pois = new ForestPoiRegistry();
            pois.Add(ForestPoiKind.Base, "base", new Vector2(-400f, 0f), 20f, 0f, 0f);
            pois.Add(ForestPoiKind.Gate, "gate", new Vector2(0f, 0f), 8f, 0f, 0f);
            pois.Add(ForestPoiKind.Zone, "zone", new Vector2(400f, 0f), 30f, 0f, 0f);
            var net = new ForestRoadNetwork { loops = 1 };
            net.roads.Add(new ForestRoad
            {
                points = new List<Vector2>
                {
                    new Vector2(-400f, 0f), new Vector2(0f, 0f), new Vector2(400f, 0f),
                },
                width = 4f,
                primary = true,
            });
            net.length = 800f;
            var result = ForestLandscapeMetrics.Roads(net, pois.All,
                (x, z) => x * 0.05f, Config());
            Assert.IsTrue(result.built, "дорога построена");
            Assert.AreEqual(1, result.components, "все primary POI в одной компоненте");
            Assert.AreEqual(1, result.loops, "петля — отчёт сети");
            Assert.AreEqual(4f, result.widthP10, 1e-4f, "ширина primary");
            Assert.Greater(result.slopeP95, 0f, "уклон дороги считается");
        }

        [Test]
        public void Hydro_KeyIsStable_AndFallbackIsVisible()
        {
            var state = new DomeErosion.State
            {
                resolution = 512,
                erosionVersion = 2,
                hydraulicStatus = "baked",
                hydraulicSeconds = 12.5f,
                deltaSum = 0x1234UL,
            };
            var a = ForestLandscapeMetrics.Hydro(state, 1337L, 2);
            var b = ForestLandscapeMetrics.Hydro(state, 1337L, 2);
            Assert.AreEqual(a.key, b.key, "тот же ключ — тот же ассет");
            Assert.AreEqual("ok", a.status, "запечённый ассет — ok");
            Assert.AreEqual(12500f, a.bakeMs, 1e-2f, "время запекания в мс");

            state.hydraulicStatus = "missing: нет места";
            Assert.AreEqual("missing", ForestLandscapeMetrics.Hydro(state, 1337L, 2).status,
                "нет ассета — видимый missing, не ошибка");
            state.hydraulicStatus = "timeout";
            Assert.AreEqual("timeout", ForestLandscapeMetrics.Hydro(state, 1337L, 2).status,
                "таймаут виден");
            state.hydraulicStatus = "cache";
            Assert.AreEqual("ok", ForestLandscapeMetrics.Hydro(state, 1337L, 2).status,
                "повторная сборка из кеша — ok");
        }
    }

    static class LandscapeTestExtensions
    {
        /// <summary>Лазаемая доля в SlopeResult; отдельное имя, чтобы тест читался.</summary>
        public static float shareClimbableForTest(this ForestLandscapeMetrics.SlopeResult r)
            => r.climbableField;
    }
}
