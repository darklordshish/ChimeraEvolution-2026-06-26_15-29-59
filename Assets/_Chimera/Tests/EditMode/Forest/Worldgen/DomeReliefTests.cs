using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// S1 «поле и формы» (2026-11-01): детекторы полос <c>H0</c> v2, классов уклона и
    /// алгебры форм. Ни один из этих тестов не существовал до S1 — имена новые намеренно.
    /// <para>
    /// v1 не проверяется здесь: его хэши заморожены S0 (<c>BaselineFingerprintTests</c>).
    /// Всё ниже — только <c>worldVersion = 2</c>; A/C/B0 остаются на v1.
    /// </para>
    /// <para>
    /// Правки P0 кросс-ревью (S1 §12): zero-mean — смещение по 20 сидам; λ_min — только
    /// на объявленном профиле Ø2000/48; аспекты — синтетические (`SyntheticForm_Aspect_*`),
    /// а не карта (места — S2); C¹ — порог от кривизны формы; объёмы и P/N — на
    /// синтетическом списке форм, без площадки и станционного холма.
    /// </para>
    /// </summary>
    public class DomeReliefTests
    {
        readonly List<DomeGenConfigSO> made = new List<DomeGenConfigSO>();

        [TearDown]
        public void TearDown()
        {
            foreach (var cfg in made)
                if (cfg != null) UnityEngine.Object.DestroyImmediate(cfg);
            made.Clear();
        }

        DomeGenConfigSO Config(long seed, int worldVersion = 2, float diameter = 2000f)
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.seed = seed;
            cfg.randomSeed = false;
            cfg.mapDiameter = diameter;
            cfg.chunkSize = diameter / 8f;
            cfg.worldVersion = worldVersion;
            made.Add(cfg);
            return cfg;
        }

        static float Radius(DomeGenConfigSO cfg) => DomeRockRing.InnerRadius(cfg);

        static float[] Grid(DomeGenConfigSO cfg, int res, out float step, out float radius)
        {
            radius = Radius(cfg);
            step = 2f * radius / (res - 1f);
            var p = DomeHeightField.DomeHeightParams.From(cfg);
            var h = new float[res * res];
            for (int iz = 0; iz < res; iz++)
                for (int ix = 0; ix < res; ix++)
                    h[iz * res + ix] = DomeHeightField.SampleRaw(p, -radius + ix * step, -radius + iz * step);
            return h;
        }

        // ------------------------------------------------------------------
        // H0 v2: полосы, октавы, warp
        // ------------------------------------------------------------------

        [Test]
        public void Bands_EnergyAbove250_AtLeast60Percent()
        {
            var cfg = Config(1337);
            var h = Grid(cfg, 96, out float step, out float radius);
            double mean = Mean(h), varAll = Variance(h, mean);
            var box = BoxFilter(h, 96, Mathf.RoundToInt(125f / step));
            double varBox = Variance(box, Mean(box));
            float share = varAll > 1e-9 ? (float)(varBox / varAll) : 0f;
            Assert.GreaterOrEqual(share, 0.60f,
                $"энергия λ>250: {share:P1} < 60 % (S1 §6.2); макро+мезо не читаются");
            Assert.Greater(varAll, 1f, "поле v2 не должно быть плоским");
            Assert.Greater(radius, 100f, "стенд Ø2000");
        }

        [Test]
        public void Bands_NoOctaveBelowFourMeshSteps()
        {
            var cfg = Config(1337);
            // P0-6: v2 объявлен ТОЛЬКО на профиле Ø2000/48. На других разрешениях
            // λ_min < 4·meshStep по построению (B0: 9.0 < 16.7) — тест неприменим,
            // а не «красный»: подгонять порог под неподдерживаемый профиль нельзя.
            if (!SamplingContract.BandProfileSupported(cfg.mapDiameter, 48))
                Assert.Ignore($"профиль Ø{cfg.mapDiameter}/48 не объявлен для v2 (S1 §12, P0-6)");
            var c = SamplingContract.From(cfg, 48);
            Assert.GreaterOrEqual(c.lambdaMin, 4f * c.meshStep - 1e-3f,
                $"λ_min={c.lambdaMin:F2} < 4·meshStep={4f * c.meshStep:F2}: октава алиасит");
        }

        [Test]
        public void BandsProfile_DeclaredOnlyFor2000At48()
        {
            Assert.IsTrue(SamplingContract.BandProfileSupported(2000f, 48),
                "Ø2000/48 — объявленный профиль полос");
            Assert.IsFalse(SamplingContract.BandProfileSupported(2000f, 16),
                "A-профиль (16 квадов) не объявлен: λ_min 45 < 4·15.6 = 62.5");
            Assert.IsFalse(SamplingContract.BandProfileSupported(400f, 48),
                "Ø400 полосы не объявлены: масштаб D/2000 уводит λ_min под шаг меша");

            // Почему ворот не косметический: B0 (Ø400, chunk 100, 24 квада) реально алиасит.
            var b0 = Config(9, 2, 400f);
            b0.chunkSize = 100f;
            var cb = SamplingContract.From(b0, 24);
            Assert.Less(cb.lambdaMin, 4f * cb.meshStep,
                $"B0: λ_min={cb.lambdaMin:F2} обязан быть ниже 4·meshStep={4f * cb.meshStep:F2} — иначе ворот лишний");
        }

        /// <summary>
        /// P0-3: S0 заморозил `slopeEpsilon` как шаг выборки уклона; класс меряется
        /// отдельной константой `classEpsilon = 1 м`, а не подменой первого поля.
        /// </summary>
        [Test]
        public void Contract_ClassEpsilonIsOneMeter_SlopeStaysSampling()
        {
            var cfg = Config(1337);
            var c = SamplingContract.From(cfg, 48);
            Assert.AreEqual(1f, c.classEpsilon, 1e-6f, "единственная линейка класса — 1 м");
            Assert.AreEqual(c.meshStep * 0.5f, c.slopeEpsilon, 1e-6f,
                "slopeEpsilon — по-прежнему шаг выборки уклона (S0), не линейка класса");
        }

        [Test]
        public void Bands_SlopeBudget_SumBelowWall()
        {
            var cfg = Config(1337);
            float macro = DomeHeightField.BandSlopeBudget(cfg.macroAmplitude, cfg.macroLambda);
            float meso = DomeHeightField.BandSlopeBudget(cfg.mesoAmplitude, cfg.mesoLambda);
            float micro = DomeHeightField.BandSlopeBudget(cfg.microAmplitude, cfg.microLambda);
            Assert.LessOrEqual(macro, 0.19f + 1e-4f, "макро: πP/λ");
            Assert.LessOrEqual(meso, 0.30f + 1e-4f, "мезо: πP/λ (S1 §11.4)");
            Assert.LessOrEqual(micro, 0.21f + 1e-4f, "микро: πP/λ (S1 §11.4)");
            Assert.LessOrEqual(macro + meso + micro, 0.700f + 1e-4f,
                "Σ πP/λ — сумма полос не даёт tan 35°");
            Assert.GreaterOrEqual(cfg.macroAmplitude, 1.3f * cfg.mesoAmplitude,
                "P_macro ≥ 1.3·P_meso — иначе макро теряет долю дисперсии");
            Assert.LessOrEqual(cfg.mesoAmplitude, 0.0955f * cfg.mesoLambda + 1e-3f,
                "P_meso = 0.0955·λ");
            Assert.LessOrEqual(cfg.microAmplitude, 0.0668f * cfg.microLambda + 1e-3f,
                "P_micro = 0.0668·λ");
        }

        [Test]
        public void Heights_WithinBandEnvelope()
        {
            var cfg = Config(1337);
            var p = DomeHeightField.DomeHeightParams.From(cfg);
            float envelope = DomeHeightField.BandEnvelope(p);
            Assert.LessOrEqual(envelope, 63f + 1e-3f, "конверт H0 ≤ 63 м (S1 §11.4)");
            var h = Grid(cfg, 64, out _, out _);
            float max = 0f;
            foreach (float v in h) max = Mathf.Max(max, Mathf.Abs(v));
            Assert.LessOrEqual(max, envelope + 1e-3f,
                $"|H0|={max:F2} вылез за конверт ΣA={envelope:F2}");
        }

        [Test]
        public void H0_IsZeroMean()
        {
            // P0-5: ворот стоит на КАЖДОМ сиде (|mean| ≤ 0.5 м), как требует S1 §3.7.
            // Среднее по 20 сидам проверяется отдельно: оно не должно уползать в плюс/минус.
            const int seeds = 20;
            const int n = 64;
            double bias = 0;
            float worst = 0f;
            for (int seed = 1; seed <= seeds; seed++)
            {
                var cfg = Config(seed);
                var h = Grid(cfg, n, out float step, out float radius);
                // Среднее считается ПО ДИСКУ: DC полос центрирован на опорном диске;
                // квадратные углы за rin — не карта и в mean не входят.
                double sum = 0;
                int count = 0;
                for (int iz = 0; iz < n; iz++)
                    for (int ix = 0; ix < n; ix++)
                    {
                        float x = -radius + ix * step, z = -radius + iz * step;
                        if (x * x + z * z > radius * radius) continue;
                        sum += h[iz * n + ix];
                        count++;
                    }
                float mean = count > 0 ? (float)(sum / count) : 0f;
                bias += mean;
                worst = Mathf.Max(worst, Mathf.Abs(mean));
            }
            bias /= seeds;
            Assert.LessOrEqual(Mathf.Abs((float)bias), 0.5f,
                $"смещение mean(H0) по {seeds} сидам = {bias:F3} м: полосы обязаны быть zero-mean (S1 §3.7)");
            Assert.LessOrEqual(worst, 0.5f + 1e-4f,
                $"худший сид |mean(H0)| = {worst:F3} м > 0.5 — полосы не zero-mean на этом сиде (S1 §3.7)");
        }

        [Test]
        public void H0_Slope_P95_And_Area()
        {
            var cfg = Config(1337);
            var p = DomeHeightField.DomeHeightParams.From(cfg);
            Func<float, float, float> field = (x, z) => DomeHeightField.SampleRaw(p, x, z);
            var slopes = Slopes(cfg, field, 80, out _, out _);
            float p95 = Percentile(slopes, 0.95f);
            float over22 = ShareAbove(slopes, cfg.WalkMaxDegrees);
            float over35 = ShareAbove(slopes, cfg.WallMinDegrees);
            Assert.GreaterOrEqual(p95, 15f, $"p95(H0 v2)={p95:F1}° ниже 15 — поле стало плоским");
            Assert.LessOrEqual(p95, 19f,
                $"p95(H0 v2)={p95:F1}° выше 19 — мезо-потолок 0.30 нарушен (S1 §11.4: вернуть 0.255)");
            Assert.LessOrEqual(over22, 0.010f + 1e-4f,
                $"H0 сам даёт {over22:P2} >22° — хвосту форм не остаётся места (≤1.0 %)");
            Assert.LessOrEqual(over35, 0.003f + 1e-4f,
                $"H0 сам даёт {over35:P2} >35° (≤0.3 %)");
        }

        [Test]
        public void Warp_DoesNotFold()
        {
            var cfg = Config(1337);
            var p = DomeHeightField.DomeHeightParams.From(cfg);
            for (int band = 0; band < 3; band++)
            {
                float max = 0f;
                float r = Radius(cfg);
                const float e = 1f;
                for (int iz = 0; iz < 24; iz++)
                    for (int ix = 0; ix < 24; ix++)
                    {
                        float x = Mathf.Lerp(-r * 0.8f, r * 0.8f, ix / 23f);
                        float z = Mathf.Lerp(-r * 0.8f, r * 0.8f, iz / 23f);
                        var dx = (DomeHeightField.WarpOffset(p, band, x + e, z) -
                                  DomeHeightField.WarpOffset(p, band, x - e, z)) / (2f * e);
                        var dz = (DomeHeightField.WarpOffset(p, band, x, z + e) -
                                  DomeHeightField.WarpOffset(p, band, x, z - e)) / (2f * e);
                        max = Mathf.Max(max, Mathf.Sqrt(dx.sqrMagnitude + dz.sqrMagnitude));
                    }
                Assert.LessOrEqual(max, 0.8f + 1e-3f,
                    $"полоса {band}: |∇w|={max:F2} > 0.8 — warp складывает поле");
            }
        }

        // ------------------------------------------------------------------
        // Классы уклона
        // ------------------------------------------------------------------

        [Test]
        public void SlopeClasses_Partition()
        {
            var cfg = Config(1337);
            var p = DomeHeightField.DomeHeightParams.From(cfg);
            Func<float, float, float> field = (x, z) => DomeHeightField.SampleRaw(p, x, z);
            var slopes = Slopes(cfg, field, 64, out _, out _);
            int pass = 0, climb = 0, wall = 0;
            foreach (float s in slopes)
            {
                switch (SlopeClasses.Classify(s, cfg))
                {
                    case SlopeClass.Passable: pass++; break;
                    case SlopeClass.Climbable: climb++; break;
                    default: wall++; break;
                }
            }
            Assert.AreEqual(slopes.Length, pass + climb + wall,
                "каждый семпл ровно в одном классе (S1 §11.6 п. 1)");
            // Монотонность: ни один семпл класса P не круче семпла класса C.
            for (int i = 0; i < slopes.Length; i++)
                for (int j = 0; j < slopes.Length; j += 37)
                    if (SlopeClasses.Classify(slopes[i], cfg) == SlopeClass.Passable &&
                        SlopeClasses.Classify(slopes[j], cfg) == SlopeClass.Climbable)
                        Assert.LessOrEqual(slopes[i], slopes[j] + 1e-4f,
                            "κ не убывает по |∇H|");
        }

        [Test]
        public void Class_DoesNotDependOnEpsilon()
        {
            var cfg = Config(1337);
            var p = DomeHeightField.DomeHeightParams.From(cfg);
            Func<float, float, float> field = (x, z) => DomeHeightField.SampleRaw(p, x, z);
            float r = Radius(cfg);
            const int n = 48;
            int changed = 0, total = 0;
            for (int iz = 0; iz < n; iz++)
                for (int ix = 0; ix < n; ix++)
                {
                    float x = Mathf.Lerp(-r * 0.8f, r * 0.8f, ix / (n - 1f));
                    float z = Mathf.Lerp(-r * 0.8f, r * 0.8f, iz / (n - 1f));
                    var one = SlopeClasses.Classify(
                        SlopeClasses.SlopeAt(cfg, x, z, field, SlopeClasses.ClassEpsilon), cfg);
                    var half = SlopeClasses.Classify(
                        SlopeClasses.SlopeAt(cfg, x, z, field, 0.5f), cfg);
                    var two = SlopeClasses.Classify(
                        SlopeClasses.SlopeAt(cfg, x, z, field, 2f), cfg);
                    total++;
                    if (one != half || one != two) changed++;
                }
            float share = total > 0 ? (float)changed / total : 0f;
            Assert.LessOrEqual(share, 0.01f + 1e-4f,
                $"класс меняется от ε у {share:P2} площади — линейка не одна");
        }

        // ------------------------------------------------------------------
        // worldVersion
        // ------------------------------------------------------------------

        [Test]
        public void WorldVersion_ParticipatesInHash()
        {
            var a = Config(1337, 2);
            var b = Config(1337, 3);
            Assert.AreNotEqual(
                DomeHeightField.SampleGridHash(a, 16, 2f),
                DomeHeightField.SampleGridHash(b, 16, 2f),
                "worldVersion — соль v2: смена версии обязана сменить карту");
            Assert.AreEqual(
                DomeHeightField.SampleGridHash(a, 16, 2f),
                DomeHeightField.SampleGridHash(a, 16, 2f),
                "один worldVersion дважды — то же поле");
        }

        /// <summary>
        /// P0-1: «0 → v1» — ОДНА точка (<see cref="DomeGenConfigSO.ResolveWorldVersion"/>),
        /// её зовут и конфиг, и сцена. Плюс: явная 1 и неявная 0 дают бит-в-бит одно поле.
        /// </summary>
        [Test]
        public void WorldVersion_ZeroMeansV1_InOnePoint()
        {
            Assert.AreEqual(1, DomeGenConfigSO.ResolveWorldVersion(0),
                "0 = «не настроено» = v1");
            Assert.AreEqual(1, DomeGenConfigSO.ResolveWorldVersion(-5),
                "отрицательное — тоже «не настроено», а не версия -5");
            Assert.AreEqual(2, DomeGenConfigSO.ResolveWorldVersion(2),
                "явная версия не подменяется");

            var cfg0 = Config(1337, 0);
            var cfg1 = Config(1337, 1);
            Assert.AreEqual(1, cfg0.WorldVersion, "конфиг трактует 0 через ту же функцию");
            Assert.AreEqual(DomeHeightField.SampleGridHash(cfg1, 16, 2f),
                DomeHeightField.SampleGridHash(cfg0, 16, 2f),
                "worldVersion 0 и 1 — один и тот же v1 бит-в-бит");

            var go = new GameObject("~WorldVersionProbe");
            try
            {
                var b = go.AddComponent<ForestBuilder>();
                Assert.AreEqual(0, b.worldVersion, "поле сцены по умолчанию — 0, гоча нового SerializeField");
                Assert.AreEqual(1, b.WorldVersion, "сцена трактует 0 через ту же функцию, а не своей");
                b.worldVersion = 2;
                Assert.AreEqual(2, b.WorldVersion);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // ------------------------------------------------------------------
        // Формы: профили, аспект, подошва, объём
        // ------------------------------------------------------------------

        [Test]
        public void SyntheticForm_Aspect_Hills()
        {
            var hill = DomeForms.Disc(Vector2.zero, 100f, 9f);
            Assert.GreaterOrEqual(hill.Aspect, 0.06f, "холм H/D ниже 0.06");
            Assert.LessOrEqual(hill.Aspect, 0.12f, "холм H/D выше 0.12");
            Assert.LessOrEqual(hill.MaxSlopeDegrees, 19.8f + 0.1f, "холм ≤19.8°");
        }

        [Test]
        public void SyntheticForm_Aspect_Ridges()
        {
            var ridge = DomeForms.Capsule(Vector2.zero, Vector2.right, 100f, 35f, 4.2f);
            Assert.LessOrEqual(ridge.Aspect, 0.12f + 1e-4f, "гряда H/D ≤0.12");
            Assert.LessOrEqual(ridge.MaxSlopeDegrees, 19.8f + 0.1f,
                "гряда W=35, L=100, H=4.2 обязана дать ≤19.8°, а не 70°");
        }

        /// <summary>
        /// Аспект выемок — на СИНТЕТИЧЕСКОЙ форме. На карте это S2: каньон и овраг
        /// получают координаты из композиции, S1 проверяет только алгебру формы.
        /// </summary>
        [Test]
        public void SyntheticForm_Aspect_Ravines()
        {
            // Проходимый овраг: H/(D−бровка) = 0.12 → 19.8°.
            var ravine = DomeForms.Canyon(Vector2.zero, Vector2.right, 80f, 100f, 12f, "Ravine");
            Assert.AreEqual(0.12f, ravine.Aspect, 1e-4f, "овраг H/D по определению");
            Assert.LessOrEqual(ravine.MaxSlopeDegrees, 19.8f + 0.1f, "проходимый овраг ≤19.8°");

            // Стена: каньон H/D 0.6 → >35°; площадь стен объявлена, а не спрятана.
            var canyon = DomeForms.Canyon(Vector2.zero, Vector2.right, 80f, 100f, 60f);
            Assert.AreEqual(0.60f, canyon.Aspect, 1e-4f, "каньон H/D 0.6");
            Assert.Greater(canyon.MaxSlopeDegrees, 35f, "каньон — объявленная стена");
        }

        [Test]
        public void SyntheticForm_Aspect_Massif()
        {
            var massif = DomeForms.Massif(Vector2.zero, 1840f);
            Assert.LessOrEqual(massif.Aspect, 0.11f + 1e-4f, "Massif: H/D ≤0.11");
            Assert.LessOrEqual(massif.MaxSlopeDegrees, 18.3f + 0.1f, "Massif ≤18.3°");
            Assert.Greater(massif.height, 30f, "Massif должен быть крупным (33–42 м)");
        }

        [Test]
        public void SyntheticForm_Aspect_Crag()
        {
            var crag = DomeForms.Crag(Vector2.zero, Vector2.right, 1840f);
            Assert.GreaterOrEqual(crag.Aspect, 0.14f - 1e-4f, "Crag: H/D ≥0.14");
            Assert.LessOrEqual(crag.Aspect, 0.21f + 1e-4f, "Crag: H/D ≤0.21");
            Assert.Greater(crag.MaxSlopeDegrees, 22f, "Crag обязан попасть в класс C");
            Assert.LessOrEqual(crag.MaxSlopeDegrees, 34.7f + 0.1f,
                "Crag с запасом 2.8° не должен вылезать в стену");
            float fraction = DomeForms.ClimbAreaFraction(crag.Aspect);
            Assert.GreaterOrEqual(fraction, 0.32f - 0.01f, "доля пятна Crag в C");
            Assert.LessOrEqual(fraction, 0.60f + 0.01f, "доля пятна Crag в C");
        }

        /// <summary>
        /// P0-8: порог C¹ — ОТ КРИВИЗНЫ ФОРМЫ, а не фиксированные 2°. Профиль `S(1−u)`
        /// гладкий, но у малой куэсты (D≈120, H/D 0.18) ход уклона на 1 м уже 2.03° —
        /// фиксированное число краснело бы на верной форме. Порог: `6H/run²·Δx + 0.5°`;
        /// сверху он связан с радиусом кривизны `run²/(6H) ≥ 5 м` (§3.6).
        /// </summary>
        [Test]
        public void Forms_C1_AtSeams()
        {
            var cfg = Config(1337);
            var hill = DomeForms.Disc(Vector2.zero, 120f, 8f);
            AssertSeamSmooth(cfg, hill, -70f, 70f, "холм D=120");

            // Куэста сканируется ПОПЕРЁК оси капсулы: вдоль неё гребень плоский и излома нет.
            var crag = DomeForms.Crag(Vector2.zero, Vector2.up, 1840f, 0.065f);
            AssertSeamSmooth(cfg, crag, -100f, 100f, "куэста D=120");
        }

        static void AssertSeamSmooth(DomeGenConfigSO cfg, DomeForm form, float from, float to,
            string what)
        {
            Func<float, float, float> field = (x, z) => form.Apply(0f, x, z);
            float prev = -999f;
            float maxJump = 0f;
            for (float x = from; x <= to; x += 1f)
            {
                float slope = SlopeClasses.SlopeAt(cfg, x, 0f, field, 0.5f);
                if (prev > -900f) maxJump = Mathf.Max(maxJump, Mathf.Abs(slope - prev));
                prev = slope;
            }
            float bound = form.SlopeStepDegrees(1f);
            Assert.LessOrEqual(maxJump, bound + 0.5f,
                $"{what}: излом {maxJump:F2}° > кривизны {bound:F2}° + 0.5° — профиль не C¹");
            Assert.LessOrEqual(bound, 11.5f,
                $"{what}: ход уклона {bound:F2}°/м — радиус кривизны меньше 5 м (§3.6)");
        }

        [Test]
        public void Soles_AreFlat_AndCounted()
        {
            const float floor = 0f;
            Assert.LessOrEqual(Mathf.Abs(DomeForms.Sole(-1f, floor)), 1e-3f,
                "под водой подошва = floor");
            Assert.AreEqual(0.5f, DomeForms.Sole(0.5f, floor), 0.2f,
                "выше воды подошва не сдвигает рельеф больше k·ln2");
            float min = float.MaxValue, max = float.MinValue;
            for (float x = 0f; x <= 4f; x += 0.5f)
            {
                float v = DomeForms.Sole(-1f, floor);
                min = Mathf.Min(min, v);
                max = Mathf.Max(max, v);
            }
            Assert.LessOrEqual(max - min, 0.01f, "плоская часть подошвы ≥4 м");
        }

        /// <summary>
        /// P0-9: объём считается по СИНТЕТИЧЕСКОЙ форме, а не по `SampleGround`
        /// (там сидят pad/hill — чужой домен). Замкнутая формула сверяется
        /// с численным интегралом профиля, а не с самой собой.
        /// </summary>
        [Test]
        public void SyntheticForm_Volume_MatchesNumericIntegral()
        {
            var massif = DomeForms.Massif(Vector2.zero, 1840f);
            float closed = DomeForms.DiscVolume(massif.diameter, massif.height);
            float numeric = NumericVolume(massif, 400);
            Assert.AreEqual(closed, numeric, closed * 0.01f,
                "V = 0.3·A·H обязан сходиться с интегралом профиля");
            Assert.Greater(closed, 5e5f, "объём Massif порядка 1.2·10⁶ м³ — печатается S5");
            Assert.Less(closed, 3e6f, "объём Massif не должен вылезать на порядок");
            float expected = 0.3f * Mathf.PI * (massif.diameter * 0.5f) * (massif.diameter * 0.5f) * massif.height;
            Assert.AreEqual(expected, closed, expected * 1e-5f, "V = 0.3·A·H");
        }

        /// <summary>
        /// Капсула/куэста: прямоугольная часть профиля даёт 0.5·4·run·L, а не 0.3.
        /// Сторож против занижения объёма гряды (~1.45×), которое не ловил диск.
        /// </summary>
        [Test]
        public void SyntheticForm_CapsuleVolume_MatchesNumericIntegral()
        {
            var ridge = DomeForms.Capsule(Vector2.zero, Vector2.right, 100f, 35f, 4.2f);
            float closed = DomeForms.VolumeOf(ridge);
            float numeric = NumericVolumeCapsule(ridge, 600);
            Assert.AreEqual(closed, numeric, closed * 0.02f,
                "объём капсулы обязан сходиться с интегралом профиля по её носителю");
        }

        /// <summary>
        /// P0-9: баланс P/N — на синтетическом списке форм поверх `H0`, без площадки
        /// и без станционного холма. Равные по профилю P и N дают `V+/V− ≈ 1`;
        /// шум `H0` в объём не входит по построению меры (§3.7).
        /// </summary>
        [Test]
        public void SyntheticForm_VolumeBalance_IsFormsNotGround()
        {
            var cfg = Config(1337);
            float rin = Radius(cfg);
            var forms = new List<DomeForm>
            {
                DomeForms.Disc(new Vector2(-420f, 0f), 300f, 20f, DomeFormSign.Lift, "Uval"),
                DomeForms.Disc(new Vector2(420f, 0f), 300f, 20f, DomeFormSign.Carve, "Pit"),
            };
            Func<float, float, float> field = Field(cfg, forms);
            var p = DomeHeightField.DomeHeightParams.From(cfg);
            const int n = 128;
            float step = 2f * rin / (n - 1f);
            double vPlus = 0, vMinus = 0;
            for (int iz = 0; iz < n; iz++)
                for (int ix = 0; ix < n; ix++)
                {
                    float x = -rin + ix * step, z = -rin + iz * step;
                    if (x * x + z * z > rin * rin) continue;
                    float h0 = DomeHeightField.SampleRaw(p, x, z);
                    float h = field(x, z);
                    if (h > h0) vPlus += (h - h0) * step * step;
                    else vMinus += (h0 - h) * step * step;
                }
            Assert.Greater(vPlus, 1e4, "подъём в синтетике не посчитан");
            Assert.Greater(vMinus, 1e4, "выемка в синтетике не посчитана");
            Assert.AreEqual(1f, (float)(vPlus / vMinus), 0.05f,
                $"V+/V− = {vPlus / vMinus:F3}: равные P и N обязаны сбалансироваться");
        }

        [Test]
        public void Curvature_Radius_AtLeast5m()
        {
            var hill = DomeForms.Disc(Vector2.zero, 100f, 9f);
            var ridge = DomeForms.Capsule(Vector2.zero, Vector2.right, 100f, 35f, 4.2f);
            Assert.GreaterOrEqual(hill.CurvatureRadius, 5f,
                $"холм: R={hill.CurvatureRadius:F2} м < 5 — игла, а не форма");
            Assert.GreaterOrEqual(ridge.CurvatureRadius, 5f,
                $"гряда: R={ridge.CurvatureRadius:F2} м < 5");
            Assert.AreEqual(0.347f * 35f, ridge.CurvatureRadius, 0.8f,
                "гряда: run²/(6H) = 0.347·W (S1 §3.6)");
        }

        // ---- Композиция: классы на сумме H0 + формы (S1 §11.6) -------------

        /// <summary>
        /// Синтетическая композиция S1: 1 Massif (P, ≤18.3°), 2 Crag (C, 22–35°)
        /// и плато с кромкой C (H/D 0.18). Мест — S2; здесь только алгебра.
        /// </summary>
        static List<DomeForm> Composition(float rin)
        {
            return new List<DomeForm>
            {
                DomeForms.Massif(new Vector2(-300f, 100f), 2f * rin),
                DomeForms.Crag(new Vector2(250f, 150f), Vector2.right, 2f * rin),
                DomeForms.Crag(new Vector2(-120f, -400f), new Vector2(0.3f, 1f), 2f * rin),
                DomeForms.Plateau(new Vector2(300f, -300f), 300f, 120f, 0.18f * 180f),
            };
        }

        static Func<float, float, float> Field(DomeGenConfigSO cfg, List<DomeForm> forms)
        {
            var p = DomeHeightField.DomeHeightParams.From(cfg);
            return (x, z) => DomeForms.Apply(forms, DomeHeightField.SampleRaw(p, x, z), x, z);
        }

        static bool InsideAny(List<DomeForm> forms, float x, float z)
        {
            foreach (var f in forms)
                if (f.U(x, z) < 1f) return true;
            return false;
        }

        /// <summary>
        /// Число КРУПНЫХ 4-связных компонент P: мелкий островок на вершине куэсты —
        /// не «мир развалился», а форма; запирает мир только компонента-регион.
        /// </summary>
        static int ComponentCount(bool[,] passable, float minShare = 0.05f)
        {
            int res = passable.GetLength(0);
            var seen = new bool[res, res];
            int openCells = 0;
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                    if (passable[x, z]) openCells++;
            int threshold = Mathf.Max(1, Mathf.RoundToInt(openCells * minShare));
            int components = 0;
            var stack = new Stack<Vector2Int>();
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    if (!passable[x, z] || seen[x, z]) continue;
                    seen[x, z] = true;
                    stack.Push(new Vector2Int(x, z));
                    int size = 0;
                    while (stack.Count > 0)
                    {
                        var c = stack.Pop();
                        size++;
                        Try(c.x + 1, c.y); Try(c.x - 1, c.y);
                        Try(c.x, c.y + 1); Try(c.x, c.y - 1);
                    }
                    if (size >= threshold) components++;
                }
            return components;

            void Try(int x, int z)
            {
                if (x < 0 || z < 0 || x >= res || z >= res) return;
                if (!passable[x, z] || seen[x, z]) return;
                seen[x, z] = true;
                stack.Push(new Vector2Int(x, z));
            }
        }

        [Test]
        public void ClimbableArea_AtLeastOnePercent()
        {
            var cfg = Config(1337);
            float rin = Radius(cfg);
            var forms = Composition(rin);
            SlopeClasses.AreaShares(cfg, Field(cfg, forms), rin, 96,
                out float aP, out float aC, out float aW);
            // P0-7: порог поднят с 0.8 до 1.0 % — это нижняя граница, при которой
            // арифметика «A_C + 80 % объявленного» вообще закрывается (§12).
            Assert.GreaterOrEqual(aC, 0.010f,
                $"A_C={aC:P2} < 1.0 %: вертикаль объявлена, а материала нет");
            Assert.LessOrEqual(aW, 0.02f + 1e-4f, $"A_W={aW:P2} > 2 % — бюджет стен");
            Assert.LessOrEqual(aW, aC + 1e-4f, $"A_W={aW:P2} > A_C={aC:P2}");
            Assert.Greater(aP, 0.9f, $"проходимое поле должно доминировать, A_P={aP:P1}");
        }

        [Test]
        public void ClimbableArea_IsDeclared()
        {
            var cfg = Config(1337);
            float rin = Radius(cfg);
            var forms = Composition(rin);
            Func<float, float, float> field = Field(cfg, forms);
            const int n = 80;
            int climb = 0, declared = 0, total = 0;
            for (int iz = 0; iz < n; iz++)
                for (int ix = 0; ix < n; ix++)
                {
                    float x = Mathf.Lerp(-rin, rin, ix / (n - 1f));
                    float z = Mathf.Lerp(-rin, rin, iz / (n - 1f));
                    if (x * x + z * z > rin * rin) continue;
                    total++;
                    if (SlopeClasses.At(cfg, x, z, field) != SlopeClass.Climbable) continue;
                    climb++;
                    if (InsideAny(forms, x, z)) declared++;
                }
            Assert.Greater(climb, 0, "в синтетике нет класса C — ворот не о чем");
            float share = (float)declared / climb;
            // P0-7, выбранная арифметика: A_C ≥ 1 % И необъявленного ≤ 0.5 % карты.
            // Относительные 80 % без абсолютного потолка прощают большой «шумовой» кусок
            // при большом A_C; абсолютный без относительного — одинокую форму-статиста.
            Assert.GreaterOrEqual(share, 0.80f,
                $"только {share:P1} площади A_C внутри пятен объявленных форм: крутое — шум, а не автор");
            float undeclared = total > 0 ? (float)(climb - declared) / total : 1f;
            Assert.LessOrEqual(undeclared, 0.005f + 1e-4f,
                $"необъявленного C = {undeclared:P2} карты > 0.5 % — абсолютный потолок шума");
        }

        [Test]
        public void Climbable_DoesNotSplitPassable()
        {
            var cfg = Config(1337);
            float rin = Radius(cfg);
            var before = new List<DomeForm> { DomeForms.Massif(new Vector2(-300f, 100f), 2f * rin) };
            var after = Composition(rin);
            int c0 = ComponentCount(PassableMask(cfg, before, rin, 64));
            int c1 = ComponentCount(PassableMask(cfg, after, rin, 64));
            Assert.LessOrEqual(c1, c0,
                $"C-формы увеличили число 4-связных компонент P: {c0} → {c1} — плотина, а не остров");
        }

        static bool[,] PassableMask(DomeGenConfigSO cfg, List<DomeForm> forms, float rin, int res)
        {
            var mask = new bool[res, res];
            Func<float, float, float> field = Field(cfg, forms);
            float step = 2f * rin / (res - 1f);
            for (int iz = 0; iz < res; iz++)
                for (int ix = 0; ix < res; ix++)
                {
                    float x = -rin + ix * step, z = -rin + iz * step;
                    if (x * x + z * z > rin * rin) continue;
                    mask[ix, iz] = SlopeClasses.At(cfg, x, z, field) == SlopeClass.Passable;
                }
            return mask;
        }

        [Test]
        public void Map_Classes_WithinBudget()
        {
            var cfg = Config(1337);
            float rin = Radius(cfg);
            SlopeClasses.AreaShares(cfg, Field(cfg, Composition(rin)), rin, 96,
                out _, out float aC, out float aW);
            float tail = aC + aW;
            Assert.GreaterOrEqual(tail, 0.02f - 1e-4f, $"хвост >22° = {tail:P2} < 2 % (Д1)");
            Assert.LessOrEqual(tail, 0.04f + 1e-4f, $"хвост >22° = {tail:P2} > 4 % (Д1)");
            Assert.LessOrEqual(aW, 0.02f + 1e-4f, $"A_W={aW:P2} > 2 %");
            Assert.LessOrEqual(aW, aC + 1e-4f, "стен не больше, чем лазаемого");
        }

        [Test]
        public void Fingerprint_IncludesRelief()
        {
            var a = new List<DomeForm> { DomeForms.Massif(Vector2.zero, 1840f) };
            var b = new List<DomeForm> { DomeForms.Massif(new Vector2(10f, 0f), 1840f) };
            ulong ha = WorldFingerprint.MixRelief(14695981039346656037UL, 2, a, 22f, 35f);
            ulong hb = WorldFingerprint.MixRelief(14695981039346656037UL, 2, b, 22f, 35f);
            Assert.AreNotEqual(ha, hb, "смена формы обязана менять отпечаток");
            ulong h22 = WorldFingerprint.MixRelief(14695981039346656037UL, 2, a, 22f, 35f);
            ulong h20 = WorldFingerprint.MixRelief(14695981039346656037UL, 2, a, 20f, 35f);
            Assert.AreNotEqual(h22, h20, "границы классов — часть отпечатка");
            ulong hv2 = WorldFingerprint.MixRelief(14695981039346656037UL, 2, a, 22f, 35f);
            ulong hv3 = WorldFingerprint.MixRelief(14695981039346656037UL, 3, a, 22f, 35f);
            Assert.AreNotEqual(hv2, hv3, "worldVersion — часть отпечатка v2");
        }

        /// <summary>
        /// P0-2: при v1 отпечаток — ТОЖДЕСТВО, поэтому появление поля v2 в коде не может
        /// сдвинуть S0-константы A/C/B0/Pavilion. Версия формулы не поднимается «по дороге».
        /// </summary>
        [Test]
        public void Fingerprint_IgnoresReliefAtV1()
        {
            const ulong seed = 14695981039346656037UL;
            var forms = new List<DomeForm> { DomeForms.Massif(Vector2.zero, 1840f) };
            Assert.AreEqual(seed, WorldFingerprint.MixRelief(seed, 1, forms, 22f, 35f, 1f),
                "v1: список форм не входит в отпечаток");
            Assert.AreEqual(seed, WorldFingerprint.MixRelief(seed, 0, forms, 22f, 35f, 1f),
                "worldVersion 0 читается как v1 и тоже не входит");
            Assert.AreEqual(1, WorldFingerprint.Version,
                "версия формулы отпечатка не менялась: ворот сделан в MixRelief, а не поднятием версии");
        }

        // ------------------------------------------------------------------
        // Инструменты
        // ------------------------------------------------------------------

        /// <summary>Численный интеграл профиля формы по её носителю (сверка `V = 0.3·A·H`).</summary>
        static float NumericVolume(DomeForm f, int res)
        {
            float r = f.diameter * 0.5f;
            float step = 2f * r / res;
            double sum = 0;
            for (int iz = 0; iz < res; iz++)
                for (int ix = 0; ix < res; ix++)
                {
                    float x = -r + (ix + 0.5f) * step, z = -r + (iz + 0.5f) * step;
                    if (x * x + z * z > r * r) continue;
                    sum += f.height * f.Profile(f.U(x, z)) * step * step;
                }
            return (float)sum;
        }

        /// <summary>Численный интеграл профиля капсулы по её носителю (прямоугольник + полукруги).</summary>
        static float NumericVolumeCapsule(DomeForm f, int res)
        {
            float run = f.Run;
            float halfLength = f.halfLength;
            float stepX = 2f * (halfLength + run) / res;
            float stepZ = 2f * run / res;
            double sum = 0;
            for (int iz = 0; iz < res; iz++)
                for (int ix = 0; ix < res; ix++)
                {
                    float x = -(halfLength + run) + (ix + 0.5f) * stepX;
                    float z = -run + (iz + 0.5f) * stepZ;
                    float u = f.U(x, z);
                    if (u > 1f) continue;
                    sum += f.height * f.Profile(u) * stepX * stepZ;
                }
            return (float)sum;
        }

        static float[] Slopes(DomeGenConfigSO cfg, Func<float, float, float> field, int res,
            out float step, out float radius)
        {
            radius = Radius(cfg);
            step = 2f * radius / (res - 1f);
            var list = new List<float>(res * res);
            for (int iz = 0; iz < res; iz++)
                for (int ix = 0; ix < res; ix++)
                {
                    float x = -radius + ix * step;
                    float z = -radius + iz * step;
                    if (x * x + z * z > radius * radius) continue;
                    list.Add(SlopeClasses.SlopeAt(cfg, x, z, field, SlopeClasses.ClassEpsilon));
                }
            return list.ToArray();
        }

        static float ShareAbove(float[] values, float threshold)
        {
            if (values.Length == 0) return 0f;
            int n = 0;
            foreach (float v in values) if (v > threshold) n++;
            return (float)n / values.Length;
        }

        static float Percentile(float[] values, float p)
        {
            if (values.Length == 0) return 0f;
            var copy = (float[])values.Clone();
            Array.Sort(copy);
            int i = Mathf.Clamp(Mathf.RoundToInt(p * (copy.Length - 1)), 0, copy.Length - 1);
            return copy[i];
        }

        static double Mean(float[] v)
        {
            double s = 0;
            foreach (float x in v) s += x;
            return v.Length > 0 ? s / v.Length : 0;
        }

        static double Variance(float[] v, double mean)
        {
            double s = 0;
            foreach (float x in v) { double d = x - mean; s += d * d; }
            return v.Length > 0 ? s / v.Length : 0;
        }

        static float[] BoxFilter(float[] v, int n, int half)
        {
            half = Mathf.Max(1, half);
            var integral = new double[(n + 1) * (n + 1)];
            for (int z = 0; z < n; z++)
            {
                double row = 0;
                for (int x = 0; x < n; x++)
                {
                    row += v[z * n + x];
                    integral[(z + 1) * (n + 1) + (x + 1)] =
                        integral[z * (n + 1) + (x + 1)] + row;
                }
            }
            var outv = new float[n * n];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    int x0 = Mathf.Max(0, x - half), x1 = Mathf.Min(n - 1, x + half);
                    int z0 = Mathf.Max(0, z - half), z1 = Mathf.Min(n - 1, z + half);
                    double sum = integral[(z1 + 1) * (n + 1) + (x1 + 1)]
                               - integral[z0 * (n + 1) + (x1 + 1)]
                               - integral[(z1 + 1) * (n + 1) + x0]
                               + integral[z0 * (n + 1) + x0];
                    int count = (x1 - x0 + 1) * (z1 - z0 + 1);
                    outv[z * n + x] = (float)(sum / count);
                }
            return outv;
        }
    }
}
