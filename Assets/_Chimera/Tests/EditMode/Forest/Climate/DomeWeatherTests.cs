using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Погода павильона: таблица валидна (снега/ветра нет, ясно в базе),
    /// мокрость темнит ступенью. Слайс s10h.
    /// </summary>
    public class DomeWeatherTests
    {
        [Test]
        public void PavilionTable_Valid_AndWarmOnly()
        {
            var table = DomeWeather.PavilionTable();
            Assert.IsEmpty(WeatherTable.Validate(table), "таблица чистая");
            Assert.AreEqual(0f, WeatherTable.ChanceFor(table, WeatherKind.Snow), "снега нет");
            Assert.AreEqual(0f, WeatherTable.ChanceFor(table, WeatherKind.Wind), "ветра нет");
            Assert.Greater(WeatherTable.ChanceFor(table, WeatherKind.Clear), 0.5f, "в базе ясно");
        }

        [Test]
        public void WetAlbedo_DarkensByStep()
        {
            var dry = new Color(0.5f, 0.5f, 0.5f);
            Assert.AreEqual(dry, DomeWeather.WetAlbedo(dry, 0f), "сухо — как было");
            Color wet = DomeWeather.WetAlbedo(dry, 1f);
            Assert.AreEqual(0.35f, wet.r, 1e-5f, "мокро — ×0.7");
            Assert.AreEqual(DomeWeather.WetAlbedo(dry, 5f), wet, "кламп сверху");
        }
    }
}
