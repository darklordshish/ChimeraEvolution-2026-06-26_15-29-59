using UnityEngine;

/// <summary>
/// НАГРАДА ЗА ПОЛНОЕ МАСТЕРСТВО: когда родство игрока со ВСЕМИ донор-видами достигает порога (дефолт 100 = кап),
/// РАЗОВО открывается химерный слот — универсальное гнездо под любой орган («мета: награда суперхимеры»).
/// Порог 75 (босс-суперхимера) — у WerewolfSpawner; это следующая ступень: освоил лес полностью → свобода сборки.
/// Положи на объект в сцене (удобно — рядом с WerewolfSpawner). Разово: повторно не выдаёт (флаг живёт на забег).
/// </summary>
public class ChimeraSlotReward : MonoBehaviour
{
    [SerializeField] int triggerAffinity = 100; // порог по КАЖДОМУ донор-виду (дефолт = кап родства)
    [SerializeField] int slotsToGrant = 1;      // сколько химерных слотов открыть по достижении

    bool granted;

    void Update()
    {
        if (granted) return;
        var pb = CreatureBody.PlayerBody; // родство локальное — читаем тело игрока
        if (pb == null || !pb.AllDonorsAffinityAtLeast(triggerAffinity)) return;
        for (int i = 0; i < slotsToGrant; i++) pb.GrantChimeraSlot();
        granted = true;
        Debug.Log($"ChimeraSlotReward: родство ≥{triggerAffinity} по ВСЕМ донорам → открыт химерный слот (+{slotsToGrant})");
    }
}
