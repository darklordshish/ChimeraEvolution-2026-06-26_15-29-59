using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Временный отладочный HUD через OnGUI: HP игрока и родство.
/// Уберём, когда сделаем нормальный UI.
/// </summary>
public class DebugHud : MonoBehaviour
{
    Health playerHealth;
    ChimeraBody body;
    GUIStyle style;

    void Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) { playerHealth = pc.GetComponent<Health>(); body = pc.GetComponent<ChimeraBody>(); }
        Debug.Log($"DebugHud старт. Игрок найден: {pc != null}, Health на игроке: {playerHealth != null}");
    }

    void Update()
    {
        if (playerHealth != null && Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
            playerHealth.GodMode = !playerHealth.GodMode; // G — режим бога (отладка)

        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
            AffinityTracker.Add("Волк", 10); // K — +10 родства (отладка: проверить скидку/бонусы)
    }

    void OnGUI()
    {
        style ??= new GUIStyle(GUI.skin.label) { fontSize = 18, normal = { textColor = Color.white } };

        string hp = playerHealth != null ? $"{playerHealth.Current}/{playerHealth.Max}" : "—";
        string combat = playerHealth != null && !playerHealth.InCombat ? "  (вне боя — реген)" : "";
        GUI.Label(new Rect(14, 10, 520, 26), $"HP: {hp}{combat}", style);
        GUI.Label(new Rect(14, 34, 760, 26), $"Родство [Волк]: {AffinityTracker.Get("Волк")}  (бонус органов ×{(body != null ? body.BonusMult : 1f):0.00})   [K +10]", style);
        GUI.Label(new Rect(14, 58, 760, 26), $"Шкала мозга: {(body != null ? body.BeastSlots : 0)}/{(body != null ? body.MaxSlots : 0)} звериных", style);
        GUI.Label(new Rect(14, 82, 760, 26), $"Пул мутагена: {(body != null ? body.PoolUsed : 0)}/{(body != null ? body.Pool : 0)}", style);
        GUI.Label(new Rect(14, 106, 760, 26), $"БОГ [G]: {(playerHealth != null && playerHealth.GodMode ? "ВКЛ" : "выкл")}", style);
        var pack = PackCoordinator.Instance;
        GUI.Label(new Rect(14, 130, 760, 26), $"Стая: атакуют {pack.AttackerCount}/{pack.MaxAttackers}, захват: {(pack.GrabActive ? "да" : "нет")}", style);
        GUI.Label(new Rect(14, 162, 760, 200), body != null ? body.SlotsInfo : "", style);
    }
}
