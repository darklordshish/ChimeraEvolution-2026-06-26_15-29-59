/// <summary>
/// Экземпляр предмета: прочность (удары), порча и топливо (время). Сломанный —
/// это отсутствие предмета: резолвер откатывает к голым. Лес, слайс s4.
/// </summary>
public class HandItemInstance
{
    public readonly HandItemDef def;
    public int durabilityLeft;
    public float spoilLeft;
    public float fuelLeft;

    public HandItemInstance(HandItemDef def)
    {
        this.def = def;
        durabilityLeft = def != null ? def.durabilityMax : 0;
        spoilLeft = def != null ? def.spoilSeconds : 0f;
        fuelLeft = def != null ? def.fuelSeconds : 0f;
    }

    public bool IsBroken
    {
        get
        {
            if (def == null) return true;
            if (def.durabilityMax > 0 && durabilityLeft <= 0) return true;
            if (def.spoilSeconds > 0f && spoilLeft <= 0f) return true;
            if (def.fuelSeconds > 0f && fuelLeft <= 0f) return true;
            return false;
        }
    }

    /// <summary>Удар предметом. Вернёт false, если этим ударом сломался.</summary>
    public bool Use()
    {
        if (def == null || IsBroken) return false;
        if (def.durabilityMax > 0) durabilityLeft--;
        return !IsBroken;
    }

    /// <summary>Время идёт: порча трофея, горение факела. dt явный, без Time.*.</summary>
    public void Tick(float dt)
    {
        if (dt <= 0f || def == null) return;
        if (def.spoilSeconds > 0f) spoilLeft -= dt;
        if (def.fuelSeconds > 0f) fuelLeft -= dt;
    }
}
