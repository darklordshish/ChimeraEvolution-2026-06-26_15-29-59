using UnityEngine;

/// <summary>
/// B0.3: общий слой чтения плана. Комнаты и двери остаются данными, а геометрия
/// берёт из них границы, глубину крыла и якоря. Сейчас им пользуется мебель крыльев;
/// перегородки и полный каркас добавятся сюда же, не меняя план и id.
/// </summary>
public static class LabPlanGeometry
{
    public static Rect Bounds(LabRoom room)
    {
        return new Rect(
            room.x - room.width * 0.5f,
            room.z - room.depth * 0.5f,
            room.width,
            room.depth);
    }

    /// <summary>Дальний край крыла с отступом от стены, в мировых Z плана.</summary>
    public static float FarZ(LabRoom room, float margin = 1.5f)
    {
        return room.depth * 0.5f - margin;
    }

    public static bool IsDeepWings(LabPlan plan)
    {
        return plan != null && plan.template == "deep-wings";
    }
}
