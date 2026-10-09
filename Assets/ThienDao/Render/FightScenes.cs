using System.Collections.Generic;
using UnityEngine;
using Unit = ThienDao.Render.SpriteLibrary.Unit;

namespace ThienDao.Render
{
    // How a fighter's spells look. The elements follow SpiritRoots.ElementNames (Kim, Mộc, Thủy, Hỏa, Thổ, Lôi,
    // Phong, Băng) one up, so Spell.Kim + element is the spell of a method of that element; the rest are beasts'.
    public enum Spell : byte { Qi, Kim, Moc, Thuy, Hoa, Tho, Loi, Phong, Bang, Ma, Claw, Flame, Venom, Tide, Gale, Sonic }

    // A fight the simulation settled in one tick, played back on screen over a couple of seconds: the two face
    // off, trade blows (kiếm khí, claws), every hit makes the one struck flash white and recoil, and the last blow
    // either fells the loser (who fades into the ground) or sends them fleeing. UnitRenderer draws the fighters,
    // FxRenderer the projectiles and sparks; both read the same timeline from here.
    public sealed class FightScene
    {
        public const float Exchange = 0.3f;   // seconds between blows
        public const float Flight = 0.22f;    // how long a blow takes to land (long enough to see a fireball fly)
        public const float FlashTime = 0.14f; // how long the one struck stays white
        public const float Aftermath = 0.9f;  // the fall, or the flight

        public float Start;
        public float X, Y;                     // the loser's feet; the winner stands to one side
        public bool WinnerRight;
        public Unit WinnerLook, LoserLook;
        public float WinnerScale = 1f, LoserScale = 1f; // a great beast fights at its own size
        public int WinnerIdx = -1, LoserIdx = -1; // cultivators hidden from normal drawing while this plays
        public bool LoserDies;
        public Color32 WinnerColor, LoserColor;   // their kiếm khí
        public Spell WinnerSpell, LoserSpell;     // the look of their spells: by method, by sect, by beast kind (Claw: it tears)
        public int Seed;
        public readonly List<(float at, bool byWinner)> Blows = new List<(float, bool)>();
        public int Impacts;  // blows whose sparks FxRenderer already threw
        public bool Smoked;  // the death puff was thrown

        public float End => Blows.Count > 0 ? Blows[Blows.Count - 1].at + Flight + Aftermath : Start;
        public float FinalBlow => Blows.Count > 0 ? Blows[Blows.Count - 1].at + Flight : Start;

        public Vector2 WinnerPos => new Vector2(X + (WinnerRight ? 1.9f : -1.9f), Y);
        public Vector2 LoserPos => new Vector2(X, Y);

        // A fair-looking fight: blows trade back and forth, the loser lands fewer, the winner lands the last ones.
        public static FightScene Make(float now, float x, float y, int seed, int exchanges)
        {
            var s = new FightScene { Start = now, X = x, Y = y, WinnerRight = (seed & 1) == 0, Seed = seed };
            var r = new System.Random(seed);
            float t = now + 0.45f; // a moment to square up and gather qi
            for (int k = 0; k < exchanges; k++, t += Exchange)
            {
                bool last = k >= exchanges - 2;
                s.Blows.Add((t, last || r.NextDouble() < 0.6));
            }
            return s;
        }

        // How white the fighter is right now (1 just struck, 0 not), and how far they are knocked back.
        public float Flash(bool winner, float now, out float recoil)
        {
            recoil = 0f;
            float best = 0f;
            for (int b = 0; b < Blows.Count; b++)
            {
                var (at, byWinner) = Blows[b];
                if (byWinner == winner || Dodged(b)) continue; // only blows aimed at them, and that landed
                float since = now - (at + Flight);
                if (since < 0f || since > FlashTime * 2f) continue;
                float k = 1f - since / (FlashTime * 2f);
                if (k > best) { best = k; recoil = 0.5f * k; } // a hit knocks them back hard
            }
            if (!winner && now >= FinalBlow && now < FinalBlow + 0.25f) best = 1f; // the last blow lands hard
            return best;
        }

        // A quarter of the early blows miss: the one aimed at steps aside and the spell flies past.
        public bool Dodged(int blow)
        {
            if (blow >= Blows.Count - 2) return false;
            uint h = (uint)Seed * 0x9E3779B1u ^ (uint)blow * 0x85EBCA6Bu;
            h ^= h >> 13;
            h *= 0xC2B2AE35u;
            return (h >> 28) < 4;
        }

        // How far up or down a fighter is right now: a slow sway while they circle, and a quick step aside from
        // each blow they dodge.
        public float Shift(bool winner, float now)
        {
            float y = Mathf.Sin((now - Start) * 2.4f + (winner ? 0f : 2.1f)) * 0.18f;
            for (int b = 0; b < Blows.Count; b++)
            {
                var (at, byWinner) = Blows[b];
                if (byWinner == winner || !Dodged(b)) continue;
                float k = (now - at + 0.08f) / (Flight + 0.2f);
                if (k > 0f && k < 1f) y += Mathf.Sin(k * Mathf.PI) * ((b & 1) == 0 ? 0.8f : -0.8f);
            }
            return y;
        }

        public bool Involves(int cultivator) => cultivator >= 0 && (cultivator == WinnerIdx || cultivator == LoserIdx);
    }

    // Where something hurt living things just now (a beast raid, a quake, a bolt…): every creature drawn inside
    // blinks white for a moment, and a share of the decorative ones (villagers, animal tokens) die there.
    public sealed class HurtZone
    {
        public float X, Y, R, Start;
        public float Kill; // share of decorative creatures inside that fall
    }

    public static class FightScenes
    {
        public const int Max = 8;
        public const float HurtTime = 0.7f;
        public static readonly List<FightScene> Active = new List<FightScene>();
        public static readonly List<HurtZone> Hurt = new List<HurtZone>();
        public static readonly List<Vector2> Poofs = new List<Vector2>(); // deaths UnitRenderer saw; FxRenderer puffs white smoke there
        public static readonly List<Vector2> Kills = new List<Vector2>(); // an animal caught by a hunter: torn, red drops
        public static readonly List<Vector2> Dust = new List<Vector2>();  // a puff of dust kicked up by something running

        public static bool Hides(int cultivator)
        {
            foreach (var s in Active)
                if (s.Involves(cultivator)) return true;
            return false;
        }

        // How white a creature at (x, y) is right now (blinking while hurt), and whether — if it is only
        // decorative — this is where it dies. `who` keeps the choice of who dies stable from frame to frame.
        public static float HurtFlash(float x, float y, float now, int who, out bool dies)
        {
            dies = false;
            float best = 0f;
            foreach (var z in Hurt)
            {
                float t = now - z.Start;
                if (t < 0f || t > HurtTime) continue;
                float dx = x - z.X, dy = y - z.Y;
                if (dx * dx + dy * dy > z.R * z.R) continue;
                float blink = (((int)(t * 14f)) & 1) == 0 ? 1f : 0.35f;
                best = Mathf.Max(best, blink * (1f - t / HurtTime * 0.6f));
                uint h = (uint)(who * 0x9E3779B1) ^ (uint)(z.Start * 1000f);
                h ^= h >> 15;
                h *= 0x2C1B3C6D;
                h ^= h >> 12;
                if (h % 1000 < z.Kill * 1000f && t > HurtTime * 0.55f) dies = true;
            }
            return best;
        }

        public static void Clear()
        {
            Active.Clear();
            Hurt.Clear();
            Poofs.Clear();
            Kills.Clear();
            Dust.Clear();
        }
    }
}
