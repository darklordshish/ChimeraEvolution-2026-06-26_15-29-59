using UnityEngine;

/// <summary>
/// Статус «схвачен» (хвост игрока / кольца змеи / будущий богомол) — ЕДИНАЯ механика захвата.
/// Правила: прошедший захват/укрепление бьёт коротким импульс-стагером; дальше жертва ДЕЙСТВУЕТ
/// по силе хвата — слабый хват (не Locked) = с места не уйти, но бьёшь схватившего; защёлк (Locked) =
/// полный контроль (непрерывный стан держит сам статус → StunTint красит «выключенным»).
/// Компонент-статус: хватающий пишет (Apply/Release), психика жертвы опрашивает. Как Venom/Rage.
/// </summary>
public class Grabbed : MonoBehaviour
{
    public Health Grabber { get; private set; } // кто держит (null = свободен)
    public int Stage { get; private set; }      // сила хвата: 1 = слабый, 2+ = защёлк
    public bool Locked { get; private set; }    // защёлкнуто — жертва полностью в контроле
    public bool IsHeld => Grabber != null;

    Stagger stagger;

    /// <summary>Зафиксировать/укрепить хват. НОВАЯ ступень силы бьёт импульс-стагером — единое правило
    /// «прошедший захват/укрепление даёт небольшой стагер всем».</summary>
    public static Grabbed Apply(GameObject victim, Health grabber, int stage, bool locked)
    {
        var g = victim.GetComponent<Grabbed>();
        if (g == null) g = victim.AddComponent<Grabbed>();
        bool strengthened = grabber != null && (g.Grabber != grabber || stage > g.Stage);
        g.Grabber = grabber; g.Stage = stage; g.Locked = locked;
        if (strengthened)
        {
            if (g.stagger == null) g.TryGetComponent(out g.stagger);
            if (g.stagger != null) g.stagger.Hitstun(0.35f);
        }
        return g;
    }

    public void Release() { Grabber = null; Stage = 0; Locked = false; }

    void Update()
    {
        // защёлк = продлеваемый стан (психики его уважают через IsStaggered, StunTint красит статус-цветом)
        if (!IsHeld || !Locked) return;
        if (stagger == null && !TryGetComponent(out stagger)) return;
        stagger.Stun(0.3f);
    }
}
