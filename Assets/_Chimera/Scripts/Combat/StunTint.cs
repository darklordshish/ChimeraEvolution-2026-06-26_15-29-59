using UnityEngine;

/// <summary>
/// Единый СТАТУС-сигнал «выключен из боя»: пока существо в СТАНЕ (вой-стан, обхват игрока — любой
/// источник), тело белёсое (TelegraphColors.Stunned). Раздел «статусы» единой цветовой легенды:
/// цвет = СВОЁ действие (телеграф) или СВОЁ состояние — никогда не действия других по тебе.
/// Короткий стаггер (&lt;1с, от попаданий) НЕ красит — для него уже есть вспышка HitFlash.
/// Стан рвёт замахи, поэтому с телеграфом приёмов не дерётся. Вешается психиками (как ScentTrail).
/// </summary>
[RequireComponent(typeof(Stagger))]
public class StunTint : MonoBehaviour
{
    Stagger stagger;
    Telegraph telegraph;
    bool painting;

    void Awake()
    {
        stagger = GetComponent<Stagger>();
        if (!TryGetComponent(out telegraph)) telegraph = gameObject.AddComponent<Telegraph>();
    }

    void Update()
    {
        bool stunned = stagger.IsStunned;
        if (stunned && !telegraph.IsShowing) { telegraph.Set(true, TelegraphColors.Stunned); painting = true; }
        else if (!stunned && painting) { telegraph.Clear(); painting = false; }
    }
}
