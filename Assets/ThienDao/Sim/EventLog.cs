using System.Collections.Generic;

namespace ThienDao.Sim
{
    public enum EventKind : byte
    {
        Awakening,      // a child shows a spirit root
        Breakthrough,
        BreakthroughFailed,
        Deviation,      // tẩu hỏa nhập ma
        Tribulation,
        Death,          // tọa hóa (old age) or other cultivator death
        Relocation,     // moved to a better cave / phúc địa
        Divine,         // Thiên Đạo intervened
        Fortune,        // cơ duyên: found herbs, treasures
        Disaster,       // plague, famine, natural calamity
        Succession,     // a new tông chủ
    }

    public readonly struct WorldEvent
    {
        public readonly long Tick;
        public readonly EventKind Kind;
        public readonly int Importance; // 0 minor … 3 world-shaking
        public readonly string Text;

        public WorldEvent(long tick, EventKind kind, int importance, string text)
        {
            Tick = tick;
            Kind = kind;
            Importance = importance;
            Text = text;
        }
    }

    // Recent notable events for the player; M5's HistoryLog will build on this.
    public sealed class EventLog
    {
        const int Capacity = 400;
        readonly List<WorldEvent> _events = new List<WorldEvent>();
        public readonly int[] CountByKind = new int[16];

        public IReadOnlyList<WorldEvent> Recent => _events;

        public void Add(long tick, EventKind kind, int importance, string text)
        {
            CountByKind[(int)kind]++;
            if (_events.Count == Capacity) _events.RemoveAt(0);
            _events.Add(new WorldEvent(tick, kind, importance, text));
        }
    }
}
