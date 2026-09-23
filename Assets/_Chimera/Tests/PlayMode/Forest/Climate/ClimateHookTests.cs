using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// Хук погоды/ночи в чувства: дождь убивает нюх и смывает след, туман режет зрение днём,
    /// ночь режет зрение и щадит термо, ясно/днём хук прозрачен. Время выставляется явно,
    /// клок запрещён (флаки на границе заката). Статики сбрасываем в TearDown.
    /// Слайс s3b (ветка forest/s3b-senses-hook, спека 2026-09-18-forest-s3b).
    /// </summary>
    public class ClimateHookTests
    {
        readonly List<Object> trash = new List<Object>();

        Senses SeededSenses()
        {
            var go = new GameObject("~Senses");
            trash.Add(go);
            var senses = go.AddComponent<Senses>();
            senses.Set(SenseKind.Sight, 30f);
            senses.Set(SenseKind.Thermal, 14f);
            senses.Set(SenseKind.Scent, 20f);
            senses.Set(SenseKind.Hearing, 28f);
            return senses;
        }

        static void SetClimate(WeatherKind kind, float timeMinutes)
        {
            ForestClimate.CurrentState = new WeatherState { kind = kind };
            ForestClimate.CurrentTimeMinutes = timeMinutes;
        }

        [TearDown]
        public void Clean()
        {
            ForestClimate.ResetStatic();
            foreach (var o in trash) if (o != null) Object.Destroy(o);
            trash.Clear();
        }

        [UnityTest]
        public IEnumerator Rain_KillsScent()
        {
            SetClimate(WeatherKind.Rain, 10f);
            var senses = SeededSenses();
            Assert.AreEqual(0f, senses.Range(SenseKind.Scent), 1e-3f, "дождь: нюх закрыт");
            Assert.AreEqual(0f, WeatherModifier.ScentLifetimeMult(WeatherKind.Rain),
                "дождь: lifetime следов в ноль");

            var field = ScentField.Instance;
            trash.Add(field.gameObject);
            Vector3 dropping = new Vector3(0f, 0f, 0f);
            field.Drop(dropping);
            yield return null;
            Vector3 target;
            Assert.IsFalse(field.TryFollow(dropping, 10f, out target), "ливень смыл след за кадр");
        }

        [UnityTest]
        public IEnumerator Fog_Day_CutsSight()
        {
            SetClimate(WeatherKind.Fog, 10f);
            var senses = SeededSenses();
            Assert.AreEqual(30f * 0.45f, senses.Range(SenseKind.Sight), 1e-3f, "туман днём: зрение ×0.45");
            Assert.AreEqual(28f * 0.8f, senses.Range(SenseKind.Hearing), 1e-3f, "туман днём: слух ×0.8");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Clear_Night_SightDown_ThermalKept()
        {
            SetClimate(WeatherKind.Clear, 25f);
            var senses = SeededSenses();
            Assert.AreEqual(30f * 0.7f, senses.Range(SenseKind.Sight), 1e-3f, "ночь: зрение ×0.7");
            Assert.AreEqual(14f, senses.Range(SenseKind.Thermal), 1e-3f, "ночь: термо цел (биология змеи)");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Clear_Day_NoOp()
        {
            SetClimate(WeatherKind.Clear, 10f);
            var senses = SeededSenses();
            Assert.AreEqual(30f, senses.Range(SenseKind.Sight), 1e-3f, "хук прозрачен: зрение как засеяно");
            Assert.AreEqual(14f, senses.Range(SenseKind.Thermal), 1e-3f);
            Assert.AreEqual(20f, senses.Range(SenseKind.Scent), 1e-3f);
            Assert.AreEqual(28f, senses.Range(SenseKind.Hearing), 1e-3f);
            yield return null;
        }
    }
}
