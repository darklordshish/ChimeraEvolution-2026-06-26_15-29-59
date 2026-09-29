using System.Collections.Generic;
using UnityEngine;

/// <summary>РАЗМЕТКА ЦЕПЕЙ ТЕЛА (спека двух слоёв §4.1, решение 13): словарь цепей и меток и вывод узлов «в долях родителя»
/// в метры. Разметку ставит модельная линия в генераторах графов, здесь — только то, что из неё следует.</summary>
public static class BodyChains
{
    public static readonly string[] Limbs = { "хребет", "шея", "голова", "хвост", "перед", "зад" };

    /// <summary>Метки, которые СТАВЯТСЯ в графе. Холка и крестец не ставятся: это проекция начала передней и задней цепи
    /// на ось хребта, её считает механика (формат 29c).</summary>
    public static readonly string[] Marks =
    {
        "плечо", "локоть", "запястье", "бедро", "колено", "скакательный",
        "основание шеи", "основание черепа", "корень хвоста",
    };

    public static bool IsLimb(string s) => System.Array.IndexOf(Limbs, s) >= 0;
    public static bool IsMark(string s) => System.Array.IndexOf(Marks, s) >= 0;

    /// <summary>УЗЛЫ В ДОЛЯХ → МЕТРЫ. Родитель выводится раньше ребёнка (узел в долях может висеть на узле в долях —
    /// сечения лофта). Узел получает `freeOrigin`: его начало — смещение от начала родителя в кадре родителя.
    /// Возвращает число выведенных узлов; узел, чей родитель не найден, остаётся как был.</summary>
    public static int ResolveRel(Bone[] bones)
    {
        if (bones == null) return 0;
        var byName = new Dictionary<string, Bone>();
        foreach (var b in bones) if (b != null && !string.IsNullOrEmpty(b.name)) byName[b.name] = b;
        var done = new HashSet<string>();
        int n = 0;
        foreach (var b in bones) n += Resolve(b, byName, done, 0);
        return n;
    }

    static int Resolve(Bone b, Dictionary<string, Bone> byName, HashSet<string> done, int depth)
    {
        if (b == null || b.rel == null || !b.rel.On || done.Contains(b.name) || depth > 64) return 0;
        if (string.IsNullOrEmpty(b.parent) || !byName.TryGetValue(b.parent, out var p)) return 0;
        int n = Resolve(p, byName, done, depth + 1);   // родитель в долях — сначала его
        var q = b.rel;
        float r = RadiusAt(p, q.at);
        b.freeOrigin = true;
        b.origin = new Vector3(q.offX * r * p.section, q.at * p.length, q.offZ * r * p.depth);
        b.length = q.len * p.length;
        b.r0 = q.r0 * r;
        b.r1 = q.r1 * r;
        done.Add(b.name);
        return n + 1;
    }

    /// <summary>Радиус родителя в точке `at`: линейно r0 → r1, за концами — по краю.</summary>
    public static float RadiusAt(Bone p, float at) => Mathf.Lerp(p.r0, p.r1, Mathf.Clamp01(at));
}
