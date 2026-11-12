using System.Diagnostics;
using UnityEngine;
using UnityEngine.AI;

public static class OptProbe
{
    public static string Run()
    {
        var b = Object.FindAnyObjectByType<ForestBuilder>();
        if (b == null || b.BuiltRoot == null) return "no built forest";
        var cols = b.BuiltRoot.GetComponentsInChildren<Collider>(true);
        int capsules = 0, boxes = 0, mesh = 0;
        foreach (var c in cols)
        {
            if (c is CapsuleCollider) capsules++;
            else if (c is BoxCollider) boxes++;
            else if (c is MeshCollider) mesh++;
        }

        var origin = b.Entry + Vector3.up * 1.6f;
        var hits = new RaycastHit[8];
        var rng = new System.Random(1337);
        var sw = Stopwatch.StartNew();
        int hitCount = 0;
        const int rays = 2000;
        for (int i = 0; i < rays; i++)
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
        return $"colliders={cols.Length} capsules={capsules} boxes={boxes} mesh={mesh} " +
               $"trees={b.Trees} rays={rays} hits={hitCount} rayMs={sw.Elapsed.TotalMilliseconds:F2} " +
               $"paths=100 complete={complete} pathMs={sw2.Elapsed.TotalMilliseconds:F2}";
    }
}