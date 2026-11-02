using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// S5 шаг 2: детектор-отчёт. Строит мир НАСТОЯЩИМ <see cref="ForestBuilder"/> профилем
/// ворот (<see cref="ForestProfiles.PavilionV2"/>), меряет <see cref="ForestLandscapeMetrics"/>
/// через <see cref="ForestLandscapeReport"/> и пишет <c>Docs/Диаграммы/Лес/ВЕРИФИКАЦИЯ.md</c>.
/// <para>
/// Один вызов <c>run_script</c> ограничен 30 с главного потока пайплайна, а полная сборка
/// v2 дольше. Поэтому здесь — ОДИН сид без гидро (M16 и 20 сидов идут через PlayMode-тесты,
/// где ограничения нет), а <see cref="OneSeed"/> добавляет строку в CSV для матрицы.
/// </para>
/// </summary>
public static class ForestVerify
{
    const string Report = "Docs/Диаграммы/Лес/ВЕРИФИКАЦИЯ.md";
    const string SeedsCsv = "Docs/Диаграммы/Лес/ВЕРИФИКАЦИЯ-сиды.csv";
    const string SeedsMarker = "<!-- SEEDS -->";
    const string GatesFile = "Docs/Диаграммы/Лес/ВЕРИФИКАЦИЯ-ворота.txt";
    const string MetricsFile = "Docs/Диаграммы/Лес/ВЕРИФИКАЦИЯ-метрики.txt";
    const string MapPath = "Docs/Диаграммы/Лес/ВЕРИФИКАЦИЯ-карта.png";

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ------------------------------------------------------------------
    // Входные точки пайплайна
    // ------------------------------------------------------------------

    /// <summary>Один сид 1337: метрики и вердикты ворот. Без гидро — см. HydroProbe/PlayMode.</summary>
    public static string Run()
    {
        var snap = Build(1337L, hydraulic: false, thermal: true);
        var sb = new StringBuilder();
        sb.AppendLine("# ВЕРИФИКАЦИЯ: метрики и ворота ландшафта (S5)");
        sb.AppendLine();
        sb.AppendLine("> Генерируется `Tools/Agent/ForestVerify.cs`. Руками не править.");
        sb.AppendLine("> Профиль: `ForestPavilion` v2, Ø2000, `terrainQuads 48`, сид 1337,");
        sb.AppendLine("> композиция S2, тепловая эрозия S4 (гидро — отдельный замер).");
        sb.AppendLine("> Пороги — S5 §3/§6; числа генерации — S1/S2/S4.");
        sb.AppendLine();
        WriteProfile(sb, snap);
        WriteMetrics(sb, snap);
        WriteGates(sb, snap);
        sb.AppendLine("## 20 сидов (v2, Ø2000, профиль 48)");
        sb.AppendLine();
        sb.AppendLine(SeedsMarker);
        sb.AppendLine();
        sb.AppendLine("## Пять полных сборок: A / C / B0 / Pavilion v1 / v2");
        sb.AppendLine();
        sb.AppendLine("Полные сборки и отпечатки сторожат `ForestBaselineTests` (A/C/B0/Pavilion v1)");
        sb.AppendLine("и `ForestLandscapeGateTests` (v2). Паритет сериализованных полей —");
        sb.AppendLine("`ForestSceneParityTests`. Здесь числа не дублируются: отчёт ссылается на них.");
        sb.AppendLine();
        sb.AppendLine("## Остаток и долг");
        sb.AppendLine();
        sb.AppendLine("- **G7 (кадры A/B)** — не снят этим прогоном: нужны камера, тур Р1–Р4 и");
        sb.AppendLine("  три независимых смотрящих. Это остаток шага 6 плана, не зелёный.");
        sb.AppendLine("- **G5 (вода/логова)** — отчёт B1 по решению 8; красное не блокирует S3.");
        sb.AppendLine("- **Гидро M16** — полный бейк 512² дольше 30 с главного потока `run_script`;");
        sb.AppendLine("  замер идёт через `ForestLandscapeGateTests.Hydro_Gate` (PlayMode, без потолка 30 с).");
        sb.AppendLine("- **Карта высот/классов** — `ВЕРИФИКАЦИЯ-карта.png`, генерируется `ForestVerify.Map`.");
        sb.AppendLine();
        File.WriteAllText(Report, sb.ToString(), Encoding.UTF8);
        return $"written {Report}\n" + Summary(snap);
    }

    /// <summary>Только сборка: отделить время мира от времени метрик.</summary>
    public static string Probe(bool hydraulic = false)
    {
        var host = new GameObject("~Probe");
        host.SetActive(false);
        var b = ForestProfiles.PavilionV2(host, 1337L, hydraulic);
        b.buildThermalErosion = true;
        host.SetActive(true);
        var sw = Stopwatch.StartNew();
        b.Build();
        sw.Stop();
        string result = $"build={sw.Elapsed.TotalSeconds:F2}s chunks={b.Chunks} trees={b.Trees} " +
                        $"hydro={(b.Erosion != null ? b.Erosion.hydraulicStatus : "off")}";
        b.Clear();
        UnityEngine.Object.Destroy(host);
        return result;
    }

    /// <summary>Один сид матрицы: строка CSV. 20 вызовов — по одному на команду.</summary>
    public static string OneSeed(int seed)
    {
        var snap = Build(seed, hydraulic: false, thermal: true);
        if (!File.Exists(SeedsCsv))
            File.WriteAllText(SeedsCsv,
                "seed;p95;over22;over35;energy;meso;local;balance;seam;minR;climb;poiWidth;" +
                "patchDensity;crown;tris;buildMs\n", Encoding.UTF8);
        File.AppendAllText(SeedsCsv,
            string.Join(";", new[]
            {
                snap.seed.ToString(Inv),
                F(snap.Slope.p95Field),
                F(100f * (snap.Slope.climbableField + snap.Slope.wallField)),
                F(100f * snap.Slope.wallField),
                F(snap.Energy),
                F(100f * snap.Meso),
                F(100f * snap.Local),
                F(snap.Balance.ratio),
                F(snap.Seams.maxSlopeJump),
                F(snap.Seams.minCurvatureRadius),
                F(100f * snap.Climb.shareClimbable),
                F(snap.Poi.corridorWidthP10),
                F(snap.Patches.localDensityPerKm2),
                F(100f * snap.Patches.crownCover),
                snap.Budget.triangles.ToString(Inv),
                F(snap.BuildMs),
            }) + "\n", Encoding.UTF8);
        return Summary(snap);
    }

    /// <summary>Собрать отчёт из метрик, ворот и CSV 20 сидов (без пересборки мира).</summary>
    public static string Finish()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# ВЕРИФИКАЦИЯ: метрики и ворота ландшафта (S5)");
        sb.AppendLine();
        sb.AppendLine("> Генерируется `Tools/Agent/ForestVerify.cs` из `ВЕРИФИКАЦИЯ-метрики.txt`,");
        sb.AppendLine("> `ВЕРИФИКАЦИЯ-ворота.txt` и `ВЕРИФИКАЦИЯ-сиды.csv`. Руками не править.");
        sb.AppendLine("> Профиль: `ForestPavilion` v2, Ø2000, `terrainQuads 48`, сид 1337,");
        sb.AppendLine("> композиция S2, тепловая эрозия S4. Пороги — S5 §3/§6; числа генерации — S1/S2/S4.");
        sb.AppendLine();
        sb.AppendLine("## Метрики M1–M16 (сид 1337)");
        sb.AppendLine();
        if (File.Exists(MetricsFile))
        {
            sb.AppendLine("```");
            sb.AppendLine(File.ReadAllText(MetricsFile, Encoding.UTF8).TrimEnd());
            sb.AppendLine("```");
        }
        else sb.AppendLine("_(нет `" + MetricsFile + "`)_");
        sb.AppendLine();

        sb.AppendLine("## Ворота (сид 1337, профиль 48)");
        sb.AppendLine();
        if (File.Exists(GatesFile))
        {
            sb.AppendLine("```");
            foreach (var line in File.ReadAllLines(GatesFile, Encoding.UTF8))
            {
                // G6 в блоке ворот — «отдельный прогон»; его вердикт живёт в
                // разделе 20 сидов ниже, чтобы не было двух разных G6.
                if (line.StartsWith("G6\t")) continue;
                sb.AppendLine(line);
            }
            sb.AppendLine("G6\tсм. раздел 20 сидов ниже");
            sb.AppendLine("```");
        }
        else sb.AppendLine("_(нет `" + GatesFile + "`)_");
        sb.AppendLine();

        sb.AppendLine("## Приоритеты P0/P1 и что дальше");
        sb.AppendLine();
        sb.AppendLine("### P0 — блокирует приёмку S5");
        sb.AppendLine();
        sb.AppendLine("- **G1:** доля `>22°` ниже 2 % (см. блок ворот) — мир «поле»; нужна");
        sb.AppendLine("  лазаемая полоса/форма (S2), порог не трогать.");
        sb.AppendLine("- **G2:** `P/N` вне 0.80–1.20 и излом >2° — перебалансировать композицию");
        sb.AppendLine("  (добавить выемки) и профиль формы; порог не трогать.");
        sb.AppendLine("- **G4:** `p95` уклона дорог >12° — S4 least-cost не держит уклон;");
        sb.AppendLine("  чинить дороги, не порог.");
        sb.AppendLine("- **G8:** сборка >12 с, локальная плотность <3000, крона <13 % —");
        sb.AppendLine("  бюджет и плотность; при красном уменьшается ПЛОЩАДЬ пятна, не плотность");
        sb.AppendLine("  (решение 10), а сборку — оптимизировать.");
        sb.AppendLine("- **G0:** `|NavMesh.y − SampleGround| > 0.5 м` на ходимой точке —");
        sb.AppendLine("  проверить коллайдеры холма/площадки и порядок бейка (S3).");
        sb.AppendLine("- **G7:** кадры A/B и тур Р1–Р4 не сняты — остаток шага 6; числами");
        sb.AppendLine("  не закрывается, провал старше зелёных чисел.");
        sb.AppendLine();
        sb.AppendLine("### P1 — долг и отчёт");
        sb.AppendLine();
        sb.AppendLine("- **G5:** вода `p50` ≈616 м, разнос 3.6 м, укрытие у воды 0 —");
        sb.AppendLine("  долг B1, не блок S3 (решение 8).");
        sb.AppendLine("- **M16:** первое запекание ~1.3 с, повтор ~0.06 с, хэш совпал —");
        sb.AppendLine("  в потолке; но сборка мира ~70 с — это G8, не M16.");
        sb.AppendLine("- **20 сидов:** `p10` подтверждает G1/G2/G8 красными; G6 — FAIL,");
        sb.AppendLine("  не «среднее».");
        sb.AppendLine("- **M0 на 20 сидах:** отпечатки не сняты в CSV (там метрики);");
        sb.AppendLine("  уникальность `Fingerprint` на 20 сидах — остаток. Существующий");
        sb.AppendLine("  `GenTraceFingerprintTests` сторожит один сид.");
        sb.AppendLine("- **Карта высот/классов:** оранжевого/красного мало, что согласуется с");
        sb.AppendLine("  `1.56 %` лазаемого и `0.27 %` стен; это визуальное подтверждение G1,");
        sb.AppendLine("  а не замена воротам.");
        sb.AppendLine("- **Расхождение плана S5, шаг 1:** «синус λ=500 → E_λ≈1» неверно для");
        sb.AppendLine("  определения M2 (`Var(box250)/Var` даёт ≈0.405). Порог 0.60 относится");
        sb.AppendLine("  к H0, а не к чистой волне. Держит");
        sb.AppendLine("  `ForestLandscapeMetricsTests.Sine500_Box250_IsAboutFortyPercent`;");
        sb.AppendLine("  порог не подкручен.");
        sb.AppendLine();
        sb.AppendLine("### Что дальше");
        sb.AppendLine();
        sb.AppendLine("1. P0 по порядку: G1/G2 (композиция S2) → G4 (дороги S4) →");
        sb.AppendLine("   G0 (NavMesh/холм S3) → G8 (бюджет/плотность) → G7 (кадры).");
        sb.AppendLine("2. Перегнать `ForestLandscapeGateTests` на профиле 48 и");
        sb.AppendLine("   `ForestSeedMatrixTests`; обновить отчёт.");
        sb.AppendLine("3. G7: снять пары A/B (скрыть `SpacePaths`, `ArenaEdge`, `CoverPods`, P6)");
        sb.AppendLine("   на 0/50/100 м, тур 8–10, три независимых смотрящих.");
        sb.AppendLine("4. Только после этого — приёмка S1/S2/S3; плейтест остаётся");
        sb.AppendLine("   за геймдизайнером.");
        sb.AppendLine();
        sb.AppendLine("## 20 сидов (v2, Ø2000, профиль 48)");
        sb.AppendLine();
        var rows = new List<double[]>();
        var seeds = new List<int>();
        if (File.Exists(SeedsCsv))
        {
            var lines = File.ReadAllLines(SeedsCsv, Encoding.UTF8);
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var parts = lines[i].Split(';');
                if (parts.Length < 17) continue;
                seeds.Add(int.Parse(parts[0], Inv));
                var row = new double[16];
                for (int k = 0; k < 16; k++)
                    row[k] = double.Parse(parts[k + 1], Inv);
                rows.Add(row);
            }
        }
        if (rows.Count == 0)
        {
            sb.AppendLine("_(нет `" + SeedsCsv + "`)_");
        }
        else
        {
            sb.AppendLine($"Сидов: **{rows.Count}**. Ворота — на `p10`, не на среднем.");
            sb.AppendLine("Тепловая эрозия включена; гидро выключено ради времени — M16");
            sb.AppendLine("меряется отдельным `Hydro_Gate` на одном профиле (сид 1337).");
            sb.AppendLine();
            sb.AppendLine("| метрика | p10 | p50 | p90 | худший сид |");
            sb.AppendLine("|---|---:|---:|---:|---:|");
            AddRow(sb, "p95 уклона, °", rows, seeds, 0, false);
            AddRow(sb, "доля >22 %, %", rows, seeds, 1, true);
            AddRow(sb, "доля >35 %, %", rows, seeds, 2, true);
            AddRow(sb, "max|∇H| по форме, °", rows, seeds, 3, true);
            AddRow(sb, "M2 E_λ", rows, seeds, 4, false);
            AddRow(sb, "M3 мезо, %", rows, seeds, 5, false);
            AddRow(sb, "M3b локальный, %", rows, seeds, 6, false);
            AddRow(sb, "P/N (объём)", rows, seeds, 7, false);
            AddRow(sb, "M5 излом, °", rows, seeds, 8, true);
            AddRow(sb, "M5 minR, м", rows, seeds, 9, false);
            AddRow(sb, "M13 лазаемо, %", rows, seeds, 10, false);
            AddRow(sb, "M8 ширина коридоров p10, м", rows, seeds, 11, false);
            AddRow(sb, "M14 локальная плотность, /км²", rows, seeds, 12, false);
            AddRow(sb, "M14 крона, %", rows, seeds, 13, false);
            AddRow(sb, "треугольники", rows, seeds, 14, true);
            AddRow(sb, "сборка, мс", rows, seeds, 15, true);
            sb.AppendLine();
            sb.AppendLine("**Вердикт по 20 сидам (p10):**");
            sb.AppendLine($"- G1: p95 `{F((float)P10(rows, 0))}°`, >22° `{F((float)P10(rows, 1))} %`, " +
                          $">35° `{F((float)P10(rows, 2))} %`, maxForm `{F((float)P10(rows, 3))}°`.");
            sb.AppendLine($"- G2: E_λ `{F((float)P10(rows, 4))}`, мезо `{F((float)P10(rows, 5))} %`, " +
                          $"локальный 20–100 `{F((float)P10(rows, 6))} %`, P/N `{F((float)P10(rows, 7))}`.");
            sb.AppendLine($"- G8: локальная плотность `{F((float)P10(rows, 12))}/км²`, " +
                          $"крона `{F((float)P10(rows, 13))} %`, сборка `{F((float)P10(rows, 15) / 1000f)} с`.");
            sb.AppendLine($"- Худший сид по p95: `{Worst(rows, seeds, 0, true)}`; " +
                          $"по локальному охвату: `{Worst(rows, seeds, 6, false)}`.");
            bool g6 = P10(rows, 1) >= 2.0 && P10(rows, 1) <= 4.0 &&
                      P10(rows, 6) >= 60.0 &&
                      P10(rows, 12) >= 3000.0 && P10(rows, 12) <= 3500.0 &&
                      P10(rows, 13) >= 13.0 && P10(rows, 13) <= 15.0 &&
                      P10(rows, 15) <= 12000.0;
            sb.AppendLine($"- **G6: {(g6 ? "PASS" : "FAIL")}** — по `p10`: " +
                          $"G1 `{(P10(rows, 1) >= 2.0 && P10(rows, 1) <= 4.0 ? "PASS" : "FAIL")}`, " +
                          $"G2-локально `{(P10(rows, 6) >= 60.0 ? "PASS" : "FAIL")}`, " +
                          $"G8-плотность `{(P10(rows, 12) >= 3000.0 && P10(rows, 12) <= 3500.0 ? "PASS" : "FAIL")}`, " +
                          $"G8-крона `{(P10(rows, 13) >= 13.0 && P10(rows, 13) <= 15.0 ? "PASS" : "FAIL")}`, " +
                          $"G8-сборка `{(P10(rows, 15) <= 12000.0 ? "PASS" : "FAIL")}`.");
            sb.AppendLine("- `0` в P/N, ширине коридора и плотности — метрика **неприменима**");
            sb.AppendLine("  на сиде (нет форм/зон/пятна), а не «идеальное значение».");
        }
        sb.AppendLine();

        sb.AppendLine("## Пять полных сборок: A / C / B0 / Pavilion v1 / v2");
        sb.AppendLine();
        sb.AppendLine("- A/C/B0/Pavilion v1 — `ForestBaselineTests` (PlayMode): замороженные");
        sb.AppendLine("  отпечатки и адреса S0; v1 не сдвинут.");
        sb.AppendLine("- v2 — `ForestLandscapeGateTests` (PlayMode): профиль ворот, отпечаток");
        sb.AppendLine("  `feac1f81ce1cdcc9` (сид 1337).");
        sb.AppendLine("- Паритет сериализованных полей — `ForestSceneParityTests` (EditMode):");
        sb.AppendLine("  перечень ведёт `SerializedObject`, а не рука.");
        sb.AppendLine();

        sb.AppendLine("## Остаток и долг");
        sb.AppendLine();
        sb.AppendLine("- **G7 (кадры A/B)** — НЕ ЗАКРЫТ: нужны камера, тур Р1–Р4 и три");
        sb.AppendLine("  независимых смотрящих. Это остаток шага 6 плана, не зелёный.");
        sb.AppendLine("- **G5 (вода/логова)** — отчёт B1 по решению 8; красное не блокирует S3.");
        sb.AppendLine("- **Гидро M16** — `Hydro_Gate` (PlayMode) с уникальным ключом и чистым");
        sb.AppendLine("  каталогом: первое запекание и повтор из кеша; см. `ВЕРИФИКАЦИЯ-ворота.txt`.");
        sb.AppendLine("- **Карта высот/классов** — `ВЕРИФИКАЦИЯ-карта.png` (`ForestVerify.Map`).");
        sb.AppendLine("- **Профиль 48** — всегда; 16 допустим только как отладочный прогон при зелёном G0.");
        sb.AppendLine();
        File.WriteAllText(Report, sb.ToString(), Encoding.UTF8);
        return $"written {Report} (seeds {rows.Count})";
    }

    /// <summary>Карта высот и классов уклона из SampleGround (без второго поля).</summary>
    public static string Map(int size = 512)
    {
        var host = new GameObject("~VerifyMap");
        host.SetActive(false);
        var b = ForestProfiles.PavilionV2(host, 1337L, false);
        host.SetActive(true);
        b.Build();
        var cfg = ForestLandscapeReport.ConfigFor(b);
        float rin = DomeRockRing.InnerRadius(cfg);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var colors = new Color[size * size];
        float min = float.MaxValue, max = float.MinValue;
        var heights = new float[size * size];
        for (int iz = 0; iz < size; iz++)
            for (int ix = 0; ix < size; ix++)
            {
                float x = Mathf.Lerp(-rin, rin, ix / (size - 1f));
                float z = Mathf.Lerp(-rin, rin, iz / (size - 1f));
                float y = b.SampleGround(x, z);
                heights[iz * size + ix] = y;
                if (y < min) min = y;
                if (y > max) max = y;
            }
        for (int iz = 0; iz < size; iz++)
            for (int ix = 0; ix < size; ix++)
            {
                float x = Mathf.Lerp(-rin, rin, ix / (size - 1f));
                float z = Mathf.Lerp(-rin, rin, iz / (size - 1f));
                if (x * x + z * z > rin * rin)
                {
                    colors[iz * size + ix] = new Color(0f, 0f, 0f, 0f);
                    continue;
                }
                float t = max > min ? Mathf.InverseLerp(min, max, heights[iz * size + ix]) : 0f;
                var c = Color.Lerp(new Color(0.10f, 0.25f, 0.12f),
                    new Color(0.95f, 0.92f, 0.78f), t);
                float slope = SlopeClasses.SlopeAt(cfg, x, z,
                    (a, b2) => b.SampleGround(a, b2), SlopeClasses.ClassEpsilon);
                var cls = SlopeClasses.Classify(slope, cfg);
                if (cls == SlopeClass.Climbable)
                    c = Color.Lerp(c, new Color(0.95f, 0.65f, 0.15f), 0.55f);
                else if (cls == SlopeClass.Wall)
                    c = Color.Lerp(c, new Color(0.75f, 0.12f, 0.12f), 0.75f);
                colors[iz * size + ix] = c;
            }
        tex.SetPixels(colors);
        tex.Apply();
        string dir = Path.GetDirectoryName(MapPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(MapPath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        b.Clear();
        UnityEngine.Object.Destroy(host);
        UnityEngine.Object.DestroyImmediate(cfg);
        return $"map {MapPath} (height {min:F1}..{max:F1} м)";
    }

    // ------------------------------------------------------------------
    // Сборка
    // ------------------------------------------------------------------

    static ForestLandscapeReport.Snapshot Build(long seed, bool hydraulic, bool thermal)
    {
        var host = new GameObject("~Verify");
        host.SetActive(false);
        var b = ForestProfiles.PavilionV2(host, seed, hydraulic);
        b.buildThermalErosion = thermal;
        host.SetActive(true);
        var sw = Stopwatch.StartNew();
        b.Build();
        sw.Stop();
        var snap = ForestLandscapeReport.Measure(b, (float)sw.Elapsed.TotalMilliseconds);
        b.Clear();
        UnityEngine.Object.Destroy(host);
        return snap;
    }

    // ------------------------------------------------------------------
    // Отчёт
    // ------------------------------------------------------------------

    static void WriteProfile(StringBuilder sb, ForestLandscapeReport.Snapshot s)
    {
        sb.AppendLine("```");
        sb.AppendLine($"seed={s.seed} worldVersion={s.worldVersion} quads={s.quads} " +
                      $"mesh={F(s.meshStep)} fingerprint={s.fingerprint:x16}");
        sb.AppendLine($"chunks={s.chunks} trees={s.trees} colliders={s.treeColliders} " +
                      $"deadLogs={s.deadLogs} lairs={s.lairs} build={F(s.BuildMs / 1000f)}s");
        sb.AppendLine("```");
        sb.AppendLine();
    }

    static void WriteMetrics(StringBuilder sb, ForestLandscapeReport.Snapshot s)
    {
        sb.AppendLine("## Метрики M1–M16 (сид 1337)");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine("M1 slope(field 1m)  " + s.Slope);
        sb.AppendLine($"M2 E_lambda={F(s.Energy)}  M3 meso={100f * s.Meso:F1}% " +
                      $"({s.MesoGood}/{s.MesoWindows})  M3b local20_100={100f * s.Local:F1}%");
        sb.AppendLine($"M4 H/D по формам: {FormsText(s.Forms)}");
        sb.AppendLine($"M4b P/N vol={F(s.Balance.ratio)} " +
                      $"(P={s.Balance.positiveVolume:E3}, N={s.Balance.negativeVolume:E3})");
        sb.AppendLine($"M5 seams max={F(s.Seams.maxSlopeJump)}°  minR={F(s.Seams.minCurvatureRadius)} m");
        sb.AppendLine($"M6 ориентир edgeShare={100f * s.Los.edgeShare:F1}% " +
                      $"fieldP50={F(s.Los.fieldP50)} m");
        sb.AppendLine($"M7 LOS zones={s.Los.zones} с 6/8={s.Los.zonesWithSixRays} " +
                      $"corridor along={F(s.Los.corridorAlongMin)} across={F(s.Los.corridorAcrossMin)}");
        sb.AppendLine($"M8 POI/зоны={s.Poi.zones} ровных={s.Poi.smooth} 2подхода={s.Poi.twoApproaches} " +
                      $"0стволов={s.Poi.noTrunks} под12={s.Poi.covered} " +
                      $"ширина p10={F(s.Poi.corridorWidthP10)} p50={F(s.Poi.corridorWidthP50)}");
        sb.AppendLine($"M9/M9b вода: логова={s.Water.lairs} сухих={s.Water.dry} " +
                      $"внекольца={s.Water.outsideRing} вода p50={F(s.Water.waterP50)} " +
                      $"max={F(s.Water.waterMax)} разнос={F(s.Water.spreadMin)} | " +
                      $"водопои={s.Water.waterholes} подходы={s.Water.approachesOk} " +
                      $"укрытие12={s.Water.coverOk} брод={F(s.Water.fordDepth)} " +
                      $"берег={F(s.Water.bankSlopeMax)}° ширина={F(s.Water.waterWidthMin)}");
        sb.AppendLine($"M13 climb band={100f * s.Climb.shareClimbable:F1}% стен={100f * s.Climb.shareWall:F1}% " +
                      $"грани={s.Climb.faces} длина={F(s.Climb.faceLengthAvg)} " +
                      $"уступы≤0.5={s.Climb.ledges} >0.5={s.Climb.ledgesHigh} обход={s.Climb.bypass}");
        sb.AppendLine($"M14 пятна={s.Patches.patchCount} площадь={100f * s.Patches.patchAreaShare:F1}% " +
                      $"локально={s.Patches.localDensityPerKm2:F0}/км² глобально=" +
                      $"{s.Patches.globalDensityPerKm2:F0}/км² крона={100f * s.Patches.crownCover:F1}% " +
                      $"стволов в запрете={s.Patches.trunksInForbidden}");
        sb.AppendLine($"M15 дороги built={s.Roads.built} POI={s.Roads.primaryPois} " +
                      $"компонент={s.Roads.components} петель={s.Roads.loops} " +
                      $"длина={F(s.Roads.lengthKm)} км p95={F(s.Roads.slopeP95)}° " +
                      $"ширина p10={F(s.Roads.widthP10)} бродов={s.Roads.fords}");
        sb.AppendLine($"M16 hydro key={s.Hydro.key} status={s.Hydro.status} " +
                      $"bake={F(s.Hydro.bakeMs)} ms hash={s.Hydro.hash:x16} res={s.Hydro.resolution}" +
                      "  (гидро выключено в этом прогоне — см. PlayMode Hydro_Gate)");
        sb.AppendLine($"M11 budget renderers={s.Budget.renderers} tris={s.Budget.triangles} " +
                      $"colliders={s.Budget.colliders}/{s.Budget.trees - s.Budget.deadLogs}");
        sb.AppendLine("```");
        sb.AppendLine();
    }

    static void WriteGates(StringBuilder sb, ForestLandscapeReport.Snapshot s)
    {
        bool g1 = s.Slope.p95Field >= 16f && s.Slope.p95Field <= 20f &&
                  s.Slope.climbableField + s.Slope.wallField >= 0.02f &&
                  s.Slope.climbableField + s.Slope.wallField <= 0.04f &&
                  s.Slope.wallField <= 0.02f && s.Slope.maxGradPerForm <= 22f &&
                  s.Slope.threeClassesDistinct && s.Slope.pairCompatible;
        bool g2 = s.Energy >= 0.60f && s.Meso >= 0.60f && s.Local >= 0.60f &&
                  (!s.Balance.measurable ||
                   (s.Balance.ratio >= 0.80f && s.Balance.ratio <= 1.20f)) &&
                  s.Seams.maxSlopeJump <= 2f + 1e-4f && s.Seams.minCurvatureRadius >= 5f;
        bool g3 = s.Los.edgeShare >= 0.80f && s.Los.zonesWithSixRays == s.Los.zones &&
                  s.Los.corridorAlongMin >= 28f && s.Los.corridorAcrossMin >= 16f;
        bool g4 = s.Poi.smooth == s.Poi.zones && s.Poi.twoApproaches == s.Poi.zones &&
                  s.Poi.noTrunks == s.Poi.zones && s.Poi.covered == s.Poi.zones &&
                  s.Poi.corridorWidthP10 >= 16f &&
                  s.Roads.built && s.Roads.components == 1 && s.Roads.loops >= 1 &&
                  s.Roads.loops <= 2 && s.Roads.lengthKm >= 2f && s.Roads.lengthKm <= 4f &&
                  s.Roads.slopeP95 <= 12f && s.Roads.widthP10 >= 2.9f;
        bool g8 = s.Budget.renderers <= 8000 && s.Budget.triangles <= 800000 &&
                  s.BuildMs <= 12000f &&
                  s.Budget.colliders == s.Budget.trees - s.Budget.deadLogs &&
                  s.Patches.localDensityPerKm2 >= 3000f &&
                  s.Patches.localDensityPerKm2 <= 3500f &&
                  s.Patches.crownCover >= 0.13f && s.Patches.crownCover <= 0.15f &&
                  s.Patches.trunksInForbidden == 0;

        sb.AppendLine("## Ворота (сид 1337, профиль 48)");
        sb.AppendLine();
        sb.AppendLine("| ворота | вердикт | ключевые числа |");
        sb.AppendLine("|---|---|---|");
        sb.AppendLine($"| G0 воспроизводимость/паритет/гидро | REPORT | " +
                      $"паритет — `ForestSceneParityTests`; hydro — PlayMode `Hydro_Gate` |");
        sb.AppendLine($"| G1 уклон | {(g1 ? "PASS" : "FAIL")} | " +
                      $"p95={s.Slope.p95Field:F1}°, >22°={100f * (s.Slope.climbableField + s.Slope.wallField):F2}%, " +
                      $">35°={100f * s.Slope.wallField:F2}%, maxForm={s.Slope.maxGradPerForm:F1}° |");
        sb.AppendLine($"| G2 поле и вертикаль | {(g2 ? "PASS" : "FAIL")} | " +
                      $"E={s.Energy:F2}, мезо={100f * s.Meso:F1}%, локально={100f * s.Local:F1}%, " +
                      $"P/N={s.Balance.ratio:F2}, излом={s.Seams.maxSlopeJump:F2}°, R={s.Seams.minCurvatureRadius:F1} |");
        sb.AppendLine($"| G3 ориентир и видимость | {(g3 ? "PASS" : "FAIL")} | " +
                      $"кромка={100f * s.Los.edgeShare:F1}%, зон 6/8={s.Los.zonesWithSixRays}/{s.Los.zones}, " +
                      $"коридор {s.Los.corridorAlongMin:F0}×{s.Los.corridorAcrossMin:F0} м |");
        sb.AppendLine($"| G4 POI, зоны и дороги | {(g4 ? "PASS" : "FAIL")} | " +
                      $"ровных {s.Poi.smooth}/{s.Poi.zones}, 2подхода {s.Poi.twoApproaches}/{s.Poi.zones}, " +
                      $"ширина p10={s.Poi.corridorWidthP10:F1}, дороги {s.Roads.components} комп., " +
                      $"{s.Roads.loops} петли, {s.Roads.lengthKm:F2} км |");
        sb.AppendLine($"| G5 вода и логова | REPORT (B1) | " +
                      $"сухо {s.Water.dry}/{s.Water.lairs}, вода p50={s.Water.waterP50:F0} м, " +
                      $"разнос {s.Water.spreadMin:F0} м |");
        sb.AppendLine($"| G6 20 сидов | см. ниже | p10, худший сид |");
        sb.AppendLine($"| G7 кадр A/B | ОСТАТОК | шаг 6 плана: тур Р1–Р4 и три смотрящих |");
        sb.AppendLine($"| G8 бюджет и плотность | {(g8 ? "PASS" : "FAIL")} | " +
                      $"рендереров={s.Budget.renderers}, тр={s.Budget.triangles}, " +
                      $"сборка={s.BuildMs / 1000f:F1} с, локально={s.Patches.localDensityPerKm2:F0}/км², " +
                      $"крона={100f * s.Patches.crownCover:F1}%, в запрете={s.Patches.trunksInForbidden} |");
        sb.AppendLine();
    }

    static string FormsText(List<ForestLandscapeMetrics.FormInfo> forms)
    {
        if (forms == null || forms.Count == 0) return "форм нет";
        var sb = new StringBuilder();
        foreach (var f in forms)
            sb.Append($"{f.name} H/D={f.aspect:F3} {f.maxSlope:F1}°; ");
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // 20 сидов
    // ------------------------------------------------------------------

    static void AddRow(StringBuilder sb, string name, List<double[]> rows, List<int> seeds,
        int column, bool worstHigh)
    {
        var values = new List<float>();
        double worst = worstHigh ? double.MinValue : double.MaxValue;
        int worstSeed = 0;
        foreach (var row in rows)
        {
            double v = row[column];
            values.Add((float)v);
            if ((worstHigh && v > worst) || (!worstHigh && v < worst))
            {
                worst = v;
                worstSeed = seeds[rows.IndexOf(row)];
            }
        }
        sb.AppendLine($"| {name} | {F(Percent(values, 0.10f))} | {F(Percent(values, 0.50f))} | " +
                      $"{F(Percent(values, 0.90f))} | {F((float)worst)} (сид {worstSeed}) |");
    }

    static double P10(List<double[]> rows, int column)
    {
        var values = new List<float>();
        foreach (var r in rows) values.Add((float)r[column]);
        return Percent(values, 0.10f);
    }

    static string Worst(List<double[]> rows, List<int> seeds, int column, bool high)
    {
        double worst = high ? double.MinValue : double.MaxValue;
        int seed = 0;
        foreach (var row in rows)
        {
            double v = row[column];
            if ((high && v > worst) || (!high && v < worst)) { worst = v; seed = seeds[rows.IndexOf(row)]; }
        }
        return $"{seed}: {F((float)worst)}";
    }

    static float Percent(List<float> values, float p)
    {
        if (values.Count == 0) return 0f;
        var copy = new List<float>(values);
        copy.Sort();
        int i = Mathf.Clamp(Mathf.RoundToInt(p * (copy.Count - 1)), 0, copy.Count - 1);
        return copy[i];
    }

    static string Summary(ForestLandscapeReport.Snapshot s) =>
        $"seed={s.seed} chunks={s.chunks} trees={s.trees} " +
        $"p95={s.Slope.p95Field:F1}° >22={100f * (s.Slope.climbableField + s.Slope.wallField):F2}% " +
        $"E={s.Energy:F2} meso={100f * s.Meso:F1}% local={100f * s.Local:F1}% " +
        $"tris={s.Budget.triangles} build={s.BuildMs / 1000f:F1}s";

    static string F(float v) => v.ToString("0.###", Inv);
}

