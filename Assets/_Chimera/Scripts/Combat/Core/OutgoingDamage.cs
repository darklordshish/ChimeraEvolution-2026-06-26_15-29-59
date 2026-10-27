using UnityEngine;

/// <summary>
/// ИСХОДЯЩИЙ УРОН ДЕЛАТЕЛЯ — один множитель на все доставки: ярость × разброс особи × дух × вигор (истощённый голодом
/// слабее) × боссовость. Прежде его считала только `WindupAbility` (доставки NPC), а удар, укус, пинок, перекат и таран
/// игрока, перекат клубком ежа и удушение обхватом шли мимо (аудит дизайн-линии 26.10, §10 п.6): клич человека давал
/// ярость, которая повышала только ВХОДЯЩИЙ урон, — чистая цена без выгоды. Одна доставка — одни модификаторы, кто бы её
/// ни вёл: NPC или игрок. Сторож — `OutgoingDamageTests`.
/// </summary>
public static class OutgoingDamage
{
    public static float Mult(Component dealer)
    {
        if (dealer == null) return 1f;
        float m = 1f;
        if (dealer.TryGetComponent<Rage>(out var rage)) m *= rage.DamageMult;
        if (dealer.TryGetComponent<SpawnVariance>(out var variance)) m *= variance.DamageMult;
        if (dealer.TryGetComponent<Morale>(out var morale)) m *= morale.DamageMult;
        if (dealer.TryGetComponent<Satiety>(out var satiety)) m *= satiety.Vigor;
        if (dealer.TryGetComponent<Bossness>(out var bossness)) m *= bossness.DamageMult;
        return m;
    }

    /// <summary>Целый урон с множителем делателя (для доставок, что бьют числом, а не пайком `MeleeBlow`).</summary>
    public static int Apply(Component dealer, int damage) => damage <= 0 ? damage : Mathf.Max(1, Mathf.RoundToInt(damage * Mult(dealer)));
}
