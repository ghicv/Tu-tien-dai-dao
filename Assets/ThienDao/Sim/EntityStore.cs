using System;
using System.Collections.Generic;

namespace ThienDao.Sim
{
    public enum Species : byte
    {
        None,
        Deer,
        Rabbit,
        Wolf,
        Migrants, // a group of villagers travelling to found a new village
        Count
    }

    public static class SpeciesInfo
    {
        public static readonly string[] Names = { "", "Hươu", "Thỏ", "Sói", "Đoàn di dân" };

        // Index by (int)Species.
        public static readonly float[] Speed = { 0f, 2f, 1.5f, 2.4f, 3f };            // cells per day
        public static readonly float[] HungerPerDay = { 0f, 3f, 1.2f, 1.6f, 0f };     // 0..100 scale; grazers eat 1 forage per hunger point
        public static readonly int[] AdultDays = { 0, 720, 150, 540, 0 };
        public static readonly int[] LifespanDays = { 0, 360 * 12, 360 * 4, 360 * 10, 0 };
        public static readonly float[] BirthChancePerMonth = { 0f, 0.18f, 0.3f, 0.09f, 0f };
        public static readonly float[] CatchChance = { 0f, 0.4f, 0.7f, 0f, 0f };       // a predator's lunge succeeding on this prey, at normal density
        public static readonly int[] MaxLitter = { 0, 1, 3, 3, 0 };
        // Same species per 16×16 bucket before breeding stops; this, not the global cap, is what regulates numbers.
        public static readonly int[] CrowdLimit = { 0, 2, 3, 1, 0 };
        public static readonly float[] MeatValue = { 0f, 60f, 40f, 30f, 0f };          // hunger a predator recovers from the kill
        public static readonly float[] HuntFood = { 0f, 8f, 2f, 4f, 0f };              // person-months of food a village gets from the kill

        public static bool IsHerbivore(Species s) => s == Species.Deer || s == Species.Rabbit;
        public static bool IsAnimal(Species s) => s == Species.Deer || s == Species.Rabbit || s == Species.Wolf;
    }

    public enum DeathCause : byte { Age, Starvation, Predation, Hunted, Divine, Settled }

    // Struct-of-arrays creature storage; ids are reused through a free list.
    public sealed class EntityStore
    {
        public int Count { get; private set; }  // highest id + 1 ever used
        public int Alive { get; private set; }
        public readonly int[] AliveBySpecies = new int[(int)Sim.Species.Count];

        public Species[] Species = new Species[1024];
        public float[] X = new float[1024], Y = new float[1024];
        public float[] PrevX = new float[1024], PrevY = new float[1024];
        public float[] TX = new float[1024], TY = new float[1024];
        public float[] Hunger = new float[1024];
        public long[] BirthTick = new long[1024];
        public int[] Lifespan = new int[1024];
        public int[] Prey = new int[1024];      // chased entity id, -1 none
        public int[] Payload = new int[1024];   // species-specific (migrants: group index)

        readonly Stack<int> _free = new Stack<int>();

        public event Action<int, Species, DeathCause> Died;

        public bool IsAlive(int id) => id >= 0 && id < Count && Species[id] != Sim.Species.None;

        public int Spawn(Species species, float x, float y, long birthTick, int lifespan)
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
            Hunger[id] = 10f;
            BirthTick[id] = birthTick;
            Lifespan[id] = lifespan;
            Prey[id] = -1;
            Payload[id] = 0;
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
            Array.Resize(ref Hunger, n);
            Array.Resize(ref BirthTick, n);
            Array.Resize(ref Lifespan, n);
            Array.Resize(ref Prey, n);
            Array.Resize(ref Payload, n);
        }
    }
}
