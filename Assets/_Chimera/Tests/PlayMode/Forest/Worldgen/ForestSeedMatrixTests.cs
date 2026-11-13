using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// S5 шаг 7: 20 сидов v2 Ø2000/48. Ворота — на `p10`, худший сид назван числом.
    /// Тяжёлый прогон: `[Category("Slow")]`, в обычный набор не входит. Гидро выключено
    /// ради времени; M16 меряется отдельным `Hydro_Gate` на одном профиле — это названо,
    /// а не спрятано.
    /// </summary>
    [Category("Slow")]
    public class ForestSeedMatrixTests
    {
        const string Csv = "Docs/Диаграммы/Лес/ВЕРИФИКАЦИЯ-сиды.csv";
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        [UnityTest, Timeout(3600000)]
        public IEnumerator TwentySeeds_P10()
        {
            var rows = new List<double[]>();
            var seeds = new List<int>();
            var sb = new StringBuilder();
            sb.AppendLine("seed;p95;over22;over35;maxForm;energy;meso;local;balance;seam;minR;" +
                          "climb;poiWidth;patchDensity;crown;tris;buildMs");
            for (int seed = 1; seed <= 20; seed++)
            {
                var host = new GameObject("~Seed" + seed);
                host.SetActive(false);
                var b = ForestProfiles.PavilionV2(host, seed, erosion: false);
                b.buildThermalErosion = true;
                host.SetActive(true);
                var sw = Stopwatch.StartNew();
                b.Build();
                sw.Stop();
                var s = ForestLandscapeReport.Measure(b, (float)sw.Elapsed.TotalMilliseconds);
                var row = new double[]
                {
                    s.Slope.p95Field,
                    100f * (s.Slope.climbableField + s.Slope.wallField),
                    100f * s.Slope.wallField,
                    s.Slope.maxGradPerForm,
                    s.Energy,
                    100f * s.Meso,
                    100f * s.Local,
                    s.Balance.ratio,
                    s.Seams.maxSlopeJump,
                    s.Seams.minCurvatureRadius,
                    100f * s.Climb.shareClimbable,
                    s.Poi.corridorWidthP10,
                    s.Patches.localDensityPerKm2,
                    100f * s.Patches.crownCover,
                    s.Budget.triangles,
                    s.BuildMs,
                };
                rows.Add(row);
                seeds.Add(seed);
                sb.Append(seed.ToString(Inv));
                foreach (double v in row) sb.Append(';').Append(v.ToString("0.###", Inv));
                sb.AppendLine();
                UnityEngine.Debug.Log($"[S5 seeds] {seed}: p95={F(row[0])}° " +
                                      $">22={F(row[1])}% local={F(row[6])}% tris={row[14]:0}");
                b.Clear();
                Object.Destroy(host);
                yield return null;
            }

            string dir = Path.GetDirectoryName(Csv);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(Csv, sb.ToString(), Encoding.UTF8);

            var p10 = new double[16];
            for (int c = 0; c < 16; c++)
                p10[c] = Percent(rows, c, 0.10f);
            UnityEngine.Debug.Log($"[S5 seeds] p10: p95={F(p10[0])}° >22={F(p10[1])}% " +
                                  $">35={F(p10[2])}% maxForm={F(p10[3])}° E={F(p10[4])} " +
                                  $"meso={F(p10[5])}% local={F(p10[6])}% P/N={F(p10[7])} " +
                                  $"seam={F(p10[8])}° minR={F(p10[9])} " +
                                  $"patch={F(p10[12])}/км² crown={F(p10[13])}% " +
                                  $"tris={p10[14]:0} build={F(p10[15] / 1000f)}s");
            UnityEngine.Debug.Log($"[S5 seeds] worst p95: {Worst(rows, seeds, 0, true)}; " +
                                  $"worst local: {Worst(rows, seeds, 6, false)}; " +
                                  $"worst build: {Worst(rows, seeds, 15, true)}");

            Assert.AreEqual(20, rows.Count, "20 сидов посчитаны");
            // G6 — не «зелёное по числам»: он фиксирует p10 и худший сид. Вердикт
            // G1–G4/G8 выносится по этим числам в отчёте, а не подгоняется здесь.
            Assert.Pass($"20 сидов: p10 p95={F(p10[0])}°, local={F(p10[6])}%, " +
                        $"patch={F(p10[12])}/км², build={F(p10[15] / 1000f)} с");
        }

        static double Percent(List<double[]> rows, int column, float p)
        {
            var values = new List<double>();
            foreach (var r in rows) values.Add(r[column]);
            values.Sort();
            int i = Mathf.Clamp(Mathf.RoundToInt(p * (values.Count - 1)), 0, values.Count - 1);
            return values[i];
        }

        static string Worst(List<double[]> rows, List<int> seeds, int column, bool high)
        {
            double worst = high ? double.MinValue : double.MaxValue;
            int seed = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                double v = rows[i][column];
                if ((high && v > worst) || (!high && v < worst)) { worst = v; seed = seeds[i]; }
            }
            return $"{seed}: {F((float)worst)}";
        }

        static string F(double v) => v.ToString("0.###", Inv);
    }
}
