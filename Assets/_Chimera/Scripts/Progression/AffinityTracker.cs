using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Хранилище РОДСТВА по видам — словарь «вид → счётчик» (статик-стаб; сбрасывается при входе в Play;
/// перманентность через прогоны придёт с мета-сейвом). Начисление живёт в CreatureBody
/// (на смерть NPC даёт +1 за каждый видо-флаг тела); потребляет экономику/бонусы тоже CreatureBody.
/// </summary>
public static class AffinityTracker
{
    public const int Cap = 100; // потолок родства на вид: дальше 100 некуда (скидка+мощь выходят на полку)

    static readonly Dictionary<string, int> values = new();

    public static void Add(string species, int n)
    {
        values.TryGetValue(species, out int c);
        values[species] = Mathf.Min(Cap, c + n);
    }

    public static int Get(string species)
    {
        values.TryGetValue(species, out int c);
        return c;
    }

    public static void Set(string species, int value) => values[species] = Mathf.Clamp(value, 0, Cap); // dev: выставить точно

    public static IEnumerable<KeyValuePair<string, int>> All => values; // HUD/dev: перечислить все виды с родством
}
