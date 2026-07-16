using UnityEngine;

/// <summary>
/// ЕДИНАЯ цветовая легенда сигналов — общий язык для всех существ, два раздела:
///  • ПРИЁМЫ (телеграф): цвет на существе = действие, которое ОНО САМО замахивается совершить;
///  • СТАТУСЫ: состояние существа, единое от ЛЮБОГО источника (стан от воя = удержание обхватом).
/// Реляционных цветов («схвачен ИМЕННО игроком») не заводим — кто виновник, ясно из контекста.
/// Один источник правды: цвет означает одно и то же у волка, вервольфа и будущих видов.
/// (Пока статик; если понадобится крутить в редакторе — повысим до ScriptableObject.)
/// </summary>
public static class TelegraphColors
{
    // приёмы
    public static readonly Color Bite   = new(1f, 0.28f, 0.20f);    // укус — красный
    public static readonly Color Leap   = new(1f, 0.60f, 0.10f);    // прыжок — оранжевый
    public static readonly Color Grab   = new(0.70f, 0.20f, 0.90f); // захват — фиолетовый
    public static readonly Color Charge = new(0.90f, 0.10f, 0.50f); // чардж — розовый
    public static readonly Color Howl   = new(0.60f, 0.50f, 1f);    // вой — бледно-синий
    public static readonly Color Antler = new(0.20f, 0.85f, 0.60f); // рога — зелёно-бирюзовый (протыкание, не таран)
    public static readonly Color Sword  = new(0.55f, 0.75f, 0.95f); // меч (человек) — стальной
    public static readonly Color Kick   = new(0.80f, 0.85f, 0.25f); // пинок (человек) — лаймовый

    // статусы
    public static readonly Color Stunned = new(0.92f, 0.92f, 0.85f); // «выключен из боя» (стан/схвачен) — белёсый
}
