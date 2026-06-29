using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Стаб родства: при смерти этого существа начисляет родство с его видом.
/// Пока просто копит счётчик и логает — полноценную экономику (скидки, конструктор) прикрутим позже.
/// </summary>
[RequireComponent(typeof(Health))]
public class GiveAffinityOnDeath : MonoBehaviour
{
    [SerializeField] string species = "Волк";
    [SerializeField] int amount = 1;

    void Awake() => GetComponent<Health>().onDeath.AddListener(Grant);

    void Grant()
    {
        AffinityTracker.Add(species, amount);
        Debug.Log($"+{amount} родство [{species}] → всего {AffinityTracker.Get(species)}");
    }
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
}
