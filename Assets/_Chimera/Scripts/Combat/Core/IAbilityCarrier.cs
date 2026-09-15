using System;

/// <summary>
/// НОСИТЕЛЬ ПРИЁМА: компонент, который исполняет способность органа — доставка NPC, грань игрока,
/// стадийная машина. Решение 12.09 «данные в органах»: ВСЕ числа приёма живут в записи органа
/// (<see cref="AbilityData"/>), у носителя — только поля с <see cref="NotOrganDataAttribute"/>.
/// Отдельно от <see cref="IAbility"/>: `TryUse` есть не у всех носителей — модификаторы рывка
/// (`PlayerCharge`, `PlayerRoll`) и машины (`Constrict`, `CurlDefense`) несут числа приёма, но так
/// не вызываются. Потребитель — детектор `OrganDataTests`.
/// </summary>
public interface IAbilityCarrier { }

/// <summary>
/// НОСИТЕЛЬ, ПЕРЕЕХАВШИЙ НА ЗАПИСЬ ОРГАНА. Тело (`CreatureBody.ProvisionAbilities`) находит носителя по
/// `DataType` и кормит раскрытой записью; нет записи — `Configure(null)`, приём недоступен, компонент остаётся
/// (на него держат ссылки психика и драйвер ввода). Носители, ещё не переехавшие по задачам спеки, реализуют
/// только <see cref="IAbilityCarrier"/> — тело их не трогает.
/// </summary>
public interface IOrganAbility : IAbilityCarrier
{
    /// <summary>Тип записи, которой кормится носитель.</summary>
    Type DataType { get; }

    /// <summary>Раскрытая запись тела; null — у тела этого приёма нет.</summary>
    void Configure(AbilityData data);

    /// <summary>Есть запись — приём можно применить.</summary>
    bool Available { get; }
}
