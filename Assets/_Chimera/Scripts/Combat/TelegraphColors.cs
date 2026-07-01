using UnityEngine;

/// <summary>
/// Единая цветовая легенда телеграфа приёмов — общий язык чтения атак для всех существ.
/// Один источник правды: цвет означает одно и то же у волка, вервольфа и будущих видов.
/// (Пока статик; если понадобится крутить в редакторе — повысим до ScriptableObject.)
/// </summary>
public static class TelegraphColors
{
    public static readonly Color Bite   = new(1f, 0.28f, 0.20f);    // укус — красный
    public static readonly Color Leap   = new(1f, 0.60f, 0.10f);    // прыжок — оранжевый
    public static readonly Color Grab   = new(0.70f, 0.20f, 0.90f); // захват — фиолетовый
    public static readonly Color Charge = new(0.90f, 0.10f, 0.50f); // чардж — розовый
    public static readonly Color Howl   = new(0.60f, 0.50f, 1f);    // вой — бледно-синий
}
