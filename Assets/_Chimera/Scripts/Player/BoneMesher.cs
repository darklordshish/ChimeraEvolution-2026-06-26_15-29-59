using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>ШКУРА ПО ПОЛЮ: кости задают скалярное поле, меш строится по его изоповерхности (спека 2026-08-18).
///
/// ПОЧЕМУ НЕ ТРУБА НА КОСТЬ. Труба вокруг каждой кости давала тело как НАБОР ОТДЕЛЬНЫХ ТЕЛ: рёбра читались
/// палками, скула — отростком, нога — сосиской, приставленной к боку. Радиусами это не лечится: сколько ни
/// утолщай, объекты остаются разными. Здесь каждая кость даёт поле-капсулу, поля сливаются МЯГКИМ МИНИМУМОМ,
/// и оболочка выходит ОДНА — рёбра сходятся в грудную клетку, скула утопает в черепе, нога вырастает из
/// корпуса. Толщина слияния — одно число на вид, то есть тюнинг вместо подгонки каждой детали.
///
/// ВЕСА СЛЕДУЮТ ИЗ ПОЛЯ. Вершина принадлежит тем костям, что её и вылепили: вклад кости в точку и есть её
/// вес. Отсюда и связь с идентичностью (спека 2026-08-14): вес скиннинга и вес формы — одна величина.
///
/// РЕЖЕТСЯ ПО СЛОТАМ. Изоповерхность общая, но треугольники раздаются модулям (слот кости-хозяйки точки):
/// один `SkinnedMeshRenderer` на слот, графт перестраивает свой модуль, имя рендерера = имя слота, поэтому
/// контракт имён частей работает без списков. Шва на границе нет — вершины у соседей общие по построению.</summary>
public static class BoneMesher
{
    /// <summary>Доля слияния МЕЖДУ модулями от слияния внутри модуля: страховка от щели там, где соседние
    /// части едва расходятся. Единица сплавила бы ногу с грудью в «юбку», ноль оставляет стык резким.
    ///
    /// ЧЕСТНО О ПОЛЬЗЕ: на волке эта страховка почти ничего не изменила — 15 проблемных срезов против 14.
    /// Значит щели у него не от резкости стыка, а оттого, что объёма там ПРОСТО НЕТ: кости не покрывают
    /// пространство, и лечится это анатомией, а не оператором композиции.</summary>
    const float Weld = 0.3f;

    // МЕШИ КЭШИРУЮТСЯ ПО ВИДУ: кости у всех волков одинаковы, значит и оболочка одна. Пересчёт нужен
    // только когда состав меняет сам скелет — до тех пор двадцать волков на арене делят одну геометрию
    static readonly Dictionary<string, (string slot, Mesh mesh)[]> cache = new();

    struct Seg   // кость, приведённая к мировой системе контейнера: поле считается по этим отрезкам
    {
        public Vector3 a, b;        // начало и конец кости
        public float r0, r1, sec, dep, blend;
        public BodyLayer layer;
        public int bone;            // индекс в массиве костей скиннинга
        public string slot;
        public Quaternion inv;      // мир → локальная кость (нужно для сплющивания сечением)
    }

    public static Transform Build(Transform container, SpeciesSO chassis, Material mat)
    {
        var bones = chassis.bones;
        var byName = new Dictionary<string, Bone>();
        foreach (var b in bones)
            if (b != null && !string.IsNullOrEmpty(b.name)) byName[b.name] = b;

        var skeleton = new GameObject("Skeleton").transform;
        skeleton.SetParent(container, false);

        // ── КОСТИ КАК ТРАНСФОРМЫ, ИЕРАРХИЕЙ. Ради этого всё и делалось: повернул плечо — поехала вся нога,
        // потому что она его потомок. Пара (`mirrorX`) даёт два трансформа: зеркалить меш на лету нельзя,
        // у скиннинга каждая вершина указывает на конкретную кость
        var placed = new Dictionary<string, (Vector3, Quaternion)>();
        var xf = new Dictionary<(string, int), Transform>();
        var segs = new List<Seg>();
        var order = new List<Transform>();

        foreach (var b in bones)
        {
            if (b == null || string.IsNullOrEmpty(b.name)) continue;
            var (p, r) = SkeletonBuilder.Place(b, byName, placed);
            for (int s = 0; s < (b.mirrorX ? 2 : 1); s++)
            {
                int side = s == 0 ? +1 : -1;
                var pos = p;
                var e = r.eulerAngles;
                // ЗЕРКАЛО — ОТРАЖЕНИЕМ ПОЗЫ, НЕ ОТРИЦАТЕЛЬНЫМ МАСШТАБОМ: минус в scale выворачивает
                // нормали наизнанку, и левая половина зверя чернеет ровно так же, как «работает»
                if (side < 0) { pos.x = -pos.x; e.y = -e.y; e.z = -e.z; }

                var t = new GameObject(b.name + (side < 0 ? ".L" : "")).transform;
                t.SetParent(skeleton, false);
                t.localPosition = pos;
                t.localEulerAngles = e;
                if (!string.IsNullOrEmpty(b.parent))
                {
                    if (xf.TryGetValue((b.parent, side), out var pt)) t.SetParent(pt, true);       // поза уже
                    else if (xf.TryGetValue((b.parent, +1), out var p0)) t.SetParent(p0, true);    // выставлена
                }
                xf[(b.name, side)] = t;

                var rot = Quaternion.Euler(e);
                var tip = pos + rot * (Vector3.up * b.length);

                // МЫШЦА НАТЯНУТА МЕЖДУ ДВУМЯ КОСТЯМИ: её конец не свободен, а живёт на кости-цели.
                // Поэтому ни длины, ни угла ей не задают — и то, и другое СЛЕДУЕТ из положения костей.
                // Подвинул сустав, сменил постановку — мышца подтянулась сама
                if (!string.IsNullOrEmpty(b.endBone) && byName.TryGetValue(b.endBone, out var eb))
                {
                    var (ep, er) = SkeletonBuilder.Place(eb, byName, placed);
                    var end = ep + er * (Vector3.up * (eb.length * b.endAttach));
                    if (side < 0) end.x = -end.x;
                    tip = end;
                    // разворачиваем саму кость вдоль мышцы, чтобы сечение считалось поперёк неё
                    var dir = end - pos;
                    if (dir.sqrMagnitude > 1e-8f)
                    {
                        rot = Quaternion.FromToRotation(Vector3.up, dir.normalized);
                        t.localRotation = Quaternion.Inverse(t.parent.rotation) * rot;
                    }
                }

                segs.Add(new Seg
                {
                    a = pos, b = tip,
                    r0 = b.r0, r1 = b.r1, sec = Mathf.Max(0.05f, b.section), dep = Mathf.Max(0.05f, b.depth),
                    blend = b.blend > 0f ? b.blend : chassis.SkinBlend, layer = b.layer,
                    bone = order.Count, slot = string.IsNullOrEmpty(b.socket) ? b.name : b.socket,
                    inv = Quaternion.Inverse(rot),
                });
                order.Add(t);
            }
        }

        var all = order.ToArray();
        var bind = new Matrix4x4[all.Length];
        for (int i = 0; i < all.Length; i++)
            bind[i] = all[i].worldToLocalMatrix * container.localToWorldMatrix;

        // ── ПОЛЕ СЧИТАЕТСЯ НА КАЖДЫЙ МОДУЛЬ ОТДЕЛЬНО, а не одно на всё тело.
        //
        // Пока поле было общим, нога НЕ МОГЛА выступить из корпуса: её поверхность растворялась в общей,
        // и рельефа не возникало ни при каких радиусах — выступать было нечему. Теперь каждый слот
        // считает свою оболочку, а модули ПЕРЕСЕКАЮТСЯ: линия пересечения и читается бороздой, как у
        // зверя нога входит в грудь.
        //     Отсюда же следует химеризация: донорский модуль просто встаёт на место шассийного, и
        // пересчитать надо ОДНУ лапу, а не всю тушу. Задача шасси — согласовать стыки: где сустав, куда
        // смотрит, какой там радиус; модуль обязан прийти в эту точку и зайти внутрь соседа с запасом.
        string key = chassis.speciesName + "#" + bones.Length + "#L" + chassis.BuildLayers;
        if (!cache.TryGetValue(key, out var parts))
            cache[key] = parts = Polygonize(segs.Where(x => (int)x.layer < chassis.BuildLayers).ToList(),
                                           bind, chassis.SkinCell, chassis.SkinBlend, chassis.SkinFur);

        foreach (var (slot, mesh) in parts)
        {
            var go = new GameObject(slot);                  // ИМЯ = СЛОТ: контракт имён частей
            go.transform.SetParent(container, false);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = all;
            smr.rootBone = skeleton;
            smr.updateWhenOffscreen = true;
            if (mat != null) smr.sharedMaterial = mat;
        }
        return skeleton;
    }

    // ── ПОЛЕ ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Расстояние до кости: капсула с ПЕРЕМЕННЫМ радиусом (конус со скруглёнными концами).
    /// Сечение сплющивается по локальному X — грудь узкая при той же глубине, как и было у труб.</summary>
    static float Distance(in Seg s, Vector3 p)
    {
        Vector3 q = s.inv * (p - s.a);        // в систему кости: она растёт по +Y
        q.x /= s.sec;                         // сплющивание сечения: по ширине и по глубине
        q.z /= s.dep;
        float h = (s.b - s.a).magnitude;
        Vector2 v = new(new Vector2(q.x, q.z).magnitude, q.y);

        float bb = (s.r0 - s.r1) / Mathf.Max(0.0001f, h);
        float aa = Mathf.Sqrt(Mathf.Max(0f, 1f - bb * bb));
        float k = -bb * v.x + aa * v.y;
        if (k < 0f) return v.magnitude - s.r0;
        if (k > aa * h) return (v - new Vector2(0f, h)).magnitude - s.r1;
        return aa * v.x + bb * v.y - s.r0;
    }

    /// <summary>МЯГКИЙ МИНИМУМ — то самое «слияние». При `k = 0` даёт обычное объединение (сосиски встык),
    /// при большом `k` тело оплывает в колбасу; между ними и живёт силуэт зверя.</summary>
    static float Smin(float a, float b, float k)
    {
        float h = Mathf.Max(k - Mathf.Abs(a - b), 0f) / Mathf.Max(0.0001f, k);
        return Mathf.Min(a, b) - h * h * k * 0.25f;
    }

    // ── ИЗОПОВЕРХНОСТЬ (Surface Nets) ─────────────────────────────────────────────────────────────────

    static (string slot, Mesh mesh)[] Polygonize(List<Seg> segs, Matrix4x4[] bind, float cell, float blend, float fur)
    {
        // сетка по габариту скелета с запасом на радиус и слияние
        Vector3 lo = new(9f, 9f, 9f), hi = new(-9f, -9f, -9f);
        foreach (var s in segs)
        {
            float r = Mathf.Max(s.r0, s.r1) * Mathf.Max(1f, Mathf.Max(s.sec, s.dep)) + s.blend + fur;
            lo = Vector3.Min(lo, Vector3.Min(s.a, s.b) - Vector3.one * r);
            hi = Vector3.Max(hi, Vector3.Max(s.a, s.b) + Vector3.one * r);
        }
        lo -= Vector3.one * cell; hi += Vector3.one * cell;

        int nx = Mathf.CeilToInt((hi.x - lo.x) / cell) + 1;
        int ny = Mathf.CeilToInt((hi.y - lo.y) / cell) + 1;
        int nz = Mathf.CeilToInt((hi.z - lo.z) / cell) + 1;
        var field = new float[nx * ny * nz];
        // ЧЕЙ ЭТО КУСОК ТЕЛА: слот кости, которая ближе всего к ячейке. Хранится рядом с полем, потому
        // что от него зависит САМ СПОСОБ композиции (ниже) — и он же потом раздаёт треугольники модулям
        var owner = new int[nx * ny * nz];
        for (int i = 0; i < field.Length; i++) { field[i] = 9f; owner[i] = -1; }
        int Idx(int x, int y, int z) => (z * ny + y) * nx + x;

        var slots = new List<string>();
        int SlotId(string s)
        {
            int k = slots.IndexOf(s);
            if (k < 0) { slots.Add(s); k = slots.Count - 1; }
            return k;
        }

        // ЗАПОЛНЯЕМ ПО КОСТЯМ, А НЕ ПО ЯЧЕЙКАМ: каждая кость трогает только свою окрестность, поэтому
        // цена растёт от объёма зверя, а не от произведения «все ячейки × все кости».
        //     ПОРЯДОК СЛОЁВ ВАЖЕН: сперва кости, затем мышцы и признаки — и только потом ВЫЧИТАНИЕ.
        // Резать надо по готовому телу, иначе мышца, добавленная после, заполнит прорезанную щель
        foreach (var s in segs.OrderBy(x => (int)x.layer))
        {
            int sid = SlotId(s.slot);
            float reach = Mathf.Max(s.r0, s.r1) * Mathf.Max(1f, Mathf.Max(s.sec, s.dep)) + s.blend + fur + cell;
            Vector3 mn = Vector3.Min(s.a, s.b) - Vector3.one * reach;
            Vector3 mx = Vector3.Max(s.a, s.b) + Vector3.one * reach;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((mn.x - lo.x) / cell)), x1 = Mathf.Min(nx - 1, Mathf.CeilToInt((mx.x - lo.x) / cell));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((mn.y - lo.y) / cell)), y1 = Mathf.Min(ny - 1, Mathf.CeilToInt((mx.y - lo.y) / cell));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((mn.z - lo.z) / cell)), z1 = Mathf.Min(nz - 1, Mathf.CeilToInt((mx.z - lo.z) / cell));

            for (int z = z0; z <= z1; z++)
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var p = lo + new Vector3(x, y, z) * cell;
                        int i = Idx(x, y, z);
                        float d = Distance(s, p);

                        // ВНУТРИ МОДУЛЯ — СЛИЯНИЕ, МЕЖДУ МОДУЛЯМИ — ОБЪЕДИНЕНИЕ.
                        //
                        // Это ответ на «unwanted blending at a distance» (Gourmel и др., 2013): наивное
                        // слияние сплавляет всё, что оказалось рядом, и нога с грудью превращаются в
                        // «юбку». В статье критерий геометрический (углы градиентов), но нам он не нужен —
                        // мы ЗНАЕМ структуру: кость принадлежит слоту, слот и есть часть тела. Рёбра
                        // сливаются в одну клетку, кости ноги в одну ногу, а нога с грудью лишь
                        // ОБЪЕДИНЯЕТСЯ — и линия их пересечения читается бороздой, как у зверя.
                        //     Поле при этом ОДНО на всё тело, поэтому поверхность непрерывна: соседние
                        // модули делят одни и те же вершины, и швов между ними нет по построению.
                        //     ЧУЖИЕ СЛИВАЮТСЯ СЛАБО, А НЕ «НИКАК». Чистое объединение оставляет щель
                        // всюду, где поверхности соседних модулей едва расходятся: у нас так рвало бок
                        // между грудной клеткой и брюхом и пах между бедром и животом. Малое слияние
                        // затягивает эти зазоры, но борозду не съедает — она глубже, чем `Weld`
                        // ВЫЧИТАНИЕ — РАЗНОСТЬ, А НЕ ОБЪЕДИНЕНИЕ: слой `Cut` вырезает объём из уже
                        // собранного тела. Так делаются рот, ноздри и глазница: это УГЛУБЛЕНИЯ, и
                        // добавлением их не получить — сколько объёмов ни клади, щель не появится
                        if (s.layer == BodyLayer.Cut)
                        {
                            field[i] = -Smin(-field[i], d, s.blend);
                            continue;
                        }
                        if (owner[i] == sid) field[i] = Smin(field[i], d, s.blend);
                        else
                        {
                            float f = Smin(field[i], d, s.blend * Weld);
                            if (d < field[i]) owner[i] = sid;
                            field[i] = f;
                        }
                    }
        }

        // ШУБА НАДЕВАЕТСЯ ЗДЕСЬ, ОДНОЙ СТРОКОЙ: поверхность ищется не на «поле = 0», а на «поле = −fur»,
        // поэтому оболочка отходит наружу равномерно по всему телу. Мех лежит НА звере — он не часть
        // скелета, и прибавлять его к радиусам костей нельзя: так выходит колбаса вдоль хребта
        if (fur > 0f) for (int i = 0; i < field.Length; i++) field[i] -= fur;

        // ── вершина на ячейку: среднее точек, где поле меняет знак вдоль её рёбер
        var vertOf = new int[(nx - 1) * (ny - 1) * (nz - 1)];
        for (int i = 0; i < vertOf.Length; i++) vertOf[i] = -1;
        int VIdx(int x, int y, int z) => (z * (ny - 1) + y) * (nx - 1) + x;

        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var weights = new List<BoneWeight>();
        var slotOf = new List<string>();
        var corner = new int[8];

        for (int z = 0; z < nz - 1; z++)
            for (int y = 0; y < ny - 1; y++)
                for (int x = 0; x < nx - 1; x++)
                {
                    corner[0] = Idx(x, y, z);         corner[1] = Idx(x + 1, y, z);
                    corner[2] = Idx(x, y + 1, z);     corner[3] = Idx(x + 1, y + 1, z);
                    corner[4] = Idx(x, y, z + 1);     corner[5] = Idx(x + 1, y, z + 1);
                    corner[6] = Idx(x, y + 1, z + 1); corner[7] = Idx(x + 1, y + 1, z + 1);

                    int inside = 0;
                    for (int c = 0; c < 8; c++) if (field[corner[c]] < 0f) inside++;
                    if (inside == 0 || inside == 8) continue;      // ячейка целиком внутри или снаружи

                    Vector3 sum = Vector3.zero; int cuts = 0;
                    for (int e = 0; e < 12; e++)
                    {
                        int ca = EdgeA[e], cb = EdgeB[e];
                        float fa = field[corner[ca]], fb = field[corner[cb]];
                        if ((fa < 0f) == (fb < 0f)) continue;
                        float t = fa / (fa - fb);
                        sum += Vector3.Lerp(CornerPos[ca], CornerPos[cb], t);
                        cuts++;
                    }
                    if (cuts == 0) continue;

                    var local = sum / cuts;
                    var world = lo + (new Vector3(x, y, z) + local) * cell;

                    // НОРМАЛЬ — ГРАДИЕНТ УЖЕ ПОСЧИТАННОГО ПОЛЯ, а не пересчёт по всем костям: иначе на
                    // каждую вершину уходит шесть проходов по полусотне костей, и генерация из десятков
                    // миллисекунд превращается в секунды. Поле то же самое, поэтому на стыке модулей
                    // нормаль непрерывна и шва между мешами разных слотов не видно
                    Vector3 g = Vector3.zero;
                    for (int c = 0; c < 8; c++)
                    {
                        var cp = CornerPos[c];
                        float w = (cp.x < 0.5f ? 1f - local.x : local.x)
                                * (cp.y < 0.5f ? 1f - local.y : local.y)
                                * (cp.z < 0.5f ? 1f - local.z : local.z);
                        if (w <= 0f) continue;
                        int gx = x + (int)cp.x, gy = y + (int)cp.y, gz = z + (int)cp.z;
                        g += w * new Vector3(
                            field[Idx(Mathf.Min(gx + 1, nx - 1), gy, gz)] - field[Idx(Mathf.Max(gx - 1, 0), gy, gz)],
                            field[Idx(gx, Mathf.Min(gy + 1, ny - 1), gz)] - field[Idx(gx, Mathf.Max(gy - 1, 0), gz)],
                            field[Idx(gx, gy, Mathf.Min(gz + 1, nz - 1))] - field[Idx(gx, gy, Mathf.Max(gz - 1, 0))]);
                    }

                    // МОДУЛЬ ВЕРШИНЫ — ПО ХОЗЯИНУ ЯЧЕЙКИ, а не по повторному поиску ближайшей кости:
                    // так разрез меша совпадает с тем, как поле СОБИРАЛОСЬ, и граница между модулями
                    // проходит ровно там, где объединение сменило хозяина
                    int own = -1;
                    for (int c = 0; c < 8 && own < 0; c++) own = owner[corner[c]];
                    weights.Add(Weigh(segs, world, blend, out string near));
                    slotOf.Add(own >= 0 ? slots[own] : near);

                    vertOf[VIdx(x, y, z)] = verts.Count;
                    verts.Add(world);
                    norms.Add(g.sqrMagnitude > 1e-12f ? g.normalized : Vector3.up);
                }

        // ── квады: ребро сетки со сменой знака соединяет четыре соседние ячейки
        var perSlot = new Dictionary<string, List<int>>();
        void Quad(int a, int b, int c, int d, bool flip)
        {
            if (a < 0 || b < 0 || c < 0 || d < 0) return;
            string slot = slotOf[a];                       // модуль по хозяйке первой вершины
            if (!perSlot.TryGetValue(slot, out var tris)) perSlot[slot] = tris = new List<int>();
            if (flip) { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(a); tris.Add(c); tris.Add(d); }
            else      { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(a); tris.Add(d); tris.Add(c); }
        }

        for (int z = 1; z < nz - 1; z++)
            for (int y = 1; y < ny - 1; y++)
                for (int x = 1; x < nx - 1; x++)
                {
                    bool s000 = field[Idx(x, y, z)] < 0f;
                    if (s000 != field[Idx(x + 1, y, z)] < 0f)
                        Quad(vertOf[VIdx(x, y - 1, z - 1)], vertOf[VIdx(x, y, z - 1)],
                             vertOf[VIdx(x, y, z)], vertOf[VIdx(x, y - 1, z)], s000);
                    if (s000 != field[Idx(x, y + 1, z)] < 0f)
                        Quad(vertOf[VIdx(x - 1, y, z - 1)], vertOf[VIdx(x, y, z - 1)],
                             vertOf[VIdx(x, y, z)], vertOf[VIdx(x - 1, y, z)], !s000);
                    if (s000 != field[Idx(x, y, z + 1)] < 0f)
                        Quad(vertOf[VIdx(x - 1, y - 1, z)], vertOf[VIdx(x, y - 1, z)],
                             vertOf[VIdx(x, y, z)], vertOf[VIdx(x - 1, y, z)], s000);
                }

        // ── меш на слот: переиндексация, чтобы каждый нёс только свои вершины
        var result = new List<(string, Mesh)>();
        foreach (var kv in perSlot)
        {
            var map = new Dictionary<int, int>();
            var mv = new List<Vector3>(); var mn = new List<Vector3>();
            var mw = new List<BoneWeight>(); var mt = new List<int>();
            foreach (int vi in kv.Value)
            {
                if (!map.TryGetValue(vi, out int local))
                {
                    map[vi] = local = mv.Count;
                    mv.Add(verts[vi]); mn.Add(norms[vi]); mw.Add(weights[vi]);
                }
                mt.Add(local);
            }
            var mesh = new Mesh { name = kv.Key };
            mesh.SetVertices(mv); mesh.SetNormals(mn); mesh.SetTriangles(mt, 0);
            mesh.boneWeights = mw.ToArray();
            mesh.bindposes = bind;
            mesh.RecalculateBounds();
            result.Add((kv.Key, mesh));
        }
        return result.ToArray();
    }

    /// <summary>ВЕС ИЗ ПОЛЯ: вершину держат те кости, что её вылепили. Две ближайшие делят вес по тому,
    /// насколько каждая ближе, — на суставе выходит ровно тот плавный переход, ради которого скиннинг.</summary>
    static BoneWeight Weigh(List<Seg> segs, Vector3 p, float blend, out string slot)
    {
        int i0 = 0, i1 = -1; float d0 = 9f, d1 = 9f;
        for (int i = 0; i < segs.Count; i++)
        {
            float d = Distance(segs[i], p);
            if (d < d0) { d1 = d0; i1 = i0; d0 = d; i0 = i; }
            else if (d < d1) { d1 = d; i1 = i; }
        }
        slot = segs[i0].slot;
        float span = Mathf.Max(0.0001f, blend * 2f);
        float w1 = i1 < 0 ? 0f : Mathf.Clamp01(1f - (d1 - d0) / span) * 0.5f;
        return new BoneWeight
        {
            boneIndex0 = segs[i0].bone, weight0 = 1f - w1,
            boneIndex1 = i1 < 0 ? segs[i0].bone : segs[i1].bone, weight1 = w1,
        };
    }

    // углы ячейки и её рёбра — порядок фиксирован и используется обоими проходами
    static readonly Vector3[] CornerPos =
    {
        new(0,0,0), new(1,0,0), new(0,1,0), new(1,1,0),
        new(0,0,1), new(1,0,1), new(0,1,1), new(1,1,1),
    };
    static readonly int[] EdgeA = { 0, 2, 4, 6, 0, 1, 4, 5, 0, 1, 2, 3 };
    static readonly int[] EdgeB = { 1, 3, 5, 7, 2, 3, 6, 7, 4, 5, 6, 7 };
}
