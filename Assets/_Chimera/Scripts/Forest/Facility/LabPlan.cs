using System;
using System.Collections.Generic;

/// <summary>
/// B0.3: план внутреннего пространства бункера как данные. Комнаты и двери —
/// единственный источник геометрии для сборки; фиксированный B0.2-план — один из вариантов.
/// </summary>
[Serializable]
public sealed class LabPlan
{
    public int version = 1;
    public string template = "fixed";
    public bool fallback;
    public string note;
    public float footprintX = 46f;
    public float footprintZ = 36f;
    public List<LabRoom> rooms = new List<LabRoom>();
    public List<LabDoor> doors = new List<LabDoor>();

    public LabRoom Room(string id)
    {
        for (int i = 0; i < rooms.Count; i++)
            if (rooms[i].id == id) return rooms[i];
        return default;
    }
}

public enum LabFunction
{
    Hall,
    Arena,
    Surgery,
    Cells,
    Rest,
    Lobby,
}

[Serializable]
public struct LabRoom
{
    public string id;
    public LabFunction function;
    /// <summary>Центр комнаты в локальном кадре лаборатории (+Z — от чаши).</summary>
    public float x;
    public float z;
    public float width;
    public float depth;
    public float height;
}

[Serializable]
public struct LabDoor
{
    public string from;
    public string to;
    public float x;
    public float z;
    public float width;
    public float height;
    /// <summary>true — стена идёт вдоль X (дверь в поперечной стене), false — вдоль Z.</summary>
    public bool alongX;
}
