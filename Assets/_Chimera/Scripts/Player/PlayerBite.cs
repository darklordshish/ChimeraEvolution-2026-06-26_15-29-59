using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Укус — вторая атака (слот «Пасть»). Отдельная кнопка (Left Shift / левый шифтер):
/// короткая дистанция, мощный единичный удар + вампиризм. Активен, только если слот «Пасть» надет
/// (ChimeraBody выставляет BiteEnabled).
/// </summary>
public class PlayerBite : MonoBehaviour
{
    [Header("Укус")]
    [SerializeField] int damage = 14;
    [SerializeField] float range = 1.2f;   // короче когтя
    [SerializeField] float radius = 0.9f;
    [SerializeField] float cooldown = 0.7f;
    [SerializeField] int lifeSteal = 6;    // лечение за результативный укус
    [SerializeField] float shake = 0.2f;
    [SerializeField, Range(0f, 1f)] float regenDebuff = 0.5f; // укус сбивает реген цели (×0.5)
    [SerializeField] float regenDebuffTime = 3f;

    public bool BiteEnabled { get; set; }  // включается слотом «Пасть»

    InputAction biteAction;
    float nextTime;
    CameraFollow cam;
    Health ownHealth;
    readonly HashSet<Health> hitThisBite = new();

    void Awake()
    {
        biteAction = new InputAction("Bite", InputActionType.Button);
        biteAction.AddBinding("<Keyboard>/leftShift");
        biteAction.AddBinding("<Gamepad>/leftShoulder");
    }

    void Start()
    {
        cam = FindAnyObjectByType<CameraFollow>();
        ownHealth = GetComponent<Health>();
    }

    void OnEnable() => biteAction.Enable();
    void OnDisable() => biteAction.Disable();

    void Update()
    {
        if (ConstructorUI.IsOpen) return; // в конструкторе не кусаем (хитстоп сбил бы замедление)
        if (!BiteEnabled) return;
        if (biteAction.WasPressedThisFrame() && Time.time >= nextTime)
        {
            nextTime = Time.time + cooldown;
            DoBite();
        }
    }

    void DoBite()
    {
        hitThisBite.Clear();
        int hits = 0;
        var hit = new Hit(ownHealth, transform.position);
        Collider[] cols = Physics.OverlapSphere(BiteCenter(), radius, ~0, QueryTriggerInteraction.Ignore);
        foreach (var col in cols)
        {
            var hp = col.GetComponentInParent<Health>();
            if (hp == null || hp.transform == transform || !hitThisBite.Add(hp)) continue;
            hit.Apply(hp, HitEffect.Damage(damage));
            hit.Apply(hp, HitEffect.RegenDebuff(regenDebuff, regenDebuffTime)); // сбиваем реген цели (контр-сустейн против босса)
            if (lifeSteal > 0) hit.Apply(hp, HitEffect.LifeSteal(lifeSteal)); // лечимся за укус
            hits++;
        }

        if (hits > 0)
        {
            if (cam != null) cam.Shake(0.12f, shake);
            Hitstop.Do(0.05f);
        }
    }

    Vector3 BiteCenter() => transform.position + transform.forward * range + Vector3.up * 0.5f;

    void OnDrawGizmos()
    {
        if (!BiteEnabled) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(BiteCenter(), radius);
    }
}
