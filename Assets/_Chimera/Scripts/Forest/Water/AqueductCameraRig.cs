using System;
using UnityEngine;

namespace Chimera.Forest.Water
{
    public enum AqueductShotId { A1, A2, A3, A4 }

    public readonly struct AqueductShot
    {
        public readonly AqueductShotId Id;
        public readonly Vector3 Position;
        public readonly Vector3 Target;
        public readonly float Fov;
        public AqueductShot(AqueductShotId id, Vector3 position, Vector3 target, float fov)
        { Id = id; Position = position; Target = target; Fov = fov; }
    }

    /// <summary>Task 4: четыре ракурса акведука; только камеры, без формы/посадки.</summary>
    public static class AqueductCameraRig
    {
        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var f = FindDeep(root.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }

        public static AqueductShot[] Presets(GameObject aqueduct)
        {
            if (aqueduct == null) throw new ArgumentNullException(nameof(aqueduct));
            var b = new Bounds(aqueduct.transform.position, Vector3.one);
            bool first = true;
            foreach (var r in aqueduct.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled) continue;
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            Vector3 fwd = aqueduct.transform.forward;
            Vector3 right = aqueduct.transform.right;
            float length = Mathf.Max(b.size.x, b.size.z);
            float width = Mathf.Min(b.size.x, b.size.z);
            if (length < 1f) length = 12f;
            if (width < 1f) width = 4f;
            Vector3 c = b.center;
            Vector3 water = c;
            var waterRoot = FindDeep(aqueduct.transform, "Water");
            if (waterRoot != null)
            {
                var wr = waterRoot.GetComponentInChildren<Renderer>();
                if (wr != null) water = wr.bounds.center;
            }

            // A3 — сбоку/снизу от плёса: губа → полотно → всплеск → плёс одной вертикалью.
            var fall = FindDeep(aqueduct.transform, "AW_WaterFall");
            var plunge = FindDeep(aqueduct.transform, "AW_WaterPlunge");
            Vector3 a3pos = water - right * (width * 2.3f) + Vector3.up * 2.5f;
            Vector3 a3target = water;
            if (fall != null)
            {
                var fr = fall.GetComponent<Renderer>();
                if (fr != null)
                {
                    Bounds fb = fr.bounds;
                    float fallH = Mathf.Max(1.5f, fb.size.y);
                    float poolTop = fb.min.y;
                    if (plunge != null)
                    {
                        var pr = plunge.GetComponent<Renderer>();
                        if (pr != null) poolTop = pr.bounds.max.y;
                    }
                    // Целимся в середину полотна, глаз ставим у воды — кадр читает падение снизу.
                    a3target = new Vector3(fb.center.x, Mathf.Lerp(fb.min.y, fb.max.y, 0.56f), fb.center.z)
                               + aqueduct.transform.forward * 0.10f;
                    a3pos = fb.center
                            + aqueduct.transform.forward * (fallH * 1.20f)
                            + right * (fallH * 1.90f);
                    a3pos.y = poolTop + 1.35f;

                    // Если берег у плёса выше воды — не проваливаемся в склон.
                    float groundY = poolTop;
                    if (Physics.Raycast(new Vector3(a3pos.x, fb.max.y + 30f, a3pos.z),
                            Vector3.down, out var hit, 90f, ~0, QueryTriggerInteraction.Ignore))
                        groundY = hit.point.y;
                    if (groundY > a3pos.y - 0.35f) a3pos.y = groundY + 1.15f;
                }
            }

            return new[]
            {
                new AqueductShot(AqueductShotId.A1,
                    c - fwd * (length * 0.55f) + right * (width * 0.35f) + Vector3.up * 5f,
                    c, 55f),
                new AqueductShot(AqueductShotId.A2,
                    c - fwd * (length * 0.35f) + right * (width * 1.8f) + Vector3.up * 9f,
                    c, 55f),
                new AqueductShot(AqueductShotId.A3,
                    a3pos,
                    a3target, 55f),
                new AqueductShot(AqueductShotId.A4,
                    c - right * (width * 1.4f) - fwd * (length * 0.30f) + Vector3.up * 2f,
                    c - Vector3.up * (b.size.y * 0.35f), 60f),
            };
        }
    }
}
