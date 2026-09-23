using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Состояние погоды: вид + ветер. Дрейф спор/плевков по ветру применяет хук (s3b),
/// здесь только данные. Лес, слайс s3.
/// </summary>
public struct WeatherState
{
    public WeatherKind kind;
    public Vector2 windDir;
    public float windStrength;

    public static List<string> Validate(WeatherState state)
    {
        var issues = new List<string>();
        if (state.windStrength < 0f)
            issues.Add($"сила ветра={state.windStrength} обязана быть >= 0");
        if (state.windStrength > 0f && state.windDir.sqrMagnitude < 1e-6f)
            issues.Add("ненулевой ветер обязан иметь направление");
        return issues;
    }
}
