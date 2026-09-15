using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// ЗАПИСЬ СПОСОБНОСТИ ОРГАНА (спека `2026-09-12-dannye-v-organah.md`). На каждый приём — свой наследник со
/// СВОИМИ числами, и силы, и формы удара. Лежит в `Organ.abilities` через `[SerializeReference]`, поэтому
/// инспектор показывает ровно поля этого приёма. Наличие записи и есть включатель.
///
/// КАК ЧИТАТЬ ПОЛЕ ЗАПИСИ — по пометке на нём:
///  • <see cref="ExpressedAttribute"/> — число раскрывается экспрессией тем же законом, что прочие числа органа
///    (`CreatureBody.Express`): родной орган — значение × мощь, донорский — бленд от записи вытесненного органа.
///  • без пометки — идёт как записано (форма удара, стаки, тайминги).
///  • <see cref="LowerIsBetterAttribute"/> — при дублях приёма берётся минимум (перезарядка, замах), иначе максимум.
///  • <see cref="MustBePositiveAttribute"/> — ноль значит «приём не работает»; сторожит тест ассетов.
///
/// ГОЧА: `[SerializeReference]` хранит в ассете ИМЯ класса. Переименование наследника молча обнуляет все его
/// записи — переименовывать только вместе с `[UnityEngine.Scripting.APIUpdating.MovedFrom]`.
/// </summary>
[Serializable]
public abstract class AbilityData
{
    /// <summary>Кто исполняет запись у NPC — доставка приёма. null — у NPC такого приёма нет.</summary>
    public abstract Type NpcCarrier { get; }

    /// <summary>Кто исполняет запись у игрока — грань управления. null — у игрока такого приёма нет.</summary>
    public abstract Type PlayerCarrier { get; }

    /// <summary>ДОМАШНИЙ ПРИЁМ: открыт, только если орган надет на своё родное шасси (`Organ.nativeChassis`; пусто — гейта нет).
    /// Гейт точечный, по приёму, а не по органу: у волчьей Пасти тоже есть родное шасси, и загейти всё подряд —
    /// оборотень-волк остался бы без укуса на человечьем теле.</summary>
    public virtual bool NativeOnly => false;

    /// <summary>МОЩЬ органа в этом теле (экспрессия / родство) — ставит раскрытие. Это не число приёма, а модификатор
    /// индивида: носитель применяет её сам, если его приём растёт с мастерством (залп игрока). В ассет не пишется.</summary>
    [NonSerialized] public float power = 1f;

    /// <summary>РАСКРЫТИЕ: копия записи с числами после экспрессии; сама запись в ассете не меняется.
    /// `displaced` — запись того же приёма у вытесненного родного органа слота (нет её — бленд от нуля).</summary>
    public AbilityData Resolve(AbilityData displaced, bool native, float power)
    {
        var copy = (AbilityData)MemberwiseClone();
        foreach (var f in Numbers(GetType()))
        {
            if (!f.IsDefined(typeof(ExpressedAttribute))) continue;
            float own = Read(f, this);
            float from = displaced != null ? Read(f, displaced) : 0f;
            Write(f, copy, native ? own * power : from + (own - from) * power);
        }
        copy.power = power;
        return copy;
    }

    /// <summary>СУПРЕМУМ ДУБЛЕЙ одного приёма (родной орган + химерный): дубль силу не растит, берётся лучшее
    /// по каждому числу — максимум, у <see cref="LowerIsBetterAttribute"/> минимум.</summary>
    public static AbilityData Sup(AbilityData a, AbilityData b)
    {
        if (a == null) return b;
        if (b == null) return a;
        var copy = (AbilityData)a.MemberwiseClone();
        foreach (var f in Numbers(a.GetType()))
        {
            float x = Read(f, a), y = Read(f, b);
            Write(f, copy, f.IsDefined(typeof(LowerIsBetterAttribute)) ? Mathf.Min(x, y) : Mathf.Max(x, y));
        }
        return copy;
    }

    static readonly Dictionary<Type, FieldInfo[]> numbers = new();

    static FieldInfo[] Numbers(Type type)
    {
        if (numbers.TryGetValue(type, out var cached)) return cached;
        var list = new List<FieldInfo>();
        foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            if (f.FieldType == typeof(float) || f.FieldType == typeof(int)) list.Add(f);
        return numbers[type] = list.ToArray();
    }

    static float Read(FieldInfo f, object o) => f.FieldType == typeof(int) ? (int)f.GetValue(o) : (float)f.GetValue(o);

    static void Write(FieldInfo f, object o, float v)
    {
        if (f.FieldType == typeof(int)) f.SetValue(o, Mathf.RoundToInt(v));
        else f.SetValue(o, v);
    }
}

/// <summary>Число записи раскрывается экспрессией (закон органа). См. <see cref="AbilityData"/>.</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class ExpressedAttribute : Attribute { }

/// <summary>При дублях приёма лучшее — меньшее (перезарядка, замах, множитель сбива регена).</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class LowerIsBetterAttribute : Attribute { }

/// <summary>Обязательное число: ноль в ассете значит «приём молча не работает» — ловит тест ассетов.</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class MustBePositiveAttribute : Attribute { }
