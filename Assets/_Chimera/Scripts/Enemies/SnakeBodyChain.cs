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
    [SerializeField] Transform[] segments;      // от шеи к погремушке (генератор заполняет)
    [SerializeField] float spacing = 0.32f;     // шаг = ПОЛОВИНА длины звена (0.64): части идут подряд и чередуются
                                                // капсула → сустав → капсула, поэтому шар всегда садится ровно на стык.
                                                // Тот же язык соединений, что в конечностях: шар-сустав, в него входит звено
    [SerializeField] float height = 0.3f;       // высота центров сегментов над путём (путь пишется по земле)
    [SerializeField] float sampleStep = 0.08f;  // шаг записи пути головы
    [SerializeField] int maxSamples = 256;

    readonly List<Vector3> path = new(); // [0] — новейшая точка
    Vector3 lastSample;

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
    static readonly string[] ChainNames = { "шея", "Тело", "Хвост" };
    // ПРИЦЕПЫ: не звенья, а грузы на ПОСЛЕДНЕМ звене — едут с ним и разлететься не могут в принципе.
    // Погремушка была звеном, и цепь растаскивала её кольца по пути, сколько их ни склеивай
    static readonly string[] Attached = { "Погремушка" };
    // МОНОЛИТНЫЕ МЕСТА: собраны из нескольких деталей, но звеном едут ОДНИМ. Приём общий — годится любому
    // месту-не-цепи, чьи детали должны держаться вместе (стопка колец погремушки, гроздь, пучок)
    static readonly string[] Monolithic = { "Погремушка" };

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
            foreach (var n in ChainNames)
                if (c.name == n) { found.Add(c); break; }
        }
        if (found.Count == 0) return; // морф ничего не дал (чужое шасси без цепи) — остаёмся на прежних сегментах

        // МОНОЛИТНОЕ ЗВЕНО: место, которое НЕ цепь, но собрано из нескольких деталей (погремушка — стопка
        // роговых колец), склеиваем в ОДИН узел: первая деталь становится звеном, остальные — её детьми.
        // Иначе цепь считает каждое кольцо отдельным звеном и растаскивает их по пути. Родство берём
        // с сохранением мировых позиций — взаимная раскладка колец уже посчитана графом
        // ГРУЗЫ НА ХВОСТЕ: всё, что не звено, вешаем на ПОСЛЕДНЕЕ звено цепи целиком
        var tail = found[found.Count - 1];
        for (int i = 0; i < morph.childCount; i++)
        {
            var c = morph.GetChild(i);
            if (System.Array.IndexOf(Attached, c.name) < 0) continue;
            // САЖАЕМ ВПЛОТНУЮ, а не сохраняя мировую позу: граф расставил части в СТАРТОВОЙ раскладке,
            // а цепь потом разложит звенья по пути совсем иначе — сохранённое смещение унесло бы груз
            // за несколько метров от хвоста. Локальная Y звена идёт ВДОЛЬ пути (звено довёрнуто на 90°)
            c.SetParent(tail, false);
            c.localPosition = Vector3.down * spacing;
            c.localRotation = Quaternion.identity;
            i--;                     // ребёнок ушёл из контейнера, индексы сдвинулись
        }

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

        // ШАГ СЧИТАЕМ ИЗ РЕАЛЬНОГО РАЗМЕРА ЗВЕНА, а не берём из поля. Сериализованное значение живёт В
        // ПРЕФАБЕ и НЕ обновляется, когда правишь дефолт в коде: в префабе лежало 0.62 против 0.32 здесь,
        // и пять итераций подряд менялось число, которое игра игнорировала. Теперь расхождение невозможно —
        // шаг = ПОЛОВИНА длины звена, потому что части чередуются капсула → сустав → капсула
        var r = found[0].GetComponent<Renderer>();
        if (r != null) spacing = Mathf.Max(0.05f, Mathf.Max(r.bounds.size.x, Mathf.Max(r.bounds.size.y, r.bounds.size.z)) * 0.5f);

        IgnoreOwnBody(); // части плотные (solid) — свои же коллайдеры не должны толкать собственный CC
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
            path.Insert(0, lastSample);
            if (path.Count > maxSamples) path.RemoveAt(path.Count - 1);
        }

        if (segments == null) return;
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == null) continue;
            Vector3 p = PointAlongPath((i + 1) * spacing, out Vector3 toHead);
            // смещение вдоль ВЕРХА ТЕЛА (transform.up): на земле = мировой верх (как было), на стене = нормаль
            // стены → сегменты отходят ОТ стены заодно с головой, а не влипают в плоскость
            segments[i].position = p + transform.up * height;
            // ПОВОРОТ ЗВЕНА ЖИВЁТ ТОЛЬКО ЗДЕСЬ. Мы задаём rotation ЦЕЛИКОМ, то есть любой наклон из данных
            // затирается каждый кадр — держать его ещё и там значит ловить то двойной доворот, то никакого.
            // Euler(90) кладёт капсулу (она вытянута по Y) вдоль пути; шар-шарнир к повороту безразличен
            if (toHead.sqrMagnitude > 0.0001f)
                segments[i].rotation = Quaternion.LookRotation(toHead, transform.up) * Quaternion.Euler(90f, 0f, 0f);
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
        dirToHead = back * -1f;
        return prev - back * remaining;
    }
}
