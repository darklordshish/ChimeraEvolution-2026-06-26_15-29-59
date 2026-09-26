using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>ПРИЁМКА СИЛУЭТНОГО ГРАФА: экспорт образца, проверка поставки, перенос в данные вида.
///
/// ЗАЧЕМ. Форму решает модельная линия, запись — механики («числа их, запись наша», `Docs/ЗОНЫ.md`).
/// Пока переноса не было, числа перекладывались руками — а это тот самый шов, на котором линии и
/// расходятся молча. Здесь перенос сделан машиной: файл графа кладётся рядом, проверяется инвариантами
/// и пишется прямо в `SpeciesSO`. Разойтись с игрой поставка больше не может.
///
/// ФОРМАТ — НЕ НОВЫЙ. Узел графа это `Bone`: те же поля, тот же смысл. Второго описания формы в проекте
/// не заводится принципиально (спека «одна линия партии»): узел силуэта — это кость с жирными радиусами
/// и обязательным `socket`, а не отдельная сущность. Файл — JSON:
///
///   { "species": "Волк",
///     "hides":   ["хребет", "шея", "голова", "Ноги"],
///     "nodes":   [ { "name": "грудь", "parent": "", "socket": "хребет", "attach": 1.0,
///                    "length": 0.34, "dir": {"x":0,"y":0,"z":0}, "r0": 0.16, "r1": 0.18,
///                    "section": 0.72, "depth": 1.0, "blend": 0.09, "chain": 0,
///                    "mirrorX": false, "freeOrigin": false,
///                    "origin": {"x":0,"y":0.62,"z":0.10}, "layer": 0 } ] }
///
/// `hides` — места, которые с приходом графа перестают рисовать себя примитивами. Список нужен ЯВНО:
/// вывести его из `socket` узлов нельзя — челюсть принадлежит слоту «Пасть», но подавить место «Пасть»
/// значит потерять зубы. Это решение формы, и принимает его модельная линия.
///
/// КАК ЗВАТЬ (скилл `chimera-unity`, раздел «Приёмка формы»):
///   unity command run_script --file Tools/Agent/Graph.cs --entry Graph.Export --args '["Волк"]'
///   unity command run_script --file Tools/Agent/Graph.cs --entry Graph.Check  --args '["Docs/models/handoff/wolf-graph.json"]'
///   unity command run_script --file Tools/Agent/Graph.cs --entry Graph.Import --args '["Docs/models/handoff/wolf-graph.json"]'
///   unity command run_script --file Tools/Agent/Graph.cs --entry Graph.Clear  --args '["Волк"]'</summary>
public static class Graph
{
    const string DataDir = "Assets/_Chimera/Data/";
    const int SoftNodeCap = 40;     // мягкий потолок: силуэт это ~20 узлов, а не анатомия

    [System.Serializable]
    public class Delivery
    {
        public string species;
        public string[] hides;
        public Bone[] nodes;
    }

    /// <summary>Выгрузить текущие кости вида как файл поставки — образец формата для модельной линии.</summary>
    public static string Export(string species)
    {
        var sp = Load(species);
        if (sp == null) return "вида нет: " + species;

        var d = new Delivery { species = sp.speciesName, hides = sp.skeletonHides ?? new string[0], nodes = sp.bones ?? new Bone[0] };
        string path = "Docs/models/handoff/" + Translit(sp.speciesName) + "-graph-current.json";
        System.IO.Directory.CreateDirectory("Docs/models/handoff");
        System.IO.File.WriteAllText(path, JsonUtility.ToJson(d, true), new UTF8Encoding(false));
        return string.Format("выгружено {0}: узлов {1}, скрытых мест {2} → {3}", sp.speciesName, d.nodes.Length, d.hides.Length, path);
    }

    /// <summary>Проверить поставку, ничего не меняя. Печатает ВСЕ нарушения разом, а не первое.</summary>
    public static string Check(string path) => Validate(path, out _, out _);

    /// <summary>Проверить и перенести в данные вида. Ассет правится только при чистой проверке.</summary>
    public static string Import(string path)
    {
        string verdict = Validate(path, out var d, out var sp);
        if (d == null || sp == null) return verdict;
        if (verdict.Contains("НАРУШЕНИЙ")) return verdict + "\nПОСТАВКА НЕ ПРИНЯТА: данные не тронуты.";

        Undo.RecordObject(sp, "Приёмка силуэтного графа");
        sp.bones = d.nodes;
        sp.skeletonHides = d.hides ?? new string[0];
        EditorUtility.SetDirty(sp);
        AssetDatabase.SaveAssets();
        return verdict + string.Format("\nПРИНЯТО: {0} — узлов {1}, скрытых мест {2}. Дальше: chimera-map и кадр.",
                                       sp.speciesName, sp.bones.Length, sp.skeletonHides.Length);
    }

    /// <summary>ЧИСТЫЙ ЛИСТ: снять кости и скрытия — вид рисуется ТОЛЬКО местами (примитивами).
    /// Это состояние, в которое приезжает новый граф: двойной геометрии нет, видно голую базу.</summary>
    public static string Clear(string species)
    {
        var sp = Load(species);
        if (sp == null) return "вида нет: " + species;
        int was = sp.bones != null ? sp.bones.Length : 0;
        Undo.RecordObject(sp, "Чистый лист формы");
        sp.bones = new Bone[0];
        sp.skeletonHides = new string[0];
        EditorUtility.SetDirty(sp);
        AssetDatabase.SaveAssets();
        return string.Format("{0}: снято {1} костей, места рисуют себя сами", sp.speciesName, was);
    }

    // ── проверка ────────────────────────────────────────────────────────────────────────

    /// <summary>ИНВАРИАНТЫ ПОСТАВКИ. Каждый из них ломается МОЛЧА, поэтому проверяются машиной:
    /// без них поставка «выглядит принятой», а зверь собирается не тем.</summary>
    static string Validate(string path, out Delivery d, out SpeciesSO sp)
    {
        d = null; sp = null;
        if (!System.IO.File.Exists(path)) return "файла нет: " + path;

        try { d = JsonUtility.FromJson<Delivery>(System.IO.File.ReadAllText(path)); }
        catch (System.Exception e) { return "не разобрать JSON: " + e.Message; }
        if (d == null) return "пустой файл поставки";

        var bad = new List<string>();
        if (string.IsNullOrEmpty(d.species)) bad.Add("не назван вид (`species`)");
        else { sp = Load(d.species); if (sp == null) bad.Add("вида «" + d.species + "» нет в Data/"); }

        var nodes = d.nodes ?? new Bone[0];
        if (nodes.Length == 0) bad.Add("узлов нет вовсе");
        if (nodes.Length > SoftNodeCap)
            bad.Add(string.Format("узлов {0} — это анатомия, а не силуэт (ориентир ~20, мягкий потолок {1})", nodes.Length, SoftNodeCap));

        var names = new HashSet<string>();
        int roots = 0, noSocket = 0, thin = 0;
        foreach (var n in nodes)
        {
            if (n == null) { bad.Add("пустой узел в списке"); continue; }
            if (string.IsNullOrEmpty(n.name)) { bad.Add("узел без имени"); continue; }
            if (!names.Add(n.name)) bad.Add("имя узла повторяется: " + n.name);
            if (string.IsNullOrEmpty(n.socket)) noSocket++;
            if (string.IsNullOrEmpty(n.parent)) roots++;
            if (n.r0 <= 0.0001f || n.r1 <= 0.0001f) thin++;
            if (n.length <= 0.0001f && string.IsNullOrEmpty(n.endBone)) bad.Add("нулевая длина у узла " + n.name);
        }
        foreach (var n in nodes)
            if (n != null && !string.IsNullOrEmpty(n.parent) && !names.Contains(n.parent))
                bad.Add("узел «" + n.name + "» ссылается на несуществующего родителя «" + n.parent + "»");

        if (roots == 0) bad.Add("нет корневого узла (с пустым `parent`)");
        if (roots > 1) bad.Add("корней " + roots + " — граф обязан быть СВЯЗНЫМ, корень один");
        if (noSocket > 0) bad.Add("узлов без `socket`: " + noSocket + " — по слоту режется меш и работает химеризация");
        if (thin > 0) bad.Add("узлов с нулевым радиусом: " + thin + " — поле не даст объёма, будут дыры");

        // места, которые граф гасит, обязаны существовать у вида
        if (sp != null && d.hides != null)
        {
            var known = new HashSet<string>();
            if (sp.sockets != null) foreach (var s in sp.sockets) if (s != null && !string.IsNullOrEmpty(s.name)) known.Add(s.name);
            foreach (var h in d.hides)
                if (!string.IsNullOrEmpty(h) && !known.Contains(h)) bad.Add("гасится место «" + h + "», которого у вида нет");
        }

        // ГНЁЗДА (спека 26.09, И6): раскладка гнёзд проверяется ПРОТИВ ЭТОГО ЖЕ графа — хозяин гнезда обязан быть его
        // узлом, у каждого места вида обязано быть гнездо. Ассет не трогаем: проверка идёт на копии с новыми костями
        string placesPath = path.Replace("-graph.json", "-places-layout.json");
        string nestNote = "";
        if (sp != null && placesPath != path && System.IO.File.Exists(placesPath))
        {
            var probe = Object.Instantiate(sp);
            probe.bones = nodes;
            var nests = SpeciesHandoff.ReadNests(probe, System.IO.File.ReadAllText(placesPath), out var problems);
            Object.DestroyImmediate(probe);
            foreach (var p in problems) bad.Add("гнёзда: " + p);
            nestNote = string.Format(", гнёзд {0}", nests.Length);
        }
        else if (sp != null) nestNote = ", раскладки гнёзд нет (тотальность не проверена)";

        var sb = new StringBuilder();
        sb.AppendFormat("поставка «{0}»: узлов {1}, скрытых мест {2}{3}",
                        d.species, nodes.Length, d.hides != null ? d.hides.Length : 0, nestNote);
        if (bad.Count == 0) { sb.Append("\nЧИСТО: инварианты держатся."); return sb.ToString(); }
        sb.Append("\nНАРУШЕНИЙ: ").Append(bad.Count);
        foreach (var b in bad) sb.Append("\n  • ").Append(b);
        return sb.ToString();
    }

    static SpeciesSO Load(string species)
    {
        var sp = AssetDatabase.LoadAssetAtPath<SpeciesSO>(DataDir + species + ".asset");
        if (sp != null) return sp;
        foreach (var guid in AssetDatabase.FindAssets("t:SpeciesSO"))
        {
            var s = AssetDatabase.LoadAssetAtPath<SpeciesSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (s != null && s.speciesName == species) return s;
        }
        return null;
    }

    /// <summary>Имя файла латиницей: поставки ходят через git и почту, кириллица в путях мешает.</summary>
    static string Translit(string s)
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
