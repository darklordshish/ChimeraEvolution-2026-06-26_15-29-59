using UnityEngine;

/// <summary>ДИСПАТЧ ПСИХИКИ (#4b-2): по ДОМИНАНТНОЙ идентичности тела вешает психику-модуль. Пока
/// тривиальный — истинная химера и доминантные равно получают ХИМЕРУ-АЛЬФУ; доминантным логируем задел
/// (#4b-3 повесит их видовой модуль). Роутер переиспользуем: позже его зовут боссовость (#4b-4) и обычная
/// химеризация NPC. Звать ПОСЛЕ сборки состава (MostKin валиден); сам пере-раздаёт статы новой психике.</summary>
public static class PsycheDispatch
{
    public static void Attach(CreatureBody body)
    {
        if (body == null) return;
        var dom = body.MostKin(out var tier);
        if (dom != null)
            Debug.Log($"диспатч: доминанта {dom.speciesName} ({tier}) → видовой модуль (TODO #4b-3), пока альфа");
        else
            Debug.Log("диспатч: истинная химера (кин ни к кому) → химера-альфа");
        body.gameObject.AddComponent<ChimeraAlphaPsyche>();
        body.Refeed(); // психика навешена ПОСЛЕ Recompute — пере-раздать урон/скорость (иначе OnBodyStats её не застал)
    }
}
