using UnityEngine;

/// <summary>
/// B0.2c: следы прорыва поверх валидного бункера. Детерминированный слой, не runtime-
/// разрушение: один отсек выгнут, стекло хирургии разбито, стол сдвинут, часть света
/// погашена, обломки лежат у стен. Обязательные проходы не перекрываются.
/// </summary>
public static class DomeLabDamage
{
    static readonly float[] CellZ = { -6.3f, -2.1f, 2.1f, 6.3f };
    static readonly string[] LightNames = { "LabLight_Surgery", "LabLight_Cells", "LabLight_Rest" };

    public static void Apply(DomeLabComplex.Result result, long seed, int version = 1)
    {
        if (result == null || result.root == null) return;
        var root = result.root.transform;

        int cell = (int)(SeededHash.Hash(seed, 9101, version) % (ulong)CellZ.Length);
        if (cell < 0) cell += CellZ.Length;
        float z = CellZ[cell];
        HideCellGate(root, z);
        BuildBentGate(root, z);
        int parts = 1;

        BreakGlass(root);
        parts++;

        Vector3 shift = TableShift(seed, version);
        ShiftSurgeryTable(root, shift);
        parts++;

        int light = (int)(SeededHash.Hash(seed, 9103, version) % (ulong)LightNames.Length);
        if (light < 0) light += LightNames.Length;
        KillLight(root, LightNames[light]);
        parts++;

        int debris = AddDebris(root, seed, version, z);
        parts += debris;

        result.damageSignature = $"gate={cell};glass=1;table={shift.x:F1},{shift.z:F1};" +
                                 $"light={LightNames[light]};debris={debris}";
        result.damageParts = parts;
    }

    static void HideCellGate(Transform root, float z)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root) continue;
            bool gatePart = t.name == "CellBar" || t.name == "CellRailTop" ||
                            t.name == "CellRailBottom" || t.name == "CellHeader" ||
                            t.name == "CellTrim" || t.name == "CellLamp" ||
                            t.name == "CellBarrier";
            if (gatePart && Mathf.Abs(t.localPosition.z - z) < 2.05f)
                t.gameObject.SetActive(false);
        }
    }

    static void BuildBentGate(Transform root, float z)
    {
        var gate = new GameObject("DamagedGate");
        gate.transform.SetParent(root, false);
        gate.transform.localPosition = new Vector3(17.5f, 0f, z);
        gate.transform.localRotation = Quaternion.Euler(0f, 22f, 0f);
        var metal = DomeLabComplex.MaterialOf(DomeLabComplex.Kit.Metal);
        for (int i = 0; i < 6; i++)
        {
            float bz = -1.7f + i * 0.68f;
            Cube(gate.transform, "DamagedGateBar",
                new Vector3(0f, 1.5f, bz), new Vector3(0.08f, 3f, 0.08f), metal);
        }
        Cube(gate.transform, "DamagedGateRail", new Vector3(0f, 3f, 0f),
            new Vector3(0.14f, 0.12f, 3.8f), metal);
    }

    static void BreakGlass(Transform root)
    {
        SetActive(root, "SurgeryGlass", false);
        SetActive(root, "SurgeryGlassBarrier", false);
        var glass = DomeLabComplex.MaterialOf(DomeLabComplex.Kit.Glass);
        for (int i = 0; i < 7; i++)
        {
            float x = -9.1f + (i % 3) * 0.35f;
            float z = -3.2f + (i / 3) * 0.55f;
            Cube(root, "DamageGlassShard", new Vector3(x, 0.16f, z),
                new Vector3(0.35f, 0.05f, 0.22f), glass,
                Quaternion.Euler(0f, i * 23f, 0f));
        }
    }

    static Vector3 TableShift(long seed, int version)
    {
        float sx = SeededHash.ToFloat01(SeededHash.Hash(seed, 9104, version)) < 0.5f ? -1.1f : 1.1f;
        float sz = SeededHash.ToFloat01(SeededHash.Hash(seed, 9105, version)) < 0.5f ? -1.3f : 1.3f;
        return new Vector3(sx, 0f, sz);
    }

    static void ShiftSurgeryTable(Transform root, Vector3 shift)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "SurgeryTableBase" || t.name == "SurgeryTableTop" ||
                t.name == "SurgeryTray" || t.name == "SurgeryStool")
                t.localPosition += shift;
        }
    }

    static void KillLight(Transform root, string name)
    {
        var t = Find(root, name);
        if (t != null) t.gameObject.SetActive(false);
    }

    static int AddDebris(Transform root, long seed, int version, float gateZ)
    {
        var dark = DomeLabComplex.MaterialOf(DomeLabComplex.Kit.ConcreteDark);
        var rust = DomeLabComplex.MaterialOf(DomeLabComplex.Kit.Rust);
        int n = 0;
        for (int i = 0; i < 8; i++)
        {
            float u = SeededHash.ToFloat01(SeededHash.Hash(seed, 9106, i + version * 100));
            float v = SeededHash.ToFloat01(SeededHash.Hash(seed, 9107, i + version * 100));
            float x = Mathf.Lerp(-8f, 8f, u);
            float z = Mathf.Lerp(-16f, 14f, v);
            if (Mathf.Abs(x) < 3f && Mathf.Abs(z) < 4f) x += x < 0f ? -4f : 4f;
            var go = Cube(root, "DamageDebris", new Vector3(x, 0.1f, z),
                new Vector3(0.35f + u * 0.5f, 0.12f + v * 0.2f, 0.3f + v * 0.5f),
                i % 3 == 0 ? rust : dark,
                Quaternion.Euler(0f, u * 180f, 0f));
            n++;
        }
        // Обломки у выгнутой решётки — отдельная читаемая группа.
        for (int i = 0; i < 4; i++)
        {
            Cube(root, "DamageDebrisGate",
                new Vector3(15.8f + i * 0.4f, 0.1f, gateZ - 1.2f + i * 0.5f),
                new Vector3(0.3f, 0.12f, 0.25f), dark,
                Quaternion.Euler(0f, i * 30f, 0f));
            n++;
        }
        return n;
    }

    static void SetActive(Transform root, string name, bool active)
    {
        var t = Find(root, name);
        if (t != null) t.gameObject.SetActive(active);
    }

    static Transform Find(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    static GameObject Cube(Transform parent, string name, Vector3 pos, Vector3 scale,
        Material mat, Quaternion? rot = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        if (rot.HasValue) go.transform.localRotation = rot.Value;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        var collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Object.Destroy(collider);
        }
        return go;
    }
}
