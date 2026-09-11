using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Награда СУПЕРХИМЕРЫ (босс из коллег: человечность + сыворотка). За ПЕРВОЕ убийство ТИПА (typeId)
/// игрок получает +пул мутагена и химерный слот; повторные убийства того же типа дают только обычное
/// родство (его начисляет само тело — SuperBossReward к родству не касается). Реестр полученных
/// типов — статик на сессию (перманентность придёт с мета-сейвом).
/// Вешает модуль боссовости (`Bossness.Finish`) и через `Configure` задаёт, за какой тип и что давать. До 11.09
/// жил на префабе вервольфа; вид удалён, а награда за ТИП босса к нему и не была привязана — переехала как есть.
/// </summary>
[RequireComponent(typeof(Health))]
public class SuperBossReward : MonoBehaviour
{
    static readonly HashSet<string> claimed = new(); // типы, за которые награда уже выдана (сбрасывается при входе в Play)

    [SerializeField] string typeId = "Суперхимера";
    [SerializeField] int poolBonus = 4;
    [SerializeField] bool grantsChimeraSlot = true;

    void Awake() => GetComponent<Health>().onDeath.AddListener(Grant);

    /// <summary>Настройка модулем боссовости: за первое убийство КАКОГО босса и что дать.</summary>
    public void Configure(string type, int pool, bool slot) { typeId = type; poolBonus = pool; grantsChimeraSlot = slot; }

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
