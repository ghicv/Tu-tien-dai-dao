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
        Disaster,       // plague, famine, flood, natural calamity
        Succession,     // a new tông chủ
        Founding,       // khai tông lập phái
        Schism,         // an elder breaks away or is driven out
        War,            // tuyên chiến
        Battle,
        Peace,          // giảng hòa / đình chiến
        Alliance,       // kết minh
        Destruction,    // diệt môn
        Patronage,      // a sect buys a tán tu as khách khanh
    }

    // Visual effect the renderer should play at the event's position.
    public enum Fx : byte { None, Lightning, Tribulation, Explosion, DemonBlast, Splash, LightPillar, Blessing }

    public readonly struct WorldEvent
    {
        public readonly long Tick;
        public readonly EventKind Kind;
        public readonly int Importance; // 0 minor … 3 world-shaking
        public readonly string Text;
        public readonly float X, Y;     // world position, X < 0 when it has none
        public readonly Fx Fx;

        public WorldEvent(long tick, EventKind kind, int importance, string text, float x, float y, Fx fx)
        {
            Tick = tick;
            Kind = kind;
            Importance = importance;
            Text = text;
            X = x;
            Y = y;
            Fx = fx;
        }
    }

    // Recent notable events for the player; M5's HistoryLog will build on this.
    public sealed class EventLog
    {
        const int Capacity = 400;
        readonly List<WorldEvent> _events = new List<WorldEvent>();
        public readonly int[] CountByKind = new int[32];

        public IReadOnlyList<WorldEvent> Recent => _events;
        public long TotalAdded { get; private set; } // lets presentation pick up only what is new

        public void Add(long tick, EventKind kind, int importance, string text, float x = -1f, float y = -1f, Fx fx = Fx.None)
        {
            CountByKind[(int)kind]++;
            TotalAdded++;
            if (_events.Count == Capacity) _events.RemoveAt(0);
            _events.Add(new WorldEvent(tick, kind, importance, text, x, y, fx));
        }
    }
}
