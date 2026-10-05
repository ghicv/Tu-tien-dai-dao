using System.Collections.Generic;
using System.Diagnostics;
using ThienDao.Core;
using ThienDao.World;

namespace ThienDao.Sim
{
    public readonly struct LoggedCommand
    {
        public readonly long Tick;
        public readonly IWorldCommand Command;

        public LoggedCommand(long tick, IWorldCommand command)
        {
            Tick = tick;
            Command = command;
        }
    }

    // Pure simulation: no rendering, no Unity objects. Player intent enters only through commands,
    // which are applied between ticks and logged so a run can be replayed exactly from seed + log.
    public sealed class Simulation
    {
        public static readonly float[] SpeedDaysPerSecond = { 0f, 6f, 30f, 120f, 3600f };
        public static readonly string[] SpeedNames = { "Dừng", "x1", "x5", "x20", "Tua" };

        public readonly WorldData World;
        public readonly SimClock Clock = new SimClock();
        public readonly QiSystem Qi;
        public readonly List<LoggedCommand> Log = new List<LoggedCommand>();

        readonly Queue<IWorldCommand> _pending = new Queue<IWorldCommand>();
        double _dayAccumulator;

        public int SpeedIndex = 1;
        public int TicksLastFrame { get; private set; }
        public bool Paused => SpeedIndex == 0;

        public Simulation(WorldData world)
        {
            World = world;
            Qi = new QiSystem(world);
        }

        public void Enqueue(IWorldCommand command) => _pending.Enqueue(command);

        public void ApplyPending()
        {
            while (_pending.Count > 0)
            {
                var c = _pending.Dequeue();
                c.Apply(this);
                Log.Add(new LoggedCommand(Clock.Tick, c));
            }
        }

        public void Step()
        {
            Clock.Advance();
            if (Clock.IsMonthStart) Qi.MonthlyStep(RegenMultiplier(Clock.Season));
        }

        public void RunFrame(float realDeltaSeconds, double budgetMs)
        {
            ApplyPending();
            TicksLastFrame = 0;
            float speed = SpeedDaysPerSecond[SpeedIndex];
            if (speed <= 0f)
            {
                _dayAccumulator = 0;
                return;
            }
            // Cap the backlog so a slow frame never triggers a catch-up spiral.
            _dayAccumulator = System.Math.Min(_dayAccumulator + speed * realDeltaSeconds, System.Math.Max(1.0, speed * 0.25));
            var sw = Stopwatch.StartNew();
            while (_dayAccumulator >= 1.0 && sw.Elapsed.TotalMilliseconds < budgetMs)
            {
                Step();
                _dayAccumulator -= 1.0;
                TicksLastFrame++;
            }
        }

        public static float RegenMultiplier(Season s)
        {
            switch (s)
            {
                case Season.Xuan: return 1.25f;
                case Season.Ha: return 1f;
                case Season.Thu: return 0.85f;
                default: return 0.6f;
            }
        }

        public ulong ComputeStateHash()
        {
            ulong h = StateHash.Seed;
            StateHash.Add(ref h, Clock.Tick);
            var w = World;
            for (int i = 0; i < w.Terrain.Length; i++)
            {
                StateHash.Add(ref h, (int)w.Terrain[i] | (w.LeyLine[i] ? 256 : 0) | (w.QiCap[i] << 9));
            }
            var objs = w.Objects;
            for (int id = 0; id < objs.Capacity; id++)
            {
                if (!objs.IsAlive(id)) continue;
                var o = objs.Get(id);
                StateHash.Add(ref h, id);
                StateHash.Add(ref h, (int)o.Type | (o.Variant << 8));
                StateHash.Add(ref h, o.X | (o.Y << 16));
            }
            Qi.HashInto(ref h);
            return h;
        }
    }

    public static class StateHash
    {
        public const ulong Seed = 14695981039346656037UL;

        public static void Add(ref ulong h, long v)
        {
            for (int b = 0; b < 8; b++)
            {
                h ^= (byte)(v >> (b * 8));
                h *= 1099511628211UL;
            }
        }
    }
}
