using System.Linq;
using UnityEditor;
using UnityEngine;

/// КАДР ЧИТАЕМОСТИ (модельная линия, код агента — вне `Assets/`, пайплайн компилирует его в памяти).
/// Мерка формы из GDD §9: «low-poly первичен по силуэту — узнаёшь человек+волк по контуру». Пять видов из ФАЙЛОВ
/// поставки (`Docs/models/handoff/<вид>-*.json`, как `Kadr.cs`) ставятся на дистанцию D впереди игрока и снимаются
/// КАМЕРОЙ ИГРОКА: `CameraFollow` — 6 м над игроком и 7 м позади, взгляд на 1.2 м, поле зрения 60°. Тела — чёрные
/// без света, фон — дневной туман леса (0.78, 0.82, 0.84). Туман купола на этих дистанциях почти ничего не гасит
/// (Exp2 0.0009–0.003: на 37 м от камеры ~1 %), поэтому его нет: читаемость решают размер на экране и контур.
///   pose: side — идёт мимо (боком), head — бежит на игрока (мордой к камере).
///   unity command run_script --file Tools/Blender/kritik/Chitaemost.cs --entry Chitaemost.Row --args '[20,"side"]'
///   unity command capture_game_view --camera ЧитаемостьCam --width 1920 --height 1080 --save_path Кадры/c-20-side.png
public static class Chitaemost
{
    const string Rig = "~ЧИТАЕМОСТЬ";
    [System.Serializable] class Delivery { public string species; public string[] hides; public Bone[] nodes; }
    static readonly (string name, string file)[] Species =
        { ("Человек", "chelovek"), ("Волк", "volk"), ("Лось", "los"), ("Ёж", "ezh"), ("Змея", "zmeya") };

    static SpeciesSO Make(string species, string file)
    {
        var src = AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/" + species + ".asset");
        var c = Object.Instantiate(src);
        c.speciesName = species + " ~читаемость " + System.DateTime.Now.Ticks;
        string H = "Docs/models/handoff/" + file;
        var d = JsonUtility.FromJson<Delivery>(System.IO.File.ReadAllText(H + "-graph.json"));
        c.bones = d.nodes; c.skeletonHides = d.hides;
        SpeciesHandoff.ApplyLayouts(c, System.IO.File.ReadAllText(H + "-head-layout.json"),
            System.IO.File.ReadAllText(H + "-organs-layout.json"));
        c.skinCell = 0.042f;
        return c;
    }

    /// Пять видов в ряд на дистанции D (м) впереди игрока; игрок в начале координат, смотрит в +Z.
    public static string Row(float D, string pose)
    {
        Wipe();
        var root = new GameObject(Rig);
        var black = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.black };
        black.SetColor("_BaseColor", Color.black);
        // шаг ряда растёт с дистанцией, чтобы звери не заслоняли друг друга на экране
        float step = 4.5f + 0.1f * D;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < Species.Length; i++)
        {
            var so = Make(Species[i].name, Species[i].file);
            var body = new GameObject(Species[i].name);
            body.transform.SetParent(root.transform, false);
            var cc = body.AddComponent<CharacterController>(); cc.height = 2f; cc.center = new Vector3(0, 1, 0);
            MorphBuilder.Build(body.transform, so, so.organs.Where(o => o != null).ToList());
            body.transform.position = new Vector3((i - 2) * step, 0f, D);
            body.transform.rotation = Quaternion.Euler(0f, pose == "head" ? 180f : 90f, 0f);
            foreach (var r in body.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = Enumerable.Repeat(black, r.sharedMaterials.Length).ToArray();
            var b = new Bounds(body.transform.position, Vector3.zero);
            foreach (var r in body.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0}: {1:F2}×{2:F2}×{3:F2} м; ",
                Species[i].name, b.size.x, b.size.y, b.size.z);
        }
        var cam = new GameObject("ЧитаемостьCam").AddComponent<Camera>();
        cam.transform.SetParent(root.transform, false);
        cam.transform.position = new Vector3(0f, 6f, -7f);                       // CameraFollow.offset
        cam.transform.LookAt(new Vector3(0f, 1.2f, 0f));                          // CameraFollow.lookHeight
        cam.fieldOfView = 60f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 200f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.78f, 0.82f, 0.84f);
        RenderSettings.fog = false;
        return sb.ToString();
    }

    public static string Wipe()
    {
        int n = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            if (go != null && go.name == Rig) { Object.DestroyImmediate(go); n++; }
        return "снесено: " + n;
    }
}
