using System.Linq;
using UnityEngine;
/// КРУПНО ЛАПУ: после ShotSpecies.Chimera навести ортокамеру шота на вершины детали правой руки (x > 0) ниже ymax метров —
/// HandView.Run кадрирует обе кисти и всю деталь, и на опорном носителе лапа выходит мелкой. Возвращает габарит лапы.
public static class HandClose
{
    public static string Run(string view, float ymax)
    {
        var root = GameObject.Find("~ШОТ");
        var cam = root.GetComponentInChildren<Camera>();
        var smr = root.GetComponentsInChildren<BodyPartView>().SelectMany(p => p.GetComponentsInChildren<SkinnedMeshRenderer>()).First();
        var m = new Mesh(); smr.BakeMesh(m, true);
        var M = smr.transform.localToWorldMatrix;
        var c = root.transform.position;
        var v = m.vertices.Select(p => M.MultiplyPoint3x4(p)).Where(p => p.x > c.x && p.y - c.y < ymax).ToArray();
        Object.DestroyImmediate(m);
        if (v.Length == 0) return "нет вершин ниже " + ymax;
        var b = new Bounds(v[0], Vector3.zero); foreach (var p in v) b.Encapsulate(p);
        var dir = view == "front" ? Vector3.forward : view == "back" ? Vector3.back : view == "profile" ? Vector3.right
                : view == "top" ? Vector3.up : new Vector3(1, 0.6f, 1).normalized;
        cam.transform.position = b.center + dir * 8;
        cam.transform.rotation = Quaternion.LookRotation(-dir, view == "top" ? Vector3.forward : Vector3.up);
        cam.orthographicSize = Mathf.Max(b.size.x, b.size.y, b.size.z) * 0.75f;
        return "лапа: " + b.size.ToString("F3") + " низ " + (b.min.y - c.y).ToString("F3");
    }
}
