using System.Collections.Generic;
using UnityEngine;

/// <summary>Класс адреса S3 §4. Адрес говорит «где», а не «что там происходит».</summary>
public enum ForestPoiKind
{
    Base = 0,
    Entry = 1,
    Gate = 2,
    Zone = 3,
    Shell = 4,
    Common = 5,
    Link = 6,
    Waterhole = 7,
    Ford = 8,
    Ash = 9,
    Lair = 10,
}

/// <summary>
/// Адрес мира: v0 для дорог (S4) и B1. Хранит позицию и габарит-радиус, чтобы
/// потребитель проверял пересечение, а не расстояние до центра.
/// </summary>
public struct ForestPoi
{
    public ForestPoiKind kind;
    public string name;
    public Vector2 pos;
    public float radius;
    public float yawDeg;
    /// <summary>Высота на своём полу (для base — Hydro0, для entry — SampleGround).</summary>
    public float groundY;
    public int index;
}

/// <summary>Реестр адресов одного мира. Не поведение: только «где».</summary>
public sealed class ForestPoiRegistry
{
    readonly List<ForestPoi> items = new List<ForestPoi>();
    public IReadOnlyList<ForestPoi> All => items;

    public void Add(ForestPoi poi) => items.Add(poi);

    public ForestPoi Add(ForestPoiKind kind, string name, Vector2 pos, float radius,
        float yawDeg, float groundY, int index = 0)
    {
        var poi = new ForestPoi
        {
            kind = kind,
            name = name,
            pos = pos,
            radius = radius,
            yawDeg = yawDeg,
            groundY = groundY,
            index = index,
        };
        items.Add(poi);
        return poi;
    }

    public int Count(ForestPoiKind kind)
    {
        int n = 0;
        foreach (var p in items) if (p.kind == kind) n++;
        return n;
    }

    public void Clear() => items.Clear();
}
