using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// МОРАЛЬ — динамическая шкала духа СТАЙНЫХ (спека 2026-07-17): страх ↔ ярость, полюса одной оси.
/// Любое событие = вклад ±1 на stackLife секунд — различие только ЗНАКОМ (исключение: приказ вожака +5).
/// Мораль = сумма ЖИВЫХ вкладов (кламп ±clamp); затухания НЕТ — вклады истекают сами, шкала дышит.
/// Пороги от личности (Bravery): ≤ −порог — ПАНИКА (без таймера: бежит, ПОКА ЖИВ СТРАХ; вой сородича
/// может вытащить досрочно), ≥ +порог — КОММИТ на опасную добычу (M2: Rage-бафф + разрешение атаки).
/// Гистерезис — у порога не дёргается. Вешает психика стайного (волк); холоднокровным не место.
/// </summary>
public class Morale : MonoBehaviour
{
    [SerializeField] float stackLife = 10f; // время жизни вклада: «завыл — 10 секунд смелее»
    [SerializeField] int clampAbs = 5;      // кап шкалы ±5 (читаемость)
    [SerializeField] float hysteresis = 1f; // вошёл в состояние — выходит на 1 ниже порога входа

    struct Stack { public float value; public float until; }
    readonly List<Stack> stacks = new();
    float threshold = 3f; // личный порог (Bravery): паника ≤ −t, коммит ≥ +t
    bool routing, committed;

    /// <summary>Текущая мораль = сумма живых вкладов (истёкшие выпалываются по дороге).</summary>
    public float Current
    {
        get
        {
            float sum = 0f;
            for (int i = stacks.Count - 1; i >= 0; i--)
            {
                if (Time.time >= stacks[i].until) { stacks.RemoveAt(i); continue; }
                sum += stacks[i].value;
            }
            return Mathf.Clamp(sum, -clampAbs, clampAbs);
        }
    }

    /// <summary>ПАНИКА: пересёк −порог; отпускает с гистерезисом (страх истёк / вой поднял / Calm вожака).</summary>
    public bool IsRouting
    {
        get
        {
            float m = Current;
            routing = routing ? m <= -threshold + hysteresis : m <= -threshold;
            return routing;
        }
    }

    /// <summary>КОММИТ: дух выше +порога — разрешение на опасную добычу (M2 повесит сюда Rage-бафф).</summary>
    public bool IsCommitted
    {
        get
        {
            float m = Current;
            committed = committed ? m >= threshold - hysteresis : m >= threshold;
            return committed;
        }
    }

    /// <summary>Шкала −1..+1 (для индикации: морда — градиент страх↔натуральный↔ярость).</summary>
    public float Normalized => Current / clampAbs;

    public void SetThreshold(float t) => threshold = Mathf.Max(0.01f, t); // личный порог храбрости (личность)

    /// <summary>Вклад: обычно ±1 (единая арифметика), приказ вожака +5.</summary>
    public void Add(float value) => stacks.Add(new Stack { value = value, until = Time.time + stackLife });

    /// <summary>Вожак гасит панику: все МИНУС-вклады стёрты (плюсы живут дальше).</summary>
    public void Calm()
    {
        for (int i = stacks.Count - 1; i >= 0; i--)
            if (stacks[i].value < 0f) stacks.RemoveAt(i);
        routing = false;
    }
}
