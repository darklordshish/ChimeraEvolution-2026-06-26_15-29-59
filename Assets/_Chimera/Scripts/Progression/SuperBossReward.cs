using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Награда СУПЕРХИМЕРЫ (босс из коллег: человечность + сыворотка). За ПЕРВОЕ убийство ТИПА (typeId)
/// игрок получает +пул мутагена и химерный слот; повторные убийства того же типа дают только обычное
/// родство (его начисляет само тело — SuperBossReward к родству не касается). Реестр полученных
/// типов — статик на сессию (перманентность придёт с мета-сейвом).
/// НОСИТЕЛЯ СЕЙЧАС НЕТ. Жил на префабе вервольфа, удалённом 11.09 («он не нужен как выделенная единица»):
/// босс-суперхимера будет собираться из конструктора. Компонент оставлен — награда за ТИП босса не
/// привязана к вервольфу, и тот, кто соберёт химеру-босса, повесит его заново.
/// </summary>
[RequireComponent(typeof(Health))]
public class SuperBossReward : MonoBehaviour
{
    static readonly HashSet<string> claimed = new(); // типы, за которые награда уже выдана (сбрасывается при входе в Play)

    [SerializeField] string typeId = "Суперхимера";
    [SerializeField] int poolBonus = 4;
    [SerializeField] bool grantsChimeraSlot = true;

    void Awake() => GetComponent<Health>().onDeath.AddListener(Grant);

    void Grant()
    {
        if (!claimed.Add(typeId)) return; // тип уже побеждён — только родство (его даёт тело)

        var pc = FindAnyObjectByType<PlayerController>();
        var body = pc != null ? pc.GetComponent<CreatureBody>() : null;
        if (body == null) return;

        if (poolBonus > 0) body.ExpandPool(poolBonus);
        if (grantsChimeraSlot) body.GrantChimeraSlot();
        Debug.Log($"СУПЕРХИМЕРА [{typeId}] повержена впервые: +{poolBonus} к пулу мутагена, +1 химерный слот."); // звук/UI-фанфары позже
    }
}
