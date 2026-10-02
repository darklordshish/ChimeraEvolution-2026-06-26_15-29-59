using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// Три слоя тела на стенде ShotSpecies: 1 — кости графа, 2 — кости + объёмы поля («мышцы»), 3 — оболочка (деталь подкрашена)
public static class ShotLayers
{
    static Material Mat(Color c)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c); m.color = c;
        return m;
    }

    static Mesh Cone(float r0, float r1, float sec, float dep, float len)
    {
        int n = 14; var v = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n; float x = Mathf.Cos(a) * sec, z = Mathf.Sin(a) * dep;
            v.Add(new Vector3(x * r0, 0, z * r0)); v.Add(new Vector3(x * r1, len, z * r1));
        }
        for (int i = 0; i < n; i++)
        {
            int a = 2 * i, b = 2 * ((i + 1) % n);
            t.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
        }
        v.Add(Vector3.zero); v.Add(Vector3.up * len); int c0 = v.Count - 2, c1 = v.Count - 1;
        for (int i = 0; i < n; i++) { int a = 2 * i, b = 2 * ((i + 1) % n); t.AddRange(new[] { c0, a, b, c1, b + 1, a + 1 }); }
        var m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); return m;
    }

    public static string Run(int mode, bool chimera)
    {
        var root = GameObject.Find("~ШОТ");
        if (root == null) return "нет стенда";
        var human = AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/Человек.asset");
        var wolf = AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/Волк.asset");
        var worn = human.organs.Where(o => o != null && (!chimera || o.slot != BodySlots.Arms)).ToList();
        if (chimera) worn.Insert(0, wolf.organs.First(o => o.slot == BodySlots.Arms));
        var body = ChainSwap.Compose(human, worn);
        var skip = new HashSet<string>(body.fieldSkip ?? new string[0]);
        var morph = root.GetComponentsInChildren<Transform>().First(t => t.name == "Morph");

        var detailCol = new Color(0.95f, 0.55f, 0.15f);
        if (mode == 3)
        {
            var dm = Mat(detailCol);
            foreach (var v in root.GetComponentsInChildren<BodyPartView>(true))
                foreach (var r in v.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = dm;
            return "оболочка: деталь подкрашена";
        }
        foreach (var r in root.GetComponentsInChildren<Renderer>()) r.enabled = false;

        var pose = (IList)typeof(BoneMesher).GetMethod("Pose", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { body });
        var pt = pose.GetType().GetGenericArguments()[0];
        FieldInfo F(string n) => pt.GetField(n);
        var boneMat = Mat(new Color(0.85f, 0.85f, 0.9f)); var jointMat = Mat(new Color(0.3f, 0.6f, 0.95f));
        var fieldMat = Mat(new Color(0.55f, 0.62f, 0.72f)); var skipMat = Mat(detailCol); var muscleMat = Mat(new Color(0.8f, 0.35f, 0.35f));
        var holder = new GameObject("viz"); holder.transform.SetParent(morph, false);
        int n = 0;
        foreach (var q in pose)
        {
            var b = (Bone)F("b").GetValue(q);
            var pos = (Vector3)F("pos").GetValue(q); var tip = (Vector3)F("tip").GetValue(q);
            var rot = (Quaternion)F("rot").GetValue(q); bool muscle = (bool)F("muscle").GetValue(q);
            float len = (tip - pos).magnitude; if (len < 1e-4f) continue;
            bool off = skip.Contains(b.name);
            var go = new GameObject(b.name); go.transform.SetParent(holder.transform, false);
            go.transform.localPosition = pos; go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (tip - pos).normalized);
            var mf = go.AddComponent<MeshFilter>(); var mr = go.AddComponent<MeshRenderer>();
            if (mode == 1)
            {
                mf.sharedMesh = Cone(0.008f, 0.004f, 1, 1, len);
                mr.sharedMaterial = off ? skipMat : boneMat;
                var j = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(j.GetComponent<Collider>());
                j.transform.SetParent(holder.transform, false); j.transform.localPosition = pos; j.transform.localScale = Vector3.one * 0.025f;
                j.GetComponent<Renderer>().sharedMaterial = off ? skipMat : jointMat;
            }
            else
            {
                go.transform.localRotation = rot;   // сечение — в кадре кости, как у поля
                mf.sharedMesh = Cone(b.r0, b.r1, Mathf.Max(0.05f, b.section), Mathf.Max(0.05f, b.depth), len);
                mr.sharedMaterial = off ? skipMat : muscle ? muscleMat : fieldMat;
            }
            n++;
        }
        return $"узлов {n}, гасит деталь: {string.Join(",", skip)}";
    }
}
