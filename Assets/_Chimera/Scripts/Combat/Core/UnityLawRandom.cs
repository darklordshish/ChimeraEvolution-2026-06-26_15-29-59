using Chimera.Laws;

/// <summary>Случайность законов поверх `UnityEngine.Random`: игра получает те же числа, что и до выноса законов.</summary>
public sealed class UnityLawRandom : ILawRandom
{
    public static readonly UnityLawRandom Instance = new();

    public float Next01() => UnityEngine.Random.value;
    public int Range(int min, int max) => UnityEngine.Random.Range(min, max);
    public float Range(float min, float max) => UnityEngine.Random.Range(min, max);
}
