using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// ЗАМЕР СБОРКИ ПО ШАГАМ (18.11, задача «карта как источник», п. 2): павильон Ø2000 сид 1337 в профилях v1 (как
    /// сцена `ForestPavilion` сейчас) и v2 (как павильон перешёл бы на v2: каньон и акведук заморожены, логова и маяк —
    /// как в сцене). Каждый профиль собирается дважды: первая сборка несёт разовые расходы домена (шейдеры, кеши
    /// мешей, запекание эрозии), вторая — цена мира как такового. Время шагов — из `GenTrace` (поле `ms`).
    /// Отчёт — `Docs/Диаграммы/Лес/СБОРКА.md`. Это замер, а не сторож: [Explicit].
    /// </summary>
    public class ForestBuildTimingTests
    {
        readonly StringBuilder report = new StringBuilder();
        readonly List<string> summary = new List<string>();

        [UnityTest, Explicit("замер — вручную"), Timeout(1800000)]
        public IEnumerator Pavilion_BuildSteps_V1_V2()
        {
            LogAssert.ignoreFailingMessages = true; // ошибку сборки пишем в отчёт, а не валим замер
            string cache = Path.Combine(Application.temporaryCachePath, "BuildTimingErosion");
            if (Directory.Exists(cache)) Directory.Delete(cache, true);

            var profiles = new (string name, System.Func<GameObject, ForestBuilder> make)[]
            {
                ("v1 (сцена сейчас)", h => ForestProfiles.PavilionV1(h)),
                ("v2 без эрозии", h => V2(h, thermal: false, hydraulic: false, cache)),
                ("v2 + тепловая эрозия (профиль ворот)", h => V2(h, thermal: true, hydraulic: false, cache)),
                ("v2 + тепловая и гидро-эрозия", h => V2(h, thermal: true, hydraulic: true, cache)),
            };
            foreach (var (name, make) in profiles)
                for (int pass = 1; pass <= 2; pass++)
                {
                    yield return Measure(name, pass, make);
                    yield return null;
                }
            Write();
        }

        static ForestBuilder V2(GameObject h, bool thermal, bool hydraulic, string cache)
        {
            var b = ForestProfiles.PavilionV2(h, 1337L, erosion: false);
            b.buildCanyon = false;     // заморожен 12.11, ассеты в теге-архиве
            b.buildAqueduct = false;   // то же
            b.buildAnimalLairs = true; // как в сцене павильона
            b.buildBeacon = true;
            b.buildThermalErosion = thermal;
            b.buildHydraulicErosion = hydraulic;
            b.erosionCacheDir = cache;
            return b;
        }

        IEnumerator Measure(string name, int pass, System.Func<GameObject, ForestBuilder> make)
        {
            var host = new GameObject("~BuildTiming");
            host.SetActive(false);
            var b = make(host);
            b.buildOnAwake = false;
            host.SetActive(true);
            var errors = new List<string>();
            Application.LogCallback onLog = (msg, st, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception) errors.Add(msg.Split('\n')[0]);
            };
            Application.logMessageReceived += onLog;
            var sw = Stopwatch.StartNew();
            try { b.Build(); }
            catch (System.Exception e) { errors.Add(e.GetType().Name + ": " + e.Message); }
            sw.Stop();
            Application.logMessageReceived -= onLog;

            report.AppendLine($"### {name} — сборка {pass}");
            report.AppendLine();
            report.AppendLine($"Всего **{sw.Elapsed.TotalSeconds:0.00} с**, worldVersion={b.WorldVersion}, " +
                              $"план {(b.Plan != null ? "есть" : "нет (null)")}, деревьев {b.Trees}.");
            if (errors.Count > 0) report.AppendLine($"Ошибки сборки ({errors.Count}): `{string.Join("` · `", errors.GetRange(0, Mathf.Min(3, errors.Count)))}`");
            report.AppendLine();
            report.AppendLine("```");
            report.Append(b.Trace != null ? b.Trace.TimedText : "(нет трассы)\n");
            report.AppendLine("```");
            report.AppendLine();
            summary.Add($"| {name} | {pass} | {sw.Elapsed.TotalSeconds:0.00} | {Top(b)} |");
            UnityEngine.Debug.Log($"[Forest] замер сборки: {name} #{pass} — {sw.Elapsed.TotalSeconds:0.00} с");

            b.Clear();
            Object.Destroy(host);
            yield return null;
        }

        /// <summary>Три самых дорогих шага: «слой мс».</summary>
        static string Top(ForestBuilder b)
        {
            if (b.Trace == null) return "—";
            var list = new List<GenTrace.Entry>(b.Trace.Entries);
            list.Sort((x, y) => y.ms.CompareTo(x.ms));
            var parts = new List<string>();
            for (int i = 0; i < Mathf.Min(3, list.Count); i++) parts.Add($"{list[i].layer} {list[i].ms / 1000f:0.0} с");
            return string.Join(", ", parts);
        }

        void Write()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Лес: сборка павильона по шагам");
            sb.AppendLine();
            sb.AppendLine("**Генерируемый отчёт** — `ForestBuildTimingTests` (PlayMode, [Explicit]); руками не правится.");
            sb.AppendLine("Павильон Ø2000, сид 1337, 48 квадов. Каждый профиль собран дважды подряд в одном домене: первая сборка");
            sb.AppendLine("несёт разовые расходы (шейдеры, кеши мешей, запекание эрозии в пустой кеш), вторая — цена мира как такового.");
            sb.AppendLine("Время шага — от предыдущей записи трассы до этой; шаги `t.*` — отметки замера, остальные — слои генератора.");
            sb.AppendLine("Редактор headless: годится для сравнения профилей и шагов, не для бюджета сборки игры.");
            sb.AppendLine();
            sb.AppendLine("| профиль | сборка | всего, с | дороже всего |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var s in summary) sb.AppendLine(s);
            sb.AppendLine();
            sb.Append(report);
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs", "Диаграммы", "Лес", "СБОРКА.md");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            UnityEngine.Debug.Log("[Forest] замер сборки: " + path);
        }
    }
}
