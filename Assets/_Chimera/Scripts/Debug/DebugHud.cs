using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Остаток экранного HUD на OnGUI: СПИСОК СПОСОБНОСТЕЙ (что дала сборка) + индикатор дев-режимов + легенда F1.
///
/// Состояние существ (HP, статусы, захват) уехало в мировой HUD `VitalsHud` — оно живёт над телами.
/// Читы и диагностика мира — в «Chimera → Dev-панель». Здесь остались две вещи, которым места на полоске нет:
/// «чем я сейчас умею бить» и «какие дев-тумблеры включены».
///
/// F1 — легенда цвет-сигналов: учебник языка игры, переживёт и этот файл.
/// </summary>
public class DebugHud : MonoBehaviour
{
    Health playerHealth;
    PlayerController player;
    PlayerBite bite;
    PlayerKick kick;
    PlayerHowl howl;
    PlayerConstrict constrict;
    PlayerBellow bellow;
    GUIStyle style, smallStyle, legendStyle;

    bool showLegend = true; // легенда видна по умолчанию — учебник языка сигналов; F1 прячет

    void Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null)
        {
            player = pc;
            playerHealth = pc.GetComponent<Health>();
            bite = pc.GetComponent<PlayerBite>();
            kick = pc.GetComponent<PlayerKick>();
            howl = pc.GetComponent<PlayerHowl>();
            constrict = pc.GetComponent<PlayerConstrict>();
            bellow = pc.GetComponent<PlayerBellow>();
        }
    }

    void Update()
    {
        // читы (G/K/L/;/N/T) уехали кнопками в Dev-панель — на экране остался только учебник
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            showLegend = !showLegend;
    }

    void OnGUI()
    {
        style ??= new GUIStyle(GUI.skin.label) { fontSize = 18, normal = { textColor = Color.white } };
        smallStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = new Color(1f, 0.85f, 0.4f) } };

        // HP, статусы, стадия захвата и полоска босса ПЕРЕЕХАЛИ в мировой HUD (`VitalsHud`): состояние
        // живёт НАД существами, а не строкой в углу. Здесь осталось то, чему на полоске места нет.

        // ── ЧТО ДАЁТ СБОРКА: «пианино» растёт с химеризацией ─────────────────
        var abil = new List<string> { "меч ЛКМ" };
        if (kick != null && kick.KickEnabled) abil.Add("пинок E");
        if (bite != null && bite.BiteEnabled) abil.Add("укус Q");
        if (howl != null && howl.HowlEnabled) abil.Add("вой Alt");
        if (constrict != null && constrict.ConstrictEnabled) abil.Add("обхват F");
        if (bellow == null && player != null) player.TryGetComponent(out bellow); // тело до-создаёт после нашего Start
        if (bellow != null && bellow.BellowEnabled) abil.Add("РЁВ Alt");
        var antler = player != null ? player.GetComponent<PlayerAntler>() : null;
        if (antler != null && antler.AntlerEnabled) abil.Add("рога R");
        var charge = player != null ? player.GetComponent<PlayerCharge>() : null;
        if (charge != null && charge.ChargeEnabled) abil.Add("таран (рывок)");
        GUI.Label(new Rect(14, 10, 900, 26), $"Способности: {string.Join(" · ", abil)}   ·   Tab — конструктор", style);

        // ── дев-режимы: молчат, пока выключены; включённый режим обязан быть виден ──
        var modes = new List<string>();
        if (playerHealth != null && playerHealth.GodMode) modes.Add("БОГ");
        if (Perception.PlayerGhost) modes.Add("ПРИЗРАК");
        if (Perception.DevThermal) modes.Add("ТЕРМО-форс");
        if (Perception.ShowOwnScent) modes.Add("свой запах");
        if (VitalsHud.ShowAll) modes.Add("полоски: ВСЕ [H]");
        if (modes.Count > 0)
            GUI.Label(new Rect(14, 36, 900, 22), $"дев: {string.Join(" · ", modes)}   [Dev-панель]", smallStyle);

        // ── ЛЕГЕНДА ЦВЕТ-СИГНАЛОВ (правый край, F1) ──────────────────────────
        if (showLegend)
        {
            legendStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            float x = Screen.width - 230f, y = 40f;
            GUI.Label(new Rect(x, y, 220, 24), "ЛЕГЕНДА [F1]", style); y += 30f;

            LegendHeader(ref y, x, "— приёмы (вспышка тела) —");
            LegendRow(ref y, x, TelegraphColors.Unknown,
                      Perception.Insight ? "нераспознанное (светлеет)" : "ЧТО-ТО ГОТОВИТ — нужно Чутьё");
            LegendRow(ref y, x, TelegraphColors.Bite,   "укус");
            LegendRow(ref y, x, TelegraphColors.Leap,   "прыжок");
            LegendRow(ref y, x, TelegraphColors.Grab,   "захват/обхват (стадии — градиент)");
            LegendRow(ref y, x, TelegraphColors.Charge, "таран/чардж");
            LegendRow(ref y, x, TelegraphColors.Howl,   "вой/рёв");
            LegendRow(ref y, x, TelegraphColors.Antler, "рога");
            LegendRow(ref y, x, TelegraphColors.Sword,  "меч");
            LegendRow(ref y, x, TelegraphColors.Kick,   "пинок");

            LegendHeader(ref y, x, "— статусы —");
            LegendRow(ref y, x, TelegraphColors.Stunned, "стан / схвачен (всё тело)");
            LegendRow(ref y, x, TelegraphColors.Venom,   "яд (значок + стаки)");
            LegendRow(ref y, x, TelegraphColors.Bleed,   "кровь (значок + стаки)");

            LegendHeader(ref y, x, "— эмоции (морда) —");
            LegendRow(ref y, x, TelegraphColors.RageTint, "ярость (лесенка — градиент)");
            LegendRow(ref y, x, TelegraphColors.FearTint, "паника/страх");
        }
    }

    // строка легенды: цветной квадратик + подпись (квадрат — белая текстура, тонированная GUI.color)
    void LegendRow(ref float y, float x, Color c, string label)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(new Rect(x, y + 4, 16, 16), Texture2D.whiteTexture);
        GUI.color = old;
        GUI.Label(new Rect(x + 22, y, 220, 24), label, legendStyle);
        y += 22f;
    }

    void LegendHeader(ref float y, float x, string text)
    {
        GUI.Label(new Rect(x, y, 220, 24), text, legendStyle);
        y += 24f;
    }
}
