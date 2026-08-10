using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>КАРТА ТЕЛ: отчёт по ФАКТИЧЕСКИ построенным существам. Числа сняты с собранного тела, а не
/// пересчитаны — поэтому карта не может разойтись с игрой и работает ДЕТЕКТОРОМ, а не отчётом.
/// Читается человеком и агентами: агенты факты НЕ пересчитывают, а берут отсюда (спека 2026-08-10).</summary>
public static class BodyMap
{
    const string OutPath = "Docs/Диаграммы/КАРТА_ТЕЛ.md";

    [MenuItem("Chimera/Выгрузить карту тел")]
    public static void Generate()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# КАРТА ТЕЛ");
        sb.AppendLine();
        sb.AppendLine("Отчёт СГЕНЕРИРОВАН: `Chimera → Выгрузить карту тел`. Руками не править — перезапишется.");
        sb.AppendLine("Числа сняты с ФАКТИЧЕСКИ построенного тела (границы рендереров), а не пересчитаны:");
        sb.AppendLine("карта строит существо настоящим билдером и обмеряет результат.");
        sb.AppendLine();

        int species = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:SpeciesSO"))
        {
            var sp = AssetDatabase.LoadAssetAtPath<SpeciesSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (sp == null) continue;
            species++;

            var parts = BodyProbe.Measure(sp);

            sb.AppendLine($"## {sp.speciesName}");
            sb.AppendLine();
            sb.AppendLine($"Деталей построено: **{parts.Count}**");
            sb.AppendLine();
            sb.AppendLine("### Детали (замер)");
            sb.AppendLine();
            sb.AppendLine("| деталь | родитель | центр x,y,z | габарит ш×в×д |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var p in parts)
                sb.AppendLine($"| {p.name} | {p.parent} | {p.center.x:F3}, {p.center.y:F3}, {p.center.z:F3} " +
                              $"| {p.size.x:F3} × {p.size.y:F3} × {p.size.z:F3} |");
            sb.AppendLine();
        }

        System.IO.File.WriteAllText(OutPath, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();
        Debug.Log($"Карта тел записана: {OutPath} (видов: {species})");
    }
}
