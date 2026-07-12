using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Временный отладочный HUD через OnGUI: HP игрока и родство.
/// Уберём, когда сделаем нормальный UI.
/// </summary>
public class DebugHud : MonoBehaviour
{
    Health playerHealth;
    CreatureBody body;
    PlayerController player;
    PlayerBite bite;
    PlayerKick kick;
    PlayerHowl howl;
    PlayerConstrict constrict;
    GUIStyle style;

    void Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null)
        {
            player = pc;
            playerHealth = pc.GetComponent<Health>();
            body = pc.GetComponent<CreatureBody>();
            bite = pc.GetComponent<PlayerBite>();
            kick = pc.GetComponent<PlayerKick>();
            howl = pc.GetComponent<PlayerHowl>();
            constrict = pc.GetComponent<PlayerConstrict>();
        }
    }

    void Update()
    {
        if (playerHealth != null && Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
            playerHealth.GodMode = !playerHealth.GodMode; // G — режим бога (отладка)

        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
            CreatureBody.PlayerBody?.AddAffinity("Волк", 10); // K — +10 родства-волк (отладка: проверить скидку/бонусы)

        if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
            CreatureBody.PlayerBody?.AddAffinity("Змея", 10); // L — +10 родства-змея

        if (Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame)
            Perception.ShowOwnScent = !Perception.ShowOwnScent; // N — показ своего запаха

        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            Perception.DevThermal = !Perception.DevThermal; // T — форс термозрения без органа (отладка)
    }

    void OnGUI()
    {
        style ??= new GUIStyle(GUI.skin.label) { fontSize = 18, normal = { textColor = Color.white } };

        string hp = playerHealth != null ? $"{playerHealth.Current}/{playerHealth.Max}" : "—";
        string combat = playerHealth != null && !playerHealth.InCombat
            ? (playerHealth.OutOfCombatRegen > 0f ? "  (вне боя — реген)" : "  (вне боя)") : "";
        var venom = playerHealth != null ? playerHealth.GetComponent<Venom>() : null;
        string poison = venom != null && venom.Stacks > 0 ? $"   ☠ яд {venom.Stacks}" : "";
        GUI.Label(new Rect(14, 10, 640, 26), $"HP: {hp}{combat}{poison}", style);
        var boss = FindAnyObjectByType<WerewolfPsyche>();
        if (boss != null && boss.TryGetComponent<Health>(out var bossHp))
            GUI.Label(new Rect(540, 10, 460, 26), $"БОСС: {bossHp.Current}/{bossHp.Max}{(bossHp.Current > bossHp.Max ? $" (+{bossHp.Current - bossHp.Max} temp)" : "")}", style);
        var affParts = new List<string>();
        if (CreatureBody.PlayerBody != null)
            foreach (var kv in CreatureBody.PlayerBody.AllAffinity) if (kv.Value != 0) affParts.Add($"{kv.Key} {kv.Value}");
        string aff = affParts.Count > 0 ? string.Join(" · ", affParts) : "—";
        GUI.Label(new Rect(14, 34, 900, 26), $"Родство: {aff}   (бонус органов ×{(body != null ? body.BonusMult : 1f):0.00})   [K: Волк +10 · L: Змея +10]", style);
        GUI.Label(new Rect(14, 58, 760, 26), $"Шкала мозга: {(body != null ? body.BeastSlots : 0)}/{(body != null ? body.MaxSlots : 0)} звериных", style);
        GUI.Label(new Rect(14, 82, 760, 26), $"Пул мутагена: {(body != null ? body.PoolUsed : 0)}/{(body != null ? body.Pool : 0)}", style);
        GUI.Label(new Rect(14, 106, 900, 26), $"БОГ [G]: {(playerHealth != null && playerHealth.GodMode ? "ВКЛ" : "выкл")}   Запах: чутьё {(Perception.WolfScent ? "да" : "нет")}, свой [N] {(Perception.ShowOwnScent ? "вкл" : "выкл")}   Термо [T]: {(Perception.ThermalOn ? (Perception.SnakeThermal ? "орган" : "дев") : "выкл")}{(Perception.PlayerGhost ? "   ПРИЗРАК" : "")}", style);
        var pack = PackCoordinator.Instance;
        string morale = pack.AnyRouting() ? "БЕГСТВО" : pack.Fearless ? "ЯРОСТЬ" : "норма";
        GUI.Label(new Rect(14, 130, 760, 26), $"Стая: атакуют {pack.AttackerCount}/{pack.MaxAttackers}, захват: {(pack.GrabActive ? "да" : "нет")}, мораль: {morale}", style);

        // какие способности сейчас активны (видно, что даёт сборка) + что происходит с тобой прямо сейчас
        var abil = new List<string> { "меч ЛКМ" };
        if (kick != null && kick.KickEnabled) abil.Add("пинок E");
        if (bite != null && bite.BiteEnabled) abil.Add("укус Shift");
        if (howl != null && howl.HowlEnabled) abil.Add("вой Alt");
        if (constrict != null && constrict.ConstrictEnabled) abil.Add("обхват F");
        GUI.Label(new Rect(14, 154, 900, 26), $"Способности: {string.Join(" · ", abil)}", style);

        string action = "";
        if (constrict != null && constrict.Holding)
            action = $"➤ ОБХВАТ ст.{constrict.Stage}{(constrict.Stage >= 2 ? " — ЗАЩЁЛКНУТО" : " — держи, вырывается!")}" +
                     (constrict.Victim != null ? $" · жертва {constrict.Victim.Current}/{constrict.Victim.Max}" : "") + "   [F — отпустить]";
        else if (player != null && player.IsGrabbed)
            action = "➤ ТЫ СХВАЧЕН — рывок/пинок!";
        GUI.Label(new Rect(14, 178, 900, 26), action, style);

        GUI.Label(new Rect(14, 210, 760, 200), body != null ? body.SlotsInfo : "", style);
    }
}
