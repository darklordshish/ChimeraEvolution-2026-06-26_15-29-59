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
            sb.AppendLine("### Стыки (ось шва — где перекрытие наименьшее)");
            sb.AppendLine();
            sb.AppendLine("| место | родитель | ось | край родителя | край места | зазор (+) / нахлёст (−) | доля: щель от толщины шва, нахлёст от глубины детали | вердикт |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");

            // СТЫК СЧИТАЕМ МЕЖДУ МЕСТАМИ, А НЕ ДЕТАЛЯМИ. У головы шесть частей, у цепи полтора десятка
            // звеньев — брать «последнюю попавшуюся» бессмысленно. Объединяем все части места в один
            // объём: он и есть то, чем место стыкуется с соседями
            // ОБЪЁМЫ МЕСТ И РОДСТВО СЧИТАЕТ `BodyProbe.Group` — тот же код, что и у правил. Разойдись
            // карта с валидатором в способе счёта, мы получили бы отчёт и проверку, спорящие об одном теле
            var pl = BodyProbe.Group(sp, parts);
            var whole = pl.whole;

            foreach (var kv in whole)
            {
                string name = kv.Key;
                string socket = pl.socketOf.TryGetValue(name, out var sn) ? sn : name;   // «Ноги (пр)» → «Ноги»
                if (!pl.parentOf.TryGetValue(socket, out var rawParent) || string.IsNullOrEmpty(rawParent)) continue;

                // ПОДНИМАЕМСЯ ДО ПРЕДКА С ГЕОМЕТРИЕЙ: место может висеть на безформенном узле, и тогда
                // фактический стык у него с ближайшим НАРИСОВАННЫМ предком
                string parentName = BodyProbe.DrawnParent(pl, socket);

                // ПРЕДКА С ГЕОМЕТРИЕЙ НЕТ ВОВСЕ — место висит на абстракции. Так было, пока несущую
                // анатомию рисовал ПОКРОВ, а хребет оставался служебным: лапе стыковаться было физически
                // не с чем. МОЛЧАТЬ ОБ ЭТОМ НЕЛЬЗЯ — это дефект скелета, а не отсутствие данных. Меряем
                // примыкание к САМОМУ КРУПНОМУ соседу по тому же родителю: фактически это корпус
                bool viaAbstract = false;
                if (string.IsNullOrEmpty(parentName))
                {
                    string biggest = null;
                    float bestVol = 0f;
                    foreach (var other in whole)
                    {
                        if (other.Key == name) continue;
                        string osock = pl.socketOf.TryGetValue(other.Key, out var on) ? on : other.Key;
                        if (osock == socket) continue;                               // другая сторона того же места
                        if (!pl.parentOf.TryGetValue(osock, out var op) || op != rawParent) continue;
                        float v = other.Value.size.x * other.Value.size.y * other.Value.size.z;
                        if (v > bestVol) { bestVol = v; biggest = other.Key; }
                    }
                    if (biggest == null) continue;
                    // САМОЕ КРУПНОЕ МЕСТО — ЭТО И ЕСТЬ КОРПУС, ему стыковаться не с чем. Печатать «Шкура →
                    // Хвост» бессмысленно: покров ищет опору у собственного отростка. Строка-абсурд, но
                    // она честно показывает, что корпуса как МЕСТА в скелете нет — говорим это прямо
                    float myVol = kv.Value.size.x * kv.Value.size.y * kv.Value.size.z;
                    if (myVol >= bestVol)
                    {
                        sb.AppendLine($"| {name} | — | — | — | — | — | — | **несущий объём, предка нет** |");
                        continue;
                    }
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

                // ОСЬ ШВА — ТА, ГДЕ ПЕРЕКРЫТИЕ НАИМЕНЬШЕЕ, а не где дальше разъехались центры. Нога
                // смещена ВПЕРЁД сильнее, чем вниз, и критерий «наибольший разнос» указывал на Z, хотя
                // с корпусом она стыкуется СВЕРХУ. Шов проходит там, где тела разделены, — по вертикали
                Vector3 d = me.center - parBounds.center;
                int axis = 0;
                float bestOverlap = float.MaxValue;
                for (int ax = 0; ax < 3; ax++)
                {
                    float ovAx = Mathf.Min(me.max[ax], parBounds.max[ax]) - Mathf.Max(me.min[ax], parBounds.min[ax]);
                    float rel = ovAx / Mathf.Max(0.000001f, me.size[ax]);   // в долях СВОЕГО калибра
                    if (rel < bestOverlap) { bestOverlap = rel; axis = ax; }
                }
                string axisName = axis == 0 ? "X" : axis == 1 ? "Y" : "Z";

                float sign = d[axis] >= 0f ? 1f : -1f;                       // в какую сторону ушло место
                float parentEdge = parBounds.center[axis] + sign * parBounds.size[axis] * 0.5f;
                float childEdge = me.center[axis] - sign * me.size[axis] * 0.5f;
                float gap = (childEdge - parentEdge) * sign;                 // + щель, − нахлёст

                // У ЩЕЛИ И НАХЛЁСТА РАЗНЫЕ МАСШТАБЫ, И МЕРИТЬ ИХ ОДНИМ ЧИСЛОМ НЕЛЬЗЯ.
                //
                // ЩЕЛЬ — разрыв ПОВЕРХНОСТИ, её глаз сравнивает с ТОЛЩИНОЙ шва: 5 см на шее диаметром 21 см
                // видно сразу. Мерили мы вдоль оси шва, и у длинных мест порог уезжал в бессмыслицу: змеиная
                // цепь тянется на 1.6–2.1 м, поэтому «щелью» считался разрыв от 13 см — тот самый
                // исторический отрыв цепи от черепа на 5 см эта таблица звала бы нормой.
                //
                // НАХЛЁСТ — ГЛУБИНА ПОГРУЖЕНИЯ, и её масштаб — глубина самой детали. Волчья голова заходит
                // на шею на 14.6 см: от длины головы это треть (норма шарнирной куклы), а от толщины шеи —
                // 68%, и карта выдала 32 «врастания» на здоровой анатомии. Валидатор, кричащий на
                // намеренное, приучает игнорировать красное — тот же урок, что с осью у изотропных мест
                float gapCal = Mathf.Max(0.000001f, 0.5f * (me.size[(axis + 1) % 3] + me.size[(axis + 2) % 3]));
                float lapCal = Mathf.Max(0.000001f, me.size[axis]);
                float cal = gap >= 0f ? gapCal : lapCal;   // в колонку идёт та доля, по которой судим

                // помечаем, что предок фактический, а не по графу: у места нет нарисованного родителя
                string via = viaAbstract ? " ⚠ через абстракцию" : "";
                string verdict = nested ? "вложено"
                               : gap > BodyRules.GapWarn * gapCal ? "**ЩЕЛЬ**"
                               : gap < -BodyRules.OverlapWarn * lapCal ? "врастание"
                               : "норма";

                sb.AppendLine($"| {name} | {parentName} | {axisName} | {parentEdge:F3} | {childEdge:F3} " +
                              $"| {gap:F3} | {(gap / cal):P0} | {verdict}{via} |");
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
