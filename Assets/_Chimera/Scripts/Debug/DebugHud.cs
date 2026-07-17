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

        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            showLegend = !showLegend; // F1 — легенда цвет-сигналов (правый край)
    }

    bool showLegend = true; // легенда видна по умолчанию — учебник языка сигналов; F1 прячет

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

    GUIStyle legendStyle;

    static string AlertRu(Alert s) => s == Alert.Attack ? "АТАКА" : s == Alert.Wary ? "настороже" : "спокоен"; // S1-отладка

    // S1-отладка: восприятие ближайшего к игроку существа типа T (пусто, если таких нет)
    string NearestAlertStr<T>(string label) where T : Component
    {
        if (player == null) return "";
        T near = null; float best = float.MaxValue;
        foreach (var c in FindObjectsByType<T>())
        {
            float d = (c.transform.position - player.transform.position).sqrMagnitude;
            if (d < best) { best = d; near = c; }
        }
        if (near == null || !near.TryGetComponent<AlertState>(out var a)) return "";
        string mor = near.TryGetComponent<Morale>(out var m) ? $" м:{m.Current:+0.#;-0.#;0}" : ""; // шкала морали (стайные)
        return $"{label} {AlertRu(a.State)}{mor} [{Mathf.Sqrt(best):0}м]";
    }

    // отладка эффектов: HP + стаки крови/яда БЛИЖАЙШЕГО врага (видно, как цель истекает от твоего укуса)
    string EnemyStatusStr()
    {
        if (player == null) return "";
        Health near = null; float best = float.MaxValue;
        foreach (var h in FindObjectsByType<Health>())
        {
            if (h == playerHealth || h.transform == player.transform) continue;
            float d = (h.transform.position - player.transform.position).sqrMagnitude;
            if (d < best) { best = d; near = h; }
        }
        return near != null ? $"Ближ.враг: HP {near.Current}/{near.Max}{EffectTags(near)} [{Mathf.Sqrt(best):0}м]" : "";
    }

    static string EffectTags(Health h)
    {
        string s = "";
        if (h.TryGetComponent<Bleed>(out var b) && b.Stacks > 0) s += $"  кровь {b.Stacks}";
        if (h.TryGetComponent<Venom>(out var v) && v.Stacks > 0) s += $"  ☠ яд {v.Stacks}";
        return s;
    }

    // отладка разброса: личность + множители особи БЛИЖАЙШЕГО волка (get-only, в инспекторе не видны)
    string NearestWolfTraits()
    {
        if (player == null) return "";
        WolfPsyche near = null; float best = float.MaxValue;
        foreach (var w in FindObjectsByType<WolfPsyche>())
        {
            float d = (w.transform.position - player.transform.position).sqrMagnitude;
            if (d < best) { best = d; near = w; }
        }
        if (near == null) return "";
        string s = "Ближ.волк особь:";
        if (near.TryGetComponent<Personality>(out var p)) s += $"  храбр {p.Bravery:0.0} · агр {p.Aggression:0.00} · любоп {p.Curiosity:0.00}";
        if (near.TryGetComponent<SpawnVariance>(out var v)) s += $"   |  hp×{v.HpMult:0.00} ск×{v.SpeedMult:0.00} ур×{v.DamageMult:0.00}";
        return s;
    }

    void OnGUI()
    {
        style ??= new GUIStyle(GUI.skin.label) { fontSize = 18, normal = { textColor = Color.white } };

        string hp = playerHealth != null ? $"{playerHealth.Current}/{playerHealth.Max}" : "—";
        string combat = playerHealth != null && !playerHealth.InCombat
            ? (playerHealth.OutOfCombatRegen > 0f ? "  (вне боя — реген)" : "  (вне боя)") : "";
        var venom = playerHealth != null ? playerHealth.GetComponent<Venom>() : null;
        string poison = venom != null && venom.Stacks > 0 ? $"   ☠ яд {venom.Stacks}" : "";
        var bleedC = playerHealth != null ? playerHealth.GetComponent<Bleed>() : null;
        string bleeding = bleedC != null && bleedC.Stacks > 0 ? $"   кровь {bleedC.Stacks}" : "";
        GUI.Label(new Rect(14, 10, 760, 26), $"HP: {hp}{combat}{poison}{bleeding}", style);
        var boss = FindAnyObjectByType<WerewolfPsyche>();
        if (boss != null && boss.TryGetComponent<Health>(out var bossHp))
            GUI.Label(new Rect(540, 10, 460, 26), $"БОСС: {bossHp.Current}/{bossHp.Max}{(bossHp.Current > bossHp.Max ? $" (+{bossHp.Current - bossHp.Max} temp)" : "")}", style);
        var affParts = new List<string>();
        if (CreatureBody.PlayerBody != null)
            foreach (var kv in CreatureBody.PlayerBody.AllAffinity) if (kv.Value != 0) affParts.Add($"{kv.Key} {kv.Value}");
        string aff = affParts.Count > 0 ? string.Join(" · ", affParts) : "—";
        GUI.Label(new Rect(14, 34, 900, 26), $"Родство: {aff}   (бонус органов ×{(body != null ? body.BonusMult : 1f):0.00})   [K: Волк +10 · L: Змея +10]", style);
        GUI.Label(new Rect(14, 58, 600, 26), $"Шкала мозга: {(body != null ? body.BeastSlots : 0)}/{(body != null ? body.MaxSlots : 0)} звериных", style);
        string enemyStatus = EnemyStatusStr(); // HP + кровь/яд ближайшего врага — видно эффекты
        if (enemyStatus != "") GUI.Label(new Rect(620, 58, 460, 26), enemyStatus, style);
        GUI.Label(new Rect(14, 82, 300, 26), $"Пул мутагена: {(body != null ? body.PoolUsed : 0)}/{(body != null ? body.Pool : 0)}", style);
        string traits = NearestWolfTraits(); // разброс особи (личность + множители) — get-only, в инспекторе не видны
        if (traits != "") GUI.Label(new Rect(330, 82, 900, 26), traits, style);
        // ШУМ игрока (дебаг-слушатель оси звука): 0 = беззвучен, 1 = полная громкость (бег/рывок)
        var noise = playerHealth != null ? playerHealth.GetComponent<Noise>() : null;
        string noiseStr = noise != null ? $"   Шум: {noise.Loudness:0.00}{(noise.Loudness < 0.15f ? " (тихо)" : noise.Loudness > 0.6f ? " (ГРОМКО)" : "")}" : "";
        GUI.Label(new Rect(14, 106, 900, 26), $"БОГ [G]: {(playerHealth != null && playerHealth.GodMode ? "ВКЛ" : "выкл")}   Запах: чутьё {(Perception.WolfScent ? "да" : "нет")}, свой [N] {(Perception.ShowOwnScent ? "вкл" : "выкл")}   Термо [T]: {(Perception.ThermalOn ? (Perception.SnakeThermal ? "орган" : "дев") : "выкл")}{(Perception.PlayerGhost ? "   ПРИЗРАК" : "")}{noiseStr}", style);
        var pack = PackCoordinator.Instance;
        string morale = pack.AnyRouting() ? "БЕГСТВО" : pack.Fearless ? "ЯРОСТЬ" : "норма";
        GUI.Label(new Rect(14, 130, 600, 26), $"Стая: атакуют {pack.AttackerCount}/{pack.MaxAttackers}, захват: {(pack.GrabActive ? "да" : "нет")}, мораль: {morale}", style);

        // S1-отладка: восприятие БЛИЖАЙШИХ волка и змеи — видно, как машина Спок→Настор→Атака ходит у обоих видов
        string wDbg = NearestAlertStr<WolfPsyche>("волк:");
        string sDbg = NearestAlertStr<SnakePsyche>("змея:");
        string percept = wDbg + (wDbg != "" && sDbg != "" ? "   " : "") + sDbg;
        if (percept != "") GUI.Label(new Rect(620, 130, 460, 26), $"Восприятие — {percept}", style);

        // какие способности сейчас активны (видно, что даёт сборка) + что происходит с тобой прямо сейчас
        var abil = new List<string> { "меч ЛКМ" };
        if (kick != null && kick.KickEnabled) abil.Add("пинок E");
        if (bite != null && bite.BiteEnabled) abil.Add("укус Q");
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

        // ЛЕГЕНДА ЦВЕТ-СИГНАЛОВ (правый край, F1): язык игры — приёмы (вспышка тела) / статусы / эмоции (морда)
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

            LegendHeader(ref y, x, "— статусы (всё тело) —");
            LegendRow(ref y, x, TelegraphColors.Stunned, "стан / схвачен");

            LegendHeader(ref y, x, "— эмоции (морда) —");
            LegendRow(ref y, x, TelegraphColors.RageTint, "ярость (лесенка — градиент)");
            LegendRow(ref y, x, TelegraphColors.FearTint, "паника/страх");
        }
    }
}
