/// <summary>Вес формы-ключа вручную для кадр-ряда: `On("Волк", 0.35)` — все тела-меши с ключом «Волк» берут этот вес,
/// `Off()` — вес снова от ступени идентичности. Кэш тел сбрасывается, следующая сборка берёт новый вес.</summary>
public static class KeyShot
{
    public static string On(string key, float weight) { BodyMesh.ForceKey = key; BodyMesh.ForceWeight = weight; BodyMesh.ResetCache(); ChainSwap.ResetCache(); return key + " = " + weight; }
    /// <summary>Куски органов на теле — для ряда «ключ × графт» (в игре выключены, `PartAssembly.MeshPieces`).</summary>
    public static string Pieces(bool on) { PartAssembly.MeshPieces = on; ChainSwap.ResetCache(); BodyMesh.ResetCache(); return "куски " + (on ? "включены" : "выключены"); }
    public static string Off() { PartAssembly.MeshPieces = true; BodyMesh.ForceKey = null; BodyMesh.ForceWeight = 0f; BodyMesh.ResetCache(); ChainSwap.ResetCache(); return "вес от ступени"; }
}
