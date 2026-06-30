using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Стаб родства: при смерти этого существа начисляет родство с его видом.
/// Полноценную экономику (скидки, конструктор) уже потребляет ChimeraBody.
/// </summary>
[RequireComponent(typeof(Health))]
public class GiveAffinityOnDeath : MonoBehaviour
{
    [SerializeField] string species = "Волк";
    [SerializeField] int amount = 1;

    void Awake() => GetComponent<Health>().onDeath.AddListener(Grant);

    void Grant() => AffinityTracker.Add(species, amount);
}

/// <summary>
/// Временное хранилище родства по видам (статик-стаб; сбрасывается при входе в Play).
/// </summary>
public static class AffinityTracker
{
    static readonly Dictionary<string, int> values = new();

    public static void Add(string species, int n)
    {
        values.TryGetValue(species, out int c);
        values[species] = c + n;
    }

    public static int Get(string species)
    {
        values.TryGetValue(species, out int c);
        return c;
    }

    public static void Set(string species, int value) => values[species] = Mathf.Max(0, value); // dev: выставить точно
}
