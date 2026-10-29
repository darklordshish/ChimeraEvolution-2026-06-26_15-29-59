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
    }

    readonly List<Entry> entries = new List<Entry>();
    public IReadOnlyList<Entry> Entries => entries;

    public void Add(string layer, int candidates, int accepted, string note = "")
    {
        entries.Add(new Entry
        {
            layer = layer,
            candidates = candidates,
            accepted = accepted,
            note = note ?? "",
        });
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
}
