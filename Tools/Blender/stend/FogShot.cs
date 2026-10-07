using UnityEngine;
/// КАДР В ТУМАНЕ НА ИГРОВОЙ ДИСТАНЦИИ (спека рук §4.3: узнаётся ли химера). После Stand.Compare/Species: перспективная камера
/// «ТуманCam» на высоте игровой (CameraFollow.offset.y = 6 м) в dist метрах перед рядом, смотрит на середину ряда на 1 м;
/// туман леса (ForestLightSetup: ExponentialSquared 0.012, цвет 0.78/0.82/0.84), фон — цвет тумана. Туман сцены восстанавливает
/// FogShot.Off. Снимать: capture_game_view --camera "ТуманCam".
public static class FogShot
{
    static bool fog; static FogMode mode; static float dens; static Color col;
    public static string On(float dist, float yaw)
    {
        fog = RenderSettings.fog; mode = RenderSettings.fogMode; dens = RenderSettings.fogDensity; col = RenderSettings.fogColor;
        var c = new Color(0.78f, 0.82f, 0.84f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = 0.012f; RenderSettings.fogColor = c;
        var stand = GameObject.Find("~СТЕНД");
        var b = new Bounds(stand.transform.position, Vector3.zero);
        foreach (var r in stand.GetComponentsInChildren<Renderer>()) if (r.name != "земля" && !r.name.StartsWith("метка")) b.Encapsulate(r.bounds);
        var look = new Vector3(b.center.x, 1f, b.center.z);
        var old = GameObject.Find("ТуманCam"); if (old) Object.DestroyImmediate(old);
        var cam = new GameObject("ТуманCam").AddComponent<Camera>();
        cam.transform.SetParent(stand.transform, true);
        cam.fieldOfView = 60f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = c;
        cam.transform.position = look + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, dist) + Vector3.up * 5f;
        cam.transform.LookAt(look);
        return "ряд " + b.size.ToString("F2") + ", камера " + cam.transform.position.ToString("F1");
    }
    public static string Off()
    {
        RenderSettings.fog = fog; RenderSettings.fogMode = mode; RenderSettings.fogDensity = dens; RenderSettings.fogColor = col;
        var old = GameObject.Find("ТуманCam"); if (old) Object.DestroyImmediate(old);
        return "туман сцены восстановлен";
    }
}
