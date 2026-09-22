using UnityEngine;

/// <summary>
/// Витрина павильона одной командой (s10i): террейн чанками + кольцо + вода +
/// флора + устья + ясень + корпуса + свод/небо/звёзды. В сцену ничего не сохраняется.
/// Приёмка — кадры + карта; NavMesh полного Ø2000 — отдельным бейком (не здесь).
/// </summary>
public static class DomeShowcase
{
    public static void Build(string seedText, string diameterText)
    {
        long seed = 1337;
        long.TryParse(seedText, out seed);
        if (seed == 0) seed = 1337;
        DomePreview.BuildPreview(diameterText, "24", seed.ToString());
        Debug.Log($"[Forest] витрина купола построена (сид {seed}): рельеф + вода + флора + руины + небо");
    }

    public static void Wipe()
    {
        DomePreview.WipePreview();
        Debug.Log("[Forest] витрина купола снесена");
    }
}
