using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Второй канал телеграфов в данных: туман и синий ambient съедают hue,
/// выживают яркость/форма (обводка + пульс). Первый канал (цвет материала) — как был.
/// Контраст правилом сторожит s3b на префабе. Лес, слайс s3.
/// </summary>
public struct TelegraphChannels
{
    public Color color;
    public Color outlineColor;
    public float outlineWidth;
    public float pulseFreq;

    public static List<string> Validate(TelegraphChannels channels)
    {
        var issues = new List<string>();
        if (channels.outlineWidth <= 0f)
            issues.Add($"ширина обводки={channels.outlineWidth} обязана быть > 0");
        if (channels.pulseFreq <= 0f)
            issues.Add($"частота пульса={channels.pulseFreq} обязана быть > 0");
        return issues;
    }
}
