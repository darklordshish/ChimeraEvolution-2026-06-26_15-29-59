using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>СНЯТЬ ПОЗУ МОРФА: пишет фактические трансформы всех деталей выделенного существа в файл.
///
/// Зачем. Правка «на глаз» в сцене часто точнее расчёта — глаз видит сопряжения, которых нет в числах.
/// Но морф-детали рождаются в РАНТАЙМЕ, и всё, что подкручено в Play, исчезает при выходе. Этот снимок
/// переводит ручную работу обратно в цифры: снял позу, и её можно занести в данные вида осознанно.
///
/// Работает и в Play, и вне его. В Play снимать ОБЯЗАТЕЛЬНО до выхода — иначе правки уже потеряны.</summary>
public static class MorphDump
{
    const string OutPath = "Docs/Диаграммы/ПОЗА_МОРФА.md";

    [MenuItem("Chimera/Снять позу морфа (выделенное)")]
    public static void Dump()
    {
        var go = Selection.activeGameObject;
        if (go == null) { Debug.LogWarning("Снять позу: сначала выдели существо в иерархии (или его контейнер Morph)."); return; }

        // ищем контейнер: сам объект, его потомок «Morph» либо родитель, если выделена деталь
        var root = go.transform;
        var morph = root.name == "Morph" ? root : root.Find("Morph");
        if (morph == null)
        {
            var t = root;
            while (t != null && t.name != "Morph") t = t.parent;
            morph = t;
        }
        if (morph == null) { Debug.LogWarning($"Снять позу: у «{go.name}» нет контейнера Morph — выдели существо целиком."); return; }

        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("# ПОЗА МОРФА (снимок из сцены)");
        sb.AppendLine();
        sb.AppendLine($"Существо: **{morph.parent?.name ?? "—"}**, снято {System.DateTime.Now:dd.MM HH:mm}, " +
                      (Application.isPlaying ? "в Play (ручные правки живы)" : "вне Play"));
        sb.AppendLine();
        sb.AppendLine("Координаты — ЛОКАЛЬНЫЕ относительно контейнера `Morph`, то есть в той же системе,");
        sb.AppendLine("в какой билдер расставляет детали. Масштаб — итоговый габарит детали в метрах.");
        sb.AppendLine();
        sb.AppendLine("| деталь | позиция x,y,z | поворот | габарит |");
        sb.AppendLine("|---|---|---|---|");

        int n = 0;
        foreach (var t in morph.GetComponentsInChildren<Transform>())
        {
            if (t == morph) continue;
            if (!t.TryGetComponent<Renderer>(out var r)) continue;   // узлы-пустышки пропускаем
            n++;
            var p = morph.InverseTransformPoint(t.position);
            var e = (Quaternion.Inverse(morph.rotation) * t.rotation).eulerAngles;
            var s = r.bounds.size;
            sb.AppendLine(string.Format(ci, "| {0} | {1:F3}, {2:F3}, {3:F3} | {4:F0}, {5:F0}, {6:F0} | {7:F3} × {8:F3} × {9:F3} |",
                          t.name, p.x, p.y, p.z, e.x, e.y, e.z, s.x, s.y, s.z));
        }

        System.IO.File.WriteAllText(OutPath, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();
        Debug.Log($"Поза морфа снята: {OutPath} (деталей: {n})");
    }
}
