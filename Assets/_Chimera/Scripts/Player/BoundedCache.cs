using System;
using System.Collections.Generic;

/// <summary>КЭШ С ПОТОЛКОМ: вытесняет давно не использованное (LRU). У тела три кэша по содержимому — оболочки поля
/// (`BoneMesher`), составные тела (`ChainSwap`), запечённые детали (`MorphBuilder`) — и все росли без предела: состав
/// особей комбинаторен, а с гротеском и разбросом особи (спека двух слоёв §6) станет почти уникальным. Консилиум формы
/// 01.10 (геометр) назвал это второй стеной после числа рендереров. Вытесненное не уничтожается насильно, если на него
/// ещё смотрят живые рендереры: `onEvict` решает сам.</summary>
public sealed class BoundedCache<TKey, TValue>
{
    readonly int capacity;
    readonly Action<TValue> onEvict;
    readonly Dictionary<TKey, LinkedListNode<(TKey key, TValue value)>> map = new();
    readonly LinkedList<(TKey key, TValue value)> order = new();   // голова — самое свежее

    public BoundedCache(int capacity, Action<TValue> onEvict = null)
    {
        this.capacity = Math.Max(1, capacity);
        this.onEvict = onEvict;
    }

    public int Count => map.Count;

    public bool TryGetValue(TKey key, out TValue value)
    {
        if (map.TryGetValue(key, out var node))
        {
            order.Remove(node);
            order.AddFirst(node);
            value = node.Value.value;
            return true;
        }
        value = default;
        return false;
    }

    public TValue this[TKey key]
    {
        set
        {
            if (map.TryGetValue(key, out var old)) { order.Remove(old); map.Remove(key); }
            map[key] = order.AddFirst((key, value));
            while (map.Count > capacity)
            {
                var last = order.Last;
                order.RemoveLast();
                map.Remove(last.Value.key);
                onEvict?.Invoke(last.Value.value);
            }
        }
    }

    public void Clear() { map.Clear(); order.Clear(); }
}
