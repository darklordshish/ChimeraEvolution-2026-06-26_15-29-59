using UnityEngine;

/// <summary>
/// Мост «предмет → удар» на теле игрока: держит раскладку рук, отдаёт разрешённый страйк,
/// тратит прочность за замах, тикает топливо/порчу. IsBackstab — выставляемый флаг
/// (TODO(чувства): истинный источник — стелс/агро; дефолт false = безопасный «в лоб»).
/// Пустой мост (нет main-дефа) — PlayerAttack идёт старым путём записи.
/// Лес, слайс s4b.
/// </summary>
public class HandsBridge : MonoBehaviour
{
    public readonly HandsLoadout loadout = new HandsLoadout();

    /// <summary>Мощь индивида (модификатор в damageMult, не печётся в int).</summary>
    public float power = 1f;

    /// <summary>Флаг засады снаружи. TODO(чувства): ставить из стелса/агро.</summary>
    public bool IsBackstab;

    public bool HasHands => loadout.main != null;

    public bool EquipMain(HandItemDef def) => loadout.EquipMain(def);
    public bool EquipOff(HandItemDef def) => loadout.EquipOff(def);
    public bool CanEquip(HandsLoadout.HandSlot slot, HandItemDef def) => loadout.CanEquip(slot, def);

    public HandStrike ResolveStrike()
    {
        var inst = loadout.main;
        if (inst == null) return HandStrikeResolver.Resolve(null, false, false, power);
        return HandStrikeResolver.Resolve(inst.def, inst.IsBroken, IsBackstab, power);
    }

    /// <summary>Прочность — за замах, не за попадание (предсказуемо для тестов и баланса).</summary>
    public void SpendSwing()
    {
        loadout.main?.Use();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        loadout.main?.Tick(dt);
        loadout.off?.Tick(dt);
    }
}
