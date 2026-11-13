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
        public void ApplyWet_ThenReset_RestoresDryColor()
        {
            var shader = Shader.Find("Sprites/Default");
            Assert.IsNotNull(shader, "нужен шейдер с _Color");
            var mat = new Material(shader);
            var dry = new Color(0.5f, 0.4f, 0.3f, 1f);
            mat.color = dry;

            DomeWeather.ApplyWet(new[] { mat }, 1f);
            Assert.Less(mat.color.r, dry.r, "мокрый материал темнее");

            DomeWeather.ResetWet();
            Assert.AreEqual(dry.r, mat.color.r, 1e-5f, "ResetWet возвращает сухой цвет");
            Assert.AreEqual(dry.g, mat.color.g, 1e-5f);
            Object.DestroyImmediate(mat);
        }

        [Test]
        public void ApplyWet_UrpLit_UsesBaseColor_AndResets()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.IsNotNull(shader, "URP Lit доступен");
            var mat = new Material(shader);
            var dry = new Color(0.5f, 0.4f, 0.3f, 1f);
            mat.SetColor("_BaseColor", dry);

            DomeWeather.ApplyWet(new[] { mat }, 1f);
            Assert.Less(mat.GetColor("_BaseColor").r, dry.r, "URP Lit мокнет через _BaseColor");
            DomeWeather.ResetWet();
            Assert.AreEqual(dry.r, mat.GetColor("_BaseColor").r, 1e-5f, "ResetWet вернул сухой цвет");
            Object.DestroyImmediate(mat);
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
