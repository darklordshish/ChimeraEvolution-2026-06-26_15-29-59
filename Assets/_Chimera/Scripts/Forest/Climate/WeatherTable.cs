using System.Collections.Generic;

/// <summary>
/// Шансы погоды по биому. Шансы, не локи: у любого биома окно Clear/Fog 10–15%,
/// иначе нет контраста и глаз замыливается. Лес, слайс s3.
/// </summary>
public struct WeatherTable
{
    public float clearWeight;
    public float rainWeight;
    public float fogWeight;
    public float snowWeight;
    public float windWeight;

    public static List<string> Validate(WeatherTable table)
    {
        var issues = new List<string>();
        if (table.clearWeight < 0f || table.rainWeight < 0f || table.fogWeight < 0f
            || table.snowWeight < 0f || table.windWeight < 0f)
            issues.Add("веса шансов обязаны быть неотрицательными");
        float sum = table.clearWeight + table.rainWeight + table.fogWeight
            + table.snowWeight + table.windWeight;
        if (sum <= 0f)
            issues.Add("сумма весов обязана быть > 0");
        else if (sum < 0.999f || sum > 1.001f)
            issues.Add($"сумма весов={sum} обязана быть 1");
        return issues;
    }

    public static float ChanceFor(WeatherTable table, WeatherKind kind)
    {
        float sum = table.clearWeight + table.rainWeight + table.fogWeight
            + table.snowWeight + table.windWeight;
        if (sum <= 0f) return 0f;
        switch (kind)
        {
            case WeatherKind.Clear: return table.clearWeight / sum;
            case WeatherKind.Rain: return table.rainWeight / sum;
            case WeatherKind.Fog: return table.fogWeight / sum;
            case WeatherKind.Snow: return table.snowWeight / sum;
            default: return table.windWeight / sum;
        }
    }
}
