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
            sb.AppendLine("| место | родитель | ось | край родителя | край места | зазор (+) / нахлёст (−) | доля калибра | вердикт |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");

            // СТЫК СЧИТАЕМ МЕЖДУ МЕСТАМИ, А НЕ ДЕТАЛЯМИ. У головы шесть частей, у цепи полтора десятка
            // звеньев — брать «последнюю попавшуюся» бессмысленно. Объединяем все части места в один
            // объём: он и есть то, чем место стыкуется с соседями
            var whole = new Dictionary<string, Bounds>();
            foreach (var p in parts)
            {
                string sock = p.name;
                int cut = sock.IndexOf('~');
                if (cut > 0) sock = sock.Substring(0, cut);          // «Тело~сустав» → «Тело»
                var b = new Bounds(p.center, p.size);
                if (whole.TryGetValue(sock, out var acc)) { acc.Encapsulate(b); whole[sock] = acc; }
                else whole[sock] = b;
            }

            // РОДСТВО — ИЗ ДАННЫХ ВИДА ЦЕЛИКОМ, включая места БЕЗ геометрии. Строй мы его по нарисованным
            // деталям — служебный хребет в дерево не попадёт, и подъём к предку оборвётся на первом шаге:
            // шея, лапы и хвост снова выпадут из таблицы, хотя именно их стыки нас и волнуют
            var placeParent = new Dictionary<string, string>();
            if (sp.sockets != null)
                foreach (var k in sp.sockets)
                    if (k != null && !string.IsNullOrEmpty(k.name))
                        placeParent[k.name] = k.parent ?? "";

            foreach (var kv in whole)
            {
                string name = kv.Key;
                if (!placeParent.TryGetValue(name, out var parentName) || string.IsNullOrEmpty(parentName)) continue;

                // ПОДНИМАЕМСЯ ДО ПРЕДКА С ГЕОМЕТРИЕЙ. Хребет служебный — своей формы у него нет, и все
                // висящие на нём (шея, лапы, хвост) молча выпадали из таблицы: стыковаться не с чем.
                // Фактический стык у них — с ближайшим НАРИСОВАННЫМ предком
                int guard = 0;
                while (!whole.ContainsKey(parentName) && placeParent.TryGetValue(parentName, out var up)
                       && !string.IsNullOrEmpty(up) && guard++ < 16)
                    parentName = up;

                // ПРЕДКА С ГЕОМЕТРИЕЙ НЕТ ВОВСЕ — место висит на абстракции (хребет служебный, выше него
                // корень). Так устроены лапы, шея и хвост: пояса конечностей как места отсутствуют, и
                // стыковаться лапе физически не с чем. МОЛЧАТЬ ОБ ЭТОМ НЕЛЬЗЯ — это и есть дефект скелета,
                // а не отсутствие данных. Меряем примыкание к САМОМУ КРУПНОМУ соседу по тому же родителю:
                // фактически это корпус, к которому конечность и должна крепиться
                bool viaAbstract = false;
                if (!whole.ContainsKey(parentName))
                {
                    string biggest = null;
                    float bestVol = 0f;
                    foreach (var other in whole)
                    {
                        if (other.Key == name) continue;
                        if (!placeParent.TryGetValue(other.Key, out var op) || op != placeParent[name]) continue;
                        float v = other.Value.size.x * other.Value.size.y * other.Value.size.z;
                        if (v > bestVol) { bestVol = v; biggest = other.Key; }
                    }
                    if (biggest == null) continue;
                    parentName = biggest;
                    viaAbstract = true;
                }
                if (!whole.TryGetValue(parentName, out var parBounds)) continue;

                var me = kv.Value;

                // ВЛОЖЕНО ИЛИ ПРИСТЫКОВАНО — разные вещи, и мерить их одинаково нельзя. Глаз не стыкуется
                // с головой, он в неё утоплен; печатать ему «нахлёст −292%» значит приучить читать таблицу
                // по диагонали. Вложение: объём места почти целиком внутри родительского
                var ov = Vector3.Min(me.max, parBounds.max) - Vector3.Max(me.min, parBounds.min);
                float inside = Mathf.Max(0f, ov.x) * Mathf.Max(0f, ov.y) * Mathf.Max(0f, ov.z);
                float mine = Mathf.Max(0.000001f, me.size.x * me.size.y * me.size.z);
                bool nested = inside / mine > 0.75f;

                Vector3 d = me.center - parBounds.center;
                int axis = Mathf.Abs(d.x) >= Mathf.Abs(d.y) && Mathf.Abs(d.x) >= Mathf.Abs(d.z) ? 0
                         : Mathf.Abs(d.y) >= Mathf.Abs(d.z) ? 1 : 2;
                string axisName = axis == 0 ? "X" : axis == 1 ? "Y" : "Z";

                float sign = d[axis] >= 0f ? 1f : -1f;                       // в какую сторону ушло место
                float parentEdge = parBounds.center[axis] + sign * parBounds.size[axis] * 0.5f;
                float childEdge = me.center[axis] - sign * me.size[axis] * 0.5f;
                float gap = (childEdge - parentEdge) * sign;                 // + щель, − нахлёст
                float caliber = Mathf.Max(0.000001f, me.size[axis]);

                // помечаем, что предок фактический, а не по графу: у места нет нарисованного родителя
                string via = viaAbstract ? " ⚠ через абстракцию" : "";
                string verdict = nested ? "вложено"
                               : gap > BodyRules.GapWarn * caliber ? "**ЩЕЛЬ**"
                               : gap < -BodyRules.OverlapWarn * caliber ? "врастание"
                               : "норма";

                sb.AppendLine($"| {name} | {parentName} | {axisName} | {parentEdge:F3} | {childEdge:F3} " +
                              $"| {gap:F3} | {(gap / caliber):P0} | {verdict}{via} |");
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
