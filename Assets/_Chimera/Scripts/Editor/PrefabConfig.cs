using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-хелпер генераторов префабов: выставляет приватные [SerializeField]-поля компонента ПО ИМЕНАМ
/// (через SerializedObject — обычным кодом к ним не достучаться). Так генератор задаёт числа доставок:
/// одна доставка (BiteAbility/LeapAbility/ChargeAbility…) — разные носители со своими значениями.
/// Жил в WerewolfPrefab, хотя им пользуются ВСЕ генераторы — вынесен в свой класс (C1-аудит, пункт F).
/// </summary>
public static class PrefabConfig
{
    public static void Set(Component c, params (string field, object value)[] values)
    {
        var so = new SerializedObject(c);
        foreach (var (field, value) in values)
        {
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"PrefabConfig: поле {field} не найдено на {c.GetType().Name}"); continue; }
            if (value is float f) p.floatValue = f;
            else if (value is int i) p.intValue = i;
            else if (value is bool b) p.boolValue = b;
            else if (value is string s) p.stringValue = s;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
