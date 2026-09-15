using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// АРБИТР АРСЕНАЛА (спека 4b-2 §2.2, доведена задачей 7 спеки «данные в органах»): чем бить с этой дистанции.
/// Арсенал — доставки, которые тело завело по записям органов (`GetComponents&lt;WindupAbility&gt;`); окна срабатывания
/// берутся из тех же записей (<see cref="WindupAbility.WindowMin"/> / <see cref="WindupAbility.WindowMax"/>).
/// Правило 4b-2 без дерева приоритетов: первая доступная доставка, в чьё окно попала цель, причём дальние (окно
/// с минимальной дистанцией: разбег, наскок, залп) спрашиваются раньше ближних (укус, рога, конечность).
/// Видов не знает: босс с лосиными ногами таранит, химера без Пасти бьёт конечностью — так записано в её органах.
/// </summary>
public static class Arsenal
{
    // доставка без записи органа (или с пустым окном) в арсенал не входит
    static readonly Predicate<WindupAbility> Unusable = a => !(a is IOrganAbility o) || !o.Available || a.WindowMax <= 0f;
    // дальние раньше ближних: по убыванию нижней границы окна
    static readonly Comparison<WindupAbility> FarFirst = (x, y) => y.WindowMin.CompareTo(x.WindowMin);

    /// <summary>Собрать арсенал тела в рабочий список вызывающего (без аллокаций в Update): доступные доставки, дальние первыми.</summary>
    public static void Collect(GameObject self, List<WindupAbility> buffer)
    {
        self.GetComponents(buffer);
        buffer.RemoveAll(Unusable);
        buffer.Sort(FarFirst);
    }

    /// <summary>Доставка из собранного арсенала, в чьё окно попала цель; null — с этой дистанции бить нечем.</summary>
    public static WindupAbility Pick(List<WindupAbility> collected, float dist)
    {
        foreach (var a in collected)
            if (dist >= a.WindowMin && dist <= a.WindowMax) return a;
        return null;
    }
}
