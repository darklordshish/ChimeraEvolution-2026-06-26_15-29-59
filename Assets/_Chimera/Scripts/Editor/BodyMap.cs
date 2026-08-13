using System.Collections.Generic;
using System.Globalization;
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
        // ЛОКАЛЬ МАШИНЫ НЕ ДОЛЖНА ПРОТЕКАТЬ В ОТЧЁТ: на русской «0.000» печаталось как «0,000»,
        // и колонка «центр x,y,z» переставала разбираться — запятая была и разделителем чисел
        var ci = CultureInfo.InvariantCulture;
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
                sb.AppendLine(string.Format(ci, "| {0} | {1} | {2:F3}, {3:F3}, {4:F3} | {5:F3} × {6:F3} × {7:F3} |",
                              p.name, p.parent, p.center.x, p.center.y, p.center.z, p.size.x, p.size.y, p.size.z));
            sb.AppendLine();

            // СТЫКИ: где кончается одна деталь и начинается следующая. Это то, что мы неделю мерили
            // глазами по скриншотам — щель за головой, гусеница пасти, разъехавшийся хвост.
            //
            // ОСЬ СТЫКА ВЫБИРАЕТСЯ ПО НАПРАВЛЕНИЮ РАЗНОСА, а не берётся Z. Раньше здесь стояла жёсткая Z,
            // и у ЧЕТВЕРОНОГИХ таблица выходила ПУСТОЙ: у волка, лося, ежа главные швы идут по Y (лапы
            // вниз, уши вверх) и по X (рёбра вбок), а вдоль Z у них разнесено немногое. Детектор молчал
            // ровно там, где мы неделю искали щели глазами
            sb.AppendLine("### Стыки (по БЛИЖАЙШЕЙ паре деталей; ось — где они разведены дальше всего)");
            sb.AppendLine();
            sb.AppendLine("| место | родитель | ось | край родителя | край места | зазор (+) / нахлёст (−) | доля от ДЕТАЛИ шва: щель к толщине, нахлёст к глубине | вердикт |");
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

                // СТЫК — МЕЖДУ БЛИЖАЙШИМИ ДЕТАЛЯМИ, А НЕ МЕЖДУ КОРОБКАМИ ГРУПП. Меряя облака целиком, мы
                // спрашивали «пересекаются ли габариты», а не «сходятся ли поверхности»: рога лося сидят
                // в коробке 1.15×1.11×1.09 м, поэтому «норма» печаталась при розетке, висящей в 3.4 см от
                // головы (порог 2.5). Класс «деталь не заполняет своё место» так не ловился ПО ПОСТРОЕНИЮ —
                // ровно тот, ради которого карта и заведена
                pl.pieces.TryGetValue(name, out var myPieces);
                pl.pieces.TryGetValue(parentName, out var parPieces);
                Vector3 seam = BodyProbe.ClosestSeam(myPieces, parPieces, out var seamPiece);

                // ОСЬ ШВА — РАЗДЕЛЯЮЩАЯ, то есть где детали разведены дальше всего. Прежний критерий
                // «наименьшее перекрытие в долях калибра» решал ту же задачу на агрегатах; здесь он
                // выражен прямо: у пары боксов шов там, где расстояние между ними наибольшее
                int axis = 0;
                for (int ax = 1; ax < 3; ax++) if (seam[ax] > seam[axis]) axis = ax;
                string axisName = axis == 0 ? "X" : axis == 1 ? "Y" : "Z";

                float gap = seam[axis];                                      // + щель, − нахлёст
                // края печатаем по ГРУППАМ: они показывают, где место стоит целиком (диагностика позы),
                // тогда как вердикт судит по фактическому шву
                Vector3 d = me.center - parBounds.center;
                float sign = d[axis] >= 0f ? 1f : -1f;
                float parentEdge = parBounds.center[axis] + sign * parBounds.size[axis] * 0.5f;
                float childEdge = me.center[axis] - sign * me.size[axis] * 0.5f;

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
                // МАСШТАБ — ОТ ДЕТАЛИ, ОБРАЗУЮЩЕЙ ШОВ, а не от коробки всего места: место может быть
                // облаком в метр (рога, щетина), и порог, снятый с него, прощает любую щель
                float gapCal = Mathf.Max(0.000001f, 0.5f * (seamPiece[(axis + 1) % 3] + seamPiece[(axis + 2) % 3]));
                float lapCal = Mathf.Max(0.000001f, seamPiece[axis]);
                float cal = gap >= 0f ? gapCal : lapCal;   // в колонку идёт та доля, по которой судим

                // помечаем, что предок фактический, а не по графу: у места нет нарисованного родителя
                string via = viaAbstract ? " ⚠ через абстракцию" : "";
                // ПОЛОЖИТЕЛЬНЫЙ ЗАЗОР — ПРИНЦИПИАЛЬНО ИНОЕ СОСТОЯНИЕ, ЧЕМ ПЕРЕКРЫТИЕ: деталь не касается
                // родителя, между ними просвет. Даже мелкий, он значит «висит», а не «сидит», и прятать
                // его в «норму» нельзя — рога лося не касаются черепа на 1.3 см, уши на 1.2 см, и по
                // прежней шкале (порог 8% толщины шва) оба проходили молча. Кричать красным на такое тоже
                // неверно, поэтому вердикта два: «не касается» — просвет в пределах порога, «ЩЕЛЬ» — сверх
                string verdict = gap > BodyRules.GapWarn * gapCal ? "**ЩЕЛЬ**"
                               : gap > BodyRules.GapTouch * gapCal ? "не касается"
                               : nested ? "вложено"
                               : gap < -BodyRules.OverlapWarn * lapCal ? "врастание"
                               : "норма";

                sb.AppendLine(string.Format(ci, "| {0} | {1} | {2} | {3:F3} | {4:F3} | {5:F3} | {6:P0} | {7}{8} |",
                              name, parentName, axisName, parentEdge, childEdge, gap, gap / cal, verdict, via));
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
                    var a = new Bounds(parts[i].center, parts[i].size);
                    var b = new Bounds(parts[j].center, parts[j].size);
                    if (!a.Intersects(b)) continue;
                    var d = Vector3.Min(a.max, b.max) - Vector3.Max(a.min, b.min);
                    float inter = Mathf.Max(0f, d.x) * Mathf.Max(0f, d.y) * Mathf.Max(0f, d.z);
                    float va = a.size.x * a.size.y * a.size.z, vb = b.size.x * b.size.y * b.size.z;
                    float share = inter / Mathf.Max(0.000001f, Mathf.Min(va, vb));
                    if (share < 0.35f) continue;                                                     // касания не шумим

                    // РОДСТВО САМО ПО СЕБЕ НЕ ОПРАВДАНИЕ — оправдание РАЗНИЦА В РАЗМЕРЕ. Пара
                    // «родитель ↔ ребёнок» пропускалась целиком, и это скрыло настоящий дубль: у ежа
                    // несущую тушу рисовал орган-покров, а место «Шкура» числится ребёнком хребта —
                    // шары плеч совпадали на 100%, крупы до третьего знака, и обе пары молчали.
                    // Мелкое в крупном (глаз в голове, сердце в корпусе) — норма; СОПОСТАВИМЫЕ объёмы
                    // на одном месте — две детали, рисующие одно и то же
                    // ДУБЛЬ — ЭТО «ДВЕ ДЕТАЛИ ПОЧТИ СОВПАЛИ», а не «сильно перекрылись». Доля от МЕНЬШЕГО
                    // велика и у нормальной посадки: лопатка внутри корпуса, голова на шее, покров поверх
                    // скелета — там мелкое просто утоплено в крупное. Спрашиваем долю от БОЛЬШЕГО: она
                    // высока, только когда тела совпадают телами, как совпадали шары ежа (плечи 100%,
                    // крупы до третьего знака). Прежний критерий «сопоставимые объёмы» метил нормальный
                    // покров и пугал зря — валидатор, кричащий на намеренное, приучает игнорировать красное
                    bool kin = parts[i].name == parts[j].parent || parts[j].name == parts[i].parent;
                    float shareBig = inter / Mathf.Max(0.000001f, Mathf.Max(va, vb));
                    bool twin = shareBig > 0.6f;
                    if (kin && !twin) continue;                                                      // мелкое в крупном — норма
                    string note = kin ? " ⚠ родня, и детали почти совпадают" : "";
                    sb.AppendLine(string.Format(ci, "| {0} | {1} | {2:P0}{3} |", parts[i].name, parts[j].name, share, note));
                }
            sb.AppendLine();
        }

        System.IO.File.WriteAllText(OutPath, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();
        Debug.Log($"Карта тел записана: {OutPath} (видов: {species})");
    }
}
