using UnityEngine;

/// <summary>ДИСПАТЧ ПСИХИКИ (#4b-3): по ДОМИНАНТНОЙ идентичности (MostKin) тела вешает психику-модуль —
/// видовую (Волк/Змея/Лось/Ёж), а истинной химере и доминанте-Человек ХИМЕРУ-АЛЬФУ («затычка пробела в
/// круге психик»). Психика-компонент = маркер идентичности: тип-B чеки (GetComponentInParent&lt;XxxPsyche&gt;)
/// находят вид по нему. Роутер переиспользуем: позже его зовут боссовость (#4b-4) и обычная химеризация NPC.
/// Звать ПОСЛЕ сборки состава (MostKin валиден); сам пере-раздаёт статы новой психике (Refeed).</summary>
public static class PsycheDispatch
{
    /// <summary>ПОРОГ ПСИХИКИ — единственное место, где решается «доминанта достаточно своя, чтобы вести себя
    /// этим видом». Первичный диспатч и МЕТАМОРФОЗА обязаны спрашивать ЗДЕСЬ: иначе одно и то же тело вело бы
    /// себя по-разному в зависимости от того, родилось оно таким или домутировало (было: диспатч на Weak,
    /// метаморфоза на Medium — тело с доминантой 0.7 получало видовую психику при рождении и теряло её,
    /// едва домутировав через второй порог).
    /// <para>
    /// ВЫБРАН `<see cref="KinTier.Weak"/>` — «есть доминанта, и она в основном своя». Доводы:
    /// (1) альфа по замыслу — «затычка пробела в круге», для тех, кто НЕ ДОТЯНУЛ НИ ДО КОГО (спека 4b3:
    /// порог диспатча = Weak; `MostKin` сам возвращает null ниже Weak). Доминанта на 0.7 — уже не «никто»:
    /// по словам самого тела это «слабое признание, в основном свой, люфт на 1–2 примеси».
    /// (2) У босса СВОЙ порог слабого признания (`Bossness.kinWeakAt` = 0.501, решение 29.09) записан в
    /// tooltip как «он же … доминанта психики». При пороге Medium ручка босса на психику не влияла бы вовсе.
    /// (3) Метаморфоза переиспользует порог диспатча, а не заводит второй: гистерезис Medium (план 31.07)
    /// был защитой от дёрганья на 50/50, но Weak (0.65) уже даёт ту же защиту и не расходится с диспатчем.
    /// </para></summary>
    public const KinTier PsycheTier = KinTier.Weak;

    /// <summary>ДОМИНАНТА ДЛЯ ПСИХИКИ: вид, дотянувший до <see cref="PsycheTier"/>; null → химера-альфа
    /// (нет доминанты / она слаба / это Человек — человечьей NPC-психики нет). Единственный источник порога:
    /// им пользуются и <see cref="Attach"/>, и `CreatureBody.Recompute` (метаморфоза).</summary>
    public static SpeciesSO Dominant(CreatureBody body) => Dominant(body, out _);

    static SpeciesSO Dominant(CreatureBody body, out KinTier tier)
    {
        tier = KinTier.None;
        if (body == null) return null;
        var dom = body.MostKin(out tier);   // MostKin сам возвращает null ниже Weak — порог тела живёт в нём
        return tier >= PsycheTier ? dom : null;
    }

    /// <summary>Есть ли уже психика-модуль (любая из круга). Нужно рождению составом: метаморфоза могла
    /// повесить модуль прямо во время компоновки, и второй `Attach` дал бы ДВА модуля (Destroy отложен
    /// до конца кадра) — двойные атаки и двойной расход.</summary>
    public static bool HasPsyche(CreatureBody body)
    {
        if (body == null) return false;
        return body.GetComponent<WolfPsyche>() != null
            || body.GetComponent<SnakePsyche>() != null
            || body.GetComponent<MoosePsyche>() != null
            || body.GetComponent<HedgehogPsyche>() != null
            || body.GetComponent<ChimeraAlphaPsyche>() != null;
    }

    public static void Attach(CreatureBody body)
    {
        if (body == null) return;
        var dom = Dominant(body, out var tier);

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

        if (EvolutionConfig.Verbose) Debug.Log(dom != null
            ? $"диспатч: доминанта {dom.speciesName} ({tier}) → {psyche.Name}"
            : "диспатч: истинная химера (кин ни к кому) → химера-альфа");

        body.gameObject.AddComponent(psyche);
        body.Refeed(); // психика навешена ПОСЛЕ Recompute — пере-раздать урон/скорость (иначе OnBodyStats её не застал)
    }
}
