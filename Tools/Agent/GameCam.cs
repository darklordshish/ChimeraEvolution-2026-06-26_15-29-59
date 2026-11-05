using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>КАДР С ИГРОВЫХ КАМЕР: тело настоящим билдером в игровой сцене и камера с теми же числами, что у `CameraFollow`
/// (3-е лицо: сзади и сверху, смещение из сцены; 1-е лицо: от глаз с наклоном). Ортокадр `ShotSpecies` меряет форму, этот
/// показывает, как тело видит игрок. Вне `Assets/`: инструмент агента.
///   run_script --file Tools/Agent/GameCam.cs --entry GameCam.Shoot --args '["Assets/_Chimera/Data/Человек.asset","tps",0,false]'
///   capture_game_view --camera ИгроваяCam --width 1280 --height 720 --save_path Кадры/x.png
/// `view`: tps | fps; `yaw` — поворот тела относительно камеры (0 — спиной к камере); `smooth` — нормали сглажены внутри 28°.</summary>
public static class GameCam
{
    const string Rig = "~ИГРОВОЙ";
    const float SmoothAngle = 28f;

    public static string Open()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        return "сцена открыта";
    }

    public static string Shoot(string asset, string view, float yaw, bool smooth)
    {
        Wipe();
        var species = AssetDatabase.LoadAssetAtPath<SpeciesSO>(asset);
        if (species == null) return "нет вида " + asset;
        var follow = Object.FindAnyObjectByType<CameraFollow>();
        if (follow == null) return "в сцене нет CameraFollow";
        var so = new SerializedObject(follow);
        var offset = so.FindProperty("offset").vector3Value;
        var look = so.FindProperty("lookHeight").floatValue;
        var head = so.FindProperty("headHeight").floatValue;
        var forward = so.FindProperty("headForward").floatValue;
        var src = follow.GetComponent<Camera>();

        var root = new GameObject(Rig);
        var worn = new List<Organ>();
        foreach (var o in species.organs) if (o != null) worn.Add(o);
        var body = new GameObject("тело");
        body.transform.SetParent(root.transform, false);
        var cc = body.AddComponent<CharacterController>(); cc.height = 2f; cc.center = Vector3.up;
        MorphBuilder.Build(body.transform, species, worn, null);
        body.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        int smoothed = smooth ? Smooth(body) : 0;

        var rends = body.GetComponentsInChildren<Renderer>();
        var b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
        var cam = new GameObject("ИгроваяCam").AddComponent<Camera>();
        cam.transform.SetParent(root.transform, false);
        cam.fieldOfView = src != null ? src.fieldOfView : 60f;
        cam.nearClipPlane = 0.05f; cam.farClipPlane = 300f;
        if (view == "fps")
        {
            var isFace = typeof(PlayerController).GetMethod("IsOwnFace", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static); foreach (var r in rends) if ((bool)isFace.Invoke(null, new object[] { r.name })) r.enabled = false;
            var bodyForward = body.transform.forward;
            cam.transform.position = Vector3.up * (head + 0.2f) + bodyForward * forward;
            cam.transform.rotation = Quaternion.Euler(45f, yaw, 0f);
        }
        else
        {
            if (view.StartsWith("tps:"))   // близкая камера: `tps:<вверх>:<назад>`, вместо смещения из сцены
            {
                var parts = view.Split(':');
                offset = new Vector3(0f, float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
            }
            cam.transform.position = offset;
            cam.transform.LookAt(Vector3.up * look);
        }
        return string.Format("{0} {1} yaw {2}: {3} рендереров, сглажено мешей {4}, высота {5:0.00}, камера {6} fov {7}", species.speciesName, view, yaw, rends.Length, smoothed, b.size.y, cam.transform.position, cam.fieldOfView);
    }

    /// <summary>Нормаль вершины — средняя по граням той же точки, отличающимся от своей грани меньше 28°: внутри массы плавно, на рёбрах границ резко.</summary>
    static int Smooth(GameObject body)
    {
        int n = 0;
        foreach (var smr in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh == null) continue;
            var m = Object.Instantiate(smr.sharedMesh);
            var v = m.vertices; var tris = m.triangles;
            var normals = new Vector3[v.Length];
            var at = new Dictionary<Vector3Int, List<Vector3>>();
            var cornerFace = new Vector3[v.Length];
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = tris[t], c = tris[t + 1], d = tris[t + 2];
                var f = Vector3.Cross(v[c] - v[a], v[d] - v[a]);
                foreach (int i in new[] { a, c, d })
                {
                    var key = Key(v[i]);
                    if (!at.TryGetValue(key, out var list)) at[key] = list = new List<Vector3>();
                    list.Add(f);
                    cornerFace[i] = f;
                }
            }
            float cos = Mathf.Cos(SmoothAngle * Mathf.Deg2Rad);
            for (int i = 0; i < v.Length; i++)
            {
                var own = cornerFace[i].normalized; var sum = Vector3.zero;
                foreach (var f in at[Key(v[i])]) if (Vector3.Dot(own, f.normalized) > cos) sum += f;
                normals[i] = sum.sqrMagnitude > 0f ? sum.normalized : own;
            }
            m.normals = normals;
            smr.sharedMesh = m;
            n++;
        }
        return n;
    }

    static Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * 10000f), Mathf.RoundToInt(p.y * 10000f), Mathf.RoundToInt(p.z * 10000f));

    /// <summary>Настоящий низ и верх тела с учётом ключа: меш запекается (границы рендерера у скиннинга завышены).</summary>
    public static string Extent()
    {
        var root = GameObject.Find(Rig);
        if (root == null) return "нет тела";
        float lo = float.MaxValue, hi = float.MinValue; var m = new Mesh();
        foreach (var s in root.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            s.BakeMesh(m, true);
            foreach (var v in m.vertices) { float y = s.transform.TransformPoint(v).y; if (y < lo) lo = y; if (y > hi) hi = y; }
        }
        Object.DestroyImmediate(m);
        return string.Format("низ {0:0.000} верх {1:0.000}", lo, hi);
    }

    public static string Wipe()
    {
        var old = GameObject.Find(Rig);
        if (old != null) Object.DestroyImmediate(old);
        return "снято";
    }
}
