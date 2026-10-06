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
        Duel,           // đấu pháp between two cultivators
        Vendetta,       // setting out to avenge a master or disciple
        Legend,         // StoryDetector: a story worth telling
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

    // Recent events for the ticker and the events window; everything that matters also goes to the HistoryLog.
    // Actors: cultivator indices a (subject) / b (other party), faction ids fa / fb.
    public sealed class EventLog
    {
        const int Capacity = 400;
        readonly List<WorldEvent> _events = new List<WorldEvent>();
        public readonly int[] CountByKind = new int[32];
        public HistoryLog History;

        public IReadOnlyList<WorldEvent> Recent => _events;
        public long TotalAdded { get; private set; } // lets presentation pick up only what is new

        public void Add(long tick, EventKind kind, int importance, string text, float x = -1f, float y = -1f, Fx fx = Fx.None,
                        int a = -1, int b = -1, int fa = -1, int fb = -1)
        {
            CountByKind[(int)kind]++;
            TotalAdded++;
            if (_events.Count == Capacity) _events.RemoveAt(0);
            _events.Add(new WorldEvent(tick, kind, importance, text, x, y, fx));
            if (History == null) return;
            History.Count(tick, kind);
            // Lesser deeds of named people are kept too (a first kill, a Trúc Cơ breakthrough): stories and biographies need them.
            if (importance >= HistoryLog.MinImportance || (importance >= 1 && a >= 0))
                History.Record(new HistoryRecord(tick, kind, importance, text, x, y, a, b, fa, fb));
        }
    }
}
