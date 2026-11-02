using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>S4: кадры v2-мира — эрозия, композиция, дороги. Сцена не сохраняется.</summary>
public static class S4Shot
{
    static ForestBuilder b;

    public static string Setup()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/ForestPavilion.unity", OpenSceneMode.Single);
        b = Object.FindAnyObjectByType<ForestBuilder>();
        if (b == null) return "no builder";
        b.worldVersion = 2;
        b.buildRelief = true;
        b.buildThermalErosion = true;
        b.buildHydraulicErosion = false;
        b.buildOnAwake = false;
        b.erosionVersion = 2;
        b.Clear();
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            c.enabled = false;
        // Сборка v2 идёт дольше 30-секундного лимита пайплайна; запускаем её
        // отложенно, чтобы run_script успел вернуть управление.
        EditorApplication.delayCall += () => b.Build();
        return "S4 v2 build scheduled; poll editor_status / wait, then S4Shot.Top";
    }

    public static string Top()
    {
        var problem = Attach();
        if (problem != null) return problem;
        var go = CameraObject("S4Top");
        var cam = go.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 1100f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 8000f;
        go.transform.position = new Vector3(0f, 420f, 0f);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        return "S4Top";
    }

    public static string Road()
    {
        var problem = Attach();
        if (problem != null) return problem;
        var net = b.Roads;
        if (net == null || net.roads.Count == 0) return "no roads";
        var road = net.roads[0];
        Vector2 mid = road.points[road.points.Count / 2];
        float h = b.SampleGround(mid.x, mid.y);
        var go = CameraObject("S4Road");
        var cam = go.GetComponent<Camera>();
        cam.orthographic = false;
        cam.fieldOfView = 55f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 3000f;
        var target = new Vector3(mid.x, h + 2f, mid.y);
        go.transform.position = target + new Vector3(-48f, 30f, -48f);
        go.transform.LookAt(target);
        return $"S4Road at {mid}";
    }

    public static string RoadClose()
    {
        var problem = Attach();
        if (problem != null) return problem;
        var net = b.Roads;
        if (net == null || net.roads.Count == 0) return "no roads";
        ForestRoad road = net.roads[0];
        float best = -1f;
        foreach (var r in net.roads)
        {
            if (r.points == null || r.points.Count < 2) continue;
            Vector2 m = r.points[r.points.Count / 2];
            float d = Vector2.Distance(m, b.LabCenter);
            if (d > best) { best = d; road = r; }
        }
        Vector2 mid = road.points[road.points.Count / 2];
        Vector2 prev = road.points[Mathf.Max(0, road.points.Count / 2 - 3)];
        Vector2 dir = (mid - prev).normalized;
        Vector2 side = new Vector2(-dir.y, dir.x);
        float h = b.SampleGround(mid.x, mid.y);
        var go = CameraObject("S4RoadClose");
        var cam = go.GetComponent<Camera>();
        cam.orthographic = false;
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 3000f;
        var target = new Vector3(mid.x, h + 1.2f, mid.y);
        var pos = new Vector3(mid.x - dir.x * 26f + side.x * 14f, h + 9f,
                              mid.y - dir.y * 26f + side.y * 14f);
        go.transform.position = pos;
        go.transform.LookAt(target);
        return $"S4RoadClose at {mid} d={best:F0}";
    }

    public static string Zone()
    {
        var problem = Attach();
        if (problem != null) return problem;
        if (b.Zones == null || b.Zones.Count == 0) return "no zones";
        var z = b.Zones[0];
        float h = b.SampleGround(z.center.x, z.center.y);
        var go = CameraObject("S4Zone");
        var cam = go.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 120f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 3000f;
        go.transform.position = new Vector3(z.center.x, h + 170f, z.center.y);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        return $"S4Zone {z.center} r={z.radius}";
    }

    static string Attach()
    {
        if (b == null) b = Object.FindAnyObjectByType<ForestBuilder>();
        if (b == null) return "no builder";
        if (b.BuiltRoot == null) return Setup();
        return null;
    }

    static GameObject CameraObject(string name)
    {
        var go = GameObject.Find(name) ?? new GameObject(name);
        go.tag = "Untagged";
        var cam = go.GetComponent<Camera>();
        if (cam == null) cam = go.AddComponent<Camera>();
        cam.enabled = true;
        cam.clearFlags = CameraClearFlags.Skybox;
        return go;
    }
}
