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
}
