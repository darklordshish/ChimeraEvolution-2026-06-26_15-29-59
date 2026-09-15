using System;

/// <summary>
/// ЗАПИСЬ СПОСОБНОСТИ ОРГАНА (спека `2026-09-12-dannye-v-organah.md`). На каждый приём — свой
/// наследник со СВОИМИ числами, и силы, и формы удара. Лежит в `Organ.abilities` через
/// `[SerializeReference]`, поэтому инспектор показывает ровно поля этого приёма. Наличие записи и есть
/// включатель: флаг `enablesX` уходит, как только его приём переезжает в запись.
///
/// ГОЧА: `[SerializeReference]` хранит в ассете ИМЯ класса. Переименование наследника молча обнуляет
/// все его записи — переименовывать только вместе с `[UnityEngine.Scripting.APIUpdating.MovedFrom]`.
/// </summary>
[Serializable]
public abstract class AbilityData { }
