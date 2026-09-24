using System.Linq;
using UnityEditor;
using UnityEngine;

/// СТЕНД КАДРА ДЛЯ РЕЦЕНЗИИ (модельная линия, код агента — вне `Assets/`, пайплайн компилирует его в памяти).
/// Граф и раскладки из ФАЙЛОВ применяются к копии ассета вида игровым путём (`SpeciesHandoff.ApplyLayouts`) и собираются
/// настоящим `MorphBuilder`: черновик генератора снимается так, как его соберёт игра, до поставки. Камера ортографическая с
/// ЗАДАННОЙ рамкой (центр и полувысота в метрах) — кадр ложится на лист по метрам, кадры разных дней сравнимы.
///   unity command --project-path <папка> run_script --file Tools/Blender/kritik/Kadr.cs --entry Kadr.Shot \
///     --args '["Человек","граф.json","голова.json","органы.json",0.042,"nose",0,0.93,0.97]'
///   unity command --project-path <папка> capture_game_view --camera "СтендCam" --width 1000 --height 1000 --save_path "Кадры/k.png"
/// Копия вида получает своё имя: `BoneMesher` кэширует оболочку по имени вида, и без этого стенд снимал бы прошлый граф.
public static class Kadr
{
    const string Rig = "~СТЕНД";
    [System.Serializable] class Delivery { public string species; public string[] hides; public Bone[] nodes; }
    static readonly System.Globalization.CultureInfo CI = System.Globalization.CultureInfo.InvariantCulture;

    static SpeciesSO Make(string species, string graph, string head, string organs, float cell)
    {
        var src = AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/" + species + ".asset");
        var c = Object.Instantiate(src);
        c.speciesName = species + " ~кадр " + System.DateTime.Now.Ticks;
        if (!string.IsNullOrEmpty(graph))
        {
            var d = JsonUtility.FromJson<Delivery>(System.IO.File.ReadAllText(graph));
            c.bones = d.nodes; c.skeletonHides = d.hides;
        }
        SpeciesHandoff.ApplyLayouts(c,
            string.IsNullOrEmpty(head) ? null : System.IO.File.ReadAllText(head),
            string.IsNullOrEmpty(organs) ? null : System.IO.File.ReadAllText(organs));
        if (cell > 0) c.skinCell = cell;
        return c;
    }

    static string Stats(GameObject body)
    {
        int tris = 0, field = 0, prims = 0;
        foreach (var r in body.GetComponentsInChildren<Renderer>())
        {
            var m = r is SkinnedMeshRenderer smr ? smr.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
            if (m == null) continue;
            int t = (int)(m.GetIndexCount(0) / 3);
            tris += t;
            if (r is SkinnedMeshRenderer) field += t;
            else if (m.name == "Sphere" || m.name == "Capsule" || m.name == "Cube" || m.name == "Cylinder") prims++;
        }
        var miss = MorphBuilder.MissingBlocks.Distinct().ToList();
        return string.Format(CI, "{0} тр (поле {1}), примитивов {2}{3}", tris, field, prims,
            miss.Count > 0 ? ", НЕТ БЛОКОВ: " + string.Join(",", miss) : "");
    }

    /// view: nose (анфас, камера с +Z; справа в кадре −X) | profile (с +X; справа +Z) | back (со спины) | top (сверху) |
    /// q, qback (3/4 спереди и сзади, камера сверху-справа) | yaw:<градусы вокруг Y, 0 = анфас, + = камера справа>:<наклон вниз>.
    /// cx — центр кадра по горизонтали (X для nose/back, Z для profile/top/q/qback/yaw), cy — по вертикали, half — полувысота, м
    public static string Shot(string species, string graph, string head, string organs, float cell, string view, float cx, float cy, float half)
    {
        Wipe();
        var root = new GameObject(Rig);
        var so = Make(species, graph, head, organs, cell);
        var body = new GameObject("тело");
        body.transform.SetParent(root.transform, false);
        var cc = body.AddComponent<CharacterController>(); cc.height = 2f; cc.center = new Vector3(0, 1, 0);
        MorphBuilder.Build(body.transform, so, so.organs.Where(o => o != null).ToList());
        var s = Stats(body);

        var cam = new GameObject("СтендCam").AddComponent<Camera>();
        cam.transform.SetParent(root.transform, false);
        Vector3 center, eye, up = Vector3.up;
        if (view == "nose") { center = new Vector3(cx, cy, 0); eye = Vector3.forward; }
        else if (view == "back") { center = new Vector3(cx, cy, 0); eye = Vector3.back; }
        else if (view == "top") { center = new Vector3(-cy, 0, cx); eye = Vector3.up; up = Vector3.right; }
        else if (view == "q") { center = new Vector3(0, cy, cx); eye = new Vector3(1f, 0.45f, 0.9f).normalized; }
        else if (view == "qback") { center = new Vector3(0, cy, cx); eye = new Vector3(0.9f, 0.35f, -1f).normalized; }
        else if (view.StartsWith("yaw:"))
        {
            var p = view.Split(':');
            float yw = float.Parse(p[1], CI) * Mathf.Deg2Rad, pt = (p.Length > 2 ? float.Parse(p[2], CI) : 0f) * Mathf.Deg2Rad;
            center = new Vector3(0, cy, cx);     // cx — середина тела по Z: у зверя она не на оси, морда ушла бы за рамку
            eye = new Vector3(Mathf.Sin(yw) * Mathf.Cos(pt), Mathf.Sin(pt), Mathf.Cos(yw) * Mathf.Cos(pt));
        }
        else { center = new Vector3(0, cy, cx); eye = Vector3.right; }
        cam.transform.position = center + eye * 20f;
        cam.transform.rotation = Quaternion.LookRotation(-eye, up);
        cam.orthographic = true; cam.orthographicSize = half;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.16f, 0.17f, 0.19f, 1f);
        cam.nearClipPlane = 0.01f; cam.farClipPlane = 60f;
        // КЛЮЧЕВОЙ СВЕТ ИЗ-ЗА ЛЕВОГО ПЛЕЧА КАМЕРЫ, сверху — как на листах-референсах; ровный фон без теней от окружения.
        // Лампа за спиной камеры гасит плоскости граней, и рецензент видит силуэт, а не форму
        var lamp = new GameObject("Свет").AddComponent<Light>();
        lamp.transform.SetParent(root.transform, false); lamp.type = LightType.Directional; lamp.intensity = 1.1f;
        var camRight = Vector3.Cross(up, eye).sqrMagnitude > 1e-4f ? Vector3.Cross(up, eye).normalized : Vector3.right;
        lamp.transform.rotation = Quaternion.LookRotation((-eye - up * 0.75f + camRight * 0.45f).normalized, up);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.36f, 0.37f, 0.40f);
        return s;
    }

    public static string Wipe()
    {
        int n = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            if (go != null && go.name == Rig) { Object.DestroyImmediate(go); n++; }
        return "снесено: " + n;
    }
}
