using System;
using UnityEngine;

namespace Chimera.Forest.Water
{
    public readonly struct AqueductPose
    {
        public readonly bool Valid;
        public readonly AqueductType Type;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly string Reason;

        public AqueductPose(bool valid, AqueductType type, Vector3 position, Quaternion rotation, string reason)
        {
            Valid = valid; Type = type; Position = position; Rotation = rotation; Reason = reason;
        }
    }

    /// <summary>
    /// Task 3: посадка акведука у воды. Terrain не режется, canyon-cut не используется.
    /// </summary>
    public static class AqueductPlacement
    {
        public const float BankClearance = 4f;
        public const string ResourceRoot = "Aqueduct/";

        public static bool TryResolve(int seed, DomeHydro.State hydro,
            Func<float, float, float> ground, out AqueductPose pose)
        {
            pose = default;
            if (!AqueductWaterCatalog.TryGet(seed, out var row))
            {
                pose = new AqueductPose(false, AqueductType.Absent, Vector3.zero, Quaternion.identity,
                    "seed not in catalog");
                return false;
            }
            return TryResolve(row, hydro, ground, out pose);
        }

        public static bool TryResolve(AqueductWaterSeed row, DomeHydro.State hydro,
            Func<float, float, float> ground, out AqueductPose pose)
        {
            pose = default;
            var plan = AqueductWaterCatalog.PlanFor(row.Seed);
            if (!plan.ShouldBuild)
            {
                pose = new AqueductPose(false, plan.Type, Vector3.zero, Quaternion.identity, plan.Reason);
                return false;
            }
            if (ground == null)
            {
                pose = new AqueductPose(false, plan.Type, Vector3.zero, Quaternion.identity, "no ground sampler");
                return false;
            }

            Vector2 point;
            Vector2 flow;
            switch (row.Primary)
            {
                case AqueductType.River:
                    if (hydro == null || hydro.riverPts == null || hydro.riverPts.Count < 2)
                    {
                        pose = new AqueductPose(false, row.Primary, Vector3.zero, Quaternion.identity, "no river");
                        return false;
                    }
                    {
                        Vector2 a = hydro.riverPts[0];
                        Vector2 b = hydro.riverPts[hydro.riverPts.Count - 1];
                        flow = (b - a).normalized;
                        if (flow.sqrMagnitude < 1e-6f) flow = Vector2.up;
                        var mid = hydro.riverPts[hydro.riverPts.Count / 2];
                        var perp = new Vector2(-flow.y, flow.x);
                        float offset = hydro.riverHalfWidth + BankClearance;
                        var left = mid + perp * offset;
                        var right = mid - perp * offset;
                        point = ground(left.x, left.y) >= ground(right.x, right.y) ? left : right;
                    }
                    break;
                case AqueductType.Lake:
                    if (hydro == null || hydro.lakeRadius <= 0f)
                    {
                        pose = new AqueductPose(false, row.Primary, Vector3.zero, Quaternion.identity, "no lake");
                        return false;
                    }
                    {
                        float angle = SeededHash.ToFloat01(SeededHash.Hash(row.Seed, 3301, 0)) * Mathf.PI * 2f;
                        var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        point = hydro.lakeCenter + dir * (hydro.lakeRadius + BankClearance);
                        flow = new Vector2(-dir.y, dir.x);
                    }
                    break;
                case AqueductType.WaterfallSpillway:
                    point = row.FallLip;
                    flow = row.FallMouth - row.FallLip;
                    if (flow.sqrMagnitude < 1e-6f) flow = Vector2.up;
                    flow.Normalize();
                    break;
                default:
                    pose = new AqueductPose(false, row.Primary, Vector3.zero, Quaternion.identity, plan.Reason);
                    return false;
            }

            float y = ground(point.x, point.y);
            float pitch = 0f;
            if (row.Primary == AqueductType.WaterfallSpillway)
            {
                // Ведём тело по уклону: лип на земле, плёс у конца — на своей земле.
                // Иначе плёс уходит под поднимающийся склон и его не видно.
                float endY = ground(point.x + flow.x * 54f, point.y + flow.y * 54f);
                pitch = -Mathf.Atan2(endY - y, 54f) * Mathf.Rad2Deg;
            }
            float yaw = Mathf.Atan2(flow.x, flow.y) * Mathf.Rad2Deg;
            pose = new AqueductPose(true, row.Primary,
                new Vector3(point.x, y, point.y),
                Quaternion.Euler(pitch, yaw, 0f),
                "water: " + AqueductWaterCatalog.CodeOf(row.Primary));
            return true;
        }

        public static GameObject Build(Transform parent, int seed, DomeHydro.State hydro,
            Func<float, float, float> ground, bool wet = true)
        {
            if (!TryResolve(seed, hydro, ground, out var pose)) return null;
            string name = PrefabName(pose.Type);
            var prefab = Resources.Load<GameObject>(ResourceRoot + name);
            if (prefab == null)
            {
                Debug.LogWarning("[Aqueduct] нет Resources/" + ResourceRoot + name + " для seed " + seed);
                return null;
            }
            var go = UnityEngine.Object.Instantiate(prefab, pose.Position, pose.Rotation, parent);
            go.name = "Aqueduct_" + AqueductWaterCatalog.CodeOf(pose.Type) + "_" + seed;
            SetWater(go, wet);
            return go;
        }

        public static void SetWater(GameObject root, bool wet)
        {
            if (root == null) return;
            var water = FindDeep(root.transform, "Water");
            if (water != null) water.gameObject.SetActive(wet);
        }

        public static string PrefabName(AqueductType type)
        {
            switch (type)
            {
                case AqueductType.River: return "AqueductRiver";
                case AqueductType.Lake: return "AqueductLake";
                case AqueductType.Reservoir: return "AqueductReservoir";
                case AqueductType.WaterfallSpillway: return "AqueductWaterfall";
                default: return null;
            }
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
