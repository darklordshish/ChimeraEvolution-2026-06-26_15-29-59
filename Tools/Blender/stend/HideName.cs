using UnityEngine;
/// Погасить/зажечь рендереры по имени объекта или меша (часть имени): «культя», «хребет» — посмотреть, ЧЕЙ это дефект.
public static class HideName
{
    public static string Run(string part, bool on)
    {
        int n = 0;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            var mf = r.GetComponent<MeshFilter>(); var m = r is SkinnedMeshRenderer s ? s.sharedMesh : mf ? mf.sharedMesh : null;
            if (r.name == part || (m != null && m.name.Contains(part))) { r.enabled = on; n++; }
        }
        return part + " " + n;
    }
}
