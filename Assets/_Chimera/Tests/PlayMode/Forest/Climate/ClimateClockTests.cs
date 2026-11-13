using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// Небо леса идёт по игровым часам: с `WorldClock` в сцене фаза суток климата — та же, что у часов, и сон
    /// игрока (ускорение часов) двигает небо вместе с миром. Первый кадр только сверяет время, погода не
    /// прокручивается разом. Спека вечной эволюции 12.11 (один носитель времени), П-3 леса 13.11.
    /// </summary>
    public class ClimateClockTests
    {
        readonly List<Object> trash = new List<Object>();

        [TearDown]
        public void Clean()
        {
            ForestClimate.ResetStatic();
            foreach (var o in trash) if (o != null) Object.Destroy(o);
            trash.Clear();
        }

        WorldClock Clock(float startHours)
        {
            var go = new GameObject("~WorldClock");
            trash.Add(go);
            var clock = go.AddComponent<WorldClock>();
            clock.Configure(12f, 6f, 8f);
            clock.Advance(startHours / clock.HoursPerSecond);
            return clock;
        }

        ForestClimate Climate(float timeMinutes)
        {
            var go = new GameObject("~Climate");
            trash.Add(go);
            var climate = go.AddComponent<ForestClimate>();
            climate.timeMinutes = timeMinutes;
            climate.timeScale = 0f; // свой темп выключен: двигаться может только от часов
            return climate;
        }

        static float ForestMinutes(WorldClock clock) =>
            clock.HourOfDay / 24f * DayNightCycle.DayLengthMinutes;

        [UnityTest]
        public IEnumerator Climate_FollowsWorldClock_IncludingSleepJump()
        {
            var clock = Clock(6f);
            var climate = Climate(0f);
            yield return null;

            Assert.AreEqual(ForestMinutes(clock), climate.timeMinutes, 0.05f,
                "климат взял фазу суток у часов, а не свою стартовую минуту");

            clock.Advance(3f / clock.HoursPerSecond); // три игровых часа разом — как сон игрока
            yield return null;

            Assert.AreEqual(ForestMinutes(clock), climate.timeMinutes, 0.05f,
                "часы ушли вперёд — небо ушло с ними, хотя свой темп климата нулевой");
            Assert.AreEqual(climate.timeMinutes, ForestClimate.CurrentTimeMinutes, 1e-4f,
                "чувства читают то же время, что и небо");
        }

        [UnityTest]
        public IEnumerator FirstFrame_SyncsTime_WithoutSpinningWeather()
        {
            Clock(18f); // вечер: далеко от стартовой минуты климата
            var climate = Climate(0f);
            climate.autoWeather = true;
            climate.weatherChangeMinutes = 0.5f;
            yield return null;

            Assert.AreEqual(0, climate.WeatherStep,
                "первый кадр сверил время, а не прожил его: погода не сменилась ни разу");
        }
    }
}
