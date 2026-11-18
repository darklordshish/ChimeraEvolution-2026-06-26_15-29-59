using System.Collections.Generic;
using System.Text;

/// <summary>
/// След генератора: где искали, сколько кандидатов, что приняли и почему.
/// Нужен, чтобы «первое годное» и fallback не были невидимыми.
/// </summary>
public sealed class GenTrace
{
    public struct Entry
    {
        public string layer;
        public int candidates;
        public int accepted;
        public string note;
        public float ms; // время шага: от предыдущей записи до этой (замер сборки по шагам, 18.11)
    }

    readonly List<Entry> entries = new List<Entry>();
    readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    double last;
    public IReadOnlyList<Entry> Entries => entries;

    public void Add(string layer, int candidates, int accepted, string note = "")
    {
        entries.Add(new Entry
        {
            layer = layer,
            candidates = candidates,
            accepted = accepted,
            note = note ?? "",
            ms = (float)(clock.Elapsed.TotalMilliseconds - last),
        });
        last = clock.Elapsed.TotalMilliseconds;
    }

    public string Text
    {
        get
        {
            var sb = new StringBuilder();
            foreach (var e in entries)
                sb.AppendLine($"{e.layer}: candidates={e.candidates} accepted={e.accepted}" +
                              (string.IsNullOrEmpty(e.note) ? "" : $" · {e.note}"));
            return sb.ToString();
        }
    }

    /// <summary>Шаги сборки со временем: где уходят секунды (не входит в отпечаток мира).</summary>
    public string TimedText
    {
        get
        {
            var sb = new StringBuilder();
            foreach (var e in entries)
                sb.AppendLine($"{e.ms,9:0.0} мс  {e.layer}");
            return sb.ToString();
        }
    }
}
