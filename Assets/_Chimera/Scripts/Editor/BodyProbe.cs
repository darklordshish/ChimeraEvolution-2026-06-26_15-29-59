using System.Collections.Generic;
using UnityEngine;

/// <summary>ИЗМЕРИТЕЛЬ ТЕЛА: собирает существо НАСТОЯЩИМ `MorphBuilder` на временном объекте и снимает
/// фактические границы деталей. Ничего не пересчитывает: повтори мы арифметику билдера — появился бы
/// ТРЕТИЙ источник правды рядом с данными и кодом (у нас уже расходились `baseSize` с `sizeRel` и
/// `SizeForGraph` с `SizeOf`), и карта начала бы врать. Тогда чинили бы карту вместо тел.
/// Отсюда же берётся то, чего в коде нет вовсе, — РЕАЛЬНЫЙ РАЗМАХ нарисованных частей, а не габарит
/// места: именно его отсутствие заставляло править стыки по скриншотам (спека 2026-08-10).</summary>
public static class BodyProbe
{
    /// <summary>Замер одной детали: что это, чей ребёнок, где и какого размера НА САМОМ ДЕЛЕ.</summary>
    public struct Part
    {
        public string name;      // имя сокета (действующий контракт имён морф-частей)
        public string parent;    // имя родительского объекта в иерархии
        public Vector3 center;   // центр по границам рендерера (объект строится в нуле → это и локальные)
        public Vector3 size;     // габарит по границам рендерера
        public bool hasRenderer;
    }

    /// <summary>Построить тело вида и вернуть замеры. Временный объект сносится до выхода в любом случае.</summary>
    public static List<Part> Measure(SpeciesSO species)
    {
        var parts = new List<Part>();
        if (species == null) return parts;

        var go = new GameObject("~BodyProbe");
        try
        {
            // CharacterController НУЖЕН БИЛДЕРУ: высоты в данных заданы ОТ ЗЕМЛИ, а корень у видов разный —
            // без контроллера сборка уедет по вертикали (MorphBuilder считает footY по низу капсулы)
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.center = new Vector3(0f, 1f, 0f);

            // РОДНОЙ СОСТАВ ШАССИ — то, что видно в игре по умолчанию. Варианты с графтами меряются
            // отдельным прогоном: смешивать их в одну карту значит потерять базовую линию
            var worn = new List<Organ>();
            if (species.organs != null)
                foreach (var o in species.organs) if (o != null) worn.Add(o);

            MorphBuilder.Build(go.transform, species, worn);

            // РОДСТВО БЕРЁМ ИЗ ДАННЫХ, А НЕ ИЗ ИЕРАРХИИ. Билдер кладёт детали ПЛОСКО в контейнер `Morph`,
            // поэтому у всех `Transform.parent` один и тот же — искать по нему стык бессмысленно, таблица
            // выходила пустой. Настоящее родство живёт в графе мест: имя детали = имя сокета (контракт)
            var socketParent = new Dictionary<string, string>();
            if (species.sockets != null)
                foreach (var k in species.sockets)
                    if (k != null && !string.IsNullOrEmpty(k.name))
                        socketParent[k.name] = string.IsNullOrEmpty(k.parent) ? "(корень)" : k.parent;

            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var t = r.transform;
                // имя части = имя сокета; у сустава цепи оно с суффиксом «~сустав» — режем до сокета
                string sock = t.name;
                int cut = sock.IndexOf('~');
                if (cut > 0) sock = sock.Substring(0, cut);

                parts.Add(new Part
                {
                    name = t.name,
                    parent = socketParent.TryGetValue(sock, out var par) ? par : "(вне графа)",
                    center = r.bounds.center,
                    size = r.bounds.size,
                    hasRenderer = true,
                });
            }
        }
        finally
        {
            // ВНЕ PLAY `Object.Destroy` ОТЛОЖЕН до конца кадра, которого в редакторе не будет: объект
            // пережил бы прогон и остался в сцене. Нужен немедленный снос
            Object.DestroyImmediate(go);
        }
        return parts;
    }

    /// <summary>ОБЪЁМЫ МЕСТ: детали, собранные по местам, которым принадлежат. Стык и вложенность —
    /// свойства МЕСТ, а не деталей: у головы шесть частей, у цепи полтора десятка звеньев, и «последняя
    /// попавшаяся» деталь не описывает ни того, ни другого. Общий код для карты и правил: разойдись они
    /// в способе счёта — получили бы отчёт и валидатор, спорящие об одном теле.</summary>
    public struct Places
    {
        public Dictionary<string, Bounds> whole;    // «голова» / «Ноги (пр)» → суммарный объём места
        public Dictionary<string, string> socketOf; // ключ с стороной → имя сокета
        public Dictionary<string, string> parentOf; // сокет → сокет-родитель («» у корня)
    }

    /// <summary>Свести замеры в объёмы мест. Парные места (`mirrorX`) разделяются по сторонам: объединив
    /// их, мы получали бокс во весь низ корпуса с центром в центре тела — ось разноса выбиралась
    /// случайно, и у волка выходил «нахлёст −128%» там, где нога просто входит в плечо.</summary>
    public static Places Group(SpeciesSO species, List<Part> parts)
    {
        var res = new Places
        {
            whole = new Dictionary<string, Bounds>(),
            socketOf = new Dictionary<string, string>(),
            parentOf = new Dictionary<string, string>(),
        };
        if (species == null || parts == null) return res;

        var mirrored = new HashSet<string>();
        if (species.sockets != null)
            foreach (var k in species.sockets)
            {
                if (k == null || string.IsNullOrEmpty(k.name)) continue;
                if (k.mirrorX) mirrored.Add(k.name);
                // РОДСТВО — ИЗ ДАННЫХ ЦЕЛИКОМ, включая места без геометрии: строй мы дерево по нарисованным
                // деталям, подъём к предку обрывался бы на первом же безформенном узле
                res.parentOf[k.name] = k.parent ?? "";
            }

        foreach (var p in parts)
        {
            string sock = p.name;
            int cut = sock.IndexOf('~');
            if (cut > 0) sock = sock.Substring(0, cut);          // «Тело~сустав» → «Тело»

            string key = mirrored.Contains(sock) ? $"{sock} ({(p.center.x >= 0f ? "пр" : "лев")})" : sock;
            res.socketOf[key] = sock;

            var b = new Bounds(p.center, p.size);
            if (res.whole.TryGetValue(key, out var acc)) { acc.Encapsulate(b); res.whole[key] = acc; }
            else res.whole[key] = b;
        }
        return res;
    }

    /// <summary>Ближайший предок, У КОТОРОГО ЕСТЬ ГЕОМЕТРИЯ. Место может висеть на безформенном узле —
    /// тогда фактический стык у него с ближайшим нарисованным предком, а не с пустотой.
    /// Возвращает пустую строку, если нарисованного предка нет вовсе (это дефект скелета, не отсутствие
    /// данных, — так было, пока несущую анатомию рисовал покров, а хребет оставался служебным).</summary>
    public static string DrawnParent(Places pl, string socket)
    {
        if (!pl.parentOf.TryGetValue(socket, out var parent) || string.IsNullOrEmpty(parent)) return "";
        int guard = 0;
        while (!pl.whole.ContainsKey(parent) && pl.parentOf.TryGetValue(parent, out var up)
               && !string.IsNullOrEmpty(up) && guard++ < 16)
            parent = up;
        return pl.whole.ContainsKey(parent) ? parent : "";
    }
}
