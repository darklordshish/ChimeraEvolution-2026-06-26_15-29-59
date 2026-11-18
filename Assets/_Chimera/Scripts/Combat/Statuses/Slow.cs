using Chimera.Laws;
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

    StackState state; // числа — `LawsConfig.slow`, шаги — `StatusLaw`

    public int Stacks => StatusLaw.Stacks(state, Time.time);

    /// <summary>Множитель скорости: 1 — свободен, меньше — увяз. Читают локомоции.</summary>
    public float MoveMult => StatusLaw.SlowMult(Stacks, LawsConfig.Current.slow);

    public void AddStack()
    {
        var p = LawsConfig.Current.slow;
        StatusLaw.AddStack(ref state, Time.time, p.MaxStacks, p.StackDuration, 1f);
    }
}
