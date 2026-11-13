using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Погода павильона (s10h): только ясно/дождь/туман (снега и ветра нет — тёплый биом).
/// Таблица шансов + мокрая земля (ступень затемнения альбедо, без ripple/puddle-нормалей —
/// вердикт художника). Туман по расписанию живёт в DomeCelestial (кривые суток).
/// </summary>
public static class DomeWeather
{
    public static WeatherTable PavilionTable()
    {
        return new WeatherTable
        {
            clearWeight = 0.6f,
            rainWeight = 0.25f,
            fogWeight = 0.15f,
            snowWeight = 0f,
            windWeight = 0f,
        };
    }

    /// <summary>Мокрость 0..1: альбедо темнеет на ступень (×(1−0.3w)), roughness не трогаем.</summary>
    public static Color WetAlbedo(Color dry, float wet)
    {
        wet = Mathf.Clamp01(wet);
        Color c = dry * (1f - 0.3f * wet);
        c.a = dry.a; // альфа не мокнет (поймано зондом: уплыла в 0.7)
        return c;
    }

    /// <summary>Применение мокрости к материалам с запоминанием базы (повторные вызовы не копят).</summary>
    public static void ApplyWet(IEnumerable<Material> mats, float wet)
    {
        foreach (var mat in mats)
        {
            if (mat == null) continue;
            string prop = mat.HasProperty("_BaseColor") ? "_BaseColor"
                : mat.HasProperty("_Color") ? "_Color" : null;
            if (prop == null) continue;
            if (!WetBases.TryGetValue(mat, out var dry))
            {
                dry = new WetBase { color = mat.GetColor(prop), prop = prop };
                WetBases[mat] = dry;
            }
            mat.SetColor(prop, WetAlbedo(dry.color, wet));
        }
    }

    struct WetBase
    {
        public Color color;
        public string prop;
    }

    static readonly Dictionary<Material, WetBase> WetBases = new Dictionary<Material, WetBase>();

    /// <summary>Вернуть сухие цвета живым материалам и очистить базы.</summary>
    public static void ResetWet()
    {
        foreach (var pair in WetBases)
            if (pair.Key != null && pair.Key.HasProperty(pair.Value.prop))
                pair.Key.SetColor(pair.Value.prop, pair.Value.color);
        WetBases.Clear();
    }
}
