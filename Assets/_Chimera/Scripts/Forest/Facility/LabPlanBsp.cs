using UnityEngine;

/// <summary>
/// BSP-план версии 2: полосы + центральный зал, комнаты получают диапазоны,
/// двери — на общих стенах. Версия 1 (fixed/mirror) не трогается.
/// </summary>
public static class LabPlanBsp
{
    public static LabPlan Create(long seed, int version = 2)
    {
        var p = new LabPlan
        {
            version = version,
            template = "bsp",
            footprintX = 46f,
            footprintZ = 36f,
        };

        float hallW = Mathf.Lerp(12f, 16f, Hash01(seed, 9101));
        float gap = Mathf.Lerp(2f, 3f, Hash01(seed, 9102));
        float sideW = (46f - hallW - 2f * gap) * 0.5f;
        float southT = Mathf.Lerp(0.35f, 0.48f, Hash01(seed, 9103));
        float restW = 46f * southT;
        float lobbyW = 46f - restW;

        Add(p, "hall", LabFunction.Hall, 0f, 0f, hallW, 14f, DomeLabComplex.HallHeight);
        Add(p, "arena", LabFunction.Arena, 0f, 14f, 46f, 8f, DomeLabComplex.RoomHeight);
        Add(p, "surgery", LabFunction.Surgery,
            -(hallW * 0.5f + gap + sideW * 0.5f), 0f, sideW, 19f, DomeLabComplex.RoomHeight);
        Add(p, "cells", LabFunction.Cells,
            hallW * 0.5f + gap + sideW * 0.5f, 0f, sideW, 19f, DomeLabComplex.RoomHeight);
        Add(p, "rest", LabFunction.Rest, -23f + restW * 0.5f, -14f,
            restW, 8f, DomeLabComplex.RoomHeight);
        Add(p, "lobby", LabFunction.Lobby, 23f - lobbyW * 0.5f, -14f,
            lobbyW, 8f, DomeLabComplex.RoomHeight);

        float hallBottom = -7f;
        float hallTop = 7f;
        float hallLeft = -hallW * 0.5f;
        float hallRight = hallW * 0.5f;
        float surgeryRight = hallLeft - gap;
        float cellsLeft = hallRight + gap;

        Door(p, "lobby", "hall", 0f, (hallBottom + -10f) * 0.5f, true);
        Door(p, "hall", "arena", 0f, (hallTop + 10f) * 0.5f, true);
        Door(p, "hall", "surgery", (hallLeft + surgeryRight) * 0.5f, 0f, false);
        Door(p, "hall", "cells", (hallRight + cellsLeft) * 0.5f, 0f, false);
        Door(p, "rest", "lobby", -23f + restW, -14f, false);
        return p;
    }

    static void Add(LabPlan p, string id, LabFunction f,
        float x, float z, float w, float d, float h)
    {
        p.rooms.Add(new LabRoom
        {
            id = id, function = f, x = x, z = z, width = w, depth = d, height = h,
        });
    }

    static void Door(LabPlan p, string from, string to, float x, float z, bool alongX)
    {
        p.doors.Add(new LabDoor
        {
            from = from, to = to, x = x, z = z,
            width = DomeLabComplex.DoorWidth,
            height = DomeLabComplex.DoorHeight,
            alongX = alongX,
        });
    }

    static float Hash01(long seed, int salt) =>
        SeededHash.ToFloat01(SeededHash.Hash(seed, salt, 0));
}
