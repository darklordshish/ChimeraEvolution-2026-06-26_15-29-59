using UnityEngine;

/// <summary>МЕТАМОРФОЗА (эволюция NPC): доминанта тела переползла (Medium+) → снять старую психику и повесить
/// новую по составу (PsycheDispatch). Шасси-якорь: физически тело не меняется — меняется поведение/узнавание
/// (волк, обожравшийся ежей, ведёт себя как ёж). Тонкий слушатель: тело про психики не знает, только эмитит
/// onDominantChanged. Смена психики сбрасывает её состояние (стаю/цели) — это и есть перерождение.</summary>
public class Metamorph : MonoBehaviour
{
    CreatureBody body;

    void Awake()
    {
        body = GetComponent<CreatureBody>();
        if (body != null) body.onDominantChanged += Remorph;
    }

    void OnDestroy() { if (body != null) body.onDominantChanged -= Remorph; }

    void Remorph(SpeciesSO dom)
    {
        // ЧИСТОЕ ПЕРЕРОЖДЕНИЕ: освободить активный обхват (Constrict) — иначе новая психика унаследует грязную
        // машину (Stage>0 без валидной жертвы) → рассинхрон/NRE (см. SnakePsyche.UpdateConstrict)
        if (TryGetComponent<Constrict>(out var cm)) cm.End();

        // снять ВСЕ текущие психики-модули (видовые + альфа), затем повесить одну по актуальной доминанте
        foreach (var p in GetComponents<MonoBehaviour>())
            if (p is WolfPsyche or SnakePsyche or MoosePsyche or HedgehogPsyche or ChimeraAlphaPsyche) Destroy(p);
        PsycheDispatch.Attach(body); // повесит по доминанте (+ Refeed раздаст статы новой психике)
        Debug.Log($"метаморфоза: {name} → доминанта {(dom != null ? dom.speciesName : "химера")}");
    }
}
