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
            // глазами по скриншотам — щель за головой, гусеница пасти, разъехавшийся хвост.
            //
            // ОСЬ СТЫКА ВЫБИРАЕТСЯ ПО НАПРАВЛЕНИЮ РАЗНОСА, а не берётся Z. Раньше здесь стояла жёсткая Z,
            // и у ЧЕТВЕРОНОГИХ таблица выходила ПУСТОЙ: у волка, лося, ежа главные швы идут по Y (лапы
            // вниз, уши вверх) и по X (рёбра вбок), а вдоль Z у них разнесено немногое. Детектор молчал
            // ровно там, где мы неделю искали щели глазами
            sb.AppendLine("### Стыки (по оси наибольшего разноса)");
            sb.AppendLine();
            sb.AppendLine("| место | родитель | ось | край родителя | край места | зазор (+) / нахлёст (−) | доля калибра |");
            sb.AppendLine("|---|---|---|---|---|---|---|");

            // СТЫК СЧИТАЕМ МЕЖДУ МЕСТАМИ, А НЕ ДЕТАЛЯМИ. У головы шесть частей, у цепи полтора десятка
            // звеньев — брать «последнюю попавшуюся» бессмысленно. Объединяем все части места в один
            // объём: он и есть то, чем место стыкуется с соседями
            var whole = new Dictionary<string, Bounds>();
            var placeParent = new Dictionary<string, string>();
            foreach (var p in parts)
            {
                string sock = p.name;
                int cut = sock.IndexOf('~');
                if (cut > 0) sock = sock.Substring(0, cut);          // «Тело~сустав» → «Тело»
                var b = new Bounds(p.center, p.size);
                if (whole.TryGetValue(sock, out var acc)) { acc.Encapsulate(b); whole[sock] = acc; }
                else { whole[sock] = b; placeParent[sock] = p.parent; }
            }

            foreach (var kv in whole)
            {
                string name = kv.Key;
                if (!placeParent.TryGetValue(name, out var parentName)) continue;
                if (!whole.TryGetValue(parentName, out var parBounds)) continue;

                var p = new BodyProbe.Part { name = name, center = kv.Value.center, size = kv.Value.size };
                var par = new BodyProbe.Part { name = parentName, center = parBounds.center, size = parBounds.size };

                Vector3 d = p.center - par.center;
                int axis = Mathf.Abs(d.x) >= Mathf.Abs(d.y) && Mathf.Abs(d.x) >= Mathf.Abs(d.z) ? 0
                         : Mathf.Abs(d.y) >= Mathf.Abs(d.z) ? 1 : 2;
                string axisName = axis == 0 ? "X" : axis == 1 ? "Y" : "Z";

                float sign = d[axis] >= 0f ? 1f : -1f;                       // в какую сторону ушёл ребёнок
                float parentEdge = par.center[axis] + sign * par.size[axis] * 0.5f;
                float childEdge = p.center[axis] - sign * p.size[axis] * 0.5f;
                float gap = (childEdge - parentEdge) * sign;                 // + щель, − нахлёст
                float caliber = Mathf.Max(0.000001f, p.size[axis]);

                sb.AppendLine($"| {p.name} | {p.parent} | {axisName} | {parentEdge:F3} | {childEdge:F3} " +
                              $"| {gap:F3} | {(gap / caliber):P0} |");
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
