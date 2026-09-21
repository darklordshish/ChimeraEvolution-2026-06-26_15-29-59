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
            if (mat == null || !mat.HasProperty("_Color")) continue;
            string key = mat.name + mat.GetInstanceID();
            if (!WetBases.TryGetValue(key, out var dry))
            {
                dry = mat.color;
                WetBases[key] = dry;
            }
            mat.color = WetAlbedo(dry, wet);
        }
    }

    static readonly Dictionary<string, Color> WetBases = new Dictionary<string, Color>();

    public static void ResetWet()
    {
        WetBases.Clear();
    }
}
