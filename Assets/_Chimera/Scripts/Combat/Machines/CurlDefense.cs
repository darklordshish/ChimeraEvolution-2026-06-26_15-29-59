using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// КЛУБОК ежа (слайс C) — оборонительная стойка последнего рубежа + КАТАНИЕ под давлением.
///
/// C1 КЛУБОК: свернулся → БРОНЯ↑, залп молчит, ЖЖЁТ СТАМИНУ (`Drain`). Клубок про броню и цену, не про
/// ответку — иглы (`Thorns`) и так наказывают удар в упор. Грабо-иммунитета нет намеренно (урон от жертвы
/// расшатывает `Constrict`, иглы возвращают урон+кровь — кто вцепился, гибнет сам).
///
/// C2 КАТАНИЕ: ПРОДАВИЛИ (дыхалка на исходе) → шар едет ТАРАНОМ сквозь угрозу, ПОДРУЛИВАЯ к ней
/// (малоуправляемо), задевая всех на пути (урон + `Bleed` + `Knockback`). Броня клубка держится в катании
/// сама. Жжёт стамину быстрее → выдох → окно «на спине» (C3). Катание — та же машина, не отдельный приём.
///
/// РЕЖИМ, не приём: психика (лестница отчаяния, слайс D) решает КОГДА свернуться и КОГДА катить.
/// </summary>
[RequireComponent(typeof(Health))]
public class CurlDefense : MonoBehaviour
{
    [Header("Клубок")]
    [SerializeField, Range(0f, 0.9f)] float curlArmor = 0.6f; // броня в клубке (МАКСИМУМ с базовой, не сумма)
    [SerializeField] float staminaDrain = 30f;                // жжём бак пока свёрнуты — быстро, иначе катание не успевает проявиться (бой короче)

    [Header("Катание (продавили клубок)")]
    [SerializeField] float rollSpeed = 9f;
    [SerializeField] float rollDrain = 40f;      // жжёт БЫСТРЕЕ клубка — прорыв дорог, потому и последний рывок
    [SerializeField] float rollTurnSpeed = 90f;  // МАЛОУПРАВЛЯЕМО: медленный доворот к цели (град/с)
    [SerializeField] int rollDamage = 10;
    [SerializeField] int rollBleed = 1;          // иглы протыкают на прокате
    [SerializeField] float rollKnock = 8f;
    [SerializeField] float rollRadius = 1.2f;    // ширина проката (кого задевает шар)
    [SerializeField] float rollGravity = 20f;    // прижатие к земле в катании (двигаем controller напрямую)

    public bool Curled { get; private set; }
    public bool Rolling { get; private set; }

    Health health;
    Stamina stamina;
    // ЛЕНИВО: бак до-создаёт ТЕЛО (CreatureBody.Recompute) ПОЗЖЕ нашего Awake — привязка в Awake поймала бы null,
    // и клубок молча не жёг бы стамину (катание не наступало). Та же причина, что у Breath в психике
    Stamina Breath { get { if (stamina == null) TryGetComponent(out stamina); return stamina; } }
    Telegraph telegraph;
    CharacterController controller;
    float baseArmor;
    Vector3 rollDir;
    readonly HashSet<Health> rolledThis = new();

    void Awake()
    {
        health = GetComponent<Health>();
        TryGetComponent(out telegraph);
        TryGetComponent(out controller);
    }

    /// <summary>Свернуться: поднять броню (не ниже базовой), зажечь телеграф клубка. Идемпотентно.</summary>
    public void Curl()
    {
        if (Curled) return;
        Curled = true;
        baseArmor = health.DamageReduction;
        health.DamageReduction = Mathf.Max(baseArmor, curlArmor);
        if (telegraph != null) telegraph.Set(true, TelegraphColors.Curl); // факт-статус (не намерение) — виден без Чутья
    }

    /// <summary>Развернуться: вернуть базовую броню и рест-вид. Идемпотентно.</summary>
    public void Uncurl()
    {
        if (!Curled) return;
        Curled = false;
        Rolling = false;
        health.DamageReduction = baseArmor;
        if (telegraph != null) telegraph.Set(false, TelegraphColors.Curl);
    }

    /// <summary>Держим клубок (не катясь): жжём стамину. Выдохся → развернулись, false психике.</summary>
    public bool Hold()
    {
        if (!Curled || Rolling) return false;
        var s = Breath;
        if (s != null)
        {
            s.Drain(staminaDrain * Time.deltaTime);
            if (s.Exhausted) { Uncurl(); return false; }
        }
        return true;
    }

    /// <summary>КАТАНИЕ: едем тараном в шаре, ПОДРУЛИВАЯ к aim (малоуправляемо), задевая всех на пути.
    /// Жжём стамину быстрее клубка. Возвращает false, когда выдохлись — сигнал психике (окно «на спине» — C3).</summary>
    public bool RollTick(Vector3 aim)
    {
        if (!Curled) return false;
        if (!Rolling)
        {
            Rolling = true;
            rolledThis.Clear();
            rollDir = Flat(aim, transform.forward);
            if (telegraph != null) telegraph.Set(true, TelegraphColors.Roll); // катящийся шар — цвет переката
        }

        // ПОДРУЛИВАНИЕ к цели — медленно (малоуправляемо, но не строго по прямой)
        rollDir = Vector3.RotateTowards(rollDir, Flat(aim, rollDir), rollTurnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f).normalized;
        transform.rotation = Quaternion.LookRotation(rollDir);

        if (controller != null)
            controller.Move((rollDir * rollSpeed + Vector3.down * rollGravity) * Time.deltaTime);

        // ПРОКАТ СКВОЗЬ: задеваем всех на пути раз за прокат (урон + кровь + толчок)
        var hit = new Hit(health, transform.position);
        var blow = new MeleeBlow { Damage = rollDamage, BleedStacks = rollBleed, KnockForce = rollKnock };
        foreach (var hp in TargetScan.Healths(transform.position + rollDir * rollRadius, rollRadius, transform))
            if (rolledThis.Add(hp)) blow.Deliver(hit, hp);

        var s = Breath;
        if (s != null)
        {
            s.Drain(rollDrain * Time.deltaTime);
            if (s.Exhausted) { Uncurl(); return false; } // выдохся — развернулся (C3 заменит на «спину»)
        }
        return true;
    }

    static Vector3 Flat(Vector3 v, Vector3 fallback) { v.y = 0f; return v.sqrMagnitude > 0.001f ? v.normalized : fallback; }

    void OnDrawGizmos()
    {
        if (!Rolling) return;
        Gizmos.color = TelegraphColors.Roll;
        Gizmos.DrawWireSphere(transform.position + rollDir * rollRadius, rollRadius);
    }
}
