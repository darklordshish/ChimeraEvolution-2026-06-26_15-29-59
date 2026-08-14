using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>СХЕМЫ ТЕЛ: карта сокет-плана каждого вида — кто к кому крепится, где цепи, где графты.
/// ВЫГРУЖАЕТСЯ ИЗ ДАННЫХ (готовых `SpeciesSO`), а не рисуется руками: нарисованная схема врёт уже через
/// неделю правок, выгруженная — не может. Заодно работает третьим верификатором рядом с `ValidateSockets`
/// и численной проверкой стыков: висячий родитель, цикл и осиротевший слот видны ГЛАЗОМ, без запуска игры.
/// Читаем именно ассеты, а не `SpeciesBootstrap.cs`: в игре живут они, и если бутстрап не прогнан, схема
/// честно покажет старое состояние — это признак, а не помеха (см. правило про гейт бутстрапа).</summary>
public static class BodyDiagram
{
    const string OutDir = "Docs/Диаграммы";

    [MenuItem("Chimera/Выгрузить схемы тел")]
    public static void Export()
    {
        // ЧИСЛА — ЧЕРЕЗ ТОЧКУ. Интерполяция берёт культуру системы, и на русской локали калибры выходили
        // «0,427»: в техдоке это читается хуже и ломает копипаст в калькулятор
        var keep = System.Threading.Thread.CurrentThread.CurrentCulture;
        System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        try { Run(); }
        finally { System.Threading.Thread.CurrentThread.CurrentCulture = keep; }
    }

    static void Run()
    {
        var root = Directory.GetParent(Application.dataPath).FullName; // Assets/.. = корень репо
        var dir = Path.Combine(root, OutDir);
        Directory.CreateDirectory(dir);

        var species = AssetDatabase.FindAssets("t:SpeciesSO")
            .Select(g => AssetDatabase.LoadAssetAtPath<SpeciesSO>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(s => s != null && s.sockets != null && s.sockets.Length > 0)
            .OrderBy(s => s.speciesName)
            .ToList();

        if (species.Count == 0) { Debug.LogWarning("Схемы тел: не найдено ни одного SpeciesSO с сокет-планом."); return; }

        var index = new StringBuilder();
        index.AppendLine("# Схемы тел");
        index.AppendLine();
        index.AppendLine("Карта сокет-плана: кто к кому крепится, где цепи звеньев, где закрытые места.");
        index.AppendLine("**Файлы выгружаются командой `Chimera → Выгрузить схемы тел` — не править руками.**");
        index.AppendLine();
        index.AppendLine("| вид | мест | корней | цепей | графтов |");
        index.AppendLine("|---|---|---|---|---|");

        foreach (var sp in species)
        {
            var live = sp.sockets.Where(s => s != null && !string.IsNullOrEmpty(s.name)).ToList();
            File.WriteAllText(Path.Combine(dir, sp.speciesName + ".md"), Build(sp, live), new UTF8Encoding(false));
            index.AppendLine($"| [{sp.speciesName}]({sp.speciesName}.md) | {live.Count} " +
                             $"| {live.Count(s => string.IsNullOrEmpty(s.parent))} " +
                             $"| {live.Count(s => s.chain > 1)} | {live.Count(s => s.graft)} |");
        }

        File.WriteAllText(Path.Combine(dir, "README.md"), index.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();
        Debug.Log($"Схемы тел: выгружено {species.Count} шт. → {OutDir}/");
    }

    static string Build(SpeciesSO sp, List<BodySocket> live)
    {
        var byName = live.ToDictionary(s => s.name, s => s);
        var organs = new Dictionary<string, string>();
        if (sp.organs != null)
            foreach (var o in sp.organs)
                if (o != null && !string.IsNullOrEmpty(o.slot) && !organs.ContainsKey(o.slot))
                    organs[o.slot] = o.organName;

        var id = new Dictionary<string, string>();
        for (int i = 0; i < live.Count; i++) id[live[i].name] = "n" + i;

        var t = new StringBuilder();
        t.AppendLine($"# {sp.speciesName} — план тела");
        t.AppendLine();
        t.AppendLine("<!-- ВЫГРУЖЕНО из SpeciesSO командой «Chimera → Выгрузить схемы тел». Руками не править. -->");
        t.AppendLine();
        // ДЕРЕВО ОТСТУПАМИ — идёт ПЕРВЫМ и намеренно: mermaid ниже рисуется только там, где его умеют
        // (GitHub, VS Code с расширением, Obsidian), а в обычном просмотрщике остаётся простынёй текста.
        // Это же дерево читается везде и никогда не «не отрисуется»
        t.AppendLine("```text");
        foreach (var r in live.Where(s => string.IsNullOrEmpty(s.parent)))
            Tree(t, r, live, byName, "", true);
        var lost = live.Where(s => !string.IsNullOrEmpty(s.parent) && !byName.ContainsKey(s.parent)).ToList();
        foreach (var s in lost)
            t.AppendLine($"(!) {s.name} — родитель «{s.parent}» НЕ НАЙДЕН, место встанет в корень");
        t.AppendLine("```");
        t.AppendLine();

        t.AppendLine("```mermaid");
        t.AppendLine("flowchart TD");

        foreach (var s in live)
        {
            var sz = MorphBuilder.SizeOf(s, byName);   // РАЗРЕШЁННЫЙ калибр: свой габарит, доля родителя или звено цепи
            var size = $"{sz.x:0.##}×{sz.y:0.##}×{sz.z:0.##}";
            var label = s.name + "<br/><small>" + size;
            if (s.chain > 1) label += $" ×{s.chain} звен.";
            if (s.mirrorX) label += " · пара";
            label += "</small>";

            // форма узла = роль места: скрытое — овал, графт — гексагон, цепь — двойная рамка
            string node = s.inner ? $"{id[s.name]}([\"{label}\"])"
                        : s.graft ? $"{id[s.name]}{{{{\"{label}\"}}}}"
                        : s.chain > 1 ? $"{id[s.name]}[[\"{label}\"]]"
                        : $"{id[s.name]}[\"{label}\"]";
            t.AppendLine("    " + node);
        }

        t.AppendLine();
        foreach (var s in live)
        {
            if (string.IsNullOrEmpty(s.parent)) continue;
            if (!byName.ContainsKey(s.parent))
            {
                t.AppendLine($"    broken_{id[s.name]}[\"ВИСЯЧИЙ РОДИТЕЛЬ: {s.parent}\"] -.-> {id[s.name]}");
                continue;
            }
            t.AppendLine($"    {id[s.parent]} -->|{s.attach:0.##}| {id[s.name]}");
        }

        t.AppendLine();
        t.AppendLine("    classDef hid fill:#eee,stroke:#999,stroke-dasharray:4 3,color:#666");
        t.AppendLine("    classDef gr fill:#fdf3e0,stroke:#c98a2b,color:#7a5310");
        t.AppendLine("    classDef ch fill:#e8f2ff,stroke:#3b74c4,color:#1c3f70");
        Cls(t, "hid", live.Where(s => s.inner), id);
        Cls(t, "gr", live.Where(s => s.graft && !s.inner), id);
        Cls(t, "ch", live.Where(s => s.chain > 1 && !s.inner && !s.graft), id);
        t.AppendLine("```");

        t.AppendLine();
        t.AppendLine("**Овал** — внутреннее место (слот есть, на теле не видно). "
                   + "**Гексагон** — закрытое: пустым не рисуется, проступает привитым органом. "
                   + "**Двойная рамка** — цепь звеньев. Цифра на стрелке — `attach`: доля вдоль длинной оси родителя.");
        t.AppendLine();
        t.AppendLine("| место | родитель | attach | калибр | своя форма | родной орган |");
        t.AppendLine("|---|---|---|---|---|---|");
        foreach (var s in live)
        {
            organs.TryGetValue(s.name, out var org);
            string role = s.inner ? " *(внутр.)*" : s.graft ? " *(графт)*" : "";
            t.AppendLine($"| **{s.name}**{role} | {(string.IsNullOrEmpty(s.parent) ? "— корень" : s.parent)} "
                       + $"| {(string.IsNullOrEmpty(s.parent) ? "—" : s.attach.ToString("0.##"))} "
                       + $"| {MorphBuilder.SizeOf(s, byName).x:0.###}×{MorphBuilder.SizeOf(s, byName).y:0.###}×{MorphBuilder.SizeOf(s, byName).z:0.###} "
                       + $"| {(s.parts != null && s.parts.Length > 0 ? s.parts.Length + " част." : "—")} "
                       + $"| {(org ?? "—")} |");
        }

        // ДЛИННАЯ ОСЬ — та самая мина: билдер выбирает её по максимальной стороне, и правка размера
        // может молча развернуть всю ветку детей. Показываем запас, чтобы риск было видно заранее
        t.AppendLine();
        t.AppendLine("### Запас длинной оси");
        t.AppendLine();
        t.AppendLine("Ось выбирается по максимальной стороне РАЗРЕШЁННОГО размера (свой габарит либо доля родителя). Мал запас — правка размера "
                   + "переключит ось и развернёт ветку детей (ловится только глазом).");
        t.AppendLine();
        t.AppendLine("| место с детьми | ось | запас |");
        t.AppendLine("|---|---|---|");
        var hasKids = new HashSet<string>(live.Where(s => !string.IsNullOrEmpty(s.parent)).Select(s => s.parent));
        foreach (var s in live.Where(s => hasKids.Contains(s.name)))
        {
            var b = MorphBuilder.SizeOf(s, byName);   // тот же размер, по которому ось выбирает сборка
            int ax = b.z >= b.x && b.z >= b.y ? 2 : (b.y >= b.x ? 1 : 0);
            float[] v = { b.x, b.y, b.z };
            System.Array.Sort(v); System.Array.Reverse(v);
            float slack = v[0] > 0f ? (v[0] - v[1]) / v[0] : 0f;
            t.AppendLine($"| {s.name} | {"XYZ"[ax]} | {slack * 100f:0.#}%{(slack < 0.05f ? " ⚠️ хрупко" : "")} |");
        }
        return t.ToString();
    }

    /// <summary>Ветка дерева отступами. `last` — последний ребёнок у родителя (рисуем угол, а не тройник).</summary>
    static void Tree(StringBuilder t, BodySocket s, List<BodySocket> live, Dictionary<string, BodySocket> byName, string pad, bool last, int depth = 0)
    {
        if (depth > 16) { t.AppendLine(pad + "└─ (!) ЦИКЛ В ГРАФЕ — обход оборван"); return; } // Unity не должна виснуть на кривых данных
        string mark = s.inner ? " (внутр.)" : s.graft ? " (графт)" : "";
        if (s.chain > 1) mark += $" ×{s.chain} звен.";
        if (s.mirrorX) mark += " (пара)";
        string at = string.IsNullOrEmpty(s.parent) ? "" : $"  attach {s.attach:0.##}";
        // ветка рисуется по ГЛУБИНЕ, а не по длине отступа: у детей корня отступ пустой, и по нему
        // дерево выходило плоским списком
        t.AppendLine($"{pad}{(depth == 0 ? "" : last ? "└─ " : "├─ ")}{s.name}{mark}{at}"
                   + $"   [{MorphBuilder.SizeOf(s, byName).x:0.###}×{MorphBuilder.SizeOf(s, byName).y:0.###}×{MorphBuilder.SizeOf(s, byName).z:0.###}]");

        var kids = live.Where(k => k.parent == s.name).ToList();
        string childPad = depth == 0 ? "" : pad + (last ? "   " : "│  ");
        for (int i = 0; i < kids.Count; i++)
            Tree(t, kids[i], live, byName, childPad, i == kids.Count - 1, depth + 1);
    }

    static void Cls(StringBuilder t, string cls, IEnumerable<BodySocket> set, Dictionary<string, string> id)
    {
        var list = set.Select(s => id[s.name]).ToList();
        if (list.Count > 0) t.AppendLine($"    class {string.Join(",", list)} {cls}");
    }
}
