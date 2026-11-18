using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ВЫГРУЗКА КАРТЫ ДЛЯ СИМУЛЯТОРА (схема — ГеймБосс 18.11; спека карты §9): `Docs/Диаграммы/Эволюция/карта.json`, команда
/// `chimera-map-export`. Шапка — версия схемы, хеш карты, сид, версия мира, отпечаток FNV по содержимому без даты (тем же
/// способом, что `SimExport`). Состав: площади, старт игрока, дверь лаборатории, логова (все поля слоя, темп в игровых
/// часах), граф путей как есть (вершины и рёбра-факты), корм (точка, вид, зона).
/// Строится из карты (<see cref="From"/>), без объектов сцены.
/// </summary>
[Serializable]
public sealed class ForestMapExportData
{
    public const int Schema = 1;

    public int schema = Schema;
    public string mapHash = "";
    public long seed;
    public int worldVersion;
    public string fingerprint = "";

    public float areaTotalKm2;
    public float areaLandKm2;
    public Point playerStart = new Point();
    public Point lab = new Point();
    public List<ForestMap.Lair> lairs = new List<ForestMap.Lair>();
    public Graph graph = new Graph();
    public List<Forage> forage = new List<Forage>();

    [Serializable] public sealed class Point { public float x, z; }

    [Serializable]
    public sealed class Graph
    {
        public List<ForestMapGraph.Vertex> vertices = new List<ForestMapGraph.Vertex>();
        public List<ForestMapGraph.Edge> edges = new List<ForestMapGraph.Edge>();
    }

    [Serializable] public sealed class Forage { public float x, z; public string kind; public int zone; }

    public static ForestMapExportData From(ForestMap map, ForestMapGraph graph)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        var d = new ForestMapExportData
        {
            mapHash = map.hash,
            seed = map.seed,
            worldVersion = map.worldVersion,
            areaTotalKm2 = map.areas.totalKm2,
            areaLandKm2 = map.areas.landKm2,
            playerStart = new Point { x = map.points.spawn.x, z = map.points.spawn.z },
            lab = map.lab.has ? new Point { x = map.lab.door.x, z = map.lab.door.z } : new Point(),
            lairs = new List<ForestMap.Lair>(map.lairs),
        };
        if (graph != null)
        {
            d.graph.vertices.AddRange(graph.vertices);
            d.graph.edges.AddRange(graph.edges);
        }
        if (map.forage != null)
            foreach (var f in map.forage) d.forage.Add(new Forage { x = f.pos.x, z = f.pos.y, kind = f.kind, zone = f.zone });
        return d;
    }

    /// <summary>Текст файла: `JsonUtility` с отступами, пустой отпечаток → FNV → отпечаток на место; `\n`, без BOM.</summary>
    public static string Serialize(ForestMapExportData d)
    {
        d.fingerprint = "";
        string blank = JsonUtility.ToJson(d, true).Replace("\r\n", "\n");
        d.fingerprint = Chimera.Laws.SimData.Fnv64(blank);
        return JsonUtility.ToJson(d, true).Replace("\r\n", "\n");
    }
}
