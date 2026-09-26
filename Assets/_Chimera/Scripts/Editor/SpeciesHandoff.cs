using System.Text;
using UnityEngine;

/// <summary>ПОСТАВКА ФОРМЫ: силуэтный граф вида приходит ФАЙЛОМ от модельной линии и задаёт форму вида
/// целиком — кости и скрытые места. В коде бутстрапа костей нет.
///
/// ЗАЧЕМ ИМЕННО ТАК. Форму решает модельная линия, запись держат механики («числа их, запись наша»,
/// `Docs/ЗОНЫ.md`). Пока граф лежал бы в нашем C#-коде, у одной формы было бы ДВА источника: файл
/// поставки и копия в бутстрапе. Разошлись бы они молча и в худший момент — приняли граф в ассет, а
/// следующее «Создать дефолтные виды» вернуло старые кости, и никакой ошибки при этом не возникает.
/// Проект такую цену уже платил (клетка против спеки идентичности — три недели), поэтому здесь
/// источник один: файл. Нет файла — у вида нет формы вовсе, он рисуется одними местами.
///
/// Формат и инварианты — `Docs/models/SPEC-priyomka-formy.md`; проверка и ручной перенос —
/// `Tools/Agent/Graph.cs` (`Graph.Check`, `Graph.Import`).</summary>
public static class SpeciesHandoff
{
    public const string Dir = "Docs/models/handoff/";

    [System.Serializable]
    class Delivery
    {
        public string species;
        public string[] hides;
        public Bone[] nodes;
    }

    // ── РАСКЛАДКИ: калибр и места деталей, куски органов ──────────────────────────────────────────
    // Формат задан поставкой 3 модельной линии (`volk-head-layout.json`; раскладка кусков органов — `volk-organs-layout.json`, до поставки 5 звалась `legs-layout`). Числа
    // массивами, а не объектами Vector3 — так их пишет генератор на Python; лишние поля («metres», «note») —
    // пояснения для людей, разбор их пропускает

    [System.Serializable] class Calibre { public float[] baseSize; public float[] sizeRel; }
    [System.Serializable] class PlaceDto { public string name, parent; public float attach; public float[] attachOffset, sizeRel, baseEuler; }
    [System.Serializable] class PartDto { public string node, block, role; public float[] offset, scale, euler, color; public bool nest; }
    [System.Serializable] class HeadLayout { public Calibre head; public PlaceDto[] places; public PartDto muzzle; public PartDto[] teeth, senses; }
    [System.Serializable] class OrganDto { public string slot, organ; public PartDto[] parts; }
    [System.Serializable] class OrgansLayout { public OrganDto[] organs; }
    [System.Serializable] class SurfaceDto { public float z0, z1; }
    [System.Serializable] class NestDto { public string name, host; public float[] pos, dir; public float unit; public bool mirror, proposed; public SurfaceDto surface; }
    [System.Serializable] class PlacesLayout { public string species; public NestDto[] places; }

    /// <summary>Применить поставку вида целиком: граф, раскладку головы, раскладку кусков органов (ноги, рога). Каждый файл
    /// независим — нет раскладки, значит те места и органы остаются, какими их задал бутстрап.</summary>
    public static bool Apply(SpeciesSO species)
    {
        if (species == null) return false;
        bool graph = ApplyGraph(species);

        string stem = Dir + Translit(species.speciesName);
        string head = System.IO.File.Exists(stem + "-head-layout.json") ? System.IO.File.ReadAllText(stem + "-head-layout.json") : null;
        string organs = System.IO.File.Exists(stem + "-organs-layout.json") ? System.IO.File.ReadAllText(stem + "-organs-layout.json") : null;
        // ИМЯ «legs-layout» СНЯТО 17.09 (поставка 5): в файле кроме ног лежат рога, а скоро иглы и хвосты — это куски
        // любых органов. Старое имя не читаем и не молчим: регенерация прежним генератором должна быть видна сразу
        if (System.IO.File.Exists(stem + "-legs-layout.json"))
            Debug.LogError($"[форма] {species.speciesName}: «{stem}-legs-layout.json» — устаревшее имя, файл НЕ читается. Раскладка кусков органов живёт в «-organs-layout.json» с корнем «organs»");
        // ГНЁЗДА — ПОСЛЕ ГРАФА: кадр гнезда переводится в кадр кости-хозяина, а кости только что пришли из поставки.
        // Нет файла — гнёзд нет, и это записывается ЯВНО: бутстрап не обнуляет поля, которые перестал присваивать
        string places = System.IO.File.Exists(stem + "-places-layout.json") ? System.IO.File.ReadAllText(stem + "-places-layout.json") : null;
        species.nests = new PlaceNest[0];
        if (places != null)
        {
            species.nests = ReadNests(species, places, out var problems);
            foreach (var p in problems) Debug.LogError($"[гнёзда] {species.speciesName}: {p}");
            Debug.Log($"[гнёзда] {species.speciesName}: принято гнёзд {species.nests.Length}, замечаний {problems.Count}");
        }

        if (head != null || organs != null)
        {
            int n = ApplyLayouts(species, head, organs);
            Debug.Log($"[форма] {species.speciesName}: приняты раскладки из поставки — изменений {n}");
        }
        return graph;
    }

    /// <summary>РАСКЛАДКИ ЧИТАЮТСЯ ИЗ ФАЙЛОВ, А НЕ ПЕРЕПИСЫВАЮТСЯ В КОД. Модельная линия предложила внести числа
    /// в данные вида руками («числа наши, запись ваша»). Но тогда у формы снова два источника: генератор
    /// раскладки у них и копия чисел у нас, и первая же регенерация разойдётся с кодом молча — ровно та беда,
    /// из-за которой граф уехал в файл. Промах имени (места или органа нет у вида) не проглатывается: в консоль.
    /// Возвращает число применённых изменений; открыт для теста, чтобы не зависеть от файлов на диске.</summary>
    public static int ApplyLayouts(SpeciesSO species, string headJson, string organsJson)
    {
        int n = 0;
        if (!string.IsNullOrEmpty(headJson))
        {
            var h = JsonUtility.FromJson<HeadLayout>(headJson);

            var headPlace = FindSocket(species, "голова");
            if (h.head != null && headPlace != null)
            {
                headPlace.baseSize = V(h.head.baseSize, headPlace.baseSize);
                headPlace.sizeRel = V(h.head.sizeRel, headPlace.sizeRel);
                n++;
            }

            foreach (var p in h.places ?? new PlaceDto[0])
            {
                var s = FindSocket(species, p.name);
                if (s == null) { Debug.LogError($"[форма] {species.speciesName}: раскладка головы называет место «{p.name}», которого у вида нет"); continue; }
                // РОДИТЕЛЬ В РАСКЛАДКЕ — СВЕРКА, А НЕ ЗАПИСЬ СТРУКТУРЫ (поставка 4 §3). Доли считаются в калибре родителя:
                // мочка, посчитанная от Пасти и применённая к месту на голове, молча встала бы вдвое шире и на 3 см выше.
                // Родство мест — запись механик в бутстрапе; разошлись — не применяем и кричим
                if (!string.IsNullOrEmpty(p.parent) && p.parent != (s.parent ?? ""))
                {
                    Debug.LogError($"[форма] {species.speciesName}: раскладка посчитала место «{p.name}» от родителя «{p.parent}», а у вида его родитель «{s.parent}» — доли не применены");
                    continue;
                }
                s.attach = p.attach;
                s.attachOffset = V(p.attachOffset, s.attachOffset);
                s.sizeRel = V(p.sizeRel, s.sizeRel);
                s.baseEuler = V(p.baseEuler, s.baseEuler);
                n++;
            }

            // МОРДА И ЗУБЫ — куски органа Пасти. Список заменяется ЦЕЛИКОМ: прежние зубы заданы в старом калибре
            // Пасти и в новой коробке морды выросли бы по высоте в 2.8 раза (письмо поставки 3, §4)
            // JsonUtility не бывает null для вложенных классов: отсутствующая «muzzle» приходит пустым объектом.
            // Морда есть, только если в ней что-то задано — иначе в Пасть лёг бы голый куб
            bool hasMuzzle = h.muzzle != null && (!string.IsNullOrEmpty(h.muzzle.block) || h.muzzle.scale != null);
            if (hasMuzzle || (h.teeth != null && h.teeth.Length > 0))
            {
                var maw = FindOrgan(species, "Пасть", null);
                if (maw == null) Debug.LogError($"[форма] {species.speciesName}: в раскладке головы есть морда, а органа на слоте «Пасть» у вида нет");
                else
                {
                    var parts = new System.Collections.Generic.List<OrganPart>();
                    if (hasMuzzle) parts.Add(Part(h.muzzle));
                    foreach (var t in h.teeth ?? new PartDto[0]) parts.Add(Part(t));
                    maw.visualParts = parts.ToArray();
                    n++;
                }
            }

            // УШИ, ГЛАЗА, НОС — части органа Чутья с ролью (решение 17.09: признаки чувств ригблоками, а не шарами).
            // Заменяются части ТОЛЬКО названных ролей: прислали уши — нос остаётся прежним. ЦВЕТ ГЛАЗА — КАНАЛ
            // механики (прозрение, нюх), а не форма: блок без своего цвета наследует прежний, иначе поставка формы
            // молча выключила бы игроку подсказку, чем зверь воспринимает мир
            if (h.senses != null && h.senses.Length > 0)
            {
                var sense = FindOrgan(species, "Чутьё", null);
                if (sense == null) Debug.LogError($"[форма] {species.speciesName}: в раскладке головы есть признаки чувств, а органа на слоте «Чутьё» у вида нет");
                else
                {
                    var roles = new System.Collections.Generic.HashSet<PartRole>();
                    var fresh = new System.Collections.Generic.List<OrganPart>();
                    foreach (var s in h.senses)
                    {
                        if (!System.Enum.TryParse(s.role, out PartRole role) || role == PartRole.None)
                        {
                            Debug.LogError($"[форма] {species.speciesName}: признак чувства с ролью «{s.role}» — роли такой нет (Eye, Ear, Nose, Pit)");
                            continue;
                        }
                        var part = Part(s);
                        part.role = role;
                        fresh.Add(part);
                        roles.Add(role);
                    }
                    var kept = new System.Collections.Generic.List<OrganPart>();
                    foreach (var old in sense.visualParts ?? new OrganPart[0])
                    {
                        if (old == null) continue;
                        if (!roles.Contains(old.role)) { kept.Add(old); continue; }
                        if (old.role == PartRole.Eye && old.color.a > 0f)
                            foreach (var f in fresh) if (f.role == PartRole.Eye && f.color.a <= 0f) f.color = old.color;
                    }
                    kept.AddRange(fresh);
                    sense.visualParts = kept.ToArray();
                    n++;
                }
            }
        }

        if (!string.IsNullOrEmpty(organsJson))
        {
            var l = JsonUtility.FromJson<OrgansLayout>(organsJson);
            foreach (var leg in l.organs ?? new OrganDto[0])
            {
                var organ = FindOrgan(species, leg.slot, leg.organ);
                if (organ == null) { Debug.LogError($"[форма] {species.speciesName}: раскладка кусков органов называет орган «{leg.organ}» на слоте «{leg.slot}», которого у вида нет"); continue; }
                var parts = new System.Collections.Generic.List<OrganPart>();
                foreach (var p in leg.parts ?? new PartDto[0]) parts.Add(Part(p));
                organ.visualParts = parts.ToArray();   // целиком: прежние колонны и мышцы на погашенном месте не нужны
                n++;
            }
        }
        return n;
    }

    /// <summary>РАСКЛАДКА ГНЁЗД → гнёзда в кадре костей (спека 26.09, П1; формат — письмо модельной линии
    /// `FEEDBACK-2026-09-26c-format-gnezd.md` §3). Поставка пишет гнездо в метрах тела — там же, где стоят кости графа
    /// (`SkeletonBuilder.Place`), поэтому перевод — одна обратная поза кости. `problems` — всё, что не так: промах имени
    /// места или кости, пустая единица, МЕСТО ВИДА БЕЗ ГНЕЗДА (тотальность, П2 — иначе привитому аугменту некуда
    /// встать). Битое гнездо не применяется, остальные — да: одна опечатка не должна гасить всё тело.</summary>
    public static PlaceNest[] ReadNests(SpeciesSO species, string json, out System.Collections.Generic.List<string> problems)
    {
        problems = new System.Collections.Generic.List<string>();
        var res = new System.Collections.Generic.List<PlaceNest>();
        PlacesLayout l;
        try { l = JsonUtility.FromJson<PlacesLayout>(json); }
        catch (System.Exception e) { problems.Add("раскладка гнёзд не разбирается — " + e.Message); return res.ToArray(); }
        if (l == null || l.places == null) { problems.Add("в раскладке гнёзд нет «places»"); return res.ToArray(); }
        if (!string.IsNullOrEmpty(l.species) && l.species != species.speciesName)
        { problems.Add($"раскладка гнёзд названа для «{l.species}», а лежит у «{species.speciesName}»"); return res.ToArray(); }

        var byBone = new System.Collections.Generic.Dictionary<string, Bone>();
        foreach (var b in species.bones ?? new Bone[0]) if (b != null && !string.IsNullOrEmpty(b.name)) byBone[b.name] = b;
        var pose = new System.Collections.Generic.Dictionary<string, (Vector3, Quaternion)>();

        foreach (var d in l.places)
        {
            if (d == null || string.IsNullOrEmpty(d.name)) continue;
            if (FindSocket(species, d.name) == null) { problems.Add($"гнездо «{d.name}» — такого места у вида нет"); continue; }
            if (d.unit <= 0f) { problems.Add($"гнездо «{d.name}»: единица не задана"); continue; }

            // хозяин — кость графа; у змеи цепь звеньев (`звено:N`) костями не является — такие гнёзда ждут своего
            // хода в билдере, но записываются, чтобы тотальность считалась честно
            Vector3 hp = Vector3.zero; Quaternion hr = Quaternion.identity;
            bool chain = d.host != null && d.host.StartsWith("звено:");
            if (!chain)
            {
                if (string.IsNullOrEmpty(d.host) || !byBone.TryGetValue(d.host, out var host))
                { problems.Add($"гнездо «{d.name}»: кости-хозяина «{d.host}» в графе нет"); continue; }
                (hp, hr) = SkeletonBuilder.Place(host, byBone, pose);
            }

            bool surface = d.surface != null && (d.surface.z0 != 0f || d.surface.z1 != 0f);
            Vector3 pos = V(d.pos, surface ? hp : Vector3.zero);
            Vector3 dir = V(d.dir, Vector3.forward);
            if (dir.sqrMagnitude < 1e-8f) dir = Vector3.forward;
            // «верх» гнезда — мировой верх; у вертикального гнезда (конечность, рог) — перёд тела
            Vector3 up = Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
            var rot = Quaternion.LookRotation(dir.normalized, up);

            var inv = Quaternion.Inverse(hr);
            res.Add(new PlaceNest
            {
                name = d.name,
                host = d.host,
                localPos = chain ? pos : inv * (pos - hp),
                localRot = chain ? rot : inv * rot,
                unit = d.unit,
                mirror = d.mirror,
                proposed = d.proposed,
                span = surface ? new Vector2(d.surface.z0, d.surface.z1) : Vector2.zero,
            });
        }

        // ТОТАЛЬНОСТЬ (П2): гнездо у КАЖДОГО места вида, включая те, которых у вида нет по природе
        var have = new System.Collections.Generic.HashSet<string>();
        foreach (var n in res) have.Add(n.name);
        foreach (var s in species.sockets ?? new BodySocket[0])
            if (s != null && !string.IsNullOrEmpty(s.name) && !have.Contains(s.name))
                problems.Add($"у места «{s.name}» нет гнезда");

        return res.ToArray();
    }

    static BodySocket FindSocket(SpeciesSO s, string name)
    {
        if (s.sockets != null) foreach (var k in s.sockets) if (k != null && k.name == name) return k;
        return null;
    }

    static Organ FindOrgan(SpeciesSO s, string slot, string organName)
    {
        if (s.organs != null)
            foreach (var o in s.organs)
                if (o != null && o.slot == slot && (organName == null || o.organName == organName)) return o;
        return null;
    }

    static OrganPart Part(PartDto p) => new OrganPart
    {
        node = p.node ?? "",
        block = p.block ?? "",
        nest = p.nest,
        offset = V(p.offset, Vector3.zero),
        scale = V(p.scale, Vector3.one),
        euler = V(p.euler, Vector3.zero),
        color = p.color != null && p.color.Length >= 3
            ? new Color(p.color[0], p.color[1], p.color[2], p.color.Length > 3 ? p.color[3] : 1f)
            : new Color(0f, 0f, 0f, 0f),       // альфа 0 = своей окраски нет, цвет по составу
    };

    static Vector3 V(float[] a, Vector3 fallback) => a != null && a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : fallback;

    /// <summary>Подменить кости и скрытия вида, если рядом лежит его граф. Битую поставку НЕ применяем молча:
    /// кричим в консоль и оставляем прежнее — тихая подмена хуже отсутствия файла.</summary>
    static bool ApplyGraph(SpeciesSO species)
    {
        string path = Dir + Translit(species.speciesName) + "-graph.json";

        // НЕТ ФАЙЛА — НЕТ ФОРМЫ, и это надо записать ЯВНО. Бутстрап загружает существующий ассет и не трогает
        // поля, которые больше не присваивает: 17.09 из кода удалили анатомические кости четырёх видов, а в
        // ассетах они остались (73/130/130/21) и продолжали рисоваться поверх мест. Заметили не сразу: ряд на
        // полигоне показал «чистые» габариты, потому что оболочки как раз пропали из мёртвого кэша
        if (!System.IO.File.Exists(path))
        {
            species.bones = new Bone[0];
            species.skeletonHides = new string[0];
            return false;
        }

        Delivery d;
        try { d = JsonUtility.FromJson<Delivery>(System.IO.File.ReadAllText(path)); }
        catch (System.Exception e) { Debug.LogError($"[форма] {species.speciesName}: поставка «{path}» не разбирается — {e.Message}"); return false; }

        if (d == null || d.nodes == null || d.nodes.Length == 0)
        {
            Debug.LogError($"[форма] {species.speciesName}: в поставке «{path}» нет узлов — форма не подменена");
            return false;
        }
        if (!string.IsNullOrEmpty(d.species) && d.species != species.speciesName)
        {
            Debug.LogError($"[форма] поставка «{path}» назвала вид «{d.species}», а лежит у «{species.speciesName}» — не применяю");
            return false;
        }

        species.bones = d.nodes;
        species.skeletonHides = d.hides ?? new string[0];
        Debug.Log($"[форма] {species.speciesName}: принят силуэтный граф из поставки — узлов {d.nodes.Length}, скрытых мест {species.skeletonHides.Length}");
        return true;
    }

    /// <summary>Имя файла латиницей: поставки ходят через git, кириллица в путях мешает.</summary>
    public static string Translit(string s)
    {
        const string ru = "абвгдеёжзийклмнопрстуфхцчшщъыьэюя";
        string[] en = { "a","b","v","g","d","e","e","zh","z","i","y","k","l","m","n","o","p","r","s","t","u","f","h","c","ch","sh","sch","","y","","e","yu","ya" };
        var sb = new StringBuilder();
        foreach (var ch in s.ToLower())
        {
            int i = ru.IndexOf(ch);
            sb.Append(i >= 0 ? en[i] : (char.IsLetterOrDigit(ch) ? ch.ToString() : "-"));
        }
        return sb.ToString();
    }
}
