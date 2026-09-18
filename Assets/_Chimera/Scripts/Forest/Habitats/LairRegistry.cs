using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Реестр логовищ: суммарные капы по виду и ближайшее логово-дом. Без статики —
/// инстанс живёт у менеджера ареалов (s2b), тесты создают свой. Лес, слайс s2.
/// </summary>
public class LairRegistry : MonoBehaviour
{
    readonly List<LairSite> sites = new List<LairSite>();

    public int Count => sites.Count;

    public void Register(LairSite site)
    {
        if (site != null && !sites.Contains(site)) sites.Add(site);
    }

    public void Unregister(LairSite site)
    {
        sites.Remove(site);
    }

    public int TotalCapacity(string speciesName)
    {
        int total = 0;
        foreach (var s in sites)
            if (s != null && s.speciesName == speciesName) total += s.capacity;
        return total;
    }

    /// <summary>Ближайшее логово вида — точка возврата домой. Нет логова — null.</summary>
    public LairSite NearestHome(string speciesName, Vector3 pos)
    {
        LairSite best = null;
        float bestDist = float.MaxValue;
        foreach (var s in sites)
        {
            if (s == null || s.speciesName != speciesName) continue;
            float d = Vector3.Distance(s.transform.position, pos);
            if (d < bestDist)
            {
                bestDist = d;
                best = s;
            }
        }
        return best;
    }
}
