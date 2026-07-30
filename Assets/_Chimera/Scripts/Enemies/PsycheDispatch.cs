using UnityEngine;

/// <summary>ДИСПАТЧ ПСИХИКИ (#4b-3): по ДОМИНАНТНОЙ идентичности (MostKin) тела вешает психику-модуль —
/// видовую (Волк/Змея/Лось/Ёж), а истинной химере и доминанте-Человек ХИМЕРУ-АЛЬФУ («затычка пробела в
/// круге психик»). Психика-компонент = маркер идентичности: тип-B чеки (GetComponentInParent&lt;XxxPsyche&gt;)
/// находят вид по нему. Роутер переиспользуем: позже его зовут боссовость (#4b-4) и обычная химеризация NPC.
/// Звать ПОСЛЕ сборки состава (MostKin валиден); сам пере-раздаёт статы новой психике (Refeed).</summary>
public static class PsycheDispatch
{
    public static void Attach(CreatureBody body)
    {
        if (body == null) return;
        var dom = body.MostKin(out var tier);

        // ДОМИНАНТА РЕШАЕТ ВИД: психика-компонент = маркер идентичности (тип-B чеки находят его по нему).
        // Человек (NPC-психики нет) и истинная химера (dom==null) → ХИМЕРА-АЛЬФА («затычка пробела в круге»)
        System.Type psyche = dom?.speciesName switch
        {
            "Волк" => typeof(WolfPsyche),
            "Змея" => typeof(SnakePsyche),
            "Лось" => typeof(MoosePsyche),
            "Ёж"   => typeof(HedgehogPsyche),
            _      => typeof(ChimeraAlphaPsyche),
        };

        Debug.Log(dom != null
            ? $"диспатч: доминанта {dom.speciesName} ({tier}) → {psyche.Name}"
            : "диспатч: истинная химера (кин ни к кому) → химера-альфа");

        body.gameObject.AddComponent(psyche);
        body.Refeed(); // психика навешена ПОСЛЕ Recompute — пере-раздать урон/скорость (иначе OnBodyStats её не застал)
    }
}
