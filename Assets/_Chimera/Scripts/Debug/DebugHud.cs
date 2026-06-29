using UnityEngine;

/// <summary>
/// Временный отладочный HUD через OnGUI: HP игрока и родство.
/// Уберём, когда сделаем нормальный UI.
/// </summary>
public class DebugHud : MonoBehaviour
{
    Health playerHealth;
    GUIStyle style;

    void Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) playerHealth = pc.GetComponent<Health>();
        Debug.Log($"DebugHud старт. Игрок найден: {pc != null}, Health на игроке: {playerHealth != null}");
    }

    void OnGUI()
    {
        style ??= new GUIStyle(GUI.skin.label) { fontSize = 18, normal = { textColor = Color.white } };

        string hp = playerHealth != null ? $"{playerHealth.Current}/{playerHealth.Max}" : "—";
        GUI.Label(new Rect(14, 10, 500, 26), $"HP: {hp}", style);
        GUI.Label(new Rect(14, 34, 500, 26), $"Родство [Волк]: {AffinityTracker.Get("Волк")}", style);
    }
}
