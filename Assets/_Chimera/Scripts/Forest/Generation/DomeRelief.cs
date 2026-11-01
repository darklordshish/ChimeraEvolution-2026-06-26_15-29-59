using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>P5-lite: 3 холма 8–15 м и 1 гряда 20–35 м поверх общего поля.
/// Строится только для B-lite (patchyFlora); A/C не меняются.</summary>
public static class DomeRelief
{
    public struct Hill
    {
        public Vector2 center;
        public float radius;
        public float height;
        public bool ridge;
        public Vector2 axis;
        public float halfLength;
        public float halfWidth;
    }

    public class State
    {
        public List<Hill> hills = new List<Hill>();
        public int RidgeCount;
    }

    public static State Build(DomeGenConfigSO cfg, long seed, Vector2 labCenter, Vector2 entry,
        DomeHydro.State hydro, DomeTerrace.State terrace, DomeCanyon.State canyon,
        IReadOnlyList<DomeRavines.Ravine> ravines)
    {
        var state = new State();
        if (cfg == null || hydro == null) return state;
        float rin = Mathf.Max(1f, DomeRockRing.InnerRadius(cfg));
        for (int i = 0; i < 3; i++)
        {
            for (int attempt = 0; attempt < 80; attempt++)
            {
                int salt = i * 101 + attempt;
                float a = SeededHash.ToFloat01(SeededHash.Hash(seed, 9951, salt)) * Mathf.PI * 2f;
                float r = Mathf.Lerp(rin * 0.22f, rin * 0.76f,
                    SeededHash.ToFloat01(SeededHash.Hash(seed, 9952, salt)));
                Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (Vector2.Distance(p, entry) < 85f) continue;
                if (Vector2.Distance(p, labCenter) < 70f) continue;
                if (DomeHydro.SampleHydro(cfg, hydro, p.x, p.y) < hydro.lakeLevel + 0.8f) continue;
                if (DomeCanyon.Contains(canyon, p.x, p.y)) continue;
                if (DomeRavines.Contains(ravines, p.x, p.y, 1.5f)) continue;
                if (terrace.valid && Vector2.Distance(p, terrace.center) < terrace.radius + 30f) continue;
                state.hills.Add(new Hill
                {
                    center = p,
                    radius = 42f + SeededHash.ToFloat01(SeededHash.Hash(seed, 9953, salt)) * 28f,
                    height = 8f + SeededHash.ToFloat01(SeededHash.Hash(seed, 9954, salt)) * 7f,
                });
                break;
            }
        }

        for (int attempt = 0; attempt < 80; attempt++)
        {
            int salt = 700 + attempt;
            float a = SeededHash.ToFloat01(SeededHash.Hash(seed, 9961, salt)) * Mathf.PI * 2f;
            float r = Mathf.Lerp(rin * 0.25f, rin * 0.70f,
                SeededHash.ToFloat01(SeededHash.Hash(seed, 9962, salt)));
            Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            if (Vector2.Distance(p, entry) < 110f) continue;
            if (Vector2.Distance(p, labCenter) < 80f) continue;
            if (DomeHydro.SampleHydro(cfg, hydro, p.x, p.y) < hydro.lakeLevel + 1.5f) continue;
            if (DomeCanyon.Contains(canyon, p.x, p.y)) continue;
            if (DomeRavines.Contains(ravines, p.x, p.y, 2f)) continue;
            if (terrace.valid && Vector2.Distance(p, terrace.center) < terrace.radius + 35f) continue;
            float axisAngle = SeededHash.ToFloat01(SeededHash.Hash(seed, 9963, salt)) * Mathf.PI;
            state.hills.Add(new Hill
            {
                center = p,
                ridge = true,
                axis = new Vector2(Mathf.Cos(axisAngle), Mathf.Sin(axisAngle)),
                halfLength = 45f + SeededHash.ToFloat01(SeededHash.Hash(seed, 9964, salt)) * 25f,
                halfWidth = 12.5f + SeededHash.ToFloat01(SeededHash.Hash(seed, 9965, salt)) * 5f,
                height = 20f + SeededHash.ToFloat01(SeededHash.Hash(seed, 9966, salt)) * 15f,
            });
            state.RidgeCount = 1;
            break;
        }
        return state;
    }

    public static float Apply(State state, float h, float x, float z)
    {
        if (state == null || state.hills == null) return h;
        Vector2 p = new Vector2(x, z);
        foreach (var hill in state.hills)
        {
            if (hill.ridge)
            {
                Vector2 d = p - hill.center;
                float along = Mathf.Abs(Vector2.Dot(d, hill.axis)) / Mathf.Max(hill.halfLength, 0.01f);
                float across = Mathf.Abs(Vector2.Dot(d, new Vector2(-hill.axis.y, hill.axis.x)))
                    / Mathf.Max(hill.halfWidth, 0.01f);
                if (along >= 1f || across >= 1f) continue;
                float fall = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Max(along, across));
                h += hill.height * fall;
            }
            else
            {
                float d = Vector2.Distance(p, hill.center) / Mathf.Max(hill.radius, 0.01f);
                if (d >= 1f) continue;
                float fall = 1f - d * d;
                h += hill.height * fall * fall;
            }
        }
        return h;
    }
}