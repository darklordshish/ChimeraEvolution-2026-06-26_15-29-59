using System.Collections.Generic;

/// <summary>
/// Чистая математика логовищ (без GameObject): валидация, страх, порог истощения.
/// Тестируется без сцены. Лес, слайс s2.
/// </summary>
public static class LairRules
{
    public static List<string> CheckSite(string speciesName, float homeRadius, float spawnRadius,
        float fearRadius, int tier, int capacity, float respawnSeconds)
    {
        var issues = new List<string>();
        if (string.IsNullOrEmpty(speciesName))
            issues.Add("вид логова не задан");
        if (homeRadius <= 0f)
            issues.Add($"homeRadius={homeRadius} должен быть > 0");
        if (spawnRadius <= 0f)
            issues.Add($"spawnRadius={spawnRadius} должен быть > 0");
        if (fearRadius <= 0f)
            issues.Add($"fearRadius={fearRadius} должен быть > 0");
        if (tier < 1 || tier > 3)
            issues.Add($"tier={tier} вне [1, 3]");
        if (capacity <= 0)
            issues.Add($"capacity={capacity} должен быть > 0");
        if (respawnSeconds <= 0f)
            issues.Add($"respawnSeconds={respawnSeconds} должен быть > 0");
        return issues;
    }

    public static float AddFear(float fear, float amount)
    {
        return Clamp01(fear + amount);
    }

    public static float DecayFear(float fear, float dt, float rate)
    {
        if (dt <= 0f) return fear;
        return Clamp01(fear - rate * dt);
    }

    public static bool IsExhausted(float fear, float threshold)
    {
        return fear >= threshold;
    }

    static float Clamp01(float v)
    {
        return v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
