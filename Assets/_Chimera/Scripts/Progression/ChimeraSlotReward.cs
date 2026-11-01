using UnityEngine;

/// <summary>
/// НАГРАДА ЗА ПОЛНОЕ МАСТЕРСТВО: когда родство игрока со ВСЕМИ донор-видами достигает порога (дефолт 100 = кап),
/// РАЗОВО открывается химерный слот — универсальное гнездо под любой орган («мета: награда суперхимеры»).
/// Освоил лес полностью → свобода сборки. Положи на объект в сцене. Разово: повторно не выдаёт (флаг живёт на забег).
/// ⚠ В СЦЕНЕ СЕЙЧАС НЕ ЛЕЖИТ — награда не выдаётся. Химерный слот игроку сейчас даёт первое убийство босса
/// (`SuperBossReward`, его вешает модуль боссовости).
/// </summary>
public class ChimeraSlotReward : MonoBehaviour
{
    [SerializeField] int triggerAffinity = 100; // порог по КАЖДОМУ донор-виду (дефолт = кап родства)
    [SerializeField] int slotsToGrant = 1;      // сколько химерных слотов открыть по достижении

    // ГОЧА НОВОГО ПОЛЯ: у компонента, уже лежащего в сцене, SerializeField приходит НУЛЁМ (инициализатор
    // применяется только к свежесозданным). Ноль здесь — «не настроено», а не порог: иначе награда выдавалась
    // бы на первом кадре (родство ≥ 0 у всех) и слот тратился впустую.
    int TriggerAffinity => triggerAffinity > 0 ? triggerAffinity : 100;
    int SlotsToGrant => slotsToGrant > 0 ? slotsToGrant : 1;

    bool granted;
    bool warnedNoDonors;

    void Update()
    {
        if (granted) return;
        var pb = CreatureBody.PlayerBody; // родство локальное — читаем тело игрока
        if (pb == null) return;
        if (pb.Donors == null || pb.Donors.Length == 0)
        {
            // Без доноров награда недостижима В ПРИНЦИПЕ (нечего осваивать) — молчать нельзя: это настройка,
            // а не «ещё не накопил». Говорим один раз, чтобы пустой список не читался как рабочий.
            if (!warnedNoDonors)
            {
                warnedNoDonors = true;
                Debug.LogWarning("ChimeraSlotReward: у тела игрока пуст список доноров — награда недостижима. " +
                                 "Заполни donors у тела игрока в сцене.", this);
            }
            return;
        }
        if (!pb.AllDonorsAffinityAtLeast(TriggerAffinity)) return;
        int slots = SlotsToGrant;
        for (int i = 0; i < slots; i++) pb.GrantChimeraSlot();
        granted = true;
        Debug.Log($"ChimeraSlotReward: родство ≥{TriggerAffinity} по ВСЕМ донорам → открыт химерный слот (+{slots})");
    }
}
