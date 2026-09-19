using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Пульс-акцент телеграфа в данных (s3c — честное имя: это НЕ обводка, а временная
/// модуляция первого канала; настоящий второй канал — emission/unlit, отдельно).
/// Туман съедает hue — выживает яркостный пульс target↔pulseColor. Рампа и гистерезис —
/// в Telegraph (старт ~0.85, полный ~0.4, щелчка на границе нет).
/// Лес, слайсы s3 (данные) + s3c (применение).
/// </summary>
public struct TelegraphChannels
{
    public Color pulseColor;
    public float pulseAmp;
    public float pulseFreq;

    public static List<string> Validate(TelegraphChannels channels)
    {
        var issues = new List<string>();
        if (channels.pulseAmp <= 0f || channels.pulseAmp > 1f)
            issues.Add($"амплитуда пульса={channels.pulseAmp} обязана быть в (0, 1]");
        if (channels.pulseFreq <= 0f)
            issues.Add($"частота пульса={channels.pulseFreq} обязана быть > 0");
        return issues;
    }
}
