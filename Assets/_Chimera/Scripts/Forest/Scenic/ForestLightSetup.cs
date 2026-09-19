using UnityEngine;

/// <summary>
/// Световой риг песочницы — только кодом (руками в инспекторе не лазить).
/// Чинит разбор синевы s5: Sun был Point внутри террейна, ambient давал синеву,
/// фон — полоса дефолтного скайбокса. Энтрипойнт для run_script + save_scene.
/// Лес, слайс s7.
/// </summary>
public static class ForestLightSetup
{
#if UNITY_EDITOR
    public static void SetupSandbox()
    {
        var sunGo = GameObject.Find("Sun");
        if (sunGo == null) sunGo = new GameObject("Sun");
        var sun = sunGo.GetComponent<Light>();
        if (sun == null) sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sunGo.transform.rotation = Quaternion.Euler(35f, 140f, 0f);
        sun.intensity = 1.3f;
        sun.color = new Color(1f, 0.945f, 0.86f);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 1f;
        UnityEngine.RenderSettings.sun = sun;

        UnityEngine.RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        UnityEngine.RenderSettings.ambientSkyColor = new Color(0.66f, 0.75f, 0.83f);
        UnityEngine.RenderSettings.ambientEquatorColor = new Color(0.49f, 0.52f, 0.47f);
        UnityEngine.RenderSettings.ambientGroundColor = new Color(0.36f, 0.35f, 0.30f);
        UnityEngine.RenderSettings.ambientIntensity = 0.75f;

        const string skyPath = "Assets/_Chimera/Materials/ForestSky.mat";
        var sky = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if (sky == null)
        {
            sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetColor("_SkyTint", new Color(0.61f, 0.71f, 0.81f));
            sky.SetColor("_GroundColor", new Color(0.42f, 0.42f, 0.37f));
            sky.SetFloat("_Exposure", 1f);
            sky.SetFloat("_SunSize", 0.04f);
            UnityEditor.AssetDatabase.CreateAsset(sky, skyPath);
        }
        UnityEngine.RenderSettings.skybox = sky;

        UnityEngine.RenderSettings.fog = true;
        UnityEngine.RenderSettings.fogMode = FogMode.ExponentialSquared;
        UnityEngine.RenderSettings.fogDensity = 0.012f;
        UnityEngine.RenderSettings.fogColor = new Color(0.78f, 0.82f, 0.84f);

        Debug.Log("[Forest] свет песочницы настроен (солнце/трилайт/скай/Exp2)");
    }
#endif
}
