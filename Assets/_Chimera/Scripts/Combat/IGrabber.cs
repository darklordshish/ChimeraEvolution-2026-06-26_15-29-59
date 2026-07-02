/// <summary>
/// Тот, кто может держать игрока в захвате. Игрок дёргает BreakFree, когда срывается рывком:
/// держащий получает урон и отпускает. Развязывает PlayerController и WolfPsyche.
/// </summary>
public interface IGrabber
{
    void BreakFree(int damage);
}
