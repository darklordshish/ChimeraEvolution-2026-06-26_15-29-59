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
    }

    void OnGUI()
    {
        style ??= new GUIStyle(GUI.skin.label) { fontSize = 18, normal = { textColor = Color.white } };

        string hp = playerHealth != null ? $"{playerHealth.Current}/{playerHealth.Max}" : "—";
        GUI.Label(new Rect(14, 10, 500, 26), $"HP: {hp}", style);
        GUI.Label(new Rect(14, 34, 500, 26), $"Родство [Волк]: {AffinityTracker.Get("Волк")}", style);
        GUI.Label(new Rect(14, 58, 760, 26), $"Шкала мозга: {(body != null ? body.BeastSlots : 0)}/{(body != null ? body.MaxSlots : 0)} звериных", style);
        GUI.Label(new Rect(14, 82, 760, 26), $"Билд [1–6]: {(body != null ? body.BuildSummary : "—")}", style);
        GUI.Label(new Rect(14, 106, 760, 26), $"БОГ [G]: {(playerHealth != null && playerHealth.GodMode ? "ВКЛ" : "выкл")}", style);
    }
}
