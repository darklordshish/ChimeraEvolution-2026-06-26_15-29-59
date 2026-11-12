using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// B0.1: флаги лаборатории-комплекса в двух лесных сценах (сцена — зона unity-integrator,
/// код мира не трогаем).
///   ForestFragment (B0)  — buildLabComplex=true, labStyle=Bastion, minLabDistance=150.
///   ForestSandbox  (A/C) — buildLabComplex=false (старый лабиринт DomeFacilityLayout),
///                          labStyle=Bastion, minLabDistance=150.
/// Каждое поле пишется ЧЕРЕЗ SerializedObject: отсутствующее поле — ошибка, а не молчание.
/// Остальные поля сцены не трогаются, после сохранения читаются заново с диска.
/// </summary>
public static class B01Scene
{
    const string SandboxPath = "Assets/Scenes/ForestSandbox.unity";
    const string FragmentPath = "Assets/Scenes/ForestFragment.unity";

    public static string Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== ForestFragment (B0: комплекс включён) ===");
        sb.Append(WriteScene(FragmentPath, expectLabComplex: true));
        sb.AppendLine("=== ForestSandbox (A/C: комплекс выключен, старый лабиринт) ===");
        sb.Append(WriteScene(SandboxPath, expectLabComplex: false));
        return sb.ToString();
    }

    public static string Verify()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== ForestFragment from DISK ===");
        sb.Append(VerifyScene(FragmentPath, expectLabComplex: true));
        sb.AppendLine("=== ForestSandbox from DISK ===");
        sb.Append(VerifyScene(SandboxPath, expectLabComplex: false));
        return sb.ToString();
    }

    // ---------- запись -------------------------------------------------------------
    static string WriteScene(string path, bool expectLabComplex)
    {
        var sb = new StringBuilder();
        if (!OpenClean(path, sb)) return sb.ToString();
        var scene = SceneManager.GetActiveScene();

        var fb = FindBuilder(scene);
        if (fb == null) return sb.ToString() + "FAILED: ForestBuilder missing in " + path;

        var so = new SerializedObject(fb);
        SetBool(so, "buildLabComplex", expectLabComplex);
        SetEnum(so, "labStyle", (int)LabStyle.Bastion);
        SetFloat(so, "minLabDistance", 150f);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("saved=" + EditorSceneManager.SaveScene(scene));

        // Читаем ФАЙЛ: открываем заново с диска и печатаем то, что реально сохранено.
        sb.AppendLine("readback from ASSET:");
        sb.Append(VerifyScene(path, expectLabComplex));
        return sb.ToString();
    }

    // ---------- проверка -----------------------------------------------------------
    static string VerifyScene(string path, bool expectLabComplex)
    {
        var sb = new StringBuilder();
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        sb.AppendLine("dirty=" + scene.isDirty);
        var fb = FindBuilder(scene);
        if (fb == null) return sb.ToString() + "FAILED: ForestBuilder missing in " + path;

        var so = new SerializedObject(fb);
        sb.AppendLine("  seed=" + Prop(so, "seed").longValue +
                      " randomSeed=" + Prop(so, "randomSeed").boolValue +
                      " mapDiameter=" + Prop(so, "mapDiameter").floatValue +
                      " chunkSize=" + Prop(so, "chunkSize").floatValue +
                      " terrainQuads=" + Prop(so, "terrainQuads").intValue +
                      " buildOnAwake=" + Prop(so, "buildOnAwake").boolValue +
                      " spawnAtEntry=" + Prop(so, "spawnAtEntry").boolValue +
                      " buildAsh=" + Prop(so, "buildAsh").boolValue);
        sb.AppendLine("  buildLabComplex=" + Prop(so, "buildLabComplex").boolValue +
                      " labStyle=" + Prop(so, "labStyle").enumValueIndex +
                      " (" + Prop(so, "labStyle").enumNames[
                          Mathf.Clamp(Prop(so, "labStyle").enumValueIndex, 0,
                              Prop(so, "labStyle").enumNames.Length - 1)] + ")" +
                      " minLabDistance=" + Prop(so, "minLabDistance").floatValue +
                      " pool=" + Prop(so, "speciesPool").arraySize);

        // A/C обязан остаться на старом лабиринте: false разрешает DomeFacilityLayout.Build.
        bool complex = Prop(so, "buildLabComplex").boolValue;
        int style = Prop(so, "labStyle").enumValueIndex;
        float minDist = Prop(so, "minLabDistance").floatValue;
        sb.AppendLine("  ASSERT buildLabComplex=" + (complex == expectLabComplex ? "ok" : "MISMATCH want " + expectLabComplex) +
                      " labStyle=" + (style == (int)LabStyle.Bastion ? "ok(Bastion)" : "MISMATCH want 0") +
                      " minLabDistance=" + (Mathf.Approximately(minDist, 150f) ? "ok(150)" : "MISMATCH want 150"));

        if (path == FragmentPath)
            sb.AppendLine("  KEEP Ø400=" + (Mathf.Approximately(Prop(so, "mapDiameter").floatValue, 400f) ? "ok" : "CHANGED") +
                          " chunk100=" + (Mathf.Approximately(Prop(so, "chunkSize").floatValue, 100f) ? "ok" : "CHANGED") +
                          " quads24=" + (Prop(so, "terrainQuads").intValue == 24 ? "ok" : "CHANGED") +
                          " seed4=" + (Prop(so, "seed").longValue == 4L ? "ok" : "CHANGED") +
                          " ashOff=" + (!Prop(so, "buildAsh").boolValue ? "ok" : "CHANGED") +
                          " entryOn=" + (Prop(so, "spawnAtEntry").boolValue ? "ok" : "CHANGED") +
                          " poolEmpty=" + (Prop(so, "speciesPool").arraySize == 0 ? "ok" : "CHANGED"));
        else
            sb.AppendLine("  KEEP Ø2000=" + (Mathf.Approximately(Prop(so, "mapDiameter").floatValue, 2000f) ? "ok" : "CHANGED") +
                          " chunk250=" + (Mathf.Approximately(Prop(so, "chunkSize").floatValue, 250f) ? "ok" : "CHANGED") +
                          " quads16=" + (Prop(so, "terrainQuads").intValue == 16 ? "ok" : "CHANGED") +
                          " seed1337=" + (Prop(so, "seed").longValue == 1337L ? "ok" : "CHANGED") +
                          " ashOn=" + (Prop(so, "buildAsh").boolValue ? "ok" : "CHANGED") +
                          " entryOff=" + (!Prop(so, "spawnAtEntry").boolValue ? "ok" : "CHANGED") +
                          " pool=" + Prop(so, "speciesPool").arraySize);
        return sb.ToString();
    }

    // ---------- helpers ------------------------------------------------------------
    static ForestBuilder FindBuilder(Scene scene)
    {
        var forest = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Forest");
        return forest != null ? forest.GetComponent<ForestBuilder>() : null;
    }

    static bool OpenClean(string path, StringBuilder sb)
    {
        // Чужую несохранённую работу не теряем: грязная сцена — стоп.
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.isDirty) { sb.AppendLine("STOP: scene " + s.path + " is dirty"); return false; }
        }
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        sb.AppendLine("opened " + scene.path);
        return true;
    }

    static SerializedProperty Prop(SerializedObject so, string name)
    {
        var p = so.FindProperty(name);
        if (p == null) throw new System.Exception("no serialized field " + name);
        return p;
    }
    static void SetBool(SerializedObject so, string name, bool v) => Prop(so, name).boolValue = v;
    static void SetFloat(SerializedObject so, string name, float v) => Prop(so, name).floatValue = v;
    static void SetEnum(SerializedObject so, string name, int v) => Prop(so, name).enumValueIndex = v;
}
