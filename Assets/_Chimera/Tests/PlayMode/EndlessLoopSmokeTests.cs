using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// СТОРОЖ ВЕЧНОГО ЦИКЛА (спека вечной эволюции 2026-11-12, §5 п.1.8, критерии К-1…К-4): настоящая `SampleScene`.
    /// Смерть игрока не перезагружает сцену и ведёт Жив → Лежит → Встаёт; пока лежит — неуязвим и невидим для NPC;
    /// часы ускоряются на срок сна; босс включён в сцене, помнит состав и возвращается им; награда за тип — один раз.
    /// Для детерминизма сцена очищена от рядовых NPC и их спавнеров, срок сна сокращён — арифметика та же, что в игре.
    /// </summary>
    public class EndlessLoopSmokeTests
    {
        PlayerController pc;
        Health hp;
        PlayerDeath death;
        WorldClock clock;
        BossChimeraSpawner boss;

        [UnitySetUp]
        public IEnumerator Load()
        {
            Time.timeScale = 1f;
            Perception.PlayerGhost = false;
            Perception.PlayerHidden = false;
            BossMemory.Clear();
            SuperBossReward.ResetClaims();

            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            yield return null;
            yield return null;

            pc = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(pc, "в SampleScene нет игрока");
            hp = pc.GetComponent<Health>();
            death = pc.GetComponent<PlayerDeath>();
            clock = Object.FindAnyObjectByType<WorldClock>();
            boss = Object.FindAnyObjectByType<BossChimeraSpawner>();

            // изоляция: без рядовых NPC и их спавнеров игрока никто не добьёт и не воскресит мимо теста
            foreach (var s in Object.FindObjectsByType<WolfSpawner>()) s.enabled = false;
            foreach (var s in Object.FindObjectsByType<SnakeSpawner>()) s.enabled = false;
            foreach (var s in Object.FindObjectsByType<MooseSpawner>()) s.enabled = false;
            foreach (var s in Object.FindObjectsByType<HedgehogSpawner>()) s.enabled = false;
            foreach (var h in Object.FindObjectsByType<Health>())
                if (h.gameObject != pc.gameObject) Object.Destroy(h.gameObject);
            yield return null;
        }

        // ВЫГРУЗКА АРЕНЫ: сцена грузится Single и без выгрузки оставляла игрока и зверей арены всем следующим тестам
        // прогона (письмо Лесника 12.11: шесть лесных PlayMode-классов краснели только в общем прогоне).
        [UnityTearDown]
        public IEnumerator Reset()
        {
            Time.timeScale = 1f;
            Perception.PlayerGhost = false;
            Perception.PlayerHidden = false;
            BossMemory.Clear();
            SuperBossReward.ResetClaims();

            var arena = SceneManager.GetSceneByName("SampleScene");
            if (!arena.IsValid() || !arena.isLoaded) yield break;
            var empty = SceneManager.CreateScene("Пусто-после-вечного-цикла");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(arena);
        }

        [UnityTest]
        public IEnumerator Death_DoesNotReloadScene_PlayerLiesThenRisesWhereHeFell()
        {
            Assert.IsNotNull(death, "на игроке нет PlayerDeath");
            Assert.IsNotNull(clock, "в сцене нет WorldClock");
            clock.Configure(12f, 0.5f, 8f); // полчаса игрового времени = ~1.9 с реальных

            var sceneBefore = SceneManager.GetActiveScene();
            var marker = new GameObject("Маркер-мира");
            Vector3 fell = pc.transform.position;
            double hoursBefore = clock.Hours;

            hp.TakeDamage(100000, true);
            yield return null; yield return null;

            Assert.AreEqual(PlayerDeath.Phase.Lying, death.State, "после смерти игрок лежит");
            Assert.AreEqual(sceneBefore.handle, SceneManager.GetActiveScene().handle, "сцена не перезагружена");
            Assert.IsTrue(marker != null, "объекты мира пережили смерть");
            Assert.IsFalse(pc.enabled, "управление снято");
            Assert.IsTrue(clock.IsSleeping, "мир ускоряется на срок сна");
            Assert.AreEqual(8f, Time.timeScale, 1e-3f, "ускорение мира во сне");

            // (б) пока лежит — урон не проходит, а для чувств NPC игрока нет
            int cur = hp.Current;
            hp.TakeDamage(50);
            hp.TakeDamage(50, true);
            Assert.AreEqual(cur, hp.Current, "лежащему урон не наносится");
            Assert.IsTrue(Perception.PlayerHidden, "игрок невидим для NPC");
            Assert.IsFalse(Perception.HasLineOfSight(fell + Vector3.forward * 5f, pc.transform), "глаза NPC лежащего не видят");

            float t0 = Time.realtimeSinceStartup;
            while (death.State != PlayerDeath.Phase.Alive && Time.realtimeSinceStartup - t0 < 30f)
            {
                if (death.State == PlayerDeath.Phase.Rising)
                {
                    Assert.AreEqual(hp.Max, hp.Current, "встаёт с полным HP");
                    Assert.IsTrue(hp.Invulnerable, "пока встаёт — неуязвим");
                }
                yield return null;
            }

            Assert.AreEqual(PlayerDeath.Phase.Alive, death.State, "игрок встал");
            Assert.Greater(hp.Current, 0);
            Assert.IsTrue(pc.enabled, "управление вернулось");
            Assert.IsFalse(Perception.PlayerHidden, "снова виден");
            Assert.Less(Vector3.Distance(pc.transform.position, fell), 0.5f, "встал там же, где упал");
            Assert.AreEqual(sceneBefore.handle, SceneManager.GetActiveScene().handle, "сцена всё ещё та же");
            Assert.IsTrue(marker != null, "мир не сброшен");

            // (в) часы продвинулись на срок сна, мир вернулся к ×1
            Assert.GreaterOrEqual(clock.Hours - hoursBefore, 0.5, "WorldClock продвинулся на срок сна");
            Assert.AreEqual(1f, Time.timeScale, 1e-3f, "после сна ×1");
            Assert.AreEqual(1, death.Deaths);
            StringAssert.Contains("Прошло", death.LastReport.ToString());
        }

        [UnityTest]
        public IEnumerator BossSpawner_IsEnabledInScene()
        {
            Assert.IsNotNull(boss, "в сцене нет BossChimeraSpawner");
            Assert.IsTrue(boss.AutoSpawn, "автоспавн босса включён в сцене");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Boss_Dies_IsRemembered_ComesBackWithSameComposition_RewardOnce()
        {
            Perception.PlayerGhost = true; // игрок невидим: босс его не трогает, пока тест идёт
            var body = CreatureBody.PlayerBody;
            Vector3 spot = pc.transform.position + new Vector3(6f, 0f, 0f);
            int slotsBefore = body.ChimeraSlots;

            var b1 = boss.Spawn(BossChimeraSpawner.Recipe.Werebeast, spot);
            Assert.IsNotNull(b1, "босс не собрался из доноров игрока");
            yield return null;
            string name1 = b1.name;
            string comp1 = ChimeraFactory.Describe(b1);
            int slots1 = b1.SlotCount;

            b1.GetComponent<Health>().TakeDamage(10000000, true);
            yield return null;

            Assert.IsTrue(BossMemory.TryGet("Босс-оборотень", out var rec), "смерть босса записана в BossMemory");
            Assert.Greater(rec.entries.Count, 0, "в записи есть органы");
            Assert.AreEqual(name1, rec.displayName);
            Assert.IsTrue(SuperBossReward.IsClaimed("Босс-оборотень"), "награда выдана");
            Assert.AreEqual(slotsBefore + 1, body.ChimeraSlots, "+1 химерный слот за первое убийство типа");
            StringAssert.Contains("ПОВЕРЖЕН", EndlessHud.CurrentBanner ?? "", "победа видна на экране");
            Assert.IsNull(boss.Current, "спавнер отпустил убитого");

            // следующий спавн того же типа — из записи: каждый орган на месте
            var b2 = boss.Spawn(BossChimeraSpawner.Recipe.Werebeast, spot + Vector3.forward * 3f);
            Assert.IsNotNull(b2);
            yield return null;
            Assert.AreEqual(name1, b2.name, "тот же тип и вид оборотня");
            foreach (var e in rec.entries)
            {
                var v = b2.GetSlot(e.index);
                Assert.IsTrue(v.installed, $"слот {e.index} ({e.slot}) должен нести орган записи");
                Assert.AreEqual(e.species, v.species, $"слот {e.index}: вид органа");
                Assert.AreEqual(e.organName, v.organName, $"слот {e.index}: орган");
            }
            Assert.GreaterOrEqual(b2.SlotCount, slots1, "тело не меньше прежнего");
            Assert.AreEqual(comp1, ChimeraFactory.Describe(b2), "состав возвращённого босса совпал");

            // (д) повторное убийство того же типа: награды нет, но игрок видит «вернётся»
            b2.GetComponent<Health>().TakeDamage(10000000, true);
            yield return null;
            Assert.AreEqual(slotsBefore + 1, body.ChimeraSlots, "награда за тип — один раз");
            StringAssert.Contains("вернётся", EndlessHud.CurrentBanner ?? "");
        }
    }
}
