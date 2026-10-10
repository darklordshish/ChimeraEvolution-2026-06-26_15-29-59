using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>СВЕРКА МЕША ВИДА В ИГРЕ (ТЗ `Docs/models/TZ-2026-10-10-rig-volka.md`): тело уже собрано `ShotSpecies.Build`
/// (корень `~ШОТ`). Чего кадр не покажет: в позе привязки меш выглядит верно, даже если кости сторон перепутаны или
/// вес ушёл не на ту кость, — ломается это только в движении. Поэтому меряем:
///   · объекты — имена, треугольники, кости с весами;
///   · «центр вершин кости ↔ сама кость» в системе тела: у парной кости знак X обязан совпасть, расстояние — малое;
///   · открытую пасть: `Jaw(угол)` поворачивает `челюсть`, кадр снимается той же камерой.
///
///   unity command run_script --file Tools/Agent/BodyMeshCheck.cs --entry BodyMeshCheck.Report
///   unity command run_script --file Tools/Agent/BodyMeshCheck.cs --entry BodyMeshCheck.Pose --args '["челюсть",35]'</summary>
public static class BodyMeshCheck
{
    public static string Report()
    {
        var root = GameObject.Find("~ШОТ");
        if (root == null) return "тела нет: сначала ShotSpecies.Build";
        var body = root.transform.Find("тело");
        var smrs = body.GetComponentsInChildren<SkinnedMeshRenderer>();
        var sb = new StringBuilder();
        sb.AppendLine("рендереров со скином: " + smrs.Length + "; прочих: " + body.GetComponentsInChildren<MeshRenderer>().Length);
        var sum = new Dictionary<Transform, Vector3>(); var cnt = new Dictionary<Transform, float>();
        int tris = 0; Bounds? all = null;
        foreach (var r in smrs)
        {
            var m = r.sharedMesh; tris += m.triangles.Length / 3;
            var baked = new Mesh(); r.BakeMesh(baked);
            var v = baked.vertices; var bw = m.boneWeights;
            var used = new HashSet<int>();
            for (int i = 0; i < v.Length; i++)
            {
                var p = body.InverseTransformPoint(r.transform.TransformPoint(v[i]));
                all = all == null ? new Bounds(p, Vector3.zero) : Enc(all.Value, p);
                Add(r.bones, bw[i].boneIndex0, bw[i].weight0, p, sum, cnt, used);
                Add(r.bones, bw[i].boneIndex1, bw[i].weight1, p, sum, cnt, used);
                Add(r.bones, bw[i].boneIndex2, bw[i].weight2, p, sum, cnt, used);
                Add(r.bones, bw[i].boneIndex3, bw[i].weight3, p, sum, cnt, used);
            }
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0}: {1} тр, {2} вершин, костей с весами {3} из {4}",
                                        r.name, m.triangles.Length / 3, v.Length, used.Count, r.bones.Length));
            Object.DestroyImmediate(baked);
        }
        var b = all.Value;
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "всего {0} тр; габарит в системе тела: X {1:0.000}..{2:0.000}, Y {3:0.000}..{4:0.000}, Z {5:0.000}..{6:0.000}",
                                    tris, b.min.x, b.max.x, b.min.y, b.max.y, b.min.z, b.max.z));
        sb.AppendLine("кость: место кости → центр её вершин (система тела, м); ! — знак X разошёлся или дальше 0.35 м");
        foreach (var t in sum.Keys.OrderBy(t => t.name))
        {
            var bp = body.InverseTransformPoint(t.position); var c = sum[t] / cnt[t];
            bool bad = (Mathf.Abs(bp.x) > 0.03f && Mathf.Abs(c.x) > 0.03f && Mathf.Sign(bp.x) != Mathf.Sign(c.x)) || (c - bp).magnitude > 0.35f;
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0}{1}: ({2:0.00} {3:0.00} {4:0.00}) → ({5:0.00} {6:0.00} {7:0.00}), вес {8:0}",
                                        bad ? "! " : "", t.name, bp.x, bp.y, bp.z, c.x, c.y, c.z, cnt[t]));
        }
        return sb.ToString();
    }

    /// <summary>Повернуть кость тела вокруг поперечной оси тела на `deg` градусов (+ — конец кости вниз у морды).</summary>
    public static string Pose(string bone, float deg)
    {
        var root = GameObject.Find("~ШОТ");
        if (root == null) return "тела нет";
        var body = root.transform.Find("тело");
        var t = body.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == bone);
        if (t == null) return "кости нет: " + bone;
        t.Rotate(body.right, deg, Space.World);
        return bone + ": повёрнута на " + deg.ToString(CultureInfo.InvariantCulture) + "°, родитель — " + t.parent.name;
    }

    /// <summary>Переставить камеру кадра (`ПрофильCam`): азимут от морды по часовой (0 — анфас, 90 — профиль, 180 — сзади),
    /// наклон вниз, полурамка в метрах и точка прицела в системе тела. `ShotSpecies` умеет только три оси.</summary>
    public static string Cam(float yaw, float pitch, float half, float x, float y, float z)
    {
        var root = GameObject.Find("~ШОТ");
        if (root == null) return "тела нет";
        var body = root.transform.Find("тело");
        var cam = root.GetComponentInChildren<Camera>();
        var dir = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(-pitch, Vector3.right) * Vector3.forward;
        var at = body.TransformPoint(new Vector3(x, y, z));
        dir = body.TransformDirection(dir);
        cam.transform.position = at + dir * 12f;
        cam.transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);
        cam.orthographicSize = half; cam.farClipPlane = 40f;
        return "камера: азимут " + yaw.ToString(CultureInfo.InvariantCulture) + "°, полурамка " + half.ToString(CultureInfo.InvariantCulture) + " м";
    }

    static void Add(Transform[] bones, int i, float w, Vector3 p, Dictionary<Transform, Vector3> sum, Dictionary<Transform, float> cnt, HashSet<int> used)
    {
        if (w <= 0f || bones[i] == null) return;
        used.Add(i);
        sum.TryGetValue(bones[i], out var s); cnt.TryGetValue(bones[i], out var c);
        sum[bones[i]] = s + p * w; cnt[bones[i]] = c + w;
    }

    static Bounds Enc(Bounds b, Vector3 p) { b.Encapsulate(p); return b; }
}
