using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Кадр сгиба: тело с ключом на весе w, колени согнуты на угол — со скелетом по ключу и без него (суставы цели
/// временно сняты). Показывает, где гнётся нога. Снимать: `capture_game_view --camera ПрофильCam`.</summary>
public static class BendShot
{
    public static string Run(string speciesAsset, float w, bool skeleton, float knee)
    {
        foreach (var o in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include).Where(g => g != null && g.name == "~СГИБ")) Object.DestroyImmediate(o);
        var sp = AssetDatabase.LoadAssetAtPath<SpeciesSO>(speciesAsset);
        var saved = sp.keyJoints;
        if (!skeleton) sp.keyJoints = new KeyJoint[0];
        BodyMesh.ForceKey = "Волк"; BodyMesh.ForceWeight = w; BodyMesh.ResetCache();
        var root = new GameObject("~СГИБ");
        try
        {
            var body = new GameObject("тело"); body.transform.SetParent(root.transform, false);
            var cc = body.AddComponent<CharacterController>(); cc.height = 2f; cc.center = Vector3.up;
            MorphBuilder.Build(body.transform, sp, sp.organs.Where(o => o != null).ToList(), null);
        }
        finally { sp.keyJoints = saved; BodyMesh.ForceKey = null; BodyMesh.ForceWeight = 0f; }
        foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "голень" || t.name == "голень.L"))
            t.Rotate(Vector3.right * knee, Space.World);   // сгиб колена вокруг оси «влево-вправо»
        var cam = new GameObject("ПрофильCam").AddComponent<Camera>(); cam.transform.SetParent(root.transform, false);
        cam.transform.position = new Vector3(5f, 0.9f, 0f); cam.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
        cam.orthographic = true; cam.orthographicSize = 1.15f; cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.16f, 0.17f, 0.19f, 1f);
        var lamp = new GameObject("Свет").AddComponent<Light>(); lamp.transform.SetParent(root.transform, false);
        lamp.type = LightType.Directional; lamp.transform.rotation = Quaternion.Euler(35f, 140f, 0f);
        return $"вес {w}, скелет по ключу {(skeleton ? "да" : "нет")}, колено {knee}°";
    }

    public static string Wipe()
    {
        foreach (var o in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include).Where(g => g != null && g.name == "~СГИБ")) Object.DestroyImmediate(o);
        return "снесено";
    }
}
