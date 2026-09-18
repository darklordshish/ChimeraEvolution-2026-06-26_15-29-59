/// <summary>
/// Вид ручного предмета. T0 голые (не предмет, а отсутствие), T1 лес, T2 кость, T3 трофей.
/// Силки/ловушки — не здесь (отложенный Constrict, s4b). Лес, слайс s4.
/// </summary>
public enum HandItemKind
{
    Bare,
    Club,
    Spear,
    Torch,
    BoneKnife,
    BoneSpear,
    ChimeraTrophy
}
