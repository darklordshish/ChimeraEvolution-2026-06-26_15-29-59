using UnityEngine;

/// <summary>
/// SO-обёртка plain-дефа предмета для инспектора (ассеты — позже, руками в Unity).
/// Лес, слайс s4b.
/// </summary>
[CreateAssetMenu(menuName = "Chimera/Forest/HandItem", fileName = "HandItem")]
public class HandItemDefSO : ScriptableObject
{
    public HandItemDef def = new HandItemDef();
}
