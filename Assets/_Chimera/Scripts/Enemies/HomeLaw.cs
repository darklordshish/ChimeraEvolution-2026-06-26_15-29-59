using UnityEngine;

/// <summary>
/// ДОМ ВЫБИРАЕТ ПСИХИКА (спека «логово по законам» §4.2). Закон один для всех существ с психикой: дом — ближайшее
/// подходящее логово вида-ДОМИНАНТЫ тела (`MostKin`, то же, по чему `PsycheDispatch` выбирает психику); занимается
/// `LairSite.Claim(body)`, отпускается `Leave(body)`. Подходит логово с тем же `speciesName`, где есть место.
/// Родной дом доминанты остаётся (не прыгаем без нужды); сменилась доминанта (`Metamorph` → `Attach`) — старый
/// отпускается, новый берётся. Веток «если химера» нет: химера с психикой альфы (доминанты нет или Человек) по этому же
/// правилу не находит логова своего вида и остаётся бездомной. ПО УМОЛЧАНИЮ, ЖДЁТ РЕШЕНИЯ ГЕЙМДИЗАЙНЕРА.
/// </summary>
public static class HomeLaw
{
    public static bool Suits(LairSite site, SpeciesSO dominant)
        => site != null && dominant != null && site.speciesName == dominant.speciesName;

    /// <summary>Ближайшее подходящее логово вида доминанты, где есть место (<paramref name="body"/> своё место не отнимает).</summary>
    public static LairSite Nearest(CreatureBody body, SpeciesSO dominant)
    {
        LairSite best = null;
        float bestSq = float.MaxValue;
        foreach (var s in Object.FindObjectsByType<LairSite>())
        {
            if (!Suits(s, dominant)) continue;
            bool mine = body.home == s;
            if (!mine && s.IsFull) continue;
            float d = (s.transform.position - body.transform.position).sqrMagnitude;
            if (d < bestSq) { bestSq = d; best = s; }
        }
        return best;
    }

    /// <summary>Пересмотреть дом: подходящий текущий оставить, иначе отпустить и занять ближайшее подходящее.</summary>
    public static void Rehome(CreatureBody body)
    {
        if (body == null) return;
        var dom = body.MostKin(out _);
        if (Suits(body.home, dom)) return;
        if (body.home != null) body.home.Leave(body);
        var site = Nearest(body, dom);
        if (site != null) site.Claim(body);
    }
}
