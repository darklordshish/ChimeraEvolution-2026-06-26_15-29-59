using UnityEngine;

/// <summary>
/// ПЕРЕВАРИВАНИЕ — физиология змеиного ШАССИ (флаг digestion на «Теле-хвосте», chassisOnly: аугументом
/// не крадётся; вешает CreatureBody по сборке, как ColdBlooded/Camouflage). Убил добычу СВОИМ оружием
/// (кольца/клыки/яд) — тело жертвы само зовёт OnAte через канал «родство — убийце» (CreditKiller) →
/// СЫТ: бонус-реген поверх вне-боевого, пока HP не полное; урон прерывает (стресс сбрасывает трапезу).
/// ЧТО ДЕЛАТЬ с сытостью — решает носитель: психика змеи прячется на насест, игрок волен сам.
/// Тело = данные/физиология, психика = тонкий код поведения.
/// </summary>
[RequireComponent(typeof(Health))]
public class Digestion : MonoBehaviour
{
    [SerializeField] float bonusRegen = 1f; // + к вне-боевому регену на переваривании (итого у природной змеи ~2 HP/с)

    Health health;
    float acc; // дробный аккумулятор лечения (Heal целочислен)

    public bool IsDigesting { get; private set; }

    void Awake()
    {
        health = GetComponent<Health>();
        health.onDamaged.AddListener(() => IsDigesting = false); // урон будит: переваривание прервано
    }

    /// <summary>Наша добыча убита нашим оружием — съедена (зовёт CreditKiller умирающего тела).</summary>
    public void OnAte() { if (health != null && health.Current < health.Max) IsDigesting = true; }

    void Update()
    {
        if (!IsDigesting) return;
        if (health.Current >= health.Max) { IsDigesting = false; acc = 0f; return; } // переварил — голод вернёт охоту
        acc += bonusRegen * Time.deltaTime;
        if (acc >= 1f) { int h = (int)acc; acc -= h; health.Heal(h); }
    }
}
