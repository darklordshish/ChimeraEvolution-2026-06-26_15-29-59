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

    public static void Apply(DomeLabComplex.Result result, long seed, int version = 1, LabPlan plan = null)
    {
        if (result == null || result.root == null) return;
        var root = result.root.transform;
        plan = plan ?? LabPlanFixed.Create();
        var cells = plan.Room("cells");
        var surgery = plan.Room("surgery");
        if (string.IsNullOrEmpty(cells.id) || string.IsNullOrEmpty(surgery.id))
        {
            result.damageSignature = $"skipped: no cells/surgery in {plan.template}";
            result.damageParts = 0;
            return;
        }

        // Адреса берутся у комнат плана, а не у литералов: fixed/mirror/deep-wings
        // отличаются сторонами и глубиной, но контракт имён объектов остаётся.
        float cellsSign = cells.x >= 0f ? 1f : -1f;
        float surgerySign = surgery.x >= 0f ? 1f : -1f;
        float gateX = cells.x + cellsSign * 1.5f;
        float glassX = surgery.x - surgerySign * 6.9f;
        int salt = plan.template == "mirror" ? 17 : plan.template == "deep-wings" ? 31 : 0;

        int cell = (int)(SeededHash.Hash(seed, 9101 + salt, version) % (ulong)CellZ.Length);
        if (cell < 0) cell += CellZ.Length;
        float z = CellZ[cell];
        HideCellGate(root, z);
        BuildBentGate(root, z, gateX);
        int parts = 1;

        BreakGlass(root, glassX);
        parts++;

        Vector3 shift = TableShift(seed, version);
        ShiftSurgeryTable(root, shift);
        parts++;

        int light = (int)(SeededHash.Hash(seed, 9103 + salt, version) % (ulong)LightNames.Length);
        if (light < 0) light += LightNames.Length;
        KillLight(root, LightNames[light]);
        parts++;

        int debris = AddDebris(root, seed, version, z, gateX);
        parts += debris;

        int breach = result.northDoor != Vector3.zero
            ? AddNorthBreach(root, result, seed, version) : 0;
        parts += breach;

        result.damageSignature = $"plan={plan.template};gate={cell};glass=1;" +
                                 $"table={shift.x:F1},{shift.z:F1};light={LightNames[light]};debris={debris}" +
                                 (breach > 0 ? $";exit=breach({breach})" : "");
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

    static void BuildBentGate(Transform root, float z, float gateX)
    {
        var gate = new GameObject("DamagedGate");
        gate.transform.SetParent(root, false);
        gate.transform.localPosition = new Vector3(gateX, 0f, z);
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

    static void BreakGlass(Transform root, float glassX)
    {
        SetActive(root, "SurgeryGlass", false);
        SetActive(root, "SurgeryGlassBarrier", false);
        var glass = DomeLabComplex.MaterialOf(DomeLabComplex.Kit.Glass);
        for (int i = 0; i < 7; i++)
        {
            float x = glassX + (i % 3) * 0.35f;
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

    static int AddDebris(Transform root, long seed, int version, float gateZ, float gateX)
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
                new Vector3(gateX - (gateX >= 0f ? 1.7f : -1.7f) + (gateX >= 0f ? i * 0.4f : -i * 0.4f),
                    0.1f, gateZ - 1.2f + i * 0.5f),
                new Vector3(0.3f, 0.12f, 0.25f), dark,
                Quaternion.Euler(0f, i * 30f, 0f));
            n++;
        }
        return n;
    }

    /// <summary>
    /// Северный выход как повреждение: рваная кромка, плиты и арматура вокруг
    /// уже вырезанного проёма. Геометрию проёма даёт DomeLabComplex, вид — этот слой.
    /// </summary>
    static int AddNorthBreach(Transform root, DomeLabComplex.Result result, long seed, int version)
    {
        Vector3 c = root.InverseTransformPoint(result.northDoor);
        var concrete = DomeLabComplex.MaterialOf(DomeLabComplex.Kit.ConcreteDark);
        var rust = DomeLabComplex.MaterialOf(DomeLabComplex.Kit.Rust);
        var dark = DomeLabComplex.MaterialOf(DomeLabComplex.Kit.Dark);
        int n = 0;
        float half = DomeLabComplex.PortalWidth * 0.5f;

        // Рваные края проёма: сколы по бокам и сверху, детерминированно по сиду.
        for (int i = 0; i < 8; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            float u = SeededHash.ToFloat01(SeededHash.Hash(seed, 9201, i + version * 100));
            float y = 0.25f + u * 3.4f;
            float x = c.x + side * (half + 0.1f + u * 0.35f);
            Cube(root, "BreachRubble", new Vector3(x, y, c.z - 0.15f),
                new Vector3(0.45f + u * 0.35f, 0.3f + u * 0.5f, 0.55f + u * 0.3f),
                i % 3 == 0 ? rust : concrete,
                Quaternion.Euler(0f, u * 40f, side * (8f + u * 12f)));
            n++;
        }

        // Верхняя кромка: обломки, свисающие в проём.
        for (int i = 0; i < 4; i++)
        {
            float u = SeededHash.ToFloat01(SeededHash.Hash(seed, 9202, i + version * 100));
            Cube(root, "BreachRubble", new Vector3(c.x - 1.4f + i * 0.95f,
                3.7f + u * 0.4f, c.z - 0.25f),
                new Vector3(0.55f, 0.35f + u * 0.3f, 0.6f), concrete,
                Quaternion.Euler(u * 18f, u * 55f, 0f));
            n++;
        }

        // Арматура: три погнутых прута, торчат из краёв.
        for (int i = 0; i < 3; i++)
        {
            float side = i == 0 ? -1f : i == 1 ? 1f : 0f;
            float x = c.x + side * (half - 0.2f);
            Cube(root, "BreachRebar", new Vector3(x, 1.8f + i * 0.55f, c.z - 0.3f),
                new Vector3(0.06f, 1.5f + i * 0.35f, 0.06f), rust,
                Quaternion.Euler(0f, 0f, side * 18f));
            n++;
        }

        // Обломки на полу снаружи: проход не перекрывают, коллайдеров нет.
        for (int i = 0; i < 5; i++)
        {
            float u = SeededHash.ToFloat01(SeededHash.Hash(seed, 9203, i + version * 100));
            float v = SeededHash.ToFloat01(SeededHash.Hash(seed, 9204, i + version * 100));
            Cube(root, "BreachDebris", new Vector3(c.x - 2.4f + u * 4.8f, 0.12f,
                c.z + 0.8f + v * 2.4f),
                new Vector3(0.4f + u * 0.4f, 0.16f + v * 0.2f, 0.35f + v * 0.4f),
                i % 2 == 0 ? concrete : dark,
                Quaternion.Euler(0f, u * 180f, 0f));
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
            if (Application.isPlaying) Object.Destroy(collider);
            else Object.DestroyImmediate(collider);
        }
        return go;
    }
}
