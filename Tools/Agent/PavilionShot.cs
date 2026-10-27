using UnityEngine;

public static class PavilionShot
{
    static ForestBuilder builder;
    static Camera cam;

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

        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            if (c.name != "PavilionCam") c.enabled = false;

        var go = GameObject.Find("PavilionCam");
        if (go == null) go = new GameObject("PavilionCam");
        go.SetActive(true);
        cam = go.GetComponent<Camera>();
        if (cam == null) cam = go.AddComponent<Camera>();
        cam.enabled = true;
        cam.tag = "MainCamera";
        cam.orthographic = false;
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 6000f;
        cam.clearFlags = CameraClearFlags.Skybox;
        go.transform.position = new Vector3(0f, 60f, -500f);
        go.transform.LookAt(new Vector3(0f, 90f, 0f));
        return "pavilion ready";
    }

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
