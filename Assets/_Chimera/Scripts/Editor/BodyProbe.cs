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
    /// <summary>СОБРАТЬ ХИМЕРУ ЧЕРЕЗ ПУБЛИЧНЫЙ API КОНСТРУКТОРА и вернуть её смешанный план — и список
    /// надетых органов в порядке, в каком их отдаёт билдеру игра (графты впереди родных).
    ///
    /// ОДИН ПОМОЩНИК НА ВСЕХ, И ЭТО НЕ КОСМЕТИКА. 17.09 инструменты кадра строили химеру сами и звали
    /// `MorphBuilder.Build` БЕЗ плана — пропорции донора на кадрах не появлялись по построению, а вывод
    /// «смешение до картинки не доходит» наполовину оказался артефактом инструмента (нашла модельная
    /// линия). Карта тел план передавала. Два способа собрать одну химеру — два ответа на один вопрос.
    /// `slots` — имена слотов через запятую; пусто — первый звериный орган, дающий план.</summary>
    public static BodySocket[] ChimeraPlan(SpeciesSO chassis, SpeciesSO donor, string slots,
                                           out List<Organ> worn, out string grafted)
    {
        worn = null;
        grafted = null;
        if (chassis == null || donor == null) return null;

        var wanted = new HashSet<string>();
        if (!string.IsNullOrEmpty(slots))
            foreach (var raw in slots.Split(',')) { var t = raw.Trim(); if (t.Length > 0) wanted.Add(t); }

        var go = new GameObject("~ХимераПлан");
        try
        {
            var cc = go.AddComponent<CharacterController>();   // билдеру нужен низ капсулы: высоты от земли
            cc.height = 2f;
            cc.center = new Vector3(0f, 1f, 0f);

            var body = go.AddComponent<CreatureBody>();
            body.Configure(chassis, new[] { donor });
            // ЭКОНОМИКА ЗДЕСЬ НЕ ПРЕДМЕТ: меряется форма. На родном пуле донорский орган почти всегда дороже
            // снимаемого, и `Install` отказывал бы — расширяем пул тем же API, что награда за суперхимеру
            body.ExpandPool(500);

            var names = new List<string>();
            var organs = new List<Organ>();
            for (int i = 0; i < body.SlotCount; i++)
            {
                var slotName = body.GetSlot(i).slot;
                if (wanted.Count > 0 && !wanted.Contains(slotName)) continue;
                var variants = body.GetVariants(i);
                int native = variants.FindIndex(x => x.native);
                for (int v = 0; v < variants.Count; v++)
                {
                    if (variants[v].native || variants[v].species != donor.speciesName) continue;
                    if (!body.Install(i, v)) continue;
                    // слот не назван, и графт плана не даёт (хребет не смешивается) — вернуть родной и искать дальше
                    if (wanted.Count == 0 && body.GetBlendedPlan() == null)
                    {
                        if (native >= 0) body.Install(i, native);
                        break;
                    }
                    names.Add(variants[v].organName);
                    break;
                }
                if (wanted.Count == 0 && names.Count > 0) break;
            }
            if (names.Count == 0) return null;

            // надетое берём у САМОГО тела, в его порядке: графт впереди родного, как собирает игра
            foreach (var o in donor.organs ?? new Organ[0])
                if (o != null && names.Contains(o.organName)) organs.Add(o);
            foreach (var o in chassis.organs ?? new Organ[0])
                if (o != null && !organs.Exists(g => g.slot == o.slot)) organs.Add(o);

            worn = organs;
            grafted = string.Join(", ", names);
            return body.GetBlendedPlan();
        }
        finally { Object.DestroyImmediate(go); }
    }

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
    /// <param name="plan">Пересчитанный сокет-план (морфология по идентичности). Пусто = чистое шасси.
    /// Нужен, чтобы детектор мерил ХИМЕРУ так же, как вид: сегодня химерные швы не меряет никто —
    /// карта строит только родной лоадаут, и «численная проверка стыков чиста» для химер неисполнима.</param>
    public static List<Part> Measure(SpeciesSO species, BodySocket[] plan = null)
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

            MorphBuilder.Build(go.transform, species, worn, plan);

            // РОДСТВО БЕРЁМ ИЗ ДАННЫХ, А НЕ ИЗ ИЕРАРХИИ. Билдер кладёт детали ПЛОСКО в контейнер `Morph`,
            // поэтому у всех `Transform.parent` один и тот же — искать по нему стык бессмысленно, таблица
            // выходила пустой. Настоящее родство живёт в графе мест: имя детали = имя сокета (контракт)
            var socketParent = new Dictionary<string, string>();
            var sockets = plan ?? species.sockets;
            if (sockets != null)
                foreach (var k in sockets)
                    if (k != null && !string.IsNullOrEmpty(k.name))
                        socketParent[k.name] = string.IsNullOrEmpty(k.parent) ? "(корень)" : k.parent;

            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var t = r.transform;
                // имя части = имя сокета; у сустава цепи оно с суффиксом «~сустав» — режем до сокета
                string sock = t.name;
                int cut = sock.IndexOf('~');
                if (cut > 0) sock = sock.Substring(0, cut);

                // ГАБАРИТ СКЕЛЕТНОЙ ОБОЛОЧКИ БЕРЁТСЯ ИЗ МЕША, А НЕ ИЗ РЕНДЕРЕРА (11.09). У
                // `SkinnedMeshRenderer` поле `bounds` вычисляется ЛЕНИВО — при скиннинге, то есть при
                // отрисовке. Здесь тело строится и сносится `DestroyImmediate` в одном вызове, кадра не
                // случается, и оболочка отдаёт НОЛЬ. Ловилось это отвратительно: счётчик «деталей
                // построено» не менялся (рендереры-то есть), просто у девяти мест из карты пропадали
                // габариты, а с ними стыки и перекрытия — треть отчёта. Причём непостоянно: в
                // долгоживущем редакторе Scene View успевал отрисовать, и числа появлялись. Детектор,
                // который врёт в зависимости от того, давно ли открыт редактор, хуже отсутствующего
                var box = r.bounds;
                if (box.size == Vector3.zero && r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                {
                    var mb = smr.sharedMesh.bounds;                 // bind-поза, система rootBone
                    var root = smr.rootBone != null ? smr.rootBone : t;
                    box = new Bounds(root.TransformPoint(mb.center), Vector3.Scale(mb.size, root.lossyScale));
                }

                parts.Add(new Part
                {
                    name = t.name,
                    parent = socketParent.TryGetValue(sock, out var par) ? par : "(вне графа)",
                    center = box.center,
                    size = box.size,
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
        // ОТДЕЛЬНЫЕ ДЕТАЛИ места — без них стык меряется между КОРОБКАМИ ГРУПП, а это не стык. Рога лося
        // сидят в коробке 1.15×1.11×1.09 м: карта says «норма», а розетка висит в 3.4 см от головы при
        // пороге 2.5 см. Класс «деталь не заполняет своё место» так не ловится ПО ПОСТРОЕНИЮ — ровно тот,
        // ради которого карта и заведена (правило тандема: мерить границы НАРИСОВАННЫХ частей)
        public Dictionary<string, List<Bounds>> pieces;
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
            pieces = new Dictionary<string, List<Bounds>>(),
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

            // ДЕТАЛЬ НА СРЕДИННОЙ ЛИНИИ ПАРНОГО МЕСТА — ОБЕИМ СТОРОНАМ. Оболочка поля одна на обе лапы, и
            // пока место рисовала только она, сторона была одна и знак её центра ничего не решал. С кусками
            // на узлах (17.09) у места появились две стороны, и центр оболочки −0.0003 уводил её влево:
            // правой доставались одни бруски в 30 см под хребтом, карта печатала «ЩЕЛЬ 434 %» на здоровой лапе
            string[] keys = !mirrored.Contains(sock) ? new[] { sock }
                          : Mathf.Abs(p.center.x) <= 0.1f * p.size.x ? new[] { sock + " (пр)", sock + " (лев)" }
                          : new[] { $"{sock} ({(p.center.x >= 0f ? "пр" : "лев")})" };

            var b = new Bounds(p.center, p.size);
            foreach (var key in keys)
            {
                res.socketOf[key] = sock;
                if (res.whole.TryGetValue(key, out var acc)) { acc.Encapsulate(b); res.whole[key] = acc; }
                else res.whole[key] = b;

                if (!res.pieces.TryGetValue(key, out var list)) res.pieces[key] = list = new List<Bounds>();
                list.Add(b);
            }
        }
        return res;
    }

    /// <summary>РАССТОЯНИЕ МЕЖДУ ДВУМЯ ДЕТАЛЯМИ по каждой оси: плюс — щель, минус — глубина захода друг в
    /// друга. Разделяющая ось та, где число НАИБОЛЬШЕЕ: по ней тела дальше всего разведены, и именно там
    /// проходит шов (перекройся они по всем трём — деталь сидит внутри).</summary>
    public static Vector3 Separation(Bounds a, Bounds b)
    {
        return new Vector3(
            Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x),
            Mathf.Max(a.min.y - b.max.y, b.min.y - a.max.y),
            Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));
    }

    /// <summary>БЛИЖАЙШАЯ ПАРА ДЕТАЛЕЙ двух мест — фактический стык. Меряя коробки групп, мы спрашивали
    /// «пересекаются ли облака», а не «сходятся ли поверхности»: у лося рога с головой не соприкасаются
    /// вовсе, а карта печатала «норма», потому что коробка рогов метровая и перекрывает голову с запасом.
    /// Возвращает разделение по осям для той пары, что сошлась теснее всех.</summary>
    public static Vector3 ClosestSeam(List<Bounds> child, List<Bounds> parent, out Vector3 seamPiece)
    {
        var best = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        float bestGap = float.MaxValue;
        seamPiece = Vector3.one;
        if (child == null || parent == null) return Vector3.zero;
        foreach (var c in child)
            foreach (var p in parent)
            {
                var sep = Separation(c, p);
                float gap = Mathf.Max(sep.x, Mathf.Max(sep.y, sep.z));  // расстояние между боксами
                // БЛИЖАЙШАЯ — ТА, ГДЕ ПОВЕРХНОСТИ СХОДЯТСЯ ТЕСНЕЕ ВСЕГО, то есть минимален МОДУЛЬ. Брать
                // просто наименьшее число нельзя: наименьшее — это самое глубокое ПЕРЕКРЫТИЕ, и выбирались
                // детали, вложенные друг в друга (шар-сустав внутри кости). Оттуда шла лавина «врастаний»
                // ровно на −100% при полном отсутствии щелей: перекос детектора в одну сторону
                // ...НО КАСАНИЕ ВАЖНЕЕ БЛИЗОСТИ. Место держится за родителя той деталью, что его КАСАЕТСЯ, а не
                // той, что ближе по модулю: верхняя бусина уха в 8 мм над черепом обходила нижнюю, вросшую на 5 см,
                // и карта печатала ЩЕЛЬ на прилегающем ухе (17.09, после раскладки головы). Среди касающихся —
                // по-прежнему наименьший модуль, среди некасающихся — наименьший зазор
                bool touches = gap <= 0f, bestTouches = bestGap <= 0f;
                if (bestTouches && !touches) continue;
                if (bestTouches == touches && Mathf.Abs(gap) >= Mathf.Abs(bestGap)) continue;
                // РАЗМЕР ИМЕННО ЭТОЙ ДЕТАЛИ — им же меряются пороги. Считая их от коробки ГРУППЫ, мы
                // получали абсурд: у места «Рога» коробка 1.15 м, поэтому «щелью» считался разрыв от
                // 8.8 см — а розетка висела в 3.4 см от головы и проходила как норма. Шов образует
                // конкретная деталь, и масштаб у него её собственный
                bestGap = gap; best = sep; seamPiece = c.size;
            }
        return bestGap == float.MaxValue ? Vector3.zero : best;
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
