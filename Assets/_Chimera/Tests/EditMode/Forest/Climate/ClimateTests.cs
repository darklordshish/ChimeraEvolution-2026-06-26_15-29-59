using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Ночь и погода как данные: таблица шансов, множители каналов, фаза суток,
    /// высотный туман, второй канал телеграфов.
    /// Слайс s3 (ветка forest/s3-night-weather, спека 2026-09-18-forest-s3).
    /// </summary>
    public class ClimateTests
    {
        static WeatherTable Table(float clear, float rain, float fog, float snow, float wind)
        {
            return new WeatherTable
            {
                clearWeight = clear,
                rainWeight = rain,
                fogWeight = fog,
                snowWeight = snow,
                windWeight = wind
            };
        }

        [Test]
        public void Table_Valid_IsClean_AndChancesSumToOne()
        {
            var t = Table(0.15f, 0.35f, 0.2f, 0.15f, 0.15f);
            Assert.IsEmpty(WeatherTable.Validate(t), "корректная таблица обязана быть чистой");
            float sum = 0f;
            foreach (WeatherKind k in System.Enum.GetValues(typeof(WeatherKind)))
                sum += WeatherTable.ChanceFor(t, k);
            Assert.AreEqual(1f, sum, 1e-5f, "шансы обязаны суммироваться в 1");
        }

        [Test]
        public void Table_BadWeights_AreReported()
        {
            Assert.IsNotEmpty(WeatherTable.Validate(Table(0.5f, 0.5f, 0.5f, 0f, 0f)),
                "сумма 1.5 обязана ловиться");
            Assert.IsNotEmpty(WeatherTable.Validate(Table(0f, -0.1f, 0f, 0f, 0f)),
                "отрицательный вес обязан ловиться");
            Assert.IsNotEmpty(WeatherTable.Validate(Table(0f, 0f, 0f, 0f, 0f)),
                "нулевая сумма обязана ловиться");
        }

        [Test]
        public void Rain_CutsVisibilityHearing_SmellAndLifetimeToZero()
        {
            Assert.AreEqual(0.7f, WeatherModifier.VisibilityMult(WeatherKind.Rain), "дождь режет видимость");
            Assert.AreEqual(0.75f, WeatherModifier.HearingMult(WeatherKind.Rain), "дождь маскирует слух");
            Assert.AreEqual(0f, WeatherModifier.SmellMult(WeatherKind.Rain), "дождь смывает запах");
            Assert.AreEqual(0f, WeatherModifier.ScentLifetimeMult(WeatherKind.Rain),
                "дождь обязан резать lifetime следов, а не только нос");
        }

        [Test]
        public void Fog_CutsVisibility_ToPointFourFive()
        {
            Assert.AreEqual(0.45f, WeatherModifier.VisibilityMult(WeatherKind.Fog));
            Assert.AreEqual(1.25f, WeatherModifier.CueLoudnessMult(WeatherKind.Fog),
                "в тумане бустим объявление приёма");
        }

        [Test]
        public void Snow_SlowsMove_WindKeepsVisibility()
        {
            Assert.AreEqual(0.9f, WeatherModifier.MoveMult(WeatherKind.Snow), "снег вязнет");
            Assert.AreEqual(1f, WeatherModifier.MoveMult(WeatherKind.Clear), "ясно не замедляет");
            Assert.AreEqual(1f, WeatherModifier.VisibilityMult(WeatherKind.Wind), "ветер видимость не режет");
        }

        [Test]
        public void Clear_AllOnes()
        {
            Assert.AreEqual(1f, WeatherModifier.VisibilityMult(WeatherKind.Clear));
            Assert.AreEqual(1f, WeatherModifier.HearingMult(WeatherKind.Clear));
            Assert.AreEqual(1f, WeatherModifier.SmellMult(WeatherKind.Clear));
            Assert.AreEqual(1f, WeatherModifier.ScentLifetimeMult(WeatherKind.Clear));
            Assert.AreEqual(1f, WeatherModifier.MoveMult(WeatherKind.Clear));
            Assert.AreEqual(1f, WeatherModifier.CueLoudnessMult(WeatherKind.Clear));
        }

        [Test]
        public void Phase_Boundaries_Wrap_AndNegative()
        {
            Assert.AreEqual(0f, DayNightCycle.Phase01(0f), 1e-5f, "рассвет — 0");
            Assert.AreEqual(0.7f, DayNightCycle.Phase01(21f), 1e-5f, "закат — 0.7");
            Assert.AreEqual(0f, DayNightCycle.Phase01(30f), 1e-5f, "сутки замкнуты");
            Assert.AreEqual(DayNightCycle.Phase01(5f), DayNightCycle.Phase01(35f), 1e-5f, "wrap");
            Assert.AreEqual(25f / 30f, DayNightCycle.Phase01(-5f), 1e-5f, "отрицательное время корректно");
        }

        [Test]
        public void IsNight_DayAndNight()
        {
            Assert.IsFalse(DayNightCycle.IsNight(10f), "10-я минута — день");
            Assert.IsTrue(DayNightCycle.IsNight(21f), "21-я — уже ночь");
            Assert.IsTrue(DayNightCycle.IsNight(25f), "25-я — ночь");
            Assert.IsFalse(DayNightCycle.IsNight(30f), "30-я — снова рассвет");
        }

        [Test]
        public void FogDensity_FallsWithHeight_AndClamps()
        {
            float low = WeatherModifier.FogDensityAt(0f, 0.8f, 0.05f);
            float high = WeatherModifier.FogDensityAt(10f, 0.8f, 0.05f);
            Assert.AreEqual(0.8f, low, 1e-5f, "внизу густо");
            Assert.Greater(low, high, "с высотой спадает монотонно");
            Assert.AreEqual(0f, WeatherModifier.FogDensityAt(100f, 0.8f, 0.05f), "вверху чисто (кламп 0)");
            Assert.AreEqual(1f, WeatherModifier.FogDensityAt(-10f, 0.8f, 0.05f), "внизу кламп 1");
        }

        [Test]
        public void Wind_ValidatesDirection()
        {
            var ok = new WeatherState { kind = WeatherKind.Wind, windDir = new Vector2(1f, 0f), windStrength = 3f };
            Assert.IsEmpty(WeatherState.Validate(ok));
            var bad = new WeatherState { kind = WeatherKind.Wind, windDir = Vector2.zero, windStrength = 3f };
            Assert.IsNotEmpty(WeatherState.Validate(bad), "ветер без направления обязан ловиться");
        }

        [Test]
        public void Channels_Valid_AndBadCaught()
        {
            var good = new TelegraphChannels
            {
                color = Color.red,
                outlineColor = Color.white,
                outlineWidth = 2f,
                pulseFreq = 4f
            };
            Assert.IsEmpty(TelegraphChannels.Validate(good));
            var noWidth = good;
            noWidth.outlineWidth = 0f;
            Assert.IsNotEmpty(TelegraphChannels.Validate(noWidth), "нулевая обводка обязана ловиться");
            var noPulse = good;
            noPulse.pulseFreq = 0f;
            Assert.IsNotEmpty(TelegraphChannels.Validate(noPulse), "нулевой пульс обязан ловиться");
        }
    }
}
