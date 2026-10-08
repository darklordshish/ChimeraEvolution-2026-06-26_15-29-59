using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>ТЕЛА НА ПЛАНАХ — инструмент к спеке `2026-10-08-vid-na-chuzhom-plane.md`. Модельная линия подбирает «волка на
/// двуногом» в долях масштаба цепи; удобнее всего начинать с чисел самого шасси, а здесь их можно выгрузить.
///
///   unity command run_script --file Tools/Agent/PlanBodies.cs --entry PlanBodies.Export \
///     --args '["Человек","Волк","Docs/models/handoff/volk-na-dvunogom.json"]'
/// — числа Человека на его плане (двуногий), записанные как тело Волка: заготовка, которую правят по листу оборотня.
/// Последний аргумент пуст — только вернуть текст. Файл в `handoff/` импорт ПРИМЕТ как поставку: класть туда только то,
/// что готовы назвать телом вида, а заготовку держать вне `handoff/`.
///
///   unity command run_script --file Tools/Agent/PlanBodies.cs --entry PlanBodies.Show --args '["Волк","двуногий"]'
/// — тело вида на плане, как его видит глобальный слой (свой план — из графа, чужой — из поставки).</summary>
public static class PlanBodies
{
    static SpeciesSO Load(string n) => AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{n}.asset");

    public static string Export(string from, string asSpecies, string path)
    {
        var sp = Load(from);
        if (sp == null) return $"вида «{from}» нет";
        var body = GlobalLayer.BodyOn(sp, sp.Plan);
        var dto = new PlanBody { species = string.IsNullOrEmpty(asSpecies) ? from : asSpecies, plan = sp.Plan, groups = body.Values.Select(Round).ToArray() };
        var json = JsonUtility.ToJson(dto, true);
        if (!string.IsNullOrEmpty(path)) System.IO.File.WriteAllText(path, json);
        return string.IsNullOrEmpty(path) ? json : $"{path}: {dto.groups.Length} групп плана «{dto.plan}» (числа {from})";
    }

    public static string Show(string species, string plan)
    {
        var sp = Load(species);
        if (sp == null) return $"вида «{species}» нет";
        var body = GlobalLayer.BodyOn(sp, plan);
        if (body == null) return $"{species} на плане «{plan}»: тела нет (поставки нет)";
        return $"{species} на плане «{plan}» ({(sp.Plan == plan ? "из графа" : "поставка")}):\n" + string.Join("\n", body.Values.Select(n =>
            $"{n.name,-15} u={n.u:0.000} len={n.len:0.000} r={n.r0:0.000}/{n.r1:0.000} off=({n.x:0.000},{n.z:0.000}) сеч={n.section:0.00}/{n.depth:0.00}"));
    }

    static GroupNumbers Round(GroupNumbers n)
    {
        float R(float v) => Mathf.Round(v * 10000f) / 10000f;
        return new GroupNumbers { name = n.name, u = R(n.u), len = R(n.len), r0 = R(n.r0), r1 = R(n.r1), x = R(n.x), z = R(n.z), section = R(n.section), depth = R(n.depth) };
    }
}
