namespace Chimera.Laws
{
    /// <summary>Внедрённая случайность законов: игра подаёт обёртку над UnityEngine.Random, симулятор — генератор по сиду.</summary>
    public interface ILawRandom
    {
        /// <summary>Число в [0, 1).</summary>
        float Next01();

        /// <summary>Целое в [min, max) — как UnityEngine.Random.Range(int, int).</summary>
        int Range(int min, int max);
    }
}
