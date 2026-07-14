/// <summary>
/// Оси чувств (сенсорный слайс S1). Новое чувство (совиный слух, ежиный тонкий нюх) = новое значение здесь
/// + канал в профиле (`Senses`) с безопасным дефолтом (range 0 = чувства у вида нет). Существующие виды правок не требуют.
/// </summary>
public enum SenseKind { Sight, Thermal, Scent }

/// <summary>
/// Канал ОДНОГО чувства в профиле вида: базовая дальность + острота (порог чувствительности, задел под S2) +
/// множители дальности ПО СОСТОЯНИЮ восприятия (Спокойствие/Настороженность/Атака). Это и есть матрица
/// «чувство × состояние» — форма выразительная, наполнение ленивое (у волка/змеи множители =1 до среза 4).
/// </summary>
[System.Serializable]
public class SenseChannel
{
    public float range = 0f;    // базовая дальность; 0 = чувства у вида нет
    public float acuity = 1f;   // порог чувствительности (S2 задействует эмиттер-силу; пока задел)
    public float calmMult = 1f; // множители дальности по состоянию восприятия
    public float waryMult = 1f;
    public float attackMult = 1f;

    public float For(Alert s) => range * (s == Alert.Attack ? attackMult : s == Alert.Wary ? waryMult : calmMult);
}
