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

    // Лаборатория-полубункер (B0.1): оболочка комплекса и его свет — один набор цвета,
    // чтобы плита, стены и портал не разъезжались по оттенку между билдерами.
    public static readonly Color Concrete = new Color(0.44f, 0.45f, 0.46f);
    public static readonly Color Floor = new Color(0.34f, 0.35f, 0.36f);
    public static readonly Color Wall = new Color(0.52f, 0.53f, 0.54f);
    public static readonly Color Metal = new Color(0.28f, 0.30f, 0.32f);
    public static readonly Color Glass = new Color(0.62f, 0.71f, 0.77f);
    public static readonly Color Rust = new Color(0.54f, 0.29f, 0.17f);
    /// <summary>Тёмный бетон: балки, рамы, тяжёлые пояса — тот же камень, но в тени.</summary>
    public static readonly Color ConcreteDark = new Color(0.30f, 0.31f, 0.32f);
    /// <summary>Оливковая ткань — только отдых (матрасы, сиденья), как в BUNKER-PLAN.</summary>
    public static readonly Color Olive = new Color(0.37f, 0.40f, 0.24f);
    /// <summary>Тёплый свет лаборатории: лампы над столом, в клетках, в отдыхе и на арене.</summary>
    public static readonly Color LabWarm = new Color(1f, 0.86f, 0.66f);
    /// <summary>Холодный общий свет бункера: потолочные полосы под сплошным перекрытием.</summary>
    public static readonly Color LabCold = new Color(0.72f, 0.82f, 0.95f);
}
