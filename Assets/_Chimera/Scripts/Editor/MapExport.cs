using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ВЫГРУЗКА КАРТЫ ЛЕСА ДЛЯ СИМУЛЯТОРА (ГеймБосс 18.11; спека карты §9): `Docs/Диаграммы/Эволюция/карта.json`. Собирает
/// павильон v2 (`ForestProfiles.PavilionV2Zone`, сид 1337) на временном объекте открытой сцены, снимает карту, строит растр
/// и граф путей из слоёв карты и пишет <see cref="ForestMapExportData"/>. Мир после выгрузки сносится, сцена не меняется.
/// До шага 5б карта — снимок собранного мира, поэтому выгрузке нужна сборка (≈10 с); сторож свежести — [Explicit].
/// </summary>
public static class MapExport
{
    public const string OutPath = "Docs/Диаграммы/Эволюция/карта.json";
    public const long Seed = 1337L;

    [MenuItem("Chimera/Выгрузить карту леса для симулятора")]
    public static void Generate()
    {
        string text = BuildText(out var data);
        File.WriteAllText(OutPath, text, new UTF8Encoding(false));
        AssetDatabase.Refresh();
        Debug.Log($"[MapExport] {OutPath}: логов {data.lairs.Count}, вершин {data.graph.vertices.Count}, рёбер {data.graph.edges.Count}, " +
                  $"корма {data.forage.Count}, отпечаток {data.fingerprint}");
    }

    /// <summary>Текст выгрузки из свежей сборки (его же сверяет сторож свежести).</summary>
    public static string BuildText(out ForestMapExportData data)
    {
        var host = new GameObject("~MapExport");
        host.hideFlags = HideFlags.DontSave;
        host.SetActive(false);
        try
        {
            var b = ForestProfiles.PavilionV2Zone(host, Seed);
            b.buildOnAwake = false;
            host.SetActive(true);
            b.Build();
            if (b.Map == null) throw new System.InvalidOperationException("у мира нет карты — см. трассу сборки");
            var raster = ForestMapRaster.Build(b.Map);
            var graph = ForestMapGraph.Build(b.Map, raster);
            data = ForestMapExportData.From(b.Map, graph);
            string text = ForestMapExportData.Serialize(data);
            b.Clear();
            return text;
        }
        finally { Object.DestroyImmediate(host); }
    }
}
