using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Риг неба павильона (s10c): фаза времени → uniform'ы шейдера свода + яркость звёзд +
/// солнце-лайт + туман + приглушение чужих направленьных (песочница) на ночь.
/// Превью-инструмент: сцена после Wipe чистая, базы интенсивностей снимаются заново.
/// </summary>
public static class DomeSkyRig
{
    static bool primed;
    static readonly Dictionary<int, float> baseIntensities = new Dictionary<int, float>();
    static bool fogCaptured;
    static bool fogEnabled;
    static FogMode fogMode;
    static float fogDensity;
    static Color fogColor;
    static float ambientIntensity;

    public static void Reset()
    {
        foreach (var pair in baseIntensities)
        {
            var l = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            foreach (var light in l)
                if (light.GetInstanceID() == pair.Key) light.intensity = pair.Value;
        }
        baseIntensities.Clear();
        primed = false;
        if (fogCaptured)
        {
            RenderSettings.fog = fogEnabled;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = fogColor;
            RenderSettings.ambientIntensity = ambientIntensity;
            fogCaptured = false;
        }
    }

    public static void ApplyPhase(GameObject root, float timeMinutes, bool log = true)
    {
        if (root == null) throw new System.ArgumentNullException(nameof(root));
        var s = DomeCelestial.StateAt(timeMinutes);
        var vault = root.transform.Find("Vault");
        if (vault != null)
        {
            var mat = vault.GetComponent<MeshRenderer>().sharedMaterial;
            mat.SetFloat("_DayT", s.dayT);
            mat.SetFloat("_DuskT", s.duskT);
            mat.SetVector("_SunDir", s.sunDir);
            mat.SetVector("_MoonDir", s.moonDir);
        }
        var stars = root.transform.Find("Stars");
        if (stars != null)
        {
            stars.GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_Level", 1f - s.dayT);
            stars.gameObject.SetActive((1f - s.dayT) > 0.02f); // днём звёзд нет вообще, не чёрные точки
        }
        var sun = root.transform.Find("DomeSun");
        if (sun != null)
        {
            var light = sun.GetComponent<Light>();
            light.transform.rotation = Quaternion.LookRotation(-s.sunDir);
            light.intensity = 1.3f * s.dayT;
            light.color = Color.Lerp(new Color(0.75f, 0.82f, 0.95f), new Color(1f, 0.95f, 0.86f), s.dayT);
        }
        if (!primed)
        {
            baseIntensities.Clear();
            foreach (var l in Object.FindObjectsByType<Light>())
            {
                if (l.type != LightType.Directional) continue;
                if (l.name == "DomeSun") continue;
                baseIntensities[l.GetInstanceID()] = l.intensity;
            }
            primed = true;
        }
        foreach (var l in Object.FindObjectsByType<Light>())
        {
            if (l.type != LightType.Directional || l.name == "DomeSun") continue;
            if (baseIntensities.TryGetValue(l.GetInstanceID(), out float @base))
                l.intensity = @base * (0.05f + 0.95f * s.dayT);
        }
        if (!fogCaptured)
        {
            fogEnabled = RenderSettings.fog;
            fogMode = RenderSettings.fogMode;
            fogDensity = RenderSettings.fogDensity;
            fogColor = RenderSettings.fogColor;
            ambientIntensity = RenderSettings.ambientIntensity;
            fogCaptured = true;
        }
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = s.fogDensity;
        RenderSettings.fogColor = s.fogColor;
        RenderSettings.ambientIntensity = ambientIntensity * (0.35f + 0.65f * s.dayT);
        if (log) Debug.Log($"[Forest] фаза неба: t={timeMinutes:F1} мин, день {s.dayT:F2}, туман {s.fogDensity:F3}");
    }
}
