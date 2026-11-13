using System.Collections.Generic;

/// <summary>Текущий план B0.2, перенесённый в данные один-в-один.</summary>
public static class LabPlanFixed
{
    public static LabPlan Create()
    {
        var p = new LabPlan
        {
            version = 1,
            template = "fixed",
            footprintX = DomeLabComplex.HalfWidth * 2f,
            footprintZ = DomeLabComplex.HalfDepth * 2f,
        };
        p.rooms.Add(new LabRoom
        {
            id = "hall", function = LabFunction.Hall,
            x = 0f, z = 0f, width = 14f, depth = 14f, height = DomeLabComplex.HallHeight,
        });
        p.rooms.Add(new LabRoom
        {
            id = "arena", function = LabFunction.Arena,
            x = 0f, z = 13.75f, width = 46f, depth = 8f, height = DomeLabComplex.RoomHeight,
        });
        p.rooms.Add(new LabRoom
        {
            id = "surgery", function = LabFunction.Surgery,
            x = -16f, z = 0f, width = 13.5f, depth = 19f, height = DomeLabComplex.RoomHeight,
        });
        p.rooms.Add(new LabRoom
        {
            id = "cells", function = LabFunction.Cells,
            x = 16f, z = 0f, width = 13.5f, depth = 19f, height = DomeLabComplex.RoomHeight,
        });
        p.rooms.Add(new LabRoom
        {
            id = "rest", function = LabFunction.Rest,
            x = -13f, z = -13.75f, width = 20f, depth = 8f, height = DomeLabComplex.RoomHeight,
        });
        p.rooms.Add(new LabRoom
        {
            id = "lobby", function = LabFunction.Lobby,
            x = 10f, z = -13.75f, width = 26f, depth = 8f, height = DomeLabComplex.RoomHeight,
        });

        p.doors.Add(new LabDoor
        {
            from = "lobby", to = "hall", x = 0f, z = -DomeLabComplex.HallHalf,
            width = DomeLabComplex.DoorWidth, height = DomeLabComplex.DoorHeight, alongX = true,
        });
        p.doors.Add(new LabDoor
        {
            from = "hall", to = "arena", x = 0f, z = DomeLabComplex.HallHalf,
            width = DomeLabComplex.DoorWidth, height = DomeLabComplex.DoorHeight, alongX = true,
        });
        p.doors.Add(new LabDoor
        {
            from = "hall", to = "surgery", x = -DomeLabComplex.HallHalf, z = 6f,
            width = DomeLabComplex.DoorWidth, height = DomeLabComplex.DoorHeight, alongX = false,
        });
        p.doors.Add(new LabDoor
        {
            from = "hall", to = "cells", x = DomeLabComplex.HallHalf, z = 0f,
            width = DomeLabComplex.DoorWidth, height = DomeLabComplex.DoorHeight, alongX = false,
        });
        p.doors.Add(new LabDoor
        {
            from = "rest", to = "lobby", x = DomeLabComplex.RestPartitionX, z = -13.5f,
            width = DomeLabComplex.DoorWidth, height = DomeLabComplex.DoorHeight, alongX = false,
        });
        return p;
    }
}
