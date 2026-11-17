using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// ПРОГОН НА ВЫНОСЛИВОСТЬ первой зоны (план среза 12.11, §3 п. 6): настоящая сцена павильона, мир ускорен ×8, час
    /// игры. Каждую игровую минуту снимается численность по видам, доля химер, смерти, число объектов, память и время
    /// кадра; отчёт — `Docs/Диаграммы/Лес/ВЫНОСЛИВОСТЬ.md`. Это ЗАМЕР, а не сторож: [Explicit], в обычном прогоне и
    /// в гейте не идёт (≈8 минут). Запуск: `run_tests --mode playmode --filter ForestEnduranceTests --include_explicit true`.
    /// Время кадра в редакторе — не время кадра сборки: годится для сравнения прогонов, не для бюджета.
    /// </summary>
    public class ForestEnduranceTests
    {
        const float Speedup = 8f;          // потолок WorldClock: выше NavMesh и CC пробивают стены
        const float PlayMinutes = 60f;     // минут игры (реального времени игрока при ×1)
        const float SampleMinutes = 1f;

        readonly Dictionary<string, int> deaths = new Dictionary<string, int>();

        [UnityTearDown]
        public IEnumerator Unload()
        {
            Time.timeScale = 1f;
            ForestClimate.ResetStatic();
            var scene = SceneManager.GetSceneByName("ForestPavilion");
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Пусто-после-выносливости"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest, Explicit("замер ≈8 минут — вручную"), Timeout(1200000)]
        public IEnumerator Pavilion_HourOfPlay_AtSpeedup()
        {
            yield return SceneManager.LoadSceneAsync("ForestPavilion", LoadSceneMode.Single);
            yield return null;
            var builder = Object.FindAnyObjectByType<ForestBuilder>();
            Assert.IsNotNull(builder, "павильон загружен");
            var player = Object.FindAnyObjectByType<PlayerController>();
            Health.AnyDeath += OnDeath;

            var rows = new List<string>();
            long mem0 = Profiler.GetTotalAllocatedMemoryLong();
            Time.timeScale = Speedup;
            float playSeconds = 0f, nextSample = 0f;
            float frameSum = 0f, frameMax = 0f; int frames = 0;
            try
            {
                while (playSeconds < PlayMinutes * 60f)
                {
                    yield return null;
                    if (Time.timeScale != Speedup && !(WorldClock.Instance != null && WorldClock.Instance.IsSleeping))
                        Time.timeScale = Speedup; // хитстоп и подъём игрока сбрасывают ускорение
                    float dt = Time.unscaledDeltaTime;
                    playSeconds += Time.deltaTime;
                    frameSum += dt; frames++; if (dt > frameMax) frameMax = dt;
                    if (playSeconds >= nextSample)
                    {
                        rows.Add(Row(playSeconds, builder, frames > 0 ? frameSum / frames : 0f, frameMax, mem0));
                        Write(rows, player != null); // отчёт после каждого замера: обрыв прогона не теряет данные
                        frameSum = 0f; frameMax = 0f; frames = 0;
                        nextSample += SampleMinutes * 60f;
                    }
                }
                rows.Add(Row(playSeconds, builder, frames > 0 ? frameSum / frames : 0f, frameMax, mem0));
            }
            finally
            {
                Health.AnyDeath -= OnDeath;
                Time.timeScale = 1f;
            }

            Write(rows, player != null);
            Assert.Greater(Alive().Count, 0, "через час игры в лесу кто-то жив");
        }

        void OnDeath(Health victim, Vector3 at, Health killer)
        {
            var body = victim != null ? victim.GetComponent<CreatureBody>() : null;
            string kind = body == null ? "?" : body.GetComponent<PlayerController>() != null ? "игрок"
                : body.Chassis != null ? body.Chassis.speciesName : "?";
            var kb = killer != null ? killer.GetComponent<CreatureBody>() : null;
            string by = killer == null ? "?" : killer.GetComponent<PlayerController>() != null ? "игрок"
                : kb != null && kb.Chassis != null ? kb.Chassis.speciesName : "?";
            string key = kind + "←" + by;
            deaths[key] = deaths.TryGetValue(key, out int n) ? n + 1 : 1;
        }

        static List<CreatureBody> Alive() => Object.FindObjectsByType<CreatureBody>()
            .Where(b => b != null && b.GetComponent<PlayerController>() == null)
            .Where(b => { var h = b.GetComponent<Health>(); return h == null || !h.IsDead; }).ToList();

        string Row(float playSeconds, ForestBuilder builder, float frameMean, float frameMax, long mem0)
        {
            var alive = Alive();
            int Count(string sp) => alive.Count(b => b.Chassis != null && b.Chassis.speciesName == sp);
            int chimeric = alive.Count(ChimeraCensus.IsChimeric);
            int objects = Object.FindObjectsByType<Transform>().Length;
            long mem = Profiler.GetTotalAllocatedMemoryLong();
            string clock = WorldClock.Instance != null ? $"д{WorldClock.Instance.Day} {WorldClock.Instance.HourOfDay:0}ч" : "—";
            string dead = string.Join(" ", deaths.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value}"));
            return $"| {playSeconds / 60f:0} | {clock} | {Count("Волк")} | {Count("Змея")} | {Count("Лось")} | {Count("Ёж")} | " +
                   $"{alive.Count - Count("Волк") - Count("Змея") - Count("Лось") - Count("Ёж")} | {chimeric} | " +
                   $"{dead} | {objects} | {mem / 1048576f:0} (+{(mem - mem0) / 1048576f:0}) | {frameMean * 1000f:0.0} / {frameMax * 1000f:0} |";
        }

        void Write(List<string> rows, bool hadPlayer)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Лес: прогон на выносливость первой зоны");
            sb.AppendLine();
            sb.AppendLine("**Генерируемый отчёт** — `ForestEnduranceTests` (PlayMode, [Explicit]); руками не правится.");
            sb.AppendLine($"Сцена `ForestPavilion`, мир ×{Speedup:0}, {PlayMinutes:0} минут игры, игрок в сцене: {(hadPlayer ? "да (стоит у входа)" : "нет")}.");
            sb.AppendLine("Время кадра — в редакторе (headless), годится для сравнения прогонов, не для бюджета сборки.");
            sb.AppendLine();
            sb.AppendLine("| мин игры | часы мира | волк | змея | лось | ёж | прочие | химер | смертей с начала | объектов | память, МБ | кадр, мс (среднее / макс) |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var r in rows) sb.AppendLine(r);
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs", "Диаграммы", "Лес", "ВЫНОСЛИВОСТЬ.md");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[Forest] прогон на выносливость: " + path);
        }
    }
}
