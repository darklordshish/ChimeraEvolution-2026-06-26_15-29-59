/// <summary>
/// Раскладка рук: main/off. Двуручное занимает оба; факел только в off + одноручное.
/// Только данные и проверки — надевает предмет хук (s4b). Лес, слайс s4.
/// </summary>
public class HandsLoadout
{
    public enum HandSlot
    {
        Main,
        Off
    }

    public HandItemInstance main;
    public HandItemInstance off;

    public bool CanEquip(HandSlot slot, HandItemDef def)
    {
        if (def == null) return false;
        if (slot == HandSlot.Off)
        {
            if (def.twoHanded) return false;
            if (main != null && main.def != null && main.def.twoHanded) return false;
            return true;
        }
        if (def.kind == HandItemKind.Torch) return false;
        return true;
    }

    public bool EquipMain(HandItemDef def)
    {
        if (!CanEquip(HandSlot.Main, def)) return false;
        main = new HandItemInstance(def);
        if (def.twoHanded) off = null;
        return true;
    }

    public bool EquipOff(HandItemDef def)
    {
        if (!CanEquip(HandSlot.Off, def)) return false;
        off = new HandItemInstance(def);
        return true;
    }
}
