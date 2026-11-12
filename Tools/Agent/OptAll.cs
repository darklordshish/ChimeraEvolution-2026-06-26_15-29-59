using System.Diagnostics;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

public static class OptAll
{
    public static string Run()
    {
        var b = Object.FindAnyObjectByType<ForestBuilder>();
        if (b == null || b.BuiltRoot == null) return "no built forest";
        int added = 0;
        var names = new System.Collections.Generic.Dictionary<string,int>();
        foreach (var mf in b.BuiltRoot.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = mf.sharedMesh;
            if (mesh == null) continue;
            string n = mesh.name;
            names[n] = names.TryGetValue(n, out var cv) ? cv + 1 : 1;
            if (n != "Trunk" && n != "Snag") continue;
            var go = mf.gameObject;
            if (go.GetComponent<Collider>() != null) continue;
            var cap = go.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, 7f, 0f);
            cap.height = 14f;
            cap.radius = 0.35f;
            added++;
        }
        var top = new System.Text.StringBuilder();
        foreach (var kv in names) if (kv.Value > 3) top.Append(kv.Key + ":" + kv.Value + " ");
        var surface = Object.FindAnyObjectByType<NavMeshSurface>();
        var bake = Stopwatch.StartNew();
        if (surface != null) surface.BuildNavMesh();
        bake.Stop();

        var origin = b.Entry + Vector3.up * 1.6f;
        var hits = new RaycastHit[8];
        var rng = new System.Random(1337);
        var sw = Stopwatch.StartNew();
        int hitCount = 0;
        for (int i = 0; i < 2000; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            hitCount += Physics.RaycastNonAlloc(origin, dir, hits, 100f, ~0, QueryTriggerInteraction.Ignore);
        }
        sw.Stop();

        var path = new NavMeshPath();
        var sw2 = Stopwatch.StartNew();
        int complete = 0;
        for (int i = 0; i < 100; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            var p = b.LabCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 200f;
            var target = new Vector3(p.x, b.SampleGround(p.x, p.y), p.y);
            if (NavMesh.CalculatePath(origin, target, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete) complete++;
        }
        sw2.Stop();
        return $"names={top} added={added} rays=2000 hits={hitCount} rayMs={sw.Elapsed.TotalMilliseconds:F2} " +
               $"bakeMs={bake.Elapsed.TotalMilliseconds:F1} paths=100 complete={complete} pathMs={sw2.Elapsed.TotalMilliseconds:F2}";
    }
}