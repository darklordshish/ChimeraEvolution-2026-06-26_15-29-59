using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Цепочка тела змеи: сегменты (шарики + погремушка) тянутся по ПУТИ головы — буфер точек, каждый
/// сегмент сидит на своей дистанции вдоль пути (на поворотах тело изгибается S-ом само). Двигается
/// только корень (CharacterController/NavMesh); сегменты — визуал + свои коллайдеры (тело плотное
/// по всей длине: игрок и волки в него врезаются). Собственный CC игнорирует коллайдеры сегментов.
/// Заполняет генератор префаба (Chimera → Создать префаб Змеи).
/// </summary>
public class SnakeBodyChain : MonoBehaviour
{
    [SerializeField] Transform[] segments;      // звенья от шеи к кончику хвоста (морф заполняет)
    [SerializeField] float spacing = 0.32f;     // шаг ПЕРВОГО звена. Расстановкой больше не заведует (у каждого
                                                // звена своя дистанция — см. `dist`), живёт ради затравки пути
                                                // в Awake, когда звеньев ещё нет и мерить нечего
    // ВЫСОТА ОСИ ЦЕПИ НАД ПУТЁМ (путь пишется по земле) — РАДИУС самого толстого звена, берётся из сборки в
    // `RebuildFromMorph`. До 22.09 здесь стояли 0.3 при толщине тела 0.30, и змея ползла, вися брюхом на 0.15
    // над землёй. Не сериализуется: число в префабе пережило бы смену толщины тела (та же беда, что с `spacing`)
    [System.NonSerialized] float height = 0.15f;
    [SerializeField] float sampleStep = 0.08f;  // шаг записи пути головы
    [SerializeField] int maxSamples = 256;

    readonly List<Vector3> path = new(); // [0] — новейшая точка
    Vector3 lastSample;
    float[] dist;                        // метры вдоль тела до каждого звена (из сборки; звенья разной длины)

    /// <summary>Точка вдоль тела: t01 0=голова(корень) … 1=хвост. Волки рвут змею ПО ДЛИНЕ, не кольцом.</summary>
    public Vector3 BodyPoint(float t01)
    {
        int n = segments != null ? segments.Length : 0;
        if (n == 0) return transform.position;
        float f = Mathf.Clamp01(t01) * n;          // точки: [голова, seg0..seg(n-1)]
        int i = Mathf.Clamp((int)f, 0, n - 1);
        Vector3 a = i == 0 ? transform.position : (segments[i - 1] != null ? segments[i - 1].position : transform.position);
        Vector3 b = segments[i] != null ? segments[i].position : a;
        return Vector3.Lerp(a, b, f - i);
    }

    // звенья хребта в порядке от головы: имена морф-частей = имена сокетов (действующий контракт)
    // ПОГРЕМУШКИ ЗДЕСЬ НЕТ — и не вычеркнута, а не существует на этом уровне: морф вешает её ПОТОМКОМ
    // последнего звена хвоста, так что она едет со звеном сама. Пока она лежала соседом звеньев, движок
    // обязан был выгораживать её списком-исключением, и она всё равно рассыпалась. Список имён — признак
    // того, что иерархия неверна: чинить надо родство деталей, а не заводить перечень особых случаев
    // «хребет» вместо прежнего «Тело»: несущее змеи зовётся как у всех (спека 2026-09-11).
    // Имена здесь — контракт с сокет-планом, менять их можно только парой с ним
    static readonly string[] ChainNames = { "шея", "хребет", "Хвост" };
    // СЛУЖЕБНЫЕ дети корня, которые сносить НЕЛЬЗЯ: не части тела, а системы (след запаха и т.п.)
    static readonly string[] KeepAlive = { "Morph", "ScentTrail", "ScentField" };

    /// <summary>ЦЕПЬ ИЗ МОРФ-ЧАСТЕЙ. Раньше сегменты лежали в префабе и назначались генератором — тело
    /// змеи было единственным, что конструктор не собирал. Теперь звенья рождаются морфом (сокет-план:
    /// шея×3 → Тело×3 → Хвост×3), а этот компонент лишь ДВИЖЕТ их: состав — данные, движение — код.
    /// Порядок звеньев берём из иерархии: билдер создаёт их в порядке сокетов, а сокеты идут от головы.
    /// Зовётся из `CreatureBody` после каждой сборки — состав может смениться прямо в бою.</summary>
    public void RebuildFromMorph()
    {
        var morph = transform.Find("Morph");
        if (morph == null) return;

        var found = new List<Transform>();
        for (int i = 0; i < morph.childCount; i++)
        {
            var c = morph.GetChild(i);
            // ОБОЛОЧКА СЛОТА — НЕ ЗВЕНО. `BoneMesher` кладёт в тот же контейнер скиннед-меши слотов с ТЕМИ ЖЕ
            // именами (`шея`, `хребет`, `Хвост`), и по одному имени их не отличить: замер модельной линии 17.09
            // нашёл 18 «звеньев», из которых первые три были оболочками, хвостовая — первой у головы. Двигать
            // трансформ скиннед-меша бессмысленно: его вершины идут за костями, а не за объектом
            if (c.GetComponent<SkinnedMeshRenderer>() != null) continue;
            foreach (var n in ChainNames)
                if (c.name == n) { found.Add(c); break; }
        }
        if (found.Count == 0) return; // морф ничего не дал (чужое шасси без цепи) — остаёмся на прежних сегментах

        // МИГРАЦИЯ СО СТАРОГО ПРЕФАБА: сносим ВСЮ статичную геометрию корня, иначе на арене две змеи —
        // ползущая морфная и неподвижная префабная. Сносим ПО ПРИЗНАКУ (есть чем рисоваться), а не по
        // списку имён: у головы префаба были отдельные Cheek/Eye/Tongue/Sphere, и перечислять их —
        // бесконечная погоня, где каждый забытый кусок висит поверх морфа кубом
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var t = transform.GetChild(i);
            if (System.Array.IndexOf(KeepAlive, t.name) >= 0) continue;
            if (t.GetComponentInChildren<Renderer>() == null) continue; // не геометрия — не трогаем
            t.gameObject.SetActive(false);
            Destroy(t.gameObject);
        }

        segments = found.ToArray();

        // ДИСТАНЦИИ БЕРЁМ ИЗ САМОЙ СБОРКИ: сколько метров вдоль тела до каждого звена, как их поставил
        // билдер. Не из поля: сериализованное значение живёт В ПРЕФАБЕ и НЕ обновляется при правке дефолта
        // в коде (лежало 0.62 против 0.32 — пять итераций подряд менялось число, которое игра игнорировала).
        // И не из ОДНОГО шага на всю цепь: звенья бывают разной длины (у хвоста 0.24 против телесных 0.36 —
        // хвостовые позвонки мельче), и общий шаг растаскивал их с зазором в 12 см. Своя дистанция у каждого
        // звена — и любая нарезка едет верно по построению, сколько бы цепей с разным звеном ни сошлось
        // МЕРИМ В СИСТЕМЕ ПУТИ, а не в мировой: звенья стоят над путём, и подъём надо снять — иначе первое звено
        // меряется от корня, у которого подъёма нет, и его 0.36 вдоль тела выходят 0.47 по диагонали. У остальных
        // подъём одинаков и сокращается сам, потому ошибка сидела ровно на одном стыке: голова отъезжала от шеи
        // ПРОЕКЦИЯ НА ПЛОСКОСТЬ ПУТИ, а не вычитание `height`: сборка ставит цепь на высоту места, и она не обязана
        // совпадать с осью движения (места знают калибр, а не землю). Проекция снимает подъём любой величины
        dist = new float[found.Count];
        Vector3 up = transform.up;
        Vector3 walk = transform.position;
        float run = 0f;
        for (int i = 0; i < found.Count; i++)
        {
            Vector3 q = found[i].position - up * Vector3.Dot(found[i].position - transform.position, up);
            run += Vector3.Distance(walk, q);
            walk = q;
            dist[i] = run;
        }

        // ОСЬ ЦЕПИ = РАДИУС САМОГО ТОЛСТОГО ЗВЕНА: брюхо тела ложится на землю. Меряем в кадре УЗЛА (его Y — верх тела),
        // а не в мировом: при пересборке в бою узлы уже стоят вдоль пути, в том числе на склоне. Узкие звенья шеи и
        // хвоста идут по той же оси — так нет ступеньки у загривка (голова змеи стоит на этой же оси, поставка 13)
        float radius = 0f;
        foreach (var node in found) radius = Mathf.Max(radius, HalfThickness(node));
        if (radius > 0f) height = radius;
        spacing = Mathf.Max(0.05f, dist[0]);   // шаг первого звена — им живёт затравка пути в Awake

        IgnoreOwnBody(); // части плотные (solid) — свои же коллайдеры не должны толкать собственный CC
    }

    /// <summary>Половина толщины звена по его верху (локальный Y узла): углы мешей всех рендереров звена в кадре узла.</summary>
    static float HalfThickness(Transform node)
    {
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var mf in node.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var b = mf.sharedMesh.bounds;
            for (int c = 0; c < 8; c++)
            {
                var corner = b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                float y = node.InverseTransformPoint(mf.transform.TransformPoint(corner)).y;
                lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y);
            }
        }
        return hi > lo ? (hi - lo) * 0.5f : 0f;
    }

    /// <summary>Своё тело — не препятствие себе. Повторяем ПОСЛЕ КАЖДОЙ пересборки: части новые, а
    /// прежние IgnoreCollision умерли вместе со старыми коллайдерами — иначе змея спотыкается о себя.</summary>
    void IgnoreOwnBody()
    {
        if (!TryGetComponent<CharacterController>(out var cc)) return;
        foreach (var col in GetComponentsInChildren<Collider>())
            if (col != cc) Physics.IgnoreCollision(cc, col);
    }

    void Awake()
    {
        // сегменты не должны становиться препятствием для СВОЕГО CharacterController
        if (TryGetComponent<CharacterController>(out var cc))
            foreach (var col in GetComponentsInChildren<Collider>())
                if (col != cc) Physics.IgnoreCollision(cc, col);

        // затравка пути: прямая линия назад — на спавне тело лежит вытянутым, а не комом
        lastSample = transform.position;
        path.Add(lastSample);
        float total = spacing * ((segments != null ? segments.Length : 0) + 1);
        for (float d = sampleStep; d <= total; d += sampleStep)
            path.Add(transform.position - transform.forward * d);
    }

    void LateUpdate()
    {
        // пишем путь головы (корня)
        if ((transform.position - lastSample).sqrMagnitude >= sampleStep * sampleStep)
        {
            lastSample = transform.position;
            // ГОЛОВА ПОШЛА НАЗАД ПО СВОЕМУ СЛЕДУ — путь СМАТЫВАЕМ, а не удлиняем. Иначе топтание на месте
            // (голова у стены ходит туда-сюда) забивает буфер зигзагом: точек много, а геометрически они
            // стоят в одном месте — тело отсчитывает по ним свои метры и садится само в себя. Признак
            // возврата: новая позиция ближе к ПРЕДПОСЛЕДНЕЙ точке, чем последняя, — значит след пятится
            while (path.Count > 1 && Vector3.Distance(lastSample, path[1]) < Vector3.Distance(path[0], path[1]))
                path.RemoveAt(0);
            path.Insert(0, lastSample);
            if (path.Count > maxSamples) path.RemoveAt(path.Count - 1);
        }

        if (segments == null) return;

        // ЗВЕНЬЯ ИДУТ ПО ПУТИ — и только по нему: плавность тела в том, что все они читают ОДНУ И ТУ ЖЕ
        // кривую, каждый на своей отметке. Была здесь попытка держать соседей ровно на `spacing`: точка
        // бралась из пути, а ставилась от соседа, рассогласование копилось вдоль тела — и змею вело
        // зигзагом с изломами на шее. Два способа расстановки не смешиваются; путь остаётся единственным
        Vector3 prev = transform.position;
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == null) continue;
            // смещение вдоль ВЕРХА ТЕЛА (transform.up): на земле = мировой верх (как было), на стене = нормаль
            // стены → сегменты отходят ОТ стены заодно с головой, а не влипают в плоскость
            // СВОЯ ДИСТАНЦИЯ У КАЖДОГО ЗВЕНА (звенья хвоста короче телесных); нет таблицы — старый общий шаг
            float at = dist != null && dist.Length == segments.Length ? dist[i] : (i + 1) * spacing;
            float step = dist != null && dist.Length == segments.Length && i > 0 ? dist[i] - dist[i - 1] : spacing;
            Vector3 p = PointAlongPath(at, out Vector3 toHead) + transform.up * height;

            // ПОЛ ПО РАССТОЯНИЮ — страховка, а не расстановка: звено не подходит к соседу ближе половины
            // своего шага. При исправном пути не срабатывает вовсе (звенья и так на своих местах), поэтому
            // следование не искажает; в патологии не даёт телу сесть в одну точку
            Vector3 d = p - prev;
            float m = d.magnitude;
            if (m < step * 0.5f) p = prev + (m > 1e-4f ? d / m : -transform.forward) * (step * 0.5f);
            segments[i].position = p;

            // ПОВОРОТ ЗВЕНА ЖИВЁТ ТОЛЬКО ЗДЕСЬ. Мы задаём rotation ЦЕЛИКОМ, то есть любой наклон из данных
            // затирается каждый кадр — держать его ещё и там значит ловить то двойной доворот, то никакого.
            // Звено — УЗЕЛ, а не капсула: доворот под форму примитива живёт внутри него, и здесь мы просто
            // смотрим вдоль пути. Раньше тут стоял ещё и Euler(90) «под капсулу» — поправка на устройство
            // меша протекала в код движения, и любая смена формы звена требовала бы править это место
            if (toHead.sqrMagnitude > 0.0001f)
                segments[i].rotation = Quaternion.LookRotation(toHead, transform.up);
            prev = p;
        }
    }

    // точка на пути в distance позади головы + направление «к голове» в этой точке
    Vector3 PointAlongPath(float distance, out Vector3 dirToHead)
    {
        Vector3 prev = transform.position;
        dirToHead = transform.forward;
        float remaining = distance;
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 pt = path[i];
            float seg = Vector3.Distance(prev, pt);
            if (seg > 0.0001f && seg >= remaining)
            {
                Vector3 pos = Vector3.Lerp(prev, pt, remaining / seg);
                dirToHead = prev - pos;
                return pos;
            }
            remaining -= seg;
            if (seg > 0.0001f) dirToHead = prev - pt;
            prev = pt;
        }
        // ПУТЬ КОРОЧЕ ТЕЛА (змея развернулась или стоит) — ПРОДОЛЖАЕМ ЕГО ПРЯМОЙ за последней точкой.
        // Раньше здесь возвращалась сама точка, и ВСЕ оставшиеся звенья садились в неё одну: тело
        // схлопывалось само в себя комом. Теперь хвост просто вытягивается назад по своему же курсу
        Vector3 back = path.Count > 1 ? (path[path.Count - 2] - path[path.Count - 1]) : -transform.forward;
        if (back.sqrMagnitude < 0.0001f) back = -transform.forward;
        back.Normalize();
        dirToHead = back;   // `back` уже смотрит К ГОЛОВЕ — лишний минус разворачивал хвост на 180°
        return prev - back * remaining;
    }
}
