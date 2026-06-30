/// <summary>
/// Тот, кто может держать игрока в захвате. Игрок дёргает BreakFree, когда срывается рывком:
/// держащий получает урон и отпускает. Развязывает PlayerController и WolfAI.
/// </summary>
public interface IGrabber
{
    void BreakFree(int damage);
}
