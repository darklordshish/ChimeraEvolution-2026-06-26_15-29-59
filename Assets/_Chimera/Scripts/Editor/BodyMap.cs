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

            // СТЫКИ: где кончается одна деталь и начинается следующая. Это то, что мы неделю мерили
            // глазами по скриншотам — щель за головой, гусеница пасти, разъехавшийся хвост
            sb.AppendLine("### Стыки вдоль тела (Z)");
            sb.AppendLine();
            sb.AppendLine("| деталь | родитель | конец родителя | начало детали | зазор (+) / нахлёст (−) |");
            sb.AppendLine("|---|---|---|---|---|");
            var byName = new Dictionary<string, BodyProbe.Part>();
            foreach (var p in parts) byName[p.name] = p;   // одноимённые звенья цепи равнозначны — берём последнее
            foreach (var p in parts)
            {
                if (!byName.TryGetValue(p.parent, out var par)) continue;
                float childFront = p.center.z - p.size.z * 0.5f;
                float parentEnd = par.center.z - par.size.z * 0.5f;
                sb.AppendLine($"| {p.name} | {p.parent} | {parentEnd:F3} | {childFront:F3} | {(childFront - parentEnd):F3} |");
            }
            sb.AppendLine();

            // ПЕРЕКРЫТИЯ: кто лезет в чужой объём. Ловит дубли вроде «пасть поверх морды» и «грудная
            // клетка распирает корпус» — оба мы нашли глазами, и оба стоили дня
            sb.AppendLine("### Перекрытия объёмов (кандидаты в дубли)");
            sb.AppendLine();
            sb.AppendLine("| A | B | доля пересечения от меньшего |");
            sb.AppendLine("|---|---|---|");
            for (int i = 0; i < parts.Count; i++)
                for (int j = i + 1; j < parts.Count; j++)
                {
                    if (parts[i].name == parts[j].name) continue;                                   // звенья одной цепи
                    if (parts[i].name == parts[j].parent || parts[j].name == parts[i].parent) continue; // родство
                    var a = new Bounds(parts[i].center, parts[i].size);
                    var b = new Bounds(parts[j].center, parts[j].size);
                    if (!a.Intersects(b)) continue;
                    var d = Vector3.Min(a.max, b.max) - Vector3.Max(a.min, b.min);
                    float inter = Mathf.Max(0f, d.x) * Mathf.Max(0f, d.y) * Mathf.Max(0f, d.z);
                    float va = a.size.x * a.size.y * a.size.z, vb = b.size.x * b.size.y * b.size.z;
                    float share = inter / Mathf.Max(0.000001f, Mathf.Min(va, vb));
                    if (share < 0.35f) continue;                                                     // касания не шумим
                    sb.AppendLine($"| {parts[i].name} | {parts[j].name} | {share:P0} |");
                }
            sb.AppendLine();
        }

        System.IO.File.WriteAllText(OutPath, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();
        Debug.Log($"Карта тел записана: {OutPath} (видов: {species})");
    }
}
