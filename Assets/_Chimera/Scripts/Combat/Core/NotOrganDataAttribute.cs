using System;

/// <summary>
/// ЧИСЛО НЕ ИЗ ОРГАНА — ОСОЗНАННО. Помечает сериализованное поле носителя приёма, которое по решению
/// 12.09 законно живёт у индивида, а не в записи органа: ощущение управления (тряска камеры, хитстоп),
/// физика тела, отладочная отрисовка. Причина обязательна: детектор `OrganDataTests` пропускает поле
/// только с ней, поэтому «почему это не данные органа» написано у самого поля, а не в перечне
/// исключений где-то в тесте.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class NotOrganDataAttribute : Attribute
{
    public string Reason { get; }
    public NotOrganDataAttribute(string reason) => Reason = reason;
}
