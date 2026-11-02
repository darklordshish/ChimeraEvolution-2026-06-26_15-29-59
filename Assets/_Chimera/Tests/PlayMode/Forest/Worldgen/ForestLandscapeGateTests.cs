using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// S5 шаг 5: ворота мира G0–G8 на профиле сцены (Ø2000, `terrainQuads 48`, v2).
    /// Пороги берутся у S1/S2/S4 и решений 01.11; свои числа S5 — M3b ≥60 % и M14.
    /// <para>
    /// Мир собирается ОДИН раз на класс: сборка v2 дольше 30 с главного потока пайплайна,
    /// но PlayMode-тест не ограничен этим потолком. G5 — отчёт B1 (решение 8), G7 — остаток
    /// кадров, Hydro_Gate собирает свой мир с гидро (M16).
    /// </para>
    /// </summary>
    public class ForestLandscapeGateTests
    {
        const string GateFile = "Docs/Диаграммы/Лес/ВЕРИФИКАЦИЯ-ворота.txt";
        const string MetricsFile = "Docs/Диаграммы/Лес/ВЕРИФИКАЦИЯ-метрики.txt";
        const string MapFile = "Docs/Диаграммы/Лес/ВЕРИФИКАЦИЯ-карта.png";

        static GameObject host;
        static ForestBuilder builder;
        static ForestLandscapeReport.Snapshot snap;
        static readonly List<string> verdicts = new List<string>();
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        [OneTimeSetUp]
        public void BuildWorld()
        {
            host = new GameObject("~LandscapeGateWorld");
            host.SetActive(false);
            builder = ForestProfiles.PavilionV2(host, 1337L, erosion: false);
            builder.buildThermalErosion = true; // гидро — отдельным Hydro_Gate
            host.SetActive(true);
            var sw = Stopwatch.StartNew();
            builder.Build();
            sw.Stop();
            Assert.IsNotNull(builder.BuiltRoot, "мир v2 не собран");
            snap = ForestLandscapeReport.Measure(builder, (float)sw.Elapsed.TotalMilliseconds);
            WriteMetrics();
        }

        [OneTimeTearDown]
        public void TearDownWorld()
        {
            WriteGates();
            if (builder != null) builder.Clear();
            if (host != null) Object.Destroy(host);
            builder = null;
            host = null;
        }

        // ------------------------------------------------------------------
        // G0: воспроизводимость, паритет, NavMesh
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator G0_ProfileAndNavMesh()
        {
            Assert.AreEqual(2, builder.WorldVersion, "G0: профиль ворот — v2");
            Assert.AreEqual(48, builder.terrainQuads, "G0: профиль сцены — 48 квадов");
            Assert.AreNotEqual(0UL, builder.Fingerprint, "G0: отпечаток собран");

            var cfg = ForestLandscapeReport.ConfigFor(builder);
            float lake = builder.Hydro != null ? builder.Hydro.lakeLevel : float.NegativeInfinity;
            int checkedPoints = 0, found = 0;
            float worst = 0f, worstGround = 0f, worstNav = 0f;
            Vector2 worstAt = Vector2.zero;
            for (int i = 0; i < 100; i++)
            {
                float x = Mathf.Lerp(-900f, 900f, (i * 37 % 100) / 99f);
                float z = Mathf.Lerp(-900f, 900f, (i * 61 % 100) / 99f);
                if (x * x + z * z > 810000f) continue;
                float g = builder.SampleGround(x, z);
                if (g < lake + 0.5f) continue; // вода — не NavMesh-поверхность
                float slope = SlopeClasses.SlopeAt(cfg, x, z,
                    (a, b) => builder.SampleGround(a, b), SlopeClasses.ClassEpsilon);
                if (slope > cfg.WalkMaxDegrees) continue; // стена/лазаемое — не ходим
                checkedPoints++;
                // Проба в 2 м над полом, поиск в 5 м: ищем именно NavMesh ЭТОЙ точки,
                // а не соседнюю стену в 20 м ниже.
                var probe = new Vector3(x, g + 2f, z);
                if (!NavMesh.SamplePosition(probe, out var hit, 5f, NavMesh.AllAreas)) continue;
                found++;
                float dy = Mathf.Abs(hit.position.y - g);
                if (dy > worst) { worst = dy; worstGround = g; worstNav = hit.position.y; worstAt = new Vector2(x, z); }
            }
            Object.DestroyImmediate(cfg);
            bool coverage = checkedPoints > 0 && found >= Mathf.CeilToInt(checkedPoints * 0.9f);
            string where = worst > 0.5f
                ? $"; худшая ({F(worstAt.x)};{F(worstAt.y)}): пол {F(worstGround)}, NavMesh {F(worstNav)}"
                : "";
            Record("G0", coverage && worst <= 0.5f,
                $"NavMesh найден {found}/{checkedPoints} ходимых, max|Δy|={F(worst)} м{where}");
            Assert.IsTrue(coverage, $"G0: NavMesh покрывает {found}/{checkedPoints} ходимых — меньше 90%");
            Assert.LessOrEqual(worst, 0.5f, "G0: |NavMesh.y − SampleGround| ≤ 0.5 м" + where);
            yield return null;
        }

        // ------------------------------------------------------------------
        // G1: уклон
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator G1_Slope_Gate()
        {
            var s = snap.Slope;
            float over22 = s.climbableField + s.wallField;
            bool ok = s.p95Field >= 16f && s.p95Field <= 20f &&
                      over22 >= 0.02f && over22 <= 0.04f &&
                      s.wallField <= 0.02f && s.maxGradPerForm <= 22f &&
                      s.threeClassesDistinct && s.pairCompatible;
            Record("G1", ok,
                $"p95={F(s.p95Field)}°, >22={F(100f * over22)}%, >35={F(100f * s.wallField)}%, " +
                $"maxForm={F(s.maxGradPerForm)}°, classes={s.threeClassesDistinct}, pair={s.pairCompatible}");
            Assert.IsTrue(s.p95Field >= 16f && s.p95Field <= 20f,
                $"G1: p95={F(s.p95Field)}° вне 16–20°");
            Assert.IsTrue(over22 >= 0.02f && over22 <= 0.04f,
                $"G1: доля >22°={F(100f * over22)}% вне 2–4%");
            Assert.LessOrEqual(s.wallField, 0.02f, "G1: стены >2%");
            Assert.LessOrEqual(s.maxGradPerForm, 22f, "G1: max|∇H| по форме >22°");
            Assert.IsTrue(s.threeClassesDistinct, "G1: три класса смешаны");
            Assert.IsTrue(s.pairCompatible, "G1: пара p95/доля несовместна");
            yield return null;
        }

        // ------------------------------------------------------------------
        // G2: поле и вертикаль
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator G2_Relief_Gate()
        {
            bool balance = !snap.Balance.measurable ||
                           (snap.Balance.ratio >= 0.80f && snap.Balance.ratio <= 1.20f);
            bool ok = snap.Energy >= 0.60f && snap.Meso >= 0.60f && snap.Local >= 0.60f &&
                      balance && snap.Seams.maxSlopeJump <= 2f + 1e-4f &&
                      snap.Seams.minCurvatureRadius >= 5f;
            Record("G2", ok,
                $"E={F(snap.Energy)}, мезо={F(100f * snap.Meso)}%, local={F(100f * snap.Local)}%, " +
                $"P/N={F(snap.Balance.ratio)}, излом={F(snap.Seams.maxSlopeJump)}°, " +
                $"R={F(snap.Seams.minCurvatureRadius)} м");
            Assert.GreaterOrEqual(snap.Energy, 0.60f, "G2: E_λ < 60%");
            Assert.GreaterOrEqual(snap.Meso, 0.60f, "G2: мезо < 60% окон");
            Assert.GreaterOrEqual(snap.Local, 0.60f, "G2: M3b < 60% диска");
            Assert.IsTrue(balance, "G2: P/N вне 0.80–1.20");
            Assert.LessOrEqual(snap.Seams.maxSlopeJump, 2f + 1e-4f, "G2: излом >2°");
            Assert.GreaterOrEqual(snap.Seams.minCurvatureRadius, 5f, "G2: R < 5 м");
            yield return null;
        }

        // ------------------------------------------------------------------
        // G3: ориентир и видимость
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator G3_Los_Gate()
        {
            bool ok = snap.Los.edgeShare >= 0.80f &&
                      snap.Los.zonesWithSixRays == snap.Los.zones &&
                      snap.Los.corridorAlongMin >= 28f && snap.Los.corridorAcrossMin >= 16f;
            Record("G3", ok,
                $"кромка={F(100f * snap.Los.edgeShare)}%, зон 6/8={snap.Los.zonesWithSixRays}/{snap.Los.zones}, " +
                $"коридор {F(snap.Los.corridorAlongMin)}×{F(snap.Los.corridorAcrossMin)} м");
            Assert.GreaterOrEqual(snap.Los.edgeShare, 0.80f, "G3: ориентир <80% точек");
            Assert.AreEqual(snap.Los.zones, snap.Los.zonesWithSixRays,
                "G3: не все зоны видны на 0.8R в 6/8");
            Assert.GreaterOrEqual(snap.Los.corridorAlongMin, 28f, "G3: коридор вдоль <28 м");
            Assert.GreaterOrEqual(snap.Los.corridorAcrossMin, 16f, "G3: коридор поперёк <16 м");
            yield return null;
        }

        // ------------------------------------------------------------------
        // G4: POI, зоны и дороги
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator G4_PoiZone_Gate()
        {
            bool ok = snap.Poi.smooth == snap.Poi.zones &&
                      snap.Poi.twoApproaches == snap.Poi.zones &&
                      snap.Poi.noTrunks == snap.Poi.zones &&
                      snap.Poi.covered == snap.Poi.zones &&
                      snap.Poi.corridorWidthP10 >= 16f &&
                      snap.Roads.built && snap.Roads.components == 1 &&
                      snap.Roads.loops >= 1 && snap.Roads.loops <= 2 &&
                      snap.Roads.lengthKm >= 2f && snap.Roads.lengthKm <= 4f &&
                      snap.Roads.slopeP95 <= 12f && snap.Roads.widthP10 >= 2.9f;
            Record("G4", ok,
                $"ровных {snap.Poi.smooth}/{snap.Poi.zones}, 2подхода {snap.Poi.twoApproaches}/{snap.Poi.zones}, " +
                $"0стволов {snap.Poi.noTrunks}/{snap.Poi.zones}, под12 {snap.Poi.covered}/{snap.Poi.zones}, " +
                $"ширина p10={F(snap.Poi.corridorWidthP10)}, дороги built={snap.Roads.built} " +
                $"comp={snap.Roads.components} loops={snap.Roads.loops} len={F(snap.Roads.lengthKm)} " +
                $"p95={F(snap.Roads.slopeP95)}° w10={F(snap.Roads.widthP10)}");
            Assert.AreEqual(snap.Poi.zones, snap.Poi.smooth, "G4: есть зона с уклоном >5°");
            Assert.AreEqual(snap.Poi.zones, snap.Poi.twoApproaches, "G4: у зоны меньше двух подходов");
            Assert.AreEqual(snap.Poi.zones, snap.Poi.noTrunks, "G4: ствол в пятне зоны");
            Assert.AreEqual(snap.Poi.zones, snap.Poi.covered, "G4: нет пода в 12 м");
            Assert.GreaterOrEqual(snap.Poi.corridorWidthP10, 16f, "G4: коридор уже 16 м (замер)");
            Assert.IsTrue(snap.Roads.built, "G4: дороги не построены");
            Assert.AreEqual(1, snap.Roads.components, "G4: primary POI не в одной компоненте");
            Assert.IsTrue(snap.Roads.loops >= 1 && snap.Roads.loops <= 2, "G4: петель не 1–2");
            Assert.IsTrue(snap.Roads.lengthKm >= 2f && snap.Roads.lengthKm <= 4f,
                "G4: сеть дорог вне 2–4 км");
            Assert.LessOrEqual(snap.Roads.slopeP95, 12f, "G4: p95 уклона дороги >12°");
            yield return null;
        }

        // ------------------------------------------------------------------
        // G5: вода и логова — ОТЧЁТ B1
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator G5_WaterLair_Report()
        {
            var w = snap.Water;
            Record("G5", true,
                $"ОТЧЁТ B1: логова={w.lairs} сухих={w.dry} внекольца={w.outsideRing} " +
                $"вода p50={F(w.waterP50)} max={F(w.waterMax)} разнос={F(w.spreadMin)} | " +
                $"водопои={w.waterholes} подходы={w.approachesOk} укрытие12={w.coverOk} " +
                $"брод={F(w.fordDepth)} берег={F(w.bankSlopeMax)}° ширина={F(w.waterWidthMin)}");
            // Решение 8: G5 — B. Красное не блокирует S3; фиксируем в отчёте.
            Assert.Pass("G5 — отчёт B1, не блок S3");
            yield return null;
        }

        // ------------------------------------------------------------------
        // G6: 20 сидов — отдельный Slow-прогон
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator G6_SeedMatrix_IsSeparate()
        {
            Record("G6", true, "20 сидов — отдельный [Slow] прогон ForestSeedMatrixTests; здесь не дублируется");
            Assert.Inconclusive("G6: 20 сидов гоняются отдельным [Slow] прогоном, не в обычном наборе");
            yield return null;
        }

        // ------------------------------------------------------------------
        // G7: кадры — остаток
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator G7_Frame_IsRemainder()
        {
            Record("G7", false, "ОСТАТОК: кадры A/B, тур Р1–Р4 и три смотрящих не сняты");
            Assert.Inconclusive("G7 A: кадры — остаток шага 6; числами не закрывается");
            yield return null;
        }

        // ------------------------------------------------------------------
        // G8: бюджет и плотность
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator G8_BudgetDensity_Gate()
        {
            bool ok = snap.Budget.renderers <= 8000 && snap.Budget.triangles <= 800000 &&
                      snap.BuildMs <= 12000f &&
                      snap.Budget.colliders == snap.Budget.trees - snap.Budget.deadLogs &&
                      snap.Patches.localDensityPerKm2 >= 3000f &&
                      snap.Patches.localDensityPerKm2 <= 3500f &&
                      // M14: крона 13–15 % ЛОКАЛЬНО внутри пятен; глобальная — отчёт.
                      snap.Patches.localCrownCover >= 0.13f && snap.Patches.localCrownCover <= 0.15f &&
                      snap.Patches.trunksInForbidden == 0;
            Record("G8", ok,
                $"рендереров={snap.Budget.renderers}, тр={snap.Budget.triangles}, " +
                $"сборка={F(snap.BuildMs / 1000f)} с, " +
                $"коллайдеры={snap.Budget.colliders}/{snap.Budget.trees - snap.Budget.deadLogs}, " +
                $"пятен={snap.Patches.patchCount} площадь={F(100f * snap.Patches.patchAreaShare)}%, " +
                $"локально={F(snap.Patches.localDensityPerKm2)}/км² " +
                $"(глобально {F(snap.Patches.globalDensityPerKm2)}/км²), " +
                $"крона локально={F(100f * snap.Patches.localCrownCover)}% " +
                $"(глобально {F(100f * snap.Patches.crownCover)}%), " +
                $"в запрете={snap.Patches.trunksInForbidden}");
            Assert.LessOrEqual(snap.Budget.renderers, 8000, "G8: >8k рендереров");
            Assert.LessOrEqual(snap.Budget.triangles, 800000, "G8: >0.8M треугольников");
            Assert.LessOrEqual(snap.BuildMs, 12000f, "G8: сборка >12 с");
            Assert.AreEqual(snap.Budget.trees - snap.Budget.deadLogs, snap.Budget.colliders,
                "G8: TreeColliders != Trees − DeadLogs");
            Assert.IsTrue(snap.Patches.localDensityPerKm2 >= 3000f &&
                          snap.Patches.localDensityPerKm2 <= 3500f,
                $"G8: локальная плотность {F(snap.Patches.localDensityPerKm2)}/км² вне 3000–3500");
            Assert.IsTrue(snap.Patches.localCrownCover >= 0.13f && snap.Patches.localCrownCover <= 0.15f,
                $"G8: крона локально {F(100f * snap.Patches.localCrownCover)}% вне 13–15% " +
                $"(глобально {F(100f * snap.Patches.crownCover)}%)");
            Assert.AreEqual(0, snap.Patches.trunksInForbidden, "G8: стволы в запрете");
            yield return null;
        }

        // ------------------------------------------------------------------
        // Карта высот и классов — из того же пола, без второй сборки
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Map_HeightAndClasses()
        {
            ForestLandscapeReport.WriteMap(builder, MapFile, 512);
            Assert.IsTrue(File.Exists(MapFile), "карта высот/классов не записана");
            Record("map", true, MapFile + " (512×512, зелёный/оранжевый/красный)");
            yield return null;
        }

        // ------------------------------------------------------------------
        // Hydro M16: своя сборка с гидро
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Hydro_Gate()
        {
            var h1 = BuildHydraulic(out var e1);
            float bakeMs = h1.bakeMs;
            string status = h1.status;
            ulong hash = h1.hash;
            string key = h1.key;
            float buildMs = BuildHydraulicMs;

            var h2 = BuildHydraulic(out var e2);
            float secondBakeMs = h2.bakeMs;
            Record("G0-hydro", status == "ok" && bakeMs <= 60000f && h2.hash == hash,
                $"key={key} status={status} bake={F(bakeMs)} мс сборка={F(buildMs / 1000f)} с " +
                $"повторный bake={F(secondBakeMs)} мс hash={hash:x16}=={h2.hash:x16}");
            Assert.AreEqual("ok", status, "M16: гидро не запечено и не из кеша");
            Assert.LessOrEqual(bakeMs, 60000f, "M16: первое запекание >60 с");
            Assert.AreEqual(hash, h2.hash, "M16: повторная сборка дала другой ассет");
            yield return null;
        }

        static float BuildHydraulicMs;
        static int hydroBuilds;

        static ForestLandscapeMetrics.HydroResult BuildHydraulic(out DomeErosion.State state)
        {
            var h = new GameObject("~HydroGate");
            h.SetActive(false);
            var b = ForestProfiles.PavilionV2(h, 1337L, erosion: true);
            // Уникальная версия и чистый каталог: M16 — это ПЕРВОЕ запекание,
            // а не «кеш с прошлого прогона». Второй вызов читает тот же ключ.
            string dir = Path.Combine(Application.temporaryCachePath, "ErosionGate");
            if (hydroBuilds == 0 && Directory.Exists(dir)) Directory.Delete(dir, true);
            b.erosionVersion = 99;
            b.erosionCacheDir = dir;
            hydroBuilds++;
            h.SetActive(true);
            var sw = Stopwatch.StartNew();
            b.Build();
            sw.Stop();
            BuildHydraulicMs = (float)sw.Elapsed.TotalMilliseconds;
            state = b.Erosion;
            var result = ForestLandscapeMetrics.Hydro(b);
            b.Clear();
            Object.Destroy(h);
            return result;
        }

        // ------------------------------------------------------------------

        static void Record(string gate, bool ok, string details)
        {
            verdicts.Add($"{gate}\t{(ok ? "PASS" : "FAIL")}\t{details}");
        }

        static void WriteGates()
        {
            EnsureDir(GateFile);
            File.WriteAllText(GateFile, string.Join("\n", verdicts) + "\n", Encoding.UTF8);
        }

        static void WriteMetrics()
        {
            EnsureDir(MetricsFile);
            var s = snap;
            var sb = new StringBuilder();
            sb.AppendLine($"seed={s.seed} worldVersion={s.worldVersion} quads={s.quads} " +
                          $"mesh={F(s.meshStep)} fingerprint={s.fingerprint:x16}");
            sb.AppendLine($"chunks={s.chunks} trees={s.trees} colliders={s.treeColliders} " +
                          $"deadLogs={s.deadLogs} lairs={s.lairs} build={F(s.BuildMs / 1000f)}s");
            sb.AppendLine("M1 " + s.Slope);
            sb.AppendLine($"M2 E={F(s.Energy)} M3 meso={F(100f * s.Meso)}% " +
                          $"({s.MesoGood}/{s.MesoWindows}) M3b local={F(100f * s.Local)}%");
            sb.AppendLine($"M4 H/D: {Forms(s.Forms)}");
            sb.AppendLine($"M4b P/N={F(s.Balance.ratio)} P={s.Balance.positiveVolume:E3} " +
                          $"N={s.Balance.negativeVolume:E3}");
            sb.AppendLine($"M5 seams={F(s.Seams.maxSlopeJump)}° minR={F(s.Seams.minCurvatureRadius)} м");
            sb.AppendLine($"M6 edge={F(100f * s.Los.edgeShare)}% fieldP50={F(s.Los.fieldP50)} м");
            sb.AppendLine($"M7 zones={s.Los.zones} six={s.Los.zonesWithSixRays} " +
                          $"corridor={F(s.Los.corridorAlongMin)}×{F(s.Los.corridorAcrossMin)} м");
            sb.AppendLine($"M8 zones={s.Poi.zones} smooth={s.Poi.smooth} " +
                          $"approaches={s.Poi.twoApproaches} noTrunks={s.Poi.noTrunks} " +
                          $"cover={s.Poi.covered} widthP10={F(s.Poi.corridorWidthP10)} " +
                          $"widthP50={F(s.Poi.corridorWidthP50)}");
            sb.AppendLine($"M9 lairs={s.Water.lairs} dry={s.Water.dry} ring={s.Water.outsideRing} " +
                          $"waterP50={F(s.Water.waterP50)} waterMax={F(s.Water.waterMax)} " +
                          $"spread={F(s.Water.spreadMin)}");
            sb.AppendLine($"M9b waterholes={s.Water.waterholes} approaches={s.Water.approachesOk} " +
                          $"cover={s.Water.coverOk} ford={F(s.Water.fordDepth)} " +
                          $"bank={F(s.Water.bankSlopeMax)} width={F(s.Water.waterWidthMin)}");
            sb.AppendLine($"M13 climb={F(100f * s.Climb.shareClimbable)}% wall={F(100f * s.Climb.shareWall)}% " +
                          $"faces={s.Climb.faces} len={F(s.Climb.faceLengthAvg)} " +
                          $"ledges={s.Climb.ledges}/{s.Climb.ledgesHigh} bypass={s.Climb.bypass}");
            sb.AppendLine($"M14 patches={s.Patches.patchCount} area={F(100f * s.Patches.patchAreaShare)}% " +
                          $"local={F(s.Patches.localDensityPerKm2)}/км² " +
                          $"global={F(s.Patches.globalDensityPerKm2)}/км² " +
                          $"crown={F(100f * s.Patches.crownCover)}% forbidden={s.Patches.trunksInForbidden}");
            sb.AppendLine($"M15 roads built={s.Roads.built} pois={s.Roads.primaryPois} " +
                          $"components={s.Roads.components} loops={s.Roads.loops} " +
                          $"len={F(s.Roads.lengthKm)} p95={F(s.Roads.slopeP95)} " +
                          $"widthP10={F(s.Roads.widthP10)} fords={s.Roads.fords}");
            sb.AppendLine($"M16 key={s.Hydro.key} status={s.Hydro.status} bake={F(s.Hydro.bakeMs)} " +
                          $"hash={s.Hydro.hash:x16} res={s.Hydro.resolution}");
            sb.AppendLine($"M11 renderers={s.Budget.renderers} tris={s.Budget.triangles} " +
                          $"colliders={s.Budget.colliders}/{s.Budget.trees - s.Budget.deadLogs}");
            File.WriteAllText(MetricsFile, sb.ToString(), Encoding.UTF8);
        }

        static string Forms(List<ForestLandscapeMetrics.FormInfo> forms)
        {
            if (forms == null || forms.Count == 0) return "нет";
            var sb = new StringBuilder();
            foreach (var f in forms)
                sb.Append($"{f.name} H/D={F(f.aspect)} {F(f.maxSlope)}°; ");
            return sb.ToString();
        }

        static void EnsureDir(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }

        static string F(float v) => v.ToString("0.###", Inv);
    }
}
