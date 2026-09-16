using System.Text;
using UnityEngine;

/// <summary>ПОСТАВКА ФОРМЫ: силуэтный граф вида приходит ФАЙЛОМ от модельной линии и подменяет кости,
/// записанные в `SpeciesBootstrap`.
///
/// ЗАЧЕМ ИМЕННО ТАК. Форму решает модельная линия, запись держат механики («числа их, запись наша»,
/// `Docs/ЗОНЫ.md`). Пока граф лежал бы в нашем C#-коде, у одной формы было бы ДВА источника: файл
/// поставки и копия в бутстрапе. Разошлись бы они молча и в худший момент — приняли граф в ассет, а
/// следующее «Создать дефолтные виды» вернуло старые кости, и никакой ошибки при этом не возникает.
/// Проект такую цену уже платил (клетка против спеки идентичности — три недели), поэтому здесь
/// источник один: файл. Нет файла — остаются кости из бутстрапа, это переходное состояние.
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

    /// <summary>Подменить кости и скрытия вида, если рядом лежит его поставка. Битую поставку НЕ
    /// применяем молча: кричим в консоль и оставляем прежнее — тихая подмена хуже отсутствия файла.</summary>
    public static bool Apply(SpeciesSO species)
    {
        if (species == null) return false;
        string path = Dir + Translit(species.speciesName) + "-graph.json";
        if (!System.IO.File.Exists(path)) return false;

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
