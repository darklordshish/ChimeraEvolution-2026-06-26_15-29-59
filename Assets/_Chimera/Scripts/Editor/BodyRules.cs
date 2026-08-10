using System.Collections.Generic;
using UnityEngine;

/// <summary>ПРАВИЛА ТЕЛА: суждения над данными и замерами. Отдельно от карты, потому что нужны бутстрапу
/// без всякого отчёта. Ругаются ТОЛЬКО на объективные поломки — анатомия сюда не входит: стилизация это
/// решение геймдизайнера, её место в карте справочной таблицей (спека 2026-08-10).
///
/// Пороги — ДОЛИ КАЛИБРА, а не метры: иначе мелкие виды всегда в допуске, а крупные всегда виноваты.</summary>
public static class BodyRules
{
    public const float GapWarn = 0.08f;      // щель больше 8% калибра — предупреждение
    public const float OverlapWarn = 0.40f;  // нахлёст больше 40% калибра
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

    /// <summary>Проверки по ЗАМЕРАМ: то, что видно только на построенном теле.</summary>
    public static List<Issue> CheckParts(string speciesName, List<BodyProbe.Part> parts)
    {
        var list = new List<Issue>();
        if (parts == null) return list;

        var byName = new Dictionary<string, BodyProbe.Part>();
        foreach (var p in parts) byName[p.name] = p;

        foreach (var p in parts)
        {
            if (!byName.TryGetValue(p.parent, out var par)) continue;

            // ДЕТАЛЬ БОЛЬШЕ РОДИТЕЛЯ СРАЗУ ПО ДВУМ ОСЯМ — она его распирает изнутри. Ровно так грудная
            // клетка оказалась шире корпуса и лепила «бочку», а мы искали причину в морде
            if (p.size.x > par.size.x && p.size.y > par.size.y)
                list.Add(new Issue
                {
                    species = speciesName, where = p.name, error = true,
                    text = $"больше родителя «{p.parent}»: {p.size.x:F3}×{p.size.y:F3} против {par.size.x:F3}×{par.size.y:F3}"
                });
        }
        return list;
    }
}
