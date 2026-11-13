using UnityEngine;

/// <summary>
/// ЭКРАННЫЕ СТРОКИ ВЕЧНОГО ЦИКЛА (спека вечной эволюции 2026-11-12, §5 п.1.4 и 1.7): итог смерти и победа над боссом
/// видны на экране, а не в логе. Простой OnGUI в стиле `DebugHud`; само-бутстрап — первый вызов `ShowBanner` / `SetDeathText`
/// заводит объект в сцене. Время гасит по `Time.realtimeSinceStartup`: во «сне» мир идёт ×8, а подпись должна жить в
/// реальных секундах.
/// </summary>
public class EndlessHud : MonoBehaviour
{
    static EndlessHud instance;

    string deathText;            // панель смерти: пока лежит — прогресс, после — итог на несколько секунд
    float deathUntil = float.PositiveInfinity;
    string bannerText;           // победа и прочие короткие объявления
    float bannerUntil;
    GUIStyle big;

    static EndlessHud Get()
    {
        if (instance == null) instance = FindAnyObjectByType<EndlessHud>();
        if (instance == null) instance = new GameObject("~EndlessHud").AddComponent<EndlessHud>();
        return instance;
    }

    /// <summary>Текст панели смерти. `seconds` — сколько держать (реальных); бесконечность — пока не снимут; null/пусто — убрать.</summary>
    public static void SetDeathText(string text, float seconds = float.PositiveInfinity)
    {
        if (string.IsNullOrEmpty(text) && instance == null) return;
        var h = Get();
        h.deathText = text;
        h.deathUntil = float.IsPositiveInfinity(seconds) ? float.PositiveInfinity : Time.realtimeSinceStartup + seconds;
    }

    /// <summary>Крупная строка по центру на `seconds` реальных секунд (победа над боссом).</summary>
    public static void ShowBanner(string text, float seconds = 7f)
    {
        var h = Get();
        h.bannerText = text;
        h.bannerUntil = Time.realtimeSinceStartup + seconds;
    }

    public static string CurrentDeathText => instance != null ? instance.deathText : null;
    public static string CurrentBanner => instance != null && Time.realtimeSinceStartup < instance.bannerUntil ? instance.bannerText : null;

    void OnDestroy() { if (instance == this) instance = null; }

    void OnGUI()
    {
        float now = Time.realtimeSinceStartup;
        big ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 26, alignment = TextAnchor.MiddleCenter, wordWrap = true, fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };

        if (!string.IsNullOrEmpty(deathText) && now < deathUntil)
            Draw(deathText, Screen.height * 0.25f, new Color(0.45f, 0.05f, 0.05f, 0.78f));
        if (!string.IsNullOrEmpty(bannerText) && now < bannerUntil)
            Draw(bannerText, Screen.height * 0.08f, new Color(0.10f, 0.35f, 0.12f, 0.82f));
    }

    void Draw(string text, float y, Color back)
    {
        float w = Mathf.Min(Screen.width - 40f, 920f);
        var rect = new Rect((Screen.width - w) * 0.5f, y, w, 150f);
        var prev = GUI.color;
        GUI.color = back;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = prev;
        GUI.Label(new Rect(rect.x + 14f, rect.y + 8f, rect.width - 28f, rect.height - 16f), text, big);
    }
}
