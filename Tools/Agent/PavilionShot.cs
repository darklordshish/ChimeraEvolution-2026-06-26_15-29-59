using System.Text;
using UnityEngine;

/// <summary>
/// Кадры большого павильона (Task 3, 27.10). Сцена — ForestPavilion.unity.
/// Камера стендовая: имя PavilionCam, тег Untagged — Camera.main не перехватываем,
/// в игре остаётся одна Main Camera (сторож ForestSceneCameraTests).
/// Позиции всех режимов выводятся из b.Canyon / b.Hydro, а не из чисел в коде:
/// каньон детерминирован сидом, кадр обязан ехать за ним.
/// </summary>
public static class PavilionShot
{
    static ForestBuilder builder;
    static Camera cam;
    static Transform root;

    public static string Setup()
    {
        builder = Object.FindAnyObjectByType<ForestBuilder>();
        if (builder == null) return "no builder";
        builder.buildClimate = true;
        builder.climateWeather = WeatherKind.Clear;
        builder.climateTimeMinutes = 10.5f;
        builder.climateTimeScale = 0f;
        builder.climateAutoWeather = false;
        builder.Clear();
        builder.Build();
        if (builder.BuiltRoot == null) return "world not built";
        root = builder.BuiltRoot.transform;
        EnsureCamera();
        return "pavilion ready; " + CanyonLine();
    }

    /// <summary>
    /// Лёгкое подключение: сцена уже собрана в Awake — второй Build не нужен.
    /// run_script выполняется в своей сборке, статики между вызовами не живут,
    /// поэтому каждый кадр заново находит builder и камеру, но НЕ пересобирает мир.
    /// </summary>
    static string Attach()
    {
        if (builder == null) builder = Object.FindAnyObjectByType<ForestBuilder>();
        if (builder == null) return "no builder";
        if (builder.BuiltRoot == null) return Setup(); // мир ещё не собран — собрать
        root = builder.BuiltRoot.transform;
        EnsureCamera();
        return null;
    }

    static void EnsureCamera()
    {
        // Стендовые камеры гасим, Main Camera не трогаем: Camera.main остаётся своим.
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            if (c.name == "TopCam" || c.name == "SandboxCam") c.enabled = false;

        var go = GameObject.Find("PavilionCam");
        if (go == null) go = new GameObject("PavilionCam");
        go.SetActive(true);
        // НЕ MainCamera: capture_game_view ищет камеру по имени, а Camera.main
        // должен остаться у игровой Main Camera (гоча «перехват камеры»).
        go.tag = "Untagged";
        cam = go.GetComponent<Camera>();
        if (cam == null) cam = go.AddComponent<Camera>();
        cam.enabled = true;
        cam.orthographic = false;
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 6000f;
        cam.clearFlags = CameraClearFlags.Skybox;
        go.transform.position = new Vector3(0f, 60f, -500f);
        go.transform.LookAt(new Vector3(0f, 90f, 0f));
    }

    public static string Info()
    {
        var problem = Attach();
        if (problem != null) return problem;
        var sb = new StringBuilder();
        sb.AppendLine(CanyonLine());
        var h = builder.Hydro;
        if (h != null)
            sb.AppendLine($"hydro: lakeCenter={h.lakeCenter} lakeRadius={h.lakeRadius:F1} " +
                          $"lakeLevel={h.lakeLevel:F2} riverHalfWidth={h.riverHalfWidth:F2} " +
                          $"riverPts={(h.riverPts != null ? h.riverPts.Count : 0)}");
        sb.AppendLine($"entry={builder.Entry} labCenter={builder.LabCenter} labDoor={builder.LabDoor} " +
                      $"root={root.position} chunks={builder.Chunks} trees={builder.Trees}");
        sb.AppendLine($"terrace: valid={builder.Terrace.valid} center={builder.Terrace.center} " +
                      $"r={builder.Terrace.radius:F1} h={builder.Terrace.height:F1} blend={builder.Terrace.blend:F1}");
        return sb.ToString();
    }

    /// <summary>Профиль дна каньона: сырое поле против SampleGround. Диагностика Task 1/3.</summary>
    public static string Profile()
    {
        var problem = Attach();
        if (problem != null) return problem;
        var c = builder.Canyon;
        if (!c.valid) return "canyon invalid";
        var h = builder.Hydro;
        var sb = new StringBuilder();
        sb.AppendLine($"lakeLevel={h.lakeLevel:F2}");
        for (int i = 0; i < c.axis.Count; i++)
        {
            var p = c.axis[i];
            float raw = builder.RawGround(p.x, p.y);
            float ground = builder.SampleGround(p.x, p.y);
            sb.AppendLine($"  {i,2}: ({p.x:F0};{p.y:F0}) raw={raw:F2} ground={ground:F2} " +
                          $"carve={raw - ground:F2}");
        }
        return sb.ToString();
    }

    /// <summary>Один из режимов: CanyonTop | Canyon3_4 | CascadePool | CascadeLip | Lake.</summary>
    public static string Shot(string mode)
    {
        var problem = Attach();
        if (problem != null) return problem;
        switch (mode)
        {
            case "CanyonTop": return CanyonTop();
            case "Canyon3_4": return Canyon3_4();
            case "CascadePool": return CascadePool();
            case "CascadeLip": return CascadeLip();
            case "Lake": return Lake();
            case "Station": return Station();
            default: return "unknown mode " + mode + " (CanyonTop|Canyon3_4|CascadePool|CascadeLip|Lake)";
        }
    }

    // ---------- режимы -------------------------------------------------------------
    static string CanyonTop()
    {
        var c = builder.Canyon;
        if (!c.valid) return "canyon invalid";
        Vector2 mid = AxisMean(c);
        Vector2 dir = SafeDir(c.lip - c.mouth);
        float length = AxisLength(c);
        float maxGround = MaxGroundAlong(c.axis);
        float height = Mathf.Max(120f, length * 0.8f);

        Vector3 target = Field(mid.x, maxGround, mid.y);
        Vector3 pos = target + Vector3.up * height;
        float size = Mathf.Max(60f, length * 0.62f + c.halfWidth * 2f);

        cam.orthographic = true;
        cam.orthographicSize = size;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 6000f;
        Vector3 up = new Vector3(dir.x, 0f, dir.y);
        if (up.sqrMagnitude < 1e-4f) up = Vector3.forward;
        cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(Vector3.down, up));
        return $"CanyonTop pos={pos} size={size:F1} target={target}";
    }

    static string Canyon3_4()
    {
        var c = builder.Canyon;
        if (!c.valid) return "canyon invalid";
        Vector2 mid = AxisMean(c);
        Vector2 d = SafeDir(c.lip - c.mouth);
        Vector2 perp = new Vector2(-d.y, d.x);
        float length = AxisLength(c);

        Vector2 camField = c.mouth - d * (length * 0.38f) + perp * (length * 0.18f);
        Vector3 target = Field(mid.x, Ground(mid.x, mid.y) + 8f, mid.y);
        Vector3 desired = Field(camField.x,
            Ground(camField.x, camField.y) + Mathf.Max(22f, length * 0.16f), camField.y);
        return PlaceClear("Canyon3_4", target, desired, 2f);
    }

    static string CascadePool()
    {
        var c = builder.Canyon;
        if (!c.valid) return "canyon invalid";
        Vector2 d = SafeDir(c.lip - c.mouth);
        Vector2 perp = new Vector2(-d.y, d.x);
        float length = AxisLength(c);
        Vector2 p = c.pool;

        Vector3 target = Field(p.x, Ground(p.x, p.y) + 3f, p.y);
        Vector2 camField = p - d * Mathf.Max(35f, length * 0.28f)
                             + perp * Mathf.Max(10f, length * 0.10f);
        Vector3 desired = Field(camField.x,
            Ground(camField.x, camField.y) + Mathf.Max(22f, length * 0.14f), camField.y);
        return PlaceClear("CascadePool", target, desired, 1.5f);
    }

    static string CascadeLip()
    {
        var c = builder.Canyon;
        if (!c.valid) return "canyon invalid";
        Vector2 d = SafeDir(c.lip - c.mouth);
        Vector2 perp = new Vector2(-d.y, d.x);
        float length = AxisLength(c);
        Vector2 p = c.lip;

        Vector3 target = Field(p.x, Ground(p.x, p.y) + 3f, p.y);
        Vector2 camField = p + d * (length * 0.22f) + perp * (length * 0.08f);
        Vector3 desired = Field(camField.x,
            Ground(camField.x, camField.y) + Mathf.Max(20f, length * 0.12f), camField.y);
        return PlaceClear("CascadeLip", target, desired, 1.5f);
    }

    static string Station()
    {
        if (builder.LabNorthDoor == Vector3.zero) return "no north door";
        Vector3 target = Field(builder.LabNorthDoor.x, builder.LabNorthDoor.y + 2f, builder.LabNorthDoor.z);
        Vector3 desired = Field(builder.LabNorthDoor.x, builder.LabNorthDoor.y + 12f, builder.LabNorthDoor.z + 28f);
        return PlaceClear("Station", target, desired, 1.5f);
    }

    static string Lake()
    {
        var h = builder.Hydro;
        if (h == null || h.lakeRadius <= 0f) return "no lake";
        var c = builder.Canyon;
        Vector2 center = h.lakeCenter;
        Vector2 away;
        if (c.valid) away = SafeDir(center - c.mouth);
        else away = SafeDir(center - new Vector2(builder.Entry.x, builder.Entry.z));
        if (away.sqrMagnitude < 1e-4f) away = Vector2.right;

        float dist = h.lakeRadius * 1.25f + 90f;
        Vector2 camField = center + away * dist;
        Vector3 target = Field(center.x, h.lakeLevel + 4f, center.y);
        Vector3 desired = Field(camField.x,
            Ground(camField.x, camField.y) + Mathf.Max(45f, h.lakeRadius * 0.35f), camField.y);
        return PlaceClear("Lake", target, desired, 3f);
    }

    // ---------- камера -------------------------------------------------------------
    static string PlaceClear(string label, Vector3 target, Vector3 desired, float radius)
    {
        float dist = Vector3.Distance(target, desired);
        float step = Mathf.Max(4f, dist * 0.10f);
        Vector3 pos = desired + Vector3.up * (10f * step);
        int raised = 10;
        for (int i = 0; i < 10; i++)
        {
            var candidate = desired + Vector3.up * (i * step);
            if (!Blocked(target, candidate, radius))
            {
                pos = candidate;
                raised = i;
                break;
            }
        }
        if (raised == 10)
            Debug.LogWarning($"[PavilionShot] {label}: стена не отпускает — камера поднята на {raised * step:F0} м");

        cam.orthographic = false;
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 6000f;
        cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(target - pos, Vector3.up));
        return $"{label} pos={pos} target={target} raised={raised * step:F0}m";
    }

    static bool Blocked(Vector3 from, Vector3 to, float radius)
    {
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist < 1e-3f) return false;
        return Physics.SphereCast(from, radius, delta / dist, out _, dist, ~0,
            QueryTriggerInteraction.Ignore);
    }

    static Vector3 Field(float x, float y, float z)
    {
        var p = new Vector3(x, y, z);
        return root != null ? root.TransformPoint(p) : p;
    }

    static float Ground(float x, float z) => builder.SampleGround(x, z);

    // ---------- геометрия ----------------------------------------------------------
    static Vector2 AxisMean(DomeCanyon.State c)
    {
        Vector2 sum = Vector2.zero;
        for (int i = 0; i < c.axis.Count; i++) sum += c.axis[i];
        return sum / Mathf.Max(1, c.axis.Count);
    }

    static float AxisLength(DomeCanyon.State c)
    {
        float len = 0f;
        for (int i = 1; i < c.axis.Count; i++) len += Vector2.Distance(c.axis[i - 1], c.axis[i]);
        return Mathf.Max(1f, len);
    }

    static float MaxGroundAlong(System.Collections.Generic.List<Vector2> axis)
    {
        float max = float.NegativeInfinity;
        for (int i = 0; i < axis.Count; i++)
            max = Mathf.Max(max, Ground(axis[i].x, axis[i].y));
        return max;
    }

    static Vector2 SafeDir(Vector2 v) =>
        v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.right;

    static string CanyonLine()
    {
        var c = builder != null ? builder.Canyon : default;
        if (!c.valid) return "canyon: none";
        return $"canyon: mouth={c.mouth} lip={c.lip} pool={c.pool} " +
               $"axis={c.axis.Count} length={AxisLength(c):F1} halfWidth={c.halfWidth:F1} " +
               $"depth={c.depthMouth:F1}..{c.depthHead:F1} fall={c.fallHeight:F1} " +
               $"poolR={c.poolRadius:F1}";
    }

    // ---------- климат (24.10) ------------------------------------------------------
    public static string Phase(string minutes)
    {
        if (builder == null) builder = Object.FindAnyObjectByType<ForestBuilder>();
        if (builder == null) return "no builder";
        var c = builder.GetComponent<ForestClimate>();
        if (c == null) return "no climate";
        c.timeMinutes = float.Parse(minutes, System.Globalization.CultureInfo.InvariantCulture);
        c.timeScale = 0f;
        c.autoWeather = false;
        c.ApplyVisuals();
        return "phase " + minutes;
    }

    public static string Weather(string kind)
    {
        if (builder == null) builder = Object.FindAnyObjectByType<ForestBuilder>();
        if (builder == null) return "no builder";
        var c = builder.GetComponent<ForestClimate>();
        if (c == null) return "no climate";
        var w = (WeatherKind)System.Enum.Parse(typeof(WeatherKind), kind);
        c.SetWeatherImmediate(w);
        c.ApplyVisuals();
        return "weather " + kind;
    }
}
