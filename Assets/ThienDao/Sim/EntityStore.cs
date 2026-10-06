using System;
using System.Collections.Generic;

namespace ThienDao.Sim
{
    public enum Species : byte
    {
        None,
        Deer,     // deer, rabbits and wolves are region populations in WildlifeSystem, not entities
        Rabbit,
        Wolf,
        Migrants,   // a group of villagers travelling to found a new village
        Cultivator, // tu sĩ; the person's data lives in CultivationSystem
        Count
    }

    public static class SpeciesInfo
    {
        public static readonly string[] Names = { "", "Hươu", "Thỏ", "Sói", "Đoàn di dân", "Tu sĩ" };

        // Index by (int)Species.
        public static readonly float[] Speed = { 0f, 2f, 1.5f, 2.4f, 3f, 3f };   // cells per day on foot
        public static readonly float[] HuntFood = { 0f, 8f, 2f, 4f, 0f, 0f };     // person-months of food per animal hunted
        public const float FlyingSpeed = 12f;                                     // ngự kiếm phi hành, cells per day
    }

    public enum DeathCause : byte { Divine, Settled, Natural }

    // Struct-of-arrays storage for the few individual creatures; ids are reused through a free list.
    public sealed class EntityStore
    {
        public int Count { get; private set; }  // highest id + 1 ever used
        public int Alive { get; private set; }
        public readonly int[] AliveBySpecies = new int[(int)Sim.Species.Count];

        public Species[] Species = new Species[256];
        public float[] X = new float[256], Y = new float[256];
        public float[] PrevX = new float[256], PrevY = new float[256];
        public float[] TX = new float[256], TY = new float[256];
        public long[] BirthTick = new long[256];
        public bool[] Flying = new bool[256];   // ignores terrain and moves at FlyingSpeed
        public int[] Payload = new int[256];    // species-specific (cultivator: index in CultivationSystem)

        readonly Stack<int> _free = new Stack<int>();

        public event Action<int, Species, DeathCause> Died;

        public bool IsAlive(int id) => id >= 0 && id < Count && Species[id] != Sim.Species.None;

        public int Spawn(Species species, float x, float y, long birthTick)
        {
            int id;
            if (_free.Count > 0) id = _free.Pop();
            else
            {
                if (Count == Species.Length) Grow();
                id = Count++;
            }
            Species[id] = species;
            X[id] = PrevX[id] = TX[id] = x;
            Y[id] = PrevY[id] = TY[id] = y;
            BirthTick[id] = birthTick;
            Flying[id] = false;
            Payload[id] = -1;
            Alive++;
            AliveBySpecies[(int)species]++;
            return id;
        }

        public void Kill(int id, DeathCause cause)
        {
            if (!IsAlive(id)) return;
            var species = Species[id];
            AliveBySpecies[(int)species]--;
            Species[id] = Sim.Species.None;
            Alive--;
            _free.Push(id);
            Died?.Invoke(id, species, cause);
        }

        void Grow()
        {
            int n = Species.Length * 2;
            Array.Resize(ref Species, n);
            Array.Resize(ref X, n);
            Array.Resize(ref Y, n);
            Array.Resize(ref PrevX, n);
            Array.Resize(ref PrevY, n);
            Array.Resize(ref TX, n);
            Array.Resize(ref TY, n);
            Array.Resize(ref BirthTick, n);
            Array.Resize(ref Flying, n);
            Array.Resize(ref Payload, n);
        }
    }
}
