using UnityEngine;

/// <summary>
/// ПЕРЕПИСЬ ХИМЕР среди NPC (спека вечной эволюции 2026-11-12, §5 п.1.4, критерий К-7): сколько живых существ,
/// кроме игрока, носят чужой орган. «Химера» здесь — есть хотя бы один надетый не родной орган (`SlotView.installed`):
/// тот же признак, что у эволюции NPC (`TryChimerize` надевает чужое). Счётчик — в итог смерти и в `DebugHud`.
/// </summary>
public static class ChimeraCensus
{
    public readonly struct Snapshot
    {
        public readonly int chimeras, total;
        public Snapshot(int chimeras, int total) { this.chimeras = chimeras; this.total = total; }
        public float Share => total > 0 ? (float)chimeras / total : 0f;
        public override string ToString() => $"{chimeras}/{total} ({Share * 100f:0}%)";
    }

    /// <summary>Чужой ли у тела орган: любой слот несёт надетый чужой.</summary>
    public static bool IsChimeric(CreatureBody body)
    {
        if (body == null) return false;
        for (int i = 0; i < body.SlotCount; i++)
            if (body.GetSlot(i).installed) return true;
        return false;
    }

    /// <summary>Пересчёт по сцене: живые тела без игрока (босс считается — он тоже участник мира).</summary>
    public static Snapshot Take()
    {
        int chimeras = 0, total = 0;
        var player = CreatureBody.PlayerBody;
        foreach (var b in Object.FindObjectsByType<CreatureBody>())
        {
            if (b == player || b == null) continue;
            if (b.TryGetComponent<Health>(out var hp) && (hp.IsDead || hp.Current <= 0)) continue;
            total++;
            if (IsChimeric(b)) chimeras++;
        }
        return new Snapshot(chimeras, total);
    }
}
