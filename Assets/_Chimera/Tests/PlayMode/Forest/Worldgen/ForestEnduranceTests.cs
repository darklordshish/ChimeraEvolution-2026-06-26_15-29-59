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
    /// кадра. Это ЗАМЕР, а не сторож: [Explicit], в обычном прогоне и в гейте не идёт (≈8 минут на прогон).
    ///
    /// Два варианта (ГеймБосс 18.11, чтобы разделить причину «химер 0» после перевода на v2): сцена как есть (v2,
    /// `ВЫНОСЛИВОСТЬ.md`) и та же сцена с миром, пересобранным по `ForestProfiles.PavilionV1` (`ВЫНОСЛИВОСТЬ-v1.md`);
    /// сравнение рядом — `ВЫНОСЛИВОСТЬ-v1-v2.md` (убийства NPC→NPC и рождения химер). Время кадра в редакторе — не время
    /// кадра сборки: годится для сравнения прогонов, не для бюджета.
    /// </summary>
    public class ForestEnduranceTests
    {
        const float Speedup = 8f;          // потолок WorldClock: выше NavMesh и CC пробивают стены
        const float PlayMinutes = 60f;     // минут игры (реального времени игрока при ×1)
        const float SampleMinutes = 1f;

        sealed class Run
        {
            public string label;
            public readonly Dictionary<string, int> deaths = new Dictionary<string, int>();
            public int npcKills;
            public readonly HashSet<int> chimeraSeen = new HashSet<int>();
            public int chimeraAtStart;
            public int chimeraAtEnd;
            public int aliveAtEnd;
            public readonly List<string> rows = new List<string>();
        }

        Run current;

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
            var run = new Run { label = "v2 (сцена)" };
            yield return Hour(run, toV1: false, "ВЫНОСЛИВОСТЬ.md");
        }

        /// <summary>v1 и v2 подряд, тот же час при ×8, и сравнение рядом (≈16 минут).</summary>
        [UnityTest, Explicit("замер ≈16 минут — вручную"), Timeout(2400000)]
        public IEnumerator Pavilion_V1_vs_V2_HourOfPlay()
        {
            var v1 = new Run { label = "v1 (PavilionV1)" };
            yield return Hour(v1, toV1: true, "ВЫНОСЛИВОСТЬ-v1.md");
            yield return Unload();
            var v2 = new Run { label = "v2 (сцена)" };
            yield return Hour(v2, toV1: false, "ВЫНОСЛИВОСТЬ.md");

            var sb = new StringBuilder();
            sb.AppendLine("# Лес: выносливость первой зоны — v1 против v2");
            sb.AppendLine();
            sb.AppendLine("**Генерируемый отчёт** — `ForestEnduranceTests.Pavilion_V1_vs_V2_HourOfPlay` ([Explicit]); руками не правится.");
            sb.AppendLine($"Сцена `ForestPavilion`, мир ×{Speedup:0}, {PlayMinutes:0} минут игры; v1 — мир сцены, пересобранный по `ForestProfiles.PavilionV1`,");
            sb.AppendLine("кусты корма — заново по навмешу (как было на v1). Подробно по минутам — `ВЫНОСЛИВОСТЬ-v1.md` и `ВЫНОСЛИВОСТЬ.md`.");
            sb.AppendLine("«Родилось химер» — тела, хотя бы раз ставшие химерой за прогон (без бывших химерами в начале).");
            sb.AppendLine();
            sb.AppendLine("| мир | живых в конце | убийств NPC→NPC | смертей всего | родилось химер | химер в конце |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var r in new[] { v1, v2 })
                sb.AppendLine($"| {r.label} | {r.aliveAtEnd} | {r.npcKills} | {r.deaths.Values.Sum()} | {r.chimeraSeen.Count - r.chimeraAtStart} | {r.chimeraAtEnd} |");
            sb.AppendLine();
            foreach (var r in new[] { v1, v2 })
                sb.AppendLine($"- **{r.label}**, смерти (жертва←убийца): {string.Join(", ", r.deaths.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value}"))}");
            File.WriteAllText(ReportPath("ВЫНОСЛИВОСТЬ-v1-v2.md"), sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[Forest] выносливость v1 против v2:\n" + sb);
        }

        IEnumerator Hour(Run run, bool toV1, string report)
        {
            current = run;
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("ForestPavilion", LoadSceneMode.Single);
            var builder = Object.FindAnyObjectByType<ForestBuilder>();
            Assert.IsNotNull(builder, "павильон загружен");
            if (toV1)
            {
                // Кусты корма раскладываются в Start: гасим раскладчик до пересборки, чтобы он разложил их по миру v1.
                var forage = Object.FindAnyObjectByType<ForageSpawner>();
                if (forage != null) forage.gameObject.SetActive(false);
                RebuildAsV1(builder);
                if (forage != null) forage.gameObject.SetActive(true);
            }
            yield return null;
            Assert.AreEqual(toV1 ? 1 : 2, builder.WorldVersion, toV1 ? "мир пересобран по v1" : "сцена — v2");
            var player = Object.FindAnyObjectByType<PlayerController>();
            Health.AnyDeath += OnDeath;

            long mem0 = Profiler.GetTotalAllocatedMemoryLong();
            Time.timeScale = Speedup;
            float playSeconds = 0f, nextSample = 0f;
            float frameSum = 0f, frameMax = 0f; int frames = 0;
            bool first = true;
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
                    if (Time.frameCount % 30 == 0) WatchChimeras(run, ref first);
                    if (playSeconds >= nextSample)
                    {
                        run.rows.Add(Row(run, playSeconds, frames > 0 ? frameSum / frames : 0f, frameMax, mem0));
                        Write(run, report, player != null);
                        frameSum = 0f; frameMax = 0f; frames = 0;
                        nextSample += SampleMinutes * 60f;
                    }
                }
                WatchChimeras(run, ref first);
                run.rows.Add(Row(run, playSeconds, frames > 0 ? frameSum / frames : 0f, frameMax, mem0));
            }
            finally
            {
                Health.AnyDeath -= OnDeath;
                Time.timeScale = 1f;
            }
            var alive = Alive();
            run.aliveAtEnd = alive.Count;
            run.chimeraAtEnd = alive.Count(ChimeraCensus.IsChimeric);
            Write(run, report, player != null);
            Assert.Greater(run.aliveAtEnd, 0, "через час игры в лесу кто-то жив");
        }

        /// <summary>Мир сцены — по профилю v1 (поля билдера, кроме пула видов), пересборка на месте.</summary>
        static void RebuildAsV1(ForestBuilder b)
        {
            var host = new GameObject("~ProfileV1");
            try
            {
                var p = ForestProfiles.PavilionV1(host);
                // КОПИЯ: FromJsonOverwrite перезаписывает тот же объект списка — ссылка осталась бы на пустой пул.
                var pool = new List<SpeciesSO>(b.speciesPool);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(p), b);
                b.speciesPool = pool;
                b.buildOnAwake = true;
                b.Clear();
                b.Build();
            }
            finally { Object.Destroy(host); }
        }

        static void WatchChimeras(Run run, ref bool first)
        {
            foreach (var b in Alive())
                if (ChimeraCensus.IsChimeric(b) && run.chimeraSeen.Add(b.GetInstanceID()) && first) run.chimeraAtStart++;
            first = false;
        }

        void OnDeath(Health victim, Vector3 at, Health killer)
        {
            var run = current;
            var body = victim != null ? victim.GetComponent<CreatureBody>() : null;
            bool victimNpc = body != null && body.GetComponent<PlayerController>() == null;
            string kind = body == null ? "?" : !victimNpc ? "игрок" : body.Chassis != null ? body.Chassis.speciesName : "?";
            var kb = killer != null ? killer.GetComponent<CreatureBody>() : null;
            bool killerNpc = kb != null && kb.GetComponent<PlayerController>() == null;
            string by = killer == null ? "?" : kb == null ? "?" : !killerNpc ? "игрок" : kb.Chassis != null ? kb.Chassis.speciesName : "?";
            string key = kind + "←" + by;
            run.deaths[key] = run.deaths.TryGetValue(key, out int n) ? n + 1 : 1;
            if (victimNpc && killerNpc) run.npcKills++;
        }

        static List<CreatureBody> Alive() => Object.FindObjectsByType<CreatureBody>()
            .Where(b => b != null && b.GetComponent<PlayerController>() == null)
            .Where(b => { var h = b.GetComponent<Health>(); return h == null || !h.IsDead; }).ToList();

        static string Row(Run run, float playSeconds, float frameMean, float frameMax, long mem0)
        {
            var alive = Alive();
            int Count(string sp) => alive.Count(b => b.Chassis != null && b.Chassis.speciesName == sp);
            int chimeric = alive.Count(ChimeraCensus.IsChimeric);
            int objects = Object.FindObjectsByType<Transform>().Length;
            long mem = Profiler.GetTotalAllocatedMemoryLong();
            string clock = WorldClock.Instance != null ? $"д{WorldClock.Instance.Day} {WorldClock.Instance.HourOfDay:0}ч" : "—";
            string dead = string.Join(" ", run.deaths.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value}"));
            return $"| {playSeconds / 60f:0} | {clock} | {Count("Волк")} | {Count("Змея")} | {Count("Лось")} | {Count("Ёж")} | " +
                   $"{alive.Count - Count("Волк") - Count("Змея") - Count("Лось") - Count("Ёж")} | {chimeric} | " +
                   $"{run.npcKills} | {run.chimeraSeen.Count - run.chimeraAtStart} | " +
                   $"{dead} | {objects} | {mem / 1048576f:0} (+{(mem - mem0) / 1048576f:0}) | {frameMean * 1000f:0.0} / {frameMax * 1000f:0} |";
        }

        static string ReportPath(string name) =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs", "Диаграммы", "Лес", name);

        static void Write(Run run, string report, bool hadPlayer)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Лес: прогон на выносливость первой зоны — " + run.label);
            sb.AppendLine();
            sb.AppendLine("**Генерируемый отчёт** — `ForestEnduranceTests` (PlayMode, [Explicit]); руками не правится.");
            sb.AppendLine($"Сцена `ForestPavilion`, мир {run.label}, ×{Speedup:0}, {PlayMinutes:0} минут игры, игрок в сцене: {(hadPlayer ? "да (стоит у входа)" : "нет")}.");
            sb.AppendLine("Время кадра — в редакторе (headless), годится для сравнения прогонов, не для бюджета сборки.");
            sb.AppendLine();
            sb.AppendLine("| мин игры | часы мира | волк | змея | лось | ёж | прочие | химер | убийств NPC→NPC | родилось химер | смертей с начала | объектов | память, МБ | кадр, мс (среднее / макс) |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var r in run.rows) sb.AppendLine(r);
            File.WriteAllText(ReportPath(report), sb.ToString(), new UTF8Encoding(false));
        }
    }
}
