using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// ИГРОВЫЕ ЧАСЫ (`WorldClock`, спека вечной эволюции 2026-11-12, 1.3): чистая арифметика времени и срок «сна».
    /// Часы двигаются через `Advance` — тот же вызов, что делает Update, — поэтому сцена и timeScale не нужны.
    /// </summary>
    public class WorldClockTests
    {
        GameObject go;
        WorldClock clock;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("часы");
            clock = go.AddComponent<WorldClock>();
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(go);

        [Test]
        public void HoursPerSecond_FollowsDayLength()
        {
            Assert.AreEqual(24f / 720f, WorldClock.HoursPerRealSecond(12f), 1e-6f, "сутки 12 минут = 720 с на 24 часа");
            clock.Configure(30f, 6f, 8f);
            Assert.AreEqual(24f / 1800f, clock.HoursPerSecond, 1e-6f);
        }

        [Test]
        public void Advance_AccumulatesHours_DayAndHourOfDayFollow()
        {
            clock.Configure(12f, 6f, 8f); // 720 с = сутки
            clock.Advance(360f);          // полсуток
            Assert.AreEqual(12.0, clock.Hours, 1e-3);
            Assert.AreEqual(1, clock.Day);
            clock.Advance(420f);          // ещё 14 ч → 26 ч от старта
            Assert.AreEqual(2, clock.Day);
            Assert.AreEqual(2f, clock.HourOfDay, 1e-2f);
        }

        [Test]
        public void Sleep_EndsAfterConfiguredHours_AndRaisesEvent()
        {
            clock.Configure(12f, 6f, 8f);
            int ended = 0;
            clock.SleepEnded += () => ended++;
            clock.Advance(60f); // стартовый сдвиг, чтобы срок считался от текущего момента, а не от нуля
            double start = clock.Hours;
            clock.BeginSleep();
            Assert.IsTrue(clock.IsSleeping);
            clock.Advance(100f); // 100 с × (24/720) = 3.33 ч — ещё спим
            Assert.IsTrue(clock.IsSleeping);
            Assert.AreEqual(0.55f, clock.SleepProgress01, 0.02f);
            clock.Advance(100f); // 6.67 ч ≥ 6
            Assert.IsFalse(clock.IsSleeping);
            Assert.AreEqual(1, ended, "событие конца сна ровно один раз");
            Assert.GreaterOrEqual(clock.Hours - start, 6.0, "мир прошёл не меньше срока");
        }

        [Test]
        public void SleepRealSeconds_AccountsForSpeedup_AndCapsIt()
        {
            Assert.AreEqual(22.5f, WorldClock.SleepRealSeconds(6f, 12f, 8f), 1e-3f, "6 ч при сутках 12 мин и ×8 — 22.5 с");
            Assert.AreEqual(WorldClock.SleepRealSeconds(6f, 12f, 8f), WorldClock.SleepRealSeconds(6f, 12f, 100f), 1e-4f,
                "выше потолка ×8 ускорение не растёт");
            clock.Configure(12f, 6f, 100f);
            Assert.AreEqual(WorldClock.MaxTimeScale, clock.SleepSpeedup);
        }

        [Test]
        public void ZeroFields_ReadAsDefaults()
        {
            clock.Configure(0f, 0f, 0f); // «не настроено» из старой сцены
            Assert.AreEqual(WorldClock.DefaultDayMinutes, clock.DayLengthMinutes);
            Assert.AreEqual(WorldClock.DefaultSleepHours, clock.SleepGameHours);
            Assert.AreEqual(WorldClock.DefaultSleepSpeedup, clock.SleepSpeedup);
        }

        [Test]
        public void PhaseAndNight_GoThroughDayNightCycle()
        {
            clock.Configure(12f, 6f, 8f);
            Assert.IsFalse(clock.IsNight, "старт — рассвет");
            clock.Advance(720f * 0.8f); // 0.8 суток — за закатом (0.7)
            Assert.IsTrue(clock.IsNight);
            Assert.AreEqual(0.8f, clock.Phase01, 1e-3f);
        }
    }
}
