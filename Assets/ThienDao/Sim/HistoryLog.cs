using System;
using System.Collections.Generic;
using ThienDao.Core;

namespace ThienDao.Sim
{
    // One remembered event: who (cultivator indices A/B, faction ids FA/FB), where, when.
    public readonly struct HistoryRecord
    {
        public readonly long Tick;
        public readonly EventKind Kind;
        public readonly int Importance;
        public readonly string Text;
        public readonly float X, Y;
        public readonly int A, B, FA, FB;

        public HistoryRecord(long tick, EventKind kind, int importance, string text, float x, float y, int a, int b, int fa, int fb)
        {
            Tick = tick;
            Kind = kind;
            Importance = importance;
            Text = text;
            X = x;
            Y = y;
            A = a;
            B = b;
            FA = fa;
            FB = fb;
        }

        public int Year => (int)(Tick / SimClock.DaysPerYear) + 1;
    }

    // The world's memory. EventLog keeps the last few hundred lines for the ticker; this keeps every event that
    // matters (importance ≥ 2, plus anything with named actors that a story might hinge on) for the whole run,
    // indexed by person and by faction, and counts every event per century for the chronicle.
    public sealed class HistoryLog
    {
        public const int MinImportance = 2;
        public const int YearsPerCentury = 100;

        public readonly List<HistoryRecord> All = new List<HistoryRecord>();
        readonly Dictionary<int, List<int>> _byCultivator = new Dictionary<int, List<int>>();
        readonly Dictionary<int, List<int>> _byFaction = new Dictionary<int, List<int>>();
        readonly List<int[]> _centuryCounts = new List<int[]>();

        public event Action<HistoryRecord> Recorded;

        public int Centuries => _centuryCounts.Count;

        public static int CenturyOf(long tick) => (int)(tick / SimClock.DaysPerYear / YearsPerCentury);

        public void Count(long tick, EventKind kind)
        {
            int c = CenturyOf(tick);
            while (_centuryCounts.Count <= c) _centuryCounts.Add(new int[32]);
            _centuryCounts[c][(int)kind]++;
        }

        public int CountIn(int century, EventKind kind) =>
            century >= 0 && century < _centuryCounts.Count ? _centuryCounts[century][(int)kind] : 0;

        public void Record(in HistoryRecord r)
        {
            int id = All.Count;
            All.Add(r);
            Index(_byCultivator, r.A, id);
            if (r.B != r.A) Index(_byCultivator, r.B, id);
            Index(_byFaction, r.FA, id);
            if (r.FB != r.FA) Index(_byFaction, r.FB, id);
            Recorded?.Invoke(r);
        }

        static void Index(Dictionary<int, List<int>> map, int key, int id)
        {
            if (key < 0) return;
            if (!map.TryGetValue(key, out var list)) map[key] = list = new List<int>();
            list.Add(id);
        }

        // Newest first, at most `max`.
        public void OfCultivator(int index, List<HistoryRecord> into, int max = int.MaxValue) => Collect(_byCultivator, index, into, max);

        public void OfFaction(int id, List<HistoryRecord> into, int max = int.MaxValue) => Collect(_byFaction, id, into, max);

        public int CountOfCultivator(int index) => _byCultivator.TryGetValue(index, out var l) ? l.Count : 0;

        void Collect(Dictionary<int, List<int>> map, int key, List<HistoryRecord> into, int max)
        {
            into.Clear();
            if (!map.TryGetValue(key, out var list)) return;
            for (int k = list.Count - 1; k >= 0 && into.Count < max; k--) into.Add(All[list[k]]);
        }

        // Records of one century at or above an importance, oldest first.
        public void InCentury(int century, int minImportance, List<HistoryRecord> into)
        {
            into.Clear();
            long from = (long)century * YearsPerCentury * SimClock.DaysPerYear, to = from + (long)YearsPerCentury * SimClock.DaysPerYear;
            int lo = 0, hi = All.Count;
            while (lo < hi) // records are appended in tick order
            {
                int mid = (lo + hi) / 2;
                if (All[mid].Tick < from) lo = mid + 1;
                else hi = mid;
            }
            for (int k = lo; k < All.Count && All[k].Tick < to; k++)
                if (All[k].Importance >= minImportance) into.Add(All[k]);
        }

        public void HashInto(ref ulong h)
        {
            StateHash.Add(ref h, All.Count);
            foreach (var r in All) StateHash.Add(ref h, r.Tick ^ ((long)r.Kind << 40) ^ ((long)(r.A + 1) << 48));
        }
    }
}
