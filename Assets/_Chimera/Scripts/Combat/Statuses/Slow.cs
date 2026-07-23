using UnityEngine;

/// <summary>
/// ЗАМЕДЛЕНИЕ — накопительный статус (эталон — Bleed/Venom): каждый стак роняет скорость на фикс-долю,
/// стаки тают без подпитки. Носитель — залп игл ежа: «иглы в теле тянут вниз, ты — подушечка для булавок».
///
/// ЗАЧЕМ СТАКАМИ, А НЕ ФЛАГОМ: глубина замедления = число попавших игл. Полный залп в упор роняет добычу
/// НИЖЕ скорости медлительного ежа → он догоняет и хватает; скользящий залп издали чуть тормозит — ушла.
/// Само-балансится дистанцией и углом, без прицеливания.
///
/// Читается ЕДИНОЙ ТОЧКОЙ движения: у NPC — `NavLocomotion.Move` (один хук на всех), у игрока —
/// `PlayerController`. Универсален: тем же компонентом придёт аугумент-иглы игроку, и будущие тормозящие
/// эффекты (лёд/смола/паутина) лягут сюда же.
/// </summary>
public class Slow : MonoBehaviour
{
    [SerializeField, Range(0f, 0.5f)] float perStack = 0.14f; // сколько скорости снимает ОДИН стак
    [SerializeField, Range(0f, 0.95f)] float maxSlow = 0.7f;  // потолок: даже полный залп не роняет в ноль
    [SerializeField] int maxStacks = 6;
    [SerializeField] float stackDuration = 2.5f;              // сколько живёт стак без подпитки

    int stacks;
    float expireAt;

    public int Stacks => Time.time < expireAt ? stacks : 0;

    /// <summary>Множитель скорости: 1 — свободен, меньше — увяз. Читают локомоции.</summary>
    public float MoveMult => 1f - Mathf.Min(maxSlow, Stacks * perStack);

    public void AddStack()
    {
        stacks = Mathf.Min(maxStacks, Stacks + 1);
        expireAt = Time.time + stackDuration;
    }
}
