using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Экранный HUD-МИНИМУМ на OnGUI: только то, без чего нельзя играть — своё состояние, что даёт сборка,
/// что происходит в захвате. Читы и диагностика мира (родство, призрак, запах/термо, восприятие NPC,
/// стая, состав тела) переехали в «Chimera → Dev-панель»: экран больше не каша.
///
/// Умрёт целиком, когда придёт мировой HUD (полоски над целями + значки статусов + сканер зон чувств).
/// F1 — легенда цвет-сигналов: учебник языка игры, остаётся и после.
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

        // ── СВОЁ СОСТОЯНИЕ ───────────────────────────────────────────────────
        string hp = playerHealth != null ? $"{playerHealth.Current}/{playerHealth.Max}" : "—";
        string combat = playerHealth != null && !playerHealth.InCombat
            ? (playerHealth.OutOfCombatRegen > 0f ? "  (вне боя — реген)" : "  (вне боя)") : "";
        GUI.Label(new Rect(14, 10, 760, 26), $"HP: {hp}{combat}", style);

        // статусы — ТОЛЬКО значок и число (цвета из общей легенды F1, слова не нужны).
        // Глифы — из БАЗОВОЙ плоскости Unicode: встроенный шрифт OnGUI не знает эмодзи (🩸 и т.п. дадут
        // пустой прямоугольник). Настоящие иконки придут в B1 вместе с переездом HUD на Canvas.
        float sx = 14f;
        var venom = playerHealth != null ? playerHealth.GetComponent<Venom>() : null;
        if (venom != null && venom.Stacks > 0) StatusIcon(ref sx, 34f, TelegraphColors.Venom, "☠", venom.Stacks);
        var bleedC = playerHealth != null ? playerHealth.GetComponent<Bleed>() : null;
        if (bleedC != null && bleedC.Stacks > 0) StatusIcon(ref sx, 34f, TelegraphColors.Bleed, "♦♦", bleedC.Stacks);

        var boss = FindAnyObjectByType<WerewolfPsyche>();
        if (boss != null && boss.TryGetComponent<Health>(out var bossHp))
            GUI.Label(new Rect(540, 10, 460, 26),
                $"БОСС: {bossHp.Current}/{bossHp.Max}{(bossHp.Current > bossHp.Max ? $" (+{bossHp.Current - bossHp.Max} temp)" : "")}", style);

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
        GUI.Label(new Rect(14, 58, 900, 26), $"Способности: {string.Join(" · ", abil)}   ·   Tab — конструктор", style);

        // ── ЧТО ПРОИСХОДИТ ПРЯМО СЕЙЧАС: стадии захвата нечитаемы без строки ──
        string action = "";
        if (constrict != null && constrict.Holding)
            action = $"➤ ОБХВАТ ст.{constrict.Stage}{(constrict.Stage >= 2 ? (constrict.Presenting ? " — ПОД УДАРОМ" : " — ЗАЩЁЛКНУТО") : " — держи, вырывается!")}" +
                     (constrict.Victim != null ? $" · жертва {constrict.Victim.Current}/{constrict.Victim.Max}" : "") +
                     (constrict.Stage >= 2 ? "   [F — отпустить · C — подставить/за спину]" : "   [F — отпустить]");
        else if (player != null && player.IsGrabbed)
            action = "➤ ТЫ СХВАЧЕН — рывок/пинок!";
        if (action != "") GUI.Label(new Rect(14, 82, 900, 26), action, style);

        // ── дев-режимы: молчат, пока выключены; включённый режим обязан быть виден ──
        var modes = new List<string>();
        if (playerHealth != null && playerHealth.GodMode) modes.Add("БОГ");
        if (Perception.PlayerGhost) modes.Add("ПРИЗРАК");
        if (Perception.DevThermal) modes.Add("ТЕРМО-форс");
        if (Perception.ShowOwnScent) modes.Add("свой запах");
        if (modes.Count > 0)
            GUI.Label(new Rect(14, 108, 900, 22), $"дев: {string.Join(" · ", modes)}   [Dev-панель]", smallStyle);

        // ── ЛЕГЕНДА ЦВЕТ-СИГНАЛОВ (правый край, F1) ──────────────────────────
        if (showLegend)
        {
            legendStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            float x = Screen.width - 230f, y = 40f;
            GUI.Label(new Rect(x, y, 220, 24), "ЛЕГЕНДА [F1]", style); y += 30f;

            LegendHeader(ref y, x, "— приёмы (вспышка тела) —");
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

    /// <summary>Значок статуса: цветной глиф из легенды + число стаков. Слов нет — цвет и форма и есть имя.
    /// Двигает x, чтобы значки выстраивались в ряд.
    /// ГЛИФ ТОЛЬКО ИЗ БАЗОВОЙ ПЛОСКОСТИ Unicode (☠ ♦ ● ▲ ☺ ☹ ♥): встроенный шрифт OnGUI без эмодзи-атласа.</summary>
    void StatusIcon(ref float x, float y, Color c, string glyph, int stacks)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.Label(new Rect(x, y, 46, 26), glyph, style);
        GUI.color = old;
        float w = glyph.Length > 1 ? 34f : 20f; // двойной глиф («♦♦» — капли) шире одиночного
        GUI.Label(new Rect(x + w, y, 44, 26), stacks.ToString(), style);
        x += w + 34f;
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
