/// <summary>Черты вида на теле без ступени по составу — для ряда «ключ × черты» (`PartAssembly.ForceTraits`).</summary>
public static class TraitShot
{
    public static string On(string donorAsset, int step)
    {
        PartAssembly.ForceTraits = UnityEditor.AssetDatabase.LoadAssetAtPath<SpeciesSO>(donorAsset); PartAssembly.ForceStep = step;
        ChainSwap.ResetCache(); BodyMesh.ResetCache();
        return PartAssembly.ForceTraits != null ? PartAssembly.ForceTraits.speciesName + " · ступень " + step : "вида нет";
    }
    public static string Off() { PartAssembly.ForceTraits = null; PartAssembly.ForceStep = 2; ChainSwap.ResetCache(); return "черты по составу"; }
}
