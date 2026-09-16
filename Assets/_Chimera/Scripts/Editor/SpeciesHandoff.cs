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
    // Формат задан поставкой 3 модельной линии (`volk-head-layout.json`, `volk-legs-layout.json`). Числа
    // массивами, а не объектами Vector3 — так их пишет генератор на Python; лишние поля («metres», «note») —
    // пояснения для людей, разбор их пропускает

    [System.Serializable] class Calibre { public float[] baseSize; public float[] sizeRel; }
    [System.Serializable] class PlaceDto { public string name; public float attach; public float[] attachOffset, sizeRel, baseEuler; }
    [System.Serializable] class PartDto { public string node, block, role; public float[] offset, scale, euler, color; }
    [System.Serializable] class HeadLayout { public Calibre head; public PlaceDto[] places; public PartDto muzzle; public PartDto[] teeth, senses; }
    [System.Serializable] class LegDto { public string slot, organ; public PartDto[] parts; }
    [System.Serializable] class LegsLayout { public LegDto[] legs; }

    /// <summary>Применить поставку вида целиком: граф, раскладку головы, раскладку ног. Каждый файл
    /// независим — нет раскладки, значит те места и органы остаются, какими их задал бутстрап.</summary>
    public static bool Apply(SpeciesSO species)
    {
        if (species == null) return false;
        bool graph = ApplyGraph(species);

        string stem = Dir + Translit(species.speciesName);
        string head = System.IO.File.Exists(stem + "-head-layout.json") ? System.IO.File.ReadAllText(stem + "-head-layout.json") : null;
        string legs = System.IO.File.Exists(stem + "-legs-layout.json") ? System.IO.File.ReadAllText(stem + "-legs-layout.json") : null;
        if (head != null || legs != null)
        {
            int n = ApplyLayouts(species, head, legs);
            Debug.Log($"[форма] {species.speciesName}: приняты раскладки из поставки — изменений {n}");
        }
        return graph;
    }

    /// <summary>РАСКЛАДКИ ЧИТАЮТСЯ ИЗ ФАЙЛОВ, А НЕ ПЕРЕПИСЫВАЮТСЯ В КОД. Модельная линия предложила внести числа
    /// в данные вида руками («числа наши, запись ваша»). Но тогда у формы снова два источника: генератор
    /// раскладки у них и копия чисел у нас, и первая же регенерация разойдётся с кодом молча — ровно та беда,
    /// из-за которой граф уехал в файл. Промах имени (места или органа нет у вида) не проглатывается: в консоль.
    /// Возвращает число применённых изменений; открыт для теста, чтобы не зависеть от файлов на диске.</summary>
    public static int ApplyLayouts(SpeciesSO species, string headJson, string legsJson)
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

        if (!string.IsNullOrEmpty(legsJson))
        {
            var l = JsonUtility.FromJson<LegsLayout>(legsJson);
            foreach (var leg in l.legs ?? new LegDto[0])
            {
                var organ = FindOrgan(species, leg.slot, leg.organ);
                if (organ == null) { Debug.LogError($"[форма] {species.speciesName}: раскладка ног называет орган «{leg.organ}» на слоте «{leg.slot}», которого у вида нет"); continue; }
                var parts = new System.Collections.Generic.List<OrganPart>();
                foreach (var p in leg.parts ?? new PartDto[0]) parts.Add(Part(p));
                organ.visualParts = parts.ToArray();   // целиком: прежние колонны и мышцы на погашенном месте не нужны
                n++;
            }
        }
        return n;
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
