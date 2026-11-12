using UnityEngine;

public static class RockShot
{
    public static string Setup() => Set("three-quarter");
    public static string Approach() => Set("approach");
    public static string Side() => Set("side");
    public static string Inside() => Set("inside");
    public static string Underground() => Set("underground");
    public static string Damage() => Set("damage");
    public static string Cells() => Set("cells");
    public static string Ash() => Set("ash");
    public static string Ravines() => Set("ravines");
    public static string Top() => Set("top");
    public static string Deep() => Set("deep");
    public static string CutawaySurgery()
    {
        var b = Object.FindAnyObjectByType<ForestBuilder>();
        if (b == null || b.LabComplex == null) return "no lab";
        foreach (var t in b.LabComplex.root.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("LabRoof") || t.name.StartsWith("Cliff")) t.gameObject.SetActive(false);
        var table = Find(b.LabComplex.root.transform, "SurgeryTableTop");
        Vector3 target = table != null ? table.position : b.LabComplex.root.transform.position;
        var go = GameObject.Find("RockCam");
        if (go == null) { go = new GameObject("RockCam"); go.AddComponent<Camera>(); }
        var cam = go.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 12f;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.useOcclusionCulling = false;
        RenderSettings.fog = false;
        go.transform.position = target + Vector3.up * 30f;
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        return "RockCam surgery cutaway at " + target;
    }

    public static string Cutaway()
    {
        var b = Object.FindAnyObjectByType<ForestBuilder>();
        if (b == null || b.LabComplex == null) return "no lab";
        foreach (var t in b.LabComplex.root.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("LabRoof") || t.name.StartsWith("Cliff")) t.gameObject.SetActive(false);
        return Set("top");
    }

    static string Set(string view)
    {
        var b = Object.FindAnyObjectByType<ForestBuilder>();
        if (b == null) return "no ForestBuilder";
        if (b.LabComplex == null) return "no LabComplex";
        Vector3 door = b.LabDoor;
        Vector3 center = new Vector3(b.LabCenter.x, door.y, b.LabCenter.y);
        Vector3 outward = door - center;
        outward.y = 0f;
        outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, outward).normalized;

        var go = GameObject.Find("RockCam");
        if (go == null)
        {
            go = new GameObject("RockCam");
            go.AddComponent<Camera>();
        }
        var cam = go.GetComponent<Camera>();
        cam.orthographic = false;
        cam.fieldOfView = 52f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 2500f;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.useOcclusionCulling = false;
        RenderSettings.fog = false;

        switch (view)
        {
            case "three-quarter":
                go.transform.position = door + outward * 30f + right * 20f + Vector3.up * 12f;
                go.transform.LookAt(door + Vector3.up * 2.6f);
                break;
            case "approach":
                go.transform.position = door + outward * 32f + Vector3.up * 8f;
                go.transform.LookAt(door + Vector3.up * 2.4f);
                break;
            case "side":
                go.transform.position = door + outward * 20f + right * 32f + Vector3.up * 10f;
                go.transform.LookAt(door + Vector3.up * 2.4f);
                break;
            case "inside":
                go.transform.position = door - outward * 3.2f + Vector3.up * 2.4f;
                go.transform.LookAt(door + outward * 10f + Vector3.up * 2.4f);
                break;
            case "underground":
                go.transform.position = door + outward * 30f + Vector3.up * 2.8f;
                go.transform.LookAt(door + Vector3.up * 1.2f);
                break;
            case "damage":
            {
                var table = Find(b.LabComplex.root.transform, "SurgeryTableTop");
                Vector3 target = table != null ? table.position : center;
                go.transform.position = target + new Vector3(6.5f, 2.6f, 5.5f);
                go.transform.LookAt(target + Vector3.up * 0.8f);
                break;
            }
            case "cells":
            {
                var gate = Find(b.LabComplex.root.transform, "DamagedGate");
                Vector3 target = gate != null ? gate.position : center;
                go.transform.position = target + new Vector3(-7f, 2.4f, 0f);
                go.transform.LookAt(target + Vector3.up * 1.2f);
                break;
            }
            case "deep":
            {
                var cabinet = Find(b.LabComplex.root.transform, "SurgeryCabinet");
                var table = Find(b.LabComplex.root.transform, "SurgeryTableTop");
                Vector3 target = cabinet != null ? cabinet.position : center;
                Vector3 origin = table != null ? table.position : center;
                go.transform.position = origin + Vector3.up * 2.2f + (target - origin).normalized * 1.5f;
                go.transform.LookAt(target + Vector3.up * 0.6f);
                break;
            }
            case "ash":
            {
                var a = b.Ash;
                Vector3 target = new Vector3(a.pos.x, a.groundY + 42f, a.pos.y);
                go.transform.position = new Vector3(a.pos.x - 70f, a.groundY + 48f, a.pos.y - 90f);
                go.transform.LookAt(target);
                break;
            }
            case "ravines":
            {
                if (b.RavineList.Count == 0) return "no ravines";
                var r = b.RavineList[0];
                Vector3 target = new Vector3(r.center.x,
                    b.SampleGround(r.center.x, r.center.y) + 0.5f, r.center.y);
                float c = Mathf.Cos(r.angleDeg * Mathf.Deg2Rad);
                float s = Mathf.Sin(r.angleDeg * Mathf.Deg2Rad);
                Vector3 acrossDir = new Vector3(-s, 0f, c);
                Vector3 alongDir = new Vector3(c, 0f, s);
                go.transform.position = target + acrossDir * 26f + alongDir * 7f + Vector3.up * 7f;
                go.transform.LookAt(target);
                break;
            }
            default:
                cam.orthographic = true;
                cam.orthographicSize = 36f;
                go.transform.position = door + Vector3.up * 55f;
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                break;
        }
        return $"RockCam {view} at {go.transform.position}, door {door}, parts {b.LabComplex.parts}";
    }

    static Transform Find(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}

