using System.Collections.Generic;
using UnityEngine;

/// <summary>ПРАВИЛА ТЕЛА: суждения над данными и замерами. Отдельно от карты, потому что нужны бутстрапу
/// без всякого отчёта. Ругаются ТОЛЬКО на объективные поломки — анатомия сюда не входит: стилизация это
/// решение геймдизайнера, её место в карте справочной таблицей (спека 2026-08-10).
///
/// Пороги — ДОЛИ КАЛИБРА, а не метры: иначе мелкие виды всегда в допуске, а крупные всегда виноваты.</summary>
public static class BodyRules
{
    public const float GapWarn = 0.08f;      // щель больше 8% ТОЛЩИНЫ шва — предупреждение
    // КАСАНИЕ ВПРИТЫК против ПРОСВЕТА. Ноль в стыке недостижим: детали ставятся расчётом, и на сходящихся
    // поверхностях остаются доли миллиметра. Печатать им «не касается» — тот же шум, что ругань на
    // намеренное. Распределение по пяти видам разошлось надвое: 0.000–0.007 м (сходятся) и 0.011–0.015
    // (висят). По долям шва граница легла на 3%
    public const float GapTouch = 0.03f;
    // НАХЛЁСТ БОЛЬШЕ 75% ГЛУБИНЫ ДЕТАЛИ. Порог 0.40 был назначен до фактов, а первый же прогон по здоровым
    // телам дал распределение 40…87% и 21 «врастание» на анатомии, которую глаз принимает. Причина не в
    // телах: шарнирная кукла НАМЕРЕННО сажает детали глубоко друг в друга — иначе на торцах капсул
    // расходятся щели (торец сходится в точку). Глубокое вхождение здесь способ сборки, а не дефект,
    // поэтому осмысленное «врастание» — это «деталь утонула почти целиком», а не «вошла наполовину»
    public const float OverlapWarn = 0.75f;
    public const float DupError = 0.60f;     // пересечение объёмов больше 60% — дубль области
    public const float AxisMargin = 0.05f;   // запас длинной оси меньше 5% — ось вот-вот переключится

    public struct Issue
    {
        public string species, where, text;
        public bool error;
    }

    /// <summary>Проверки по ДАННЫМ вида: то, что видно без сборки.</summary>
    public static List<Issue> CheckData(SpeciesSO s)
    {
        var list = new List<Issue>();
        if (s == null || s.sockets == null) return list;

        var byName = new Dictionary<string, BodySocket>();
        foreach (var k in s.sockets)
            if (k != null && !string.IsNullOrEmpty(k.name)) byName[k.name] = k;

        foreach (var k in s.sockets)
        {
            if (k == null || string.IsNullOrEmpty(k.name)) continue;

            // ЗАПАС ДЛИННОЙ ОСИ. Ось выбирается по максимальной стороне, и при близких числах молча
            // переключается, разворачивая ВСЮ ветку детей: у человека шея 0.128Y при 0.132Z уронила
            // рост с 1.85 до 1.76, и искали это долго — ошибок нет, просто «голова уехала вперёд».
            //
            // НО СПРАШИВАЕМ ТОЛЬКО ТАМ, ГДЕ ОСЬ НА ЧТО-ТО ВЛИЯЕТ: вдоль неё считается `attach` детей и
            // растёт цепь. У бездетного не-цепного места (глаз-шар, рога-калибр) переключись ось хоть
            // трижды — не сдвинется ничего, и ругань тут приучила бы игнорировать красное.
            // Изотропный калибр — тоже не поломка, а намерение: у шара длинной оси нет по определению
            var b = k.baseSize;
            bool hasChildren = false;
            foreach (var other in s.sockets)
                if (other != null && other.parent == k.name) { hasChildren = true; break; }
            bool axisMatters = hasChildren || k.linkLength > 0f;

            if (axisMatters)
            {
                float max = Mathf.Max(b.x, Mathf.Max(b.y, b.z));
                float min = Mathf.Min(b.x, Mathf.Min(b.y, b.z));
                float second = 0f;
                if (b.x < max && b.x > second) second = b.x;
                if (b.y < max && b.y > second) second = b.y;
                if (b.z < max && b.z > second) second = b.z;
                bool isotropic = max > 0f && (max / Mathf.Max(0.000001f, min) - 1f) < AxisMargin; // куб/шар
                if (!isotropic && max > 0f && second > 0f && (max / second - 1f) < AxisMargin)
                    list.Add(new Issue
                    {
                        species = s.speciesName, where = k.name, error = true,
                        text = $"запас длинной оси {(max / second - 1f) * 100f:F0}% — ось может молча переключиться " +
                               $"и развернуть ветку детей"
                    });
            }

            // НУЛЕВОЙ КАЛИБР. Деталь схлопнется в плоскость без единой ошибки в консоли
            if (k.linkLength <= 0f && (b.x <= 0f || b.y <= 0f || b.z <= 0f) && k.sizeRel == Vector3.zero)
                list.Add(new Issue
                {
                    species = s.speciesName, where = k.name, error = true,
                    text = $"нулевой калибр ({b.x:F3}, {b.y:F3}, {b.z:F3}) и нет доли родителя"
                });

            // ЦЕПЬ БЕЗ ДИАМЕТРА: ни своего, ни наследуемого от родительской цепи
            if (k.linkLength > 0f && k.linkDiameter <= 0f)
            {
                bool inherits = !string.IsNullOrEmpty(k.parent)
                                && byName.TryGetValue(k.parent, out var par) && par.linkLength > 0f;
                if (!inherits)
                    list.Add(new Issue
                    {
                        species = s.speciesName, where = k.name, error = true,
                        text = "цепь без диаметра: свой не задан, а родитель не цепь — наследовать не от кого"
                    });
            }

            // ПОКОМПОНЕНТНЫЙ НОЛЬ В ДОЛЕ: «оставить эту ось как есть» так не работает — выйдет плоская деталь
            if (k.sizeRel != Vector3.zero && (k.sizeRel.x <= 0f || k.sizeRel.y <= 0f || k.sizeRel.z <= 0f))
                list.Add(new Issue
                {
                    species = s.speciesName, where = k.name, error = true,
                    text = $"доля родителя с нулём по оси ({k.sizeRel.x:F2}, {k.sizeRel.y:F2}, {k.sizeRel.z:F2})"
                });

            // ЦИКЛ В ГРАФЕ. Билдер страхуется глубиной и молчит, но данные всё равно неверны
            var seen = new HashSet<string> { k.name };
            var cur = k;
            int guard = 0;
            while (!string.IsNullOrEmpty(cur.parent) && byName.TryGetValue(cur.parent, out var up) && guard++ < 32)
            {
                if (!seen.Add(up.name))
                {
                    list.Add(new Issue
                    {
                        species = s.speciesName, where = k.name, error = true,
                        text = $"цикл в графе мест через «{up.name}»"
                    });
                    break;
                }
                cur = up;
            }
        }
        return list;
    }

    /// <summary>Проверки по ЗАМЕРАМ: то, что видно только на построенном теле.
    ///
    /// СУДИМ О МЕСТАХ, А НЕ О ДЕТАЛЯХ. Прежняя версия складывала детали в словарь по имени — а имя у
    /// детали одно на всё место, и десять частей змеиной головы схлопывались в последнюю (левую ноздрю).
    /// Сравнение с «родителем» шло тогда с случайной деталью: на змее это давало полтора десятка ложных
    /// ошибок. Валидатор, кричащий на исправное, приучает игнорировать красное — поэтому объёмы мест
    /// считает `BodyProbe.Group`, общий с картой.</summary>
    public static List<Issue> CheckParts(SpeciesSO species, List<BodyProbe.Part> parts)
    {
        var list = new List<Issue>();
        if (species == null || parts == null) return list;

        var pl = BodyProbe.Group(species, parts);

        foreach (var kv in pl.whole)
        {
            string socket = pl.socketOf.TryGetValue(kv.Key, out var sn) ? sn : kv.Key;
            if (!pl.parentOf.TryGetValue(socket, out var rawParent) || string.IsNullOrEmpty(rawParent)) continue;

            string drawn = BodyProbe.DrawnParent(pl, socket);

            // МЕСТО ВИСИТ НА ПУСТОТЕ: по графу родитель есть, а нарисованного предка нет ни на одном
            // уровне вверх. Так жили шея, лапы и хвост, пока несущую анатомию рисовал ПОКРОВ, а хребет
            // числился служебным: примыкать было не к чему, и любой стык держался на совпадении чисел.
            // Теперь форму несущему даёт орган «Хребет» — правило сторожит возврат к прежнему
            if (string.IsNullOrEmpty(drawn))
            {
                list.Add(new Issue
                {
                    species = species.speciesName, where = kv.Key, error = true,
                    text = $"нет нарисованного предка: по графу висит на «{rawParent}», а тот ничего не рисует — " +
                           $"примыкать физически не к чему"
                });
                continue;
            }

            if (!pl.whole.TryGetValue(drawn, out var par)) continue;
            var me = kv.Value;

            // ВНУТРЕННЕЕ МЕСТО, ВЫЛЕЗШЕЕ ИЗ НОСИТЕЛЯ. Ровно так грудная клетка оказалась шире корпуса и
            // лепила «бочку», а причину мы искали в морде. Спрашиваем ТОЛЬКО с внутренних: голова
            // законно шире шеи, и требовать от неё «помещаться в родителя» значит ругаться на норму
            var inner = System.Array.Find(species.sockets, k => k != null && k.name == socket);
            if (inner == null || !inner.inner) continue;

            if (me.size.x > par.size.x && me.size.y > par.size.y && me.size.z > par.size.z)
                list.Add(new Issue
                {
                    species = species.speciesName, where = kv.Key, error = true,
                    text = $"внутреннее место больше носителя «{drawn}» по всем осям: " +
                           $"{me.size.x:F3}×{me.size.y:F3}×{me.size.z:F3} против {par.size.x:F3}×{par.size.y:F3}×{par.size.z:F3}"
                });
        }
        return list;
    }
}
