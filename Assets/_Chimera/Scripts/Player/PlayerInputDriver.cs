using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Водитель игрока: читает ввод и дёргает приёмы-способности (IAbility) на теле. Сами приёмы ввод
/// больше не читают — активацию решает драйвер (симметрично будущей психике ИИ). ЛКМ→меч, Shift→укус, E→пинок.
/// Плюс хоткеи химеризации (1–6) — по данным слотов тела (CreatureBody.ToggleSlot).
/// </summary>
[RequireComponent(typeof(PlayerAttack))]
public class PlayerInputDriver : MonoBehaviour
{
    PlayerAttack melee;
    PlayerBite bite;
    PlayerKick kick;
    PlayerHowl howl;
    CreatureBody body;
    InputAction attackAction, biteAction, kickAction, howlAction;
    readonly List<(InputAction action, int slot)> slotActions = new();

    void Awake()
    {
        melee = GetComponent<PlayerAttack>();
        bite = GetComponent<PlayerBite>();
        kick = GetComponent<PlayerKick>();
        howl = GetComponent<PlayerHowl>();

        // ЛКМ / X на геймпаде / J
        attackAction = new InputAction("Attack", InputActionType.Button);
        attackAction.AddBinding("<Mouse>/leftButton");
        attackAction.AddBinding("<Gamepad>/buttonWest");
        attackAction.AddBinding("<Keyboard>/j");

        // Left Shift / левый шифтер
        biteAction = new InputAction("Bite", InputActionType.Button);
        biteAction.AddBinding("<Keyboard>/leftShift");
        biteAction.AddBinding("<Gamepad>/leftShoulder");

        // E / B на геймпаде (пинок — фича человеческих ног)
        kickAction = new InputAction("Kick", InputActionType.Button);
        kickAction.AddBinding("<Keyboard>/e");
        kickAction.AddBinding("<Gamepad>/buttonEast");

        // Alt / правый шифтер (вой-стан — фича волчьей Пасти)
        howlAction = new InputAction("Howl", InputActionType.Button);
        howlAction.AddBinding("<Keyboard>/leftAlt");
        howlAction.AddBinding("<Gamepad>/rightShoulder");
    }

    int knownSlots; // пересобрать хоткеи, когда слотов стало больше (выдан химерный слот)

    // хоткеи слотов строим в Start: тело собирает слоты в своём Awake
    void Start()
    {
        body = GetComponent<CreatureBody>();
        BuildSlotHotkeys();
    }

    void BuildSlotHotkeys()
    {
        if (body == null) return;
        foreach (var (a, _) in slotActions) a.Disable();
        slotActions.Clear();
        knownSlots = body.SlotCount;
        for (int i = 0; i < body.SlotCount; i++)
        {
            var v = body.GetSlot(i);
            if (!v.hasBeast || string.IsNullOrEmpty(v.hotkey)) continue;
            var a = new InputAction(v.slot, InputActionType.Button);
            a.AddBinding($"<Keyboard>/{v.hotkey}");
            a.Enable();
            slotActions.Add((a, i));
        }
    }

    void OnEnable()
    {
        attackAction.Enable(); biteAction.Enable(); kickAction.Enable(); howlAction.Enable();
        foreach (var (a, _) in slotActions) a.Enable();
    }

    void OnDisable()
    {
        attackAction.Disable(); biteAction.Disable(); kickAction.Disable(); howlAction.Disable();
        foreach (var (a, _) in slotActions) a.Disable();
    }

    void Update()
    {
        if (body != null && body.SlotCount != knownSlots) BuildSlotHotkeys(); // выдали химерный слот — новый хоткей

        // химеризация хоткеями — работает и при открытом конструкторе (UI сам синхронится)
        for (int i = 0; i < slotActions.Count; i++)
            if (slotActions[i].action.WasPressedThisFrame()) body.ToggleSlot(slotActions[i].slot);

        if (ConstructorUI.IsOpen) return; // в конструкторе не деремся (иначе хитстоп сбивает замедление)

        if (attackAction.WasPressedThisFrame()) melee.TryUse();
        if (bite != null && biteAction.WasPressedThisFrame()) bite.TryUse();
        if (kick != null && kickAction.WasPressedThisFrame()) kick.TryUse();
        if (howl != null && howlAction.WasPressedThisFrame()) howl.TryUse();
    }
}
