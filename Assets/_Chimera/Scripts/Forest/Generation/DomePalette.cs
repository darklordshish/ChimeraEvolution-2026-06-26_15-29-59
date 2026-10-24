using UnityEngine;

/// <summary>
/// Палитра павильона в одном месте: превью и рантайм-мир красятся одинаково.
/// Цвета — из кадров s10 (трава/скала/вода) и ясеня; менять здесь, а не в двух билдерах.
/// Лес, срез A.
/// </summary>
public static class DomePalette
{
    public static readonly Color Terrain = new Color(0.32f, 0.44f, 0.26f);
    public static readonly Color Rock = new Color(0.50f, 0.50f, 0.50f);
    public static readonly Color Water = new Color(0.20f, 0.45f, 0.60f);
    public static readonly Color Dark = new Color(0.14f, 0.15f, 0.16f);
    public static readonly Color Nest = new Color(0.35f, 0.42f, 0.23f);
    public static readonly Color Bark = new Color(0.35f, 0.25f, 0.18f);
    public static readonly Color Crown = new Color(0.24f, 0.38f, 0.20f);
    public static readonly Color Owl = new Color(0.55f, 0.52f, 0.47f);
}
