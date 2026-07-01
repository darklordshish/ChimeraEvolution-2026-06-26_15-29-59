using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Водитель игрока: читает ввод и дёргает приёмы-способности (IAbility) на теле. Сами приёмы ввод
/// больше не читают — активацию решает драйвер (симметрично будущей психике ИИ). ЛКМ→меч, Shift→укус, ПКМ→пинок.
/// </summary>
[RequireComponent(typeof(PlayerAttack))]
public class PlayerInputDriver : MonoBehaviour
{
    PlayerAttack melee;
    PlayerBite bite;
    PlayerKick kick;
    InputAction attackAction, biteAction, kickAction;

    void Awake()
    {
        melee = GetComponent<PlayerAttack>();
        bite = GetComponent<PlayerBite>();
        kick = GetComponent<PlayerKick>();

        // ЛКМ / X на геймпаде / J
        attackAction = new InputAction("Attack", InputActionType.Button);
        attackAction.AddBinding("<Mouse>/leftButton");
        attackAction.AddBinding("<Gamepad>/buttonWest");
        attackAction.AddBinding("<Keyboard>/j");

        // Left Shift / левый шифтер
        biteAction = new InputAction("Bite", InputActionType.Button);
        biteAction.AddBinding("<Keyboard>/leftShift");
        biteAction.AddBinding("<Gamepad>/leftShoulder");

        // ПКМ / B на геймпаде
        kickAction = new InputAction("Kick", InputActionType.Button);
        kickAction.AddBinding("<Mouse>/rightButton");
        kickAction.AddBinding("<Gamepad>/buttonEast");
    }

    void OnEnable() { attackAction.Enable(); biteAction.Enable(); kickAction.Enable(); }
    void OnDisable() { attackAction.Disable(); biteAction.Disable(); kickAction.Disable(); }

    void Update()
    {
        if (ConstructorUI.IsOpen) return; // в конструкторе не деремся (иначе хитстоп сбивает замедление)

        if (attackAction.WasPressedThisFrame()) melee.TryUse();
        if (bite != null && biteAction.WasPressedThisFrame()) bite.TryUse();
        if (kick != null && kickAction.WasPressedThisFrame()) kick.TryUse();
    }
}
