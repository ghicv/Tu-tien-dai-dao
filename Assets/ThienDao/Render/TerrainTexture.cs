using ThienDao.Core;
using ThienDao.World;
using UnityEngine;
using Terrain = ThienDao.World.Terrain;

namespace ThienDao.Render
{
    // What the ground looks like up close: each terrain paints its own 8×8 pattern (dune ripples, ash cracks,
    // mesa strata, grass tufts, snow glints, …) and each great region adds its accent (the flowers of Chính Đạo,
    // the embers of Ma Đạo, the wind-combed grass of Mộ Lan, the frost of the north).
    // Everything is a pure function of world pixel coordinates, so chunks tile seamlessly.
    static class TerrainTexture
    {
        static readonly Color32 Soil = new Color32(128, 92, 56, 255);
        static readonly Color32 Wheat = new Color32(214, 184, 92, 255);
        static readonly Color32 Rice = new Color32(118, 172, 76, 255);
        static readonly Color32 Fallow = new Color32(150, 112, 70, 255);
        static readonly Color32 Ember = new Color32(236, 96, 40, 255);
        static readonly Color32 Glow = new Color32(255, 176, 64, 255);
        static readonly Color32 Crust = new Color32(64, 34, 30, 255);
        static readonly Color32 Bog = new Color32(52, 82, 74, 255);
        static readonly Color32 Reed = new Color32(150, 150, 84, 255);
        static readonly Color32 Lichen = new Color32(176, 160, 112, 255);
        static readonly Color32 Frost = new Color32(236, 244, 252, 255);
        static readonly Color32 SnowShadow = new Color32(176, 196, 232, 255);
        static readonly Color32 Rock = new Color32(118, 114, 120, 255);
        static readonly Color32 Pebble = new Color32(150, 146, 140, 255);
        static readonly Color32 Shell = new Color32(252, 244, 222, 255);
        static readonly Color32 Shrub = new Color32(110, 120, 52, 255);
        static readonly Color32 DryGrass = new Color32(214, 196, 120, 255);
        static readonly Color32 Ochre = new Color32(222, 150, 80, 255);
        static readonly Color32 RedClay = new Color32(160, 70, 52, 255);
        static readonly Color32 Glint = new Color32(250, 252, 255, 255);
        static readonly Color32[] Flowers =
        {
            new Color32(250, 250, 240, 255), new Color32(250, 220, 80, 255),
            new Color32(244, 150, 190, 255), new Color32(190, 160, 240, 255),
        };

        // ---------------------------------------------------------------- per pixel

        // n is the pixel's hash; its low byte already went into f (the plain grain), the rest is free here.
        public static Color32 Paint(Terrain t, RegionKind region, Color32 c, float f, int wx, int wy, uint n, uint seed)
        {
            switch (t)
            {
                case Terrain.Grass:
                {
                    f *= 0.92f + Value(seed + 1u, wx, wy, 4) * 0.16f; // lighter and darker swards
                    if (region == RegionKind.ThaoNguyen)
                    {
                        // Wind-combed steppe: long pale streaks all leaning one way.
                        int s = wx * 2 + wy + (int)(Value(seed + 2u, wx, wy, 3) * 10f);
                        if (s % 9 == 0) return Color32.Lerp(SpriteLibrary.Shade(c, f), DryGrass, 0.45f);
                    }
                    if (Tuft(seed, wx, wy, n, 31u, out bool tip)) f *= tip ? 1.12f : 0.8f;
                    if (region == RegionKind.ChinhDao || region == RegionKind.CuuQuocMinh)
                    {
                        if (((n >> 16) & 255) < 3 && Value(seed + 3u, wx, wy, 4) > 0.55f) return Flowers[(n >> 24) & 3];
                    }
                    else if (region == RegionKind.ThienDaoMinh && ((n >> 16) & 255) < 3) return SpriteLibrary.Shade(Pebble, f);
                    else if (region == RegionKind.BangNguyen && Value(seed + 4u, wx, wy, 3) > 0.72f)
                        return Color32.Lerp(SpriteLibrary.Shade(c, f), Frost, 0.4f);
                    return SpriteLibrary.Shade(c, f);
                }

                case Terrain.Forest:
                case Terrain.Jungle:
                {
                    // Canopy: round crowns with dark gaps between them, lit from the top-left.
                    float v = Value(seed + 5u, wx, wy, 2);
                    float lit = Value(seed + 5u, wx - 1, wy + 1, 2);
                    f *= 0.84f + v * 0.3f + (lit - v) * 0.25f;
                    if (v < 0.2f) f *= 0.78f;
                    if (t == Terrain.Jungle && ((n >> 8) & 31) == 0) f *= 1.25f; // glossy leaves catching the sun
                    return SpriteLibrary.Shade(c, f);
                }

                case Terrain.Savanna:
                {
                    int s = wy + (int)(Value(seed + 6u, wx, wy, 3) * 4f);
                    if ((s & 3) == 0) f *= 1.08f;
                    if (((n >> 8) & 127) == 0) return SpriteLibrary.Shade(Shrub, f);
                    if (Tuft(seed, wx, wy, n, 63u, out bool tip)) f *= tip ? 1.1f : 0.86f;
                    return SpriteLibrary.Shade(c, f);
                }

                case Terrain.Desert:
                {
                    // Dunes: long ridges bent by the wind; a bright crest, then the shadowed lee.
                    float strength = region == RegionKind.SaMac ? 1.4f : 1f;
                    float d = wy + wx * 0.35f + Value(seed + 7u, wx, wy, 5) * 16f + Value(seed + 8u, wx, wy, 3) * 3f;
                    float ph = d - Mathf.Floor(d / 7f) * 7f;
                    if (ph < 1f) f *= 1f + 0.1f * strength;
                    else if (ph < 2.6f) f *= 1f - 0.1f * strength;
                    if (((n >> 8) & 255) == 0) f *= 0.82f; // a pebble
                    return SpriteLibrary.Shade(c, f);
                }

                case Terrain.Badlands:
                {
                    // Mesa strata: bands of ochre and red clay, broken by dark gullies.
                    float d = wy + Value(seed + 9u, wx, wy, 4) * 7f;
                    int band = Mathf.FloorToInt(d / 2f);
                    uint bh = Rand(seed + 10u, band, 0);
                    var layer = (bh & 3) == 0 ? Ochre : (bh & 3) == 1 ? RedClay : c;
                    c = Color32.Lerp(c, layer, 0.45f);
                    f *= 0.9f + (bh >> 8 & 255) / 255f * 0.2f;
                    if (Crack(seed + 11u, wx, wy, 11) < 0.9f) f *= 0.68f;
                    return SpriteLibrary.Shade(c, f);
                }

                case Terrain.Ashland:
                {
                    // Cracked grey ash; in Ma Đạo the cracks still glow in places.
                    float cr = Crack(seed + 12u, wx, wy, 6);
                    if (cr < 0.8f)
                    {
                        if (region == RegionKind.MaDao && Value(seed + 13u, wx, wy, 4) > 0.7f)
                            return Color32.Lerp(SpriteLibrary.Shade(c, f * 0.7f), Ember, 0.7f);
                        f *= 0.62f;
                    }
                    else if (((n >> 8) & 63) == 0) f *= 1.22f; // drifting ash
                    f *= 0.94f + Value(seed + 14u, wx, wy, 4) * 0.12f;
                    return SpriteLibrary.Shade(c, f);
                }

                case Terrain.Swamp:
                {
                    // Pools of black water among the reeds.
                    float v = Value(seed + 15u, wx, wy, 3);
                    if (v < 0.32f)
                    {
                        var pool = Color32.Lerp(SpriteLibrary.Shade(c, f), Bog, 0.6f);
                        return ((n >> 8) & 63) == 0 ? SpriteLibrary.Shade(pool, 1.3f) : pool;
                    }
                    if (Tuft(seed, wx, wy, n, 15u, out bool tip)) return SpriteLibrary.Shade(Reed, tip ? f * 1.1f : f * 0.85f);
                    return SpriteLibrary.Shade(c, f);
                }

                case Terrain.Tundra:
                {
                    if (Value(seed + 16u, wx, wy, 3) > 0.68f) return Color32.Lerp(SpriteLibrary.Shade(c, f), Frost, 0.45f);
                    if (((n >> 8) & 31) == 0) return SpriteLibrary.Shade(Lichen, f);
                    if (((n >> 8) & 31) == 1) f *= 0.8f;
                    return SpriteLibrary.Shade(c, f);
                }

                case Terrain.Snow:
                case Terrain.Peak:
                {
                    float v = Value(seed + 17u, wx, wy, 4);
                    if (t == Terrain.Peak && Value(seed + 18u, wx, wy, 3) < 0.3f)
                        c = Color32.Lerp(c, Rock, 0.6f); // bare rock through the snow
                    else if (v < 0.35f) c = Color32.Lerp(c, SnowShadow, 0.3f);
                    // Sastrugi: wind ridges in the snow.
                    if ((wx - wy * 2 + (int)(v * 8f)) % 9 == 0) f *= 0.95f;
                    if (((n >> 8) & 511) < 3) return Glint;
                    return SpriteLibrary.Shade(c, f);
                }

                case Terrain.Hills:
                {
                    // Terraces following the slope, and the odd boulder.
                    float d = wy * 0.6f + Value(seed + 19u, wx, wy, 4) * 9f;
                    float ph = d - Mathf.Floor(d / 4f) * 4f;
                    if (ph < 0.7f) f *= 0.9f;
                    else if (ph < 1.4f) f *= 1.06f;
                    if (((n >> 8) & 127) == 0) return SpriteLibrary.Shade(Pebble, f);
                    return SpriteLibrary.Shade(c, f);
                }

                case Terrain.Mountain:
                {
                    // Crags: sharp ridges and fissures, facets lit unevenly.
                    float v = Value(seed + 20u, wx, wy, 2);
                    f *= 0.88f + Value(seed + 21u, wx, wy, 3) * 0.24f;
                    if (Mathf.Abs(v - 0.5f) < 0.05f) f *= 0.7f;
                    if (((wx + wy * 3) & 15) == 0 && ((n >> 8) & 3) == 0) f *= 0.8f;
                    if (region == RegionKind.MaDao && ((n >> 8) & 255) < 2) return Ember;
                    return SpriteLibrary.Shade(c, f);
                }

                case Terrain.Beach:
                {
                    int s = wy + (int)(Value(seed + 22u, wx, wy, 3) * 5f);
                    if (s % 5 == 0) f *= 1.05f;
                    if (((n >> 8) & 255) < 2) return Shell;
                    return SpriteLibrary.Shade(c, f);
                }

                case Terrain.Farmland:
                {
                    // Patchwork: every 3×3-cell plot grows its own crop, rows running one way or the other.
                    int bx = (wx >> 3) / 3, by = (wy >> 3) / 3;
                    uint ph = Rand(seed + 23u, bx, by);
                    var crop = (ph & 3) == 0 ? Wheat : (ph & 3) == 1 ? Rice : (ph & 3) == 2 ? c : Fallow;
                    bool across = (ph & 4) != 0;
                    bool furrow = ((across ? wy : wx) & 3) == 0;
                    if ((wx % 24) == 0 || (wy % 24) == 0) return SpriteLibrary.Shade(Soil, f * 0.85f); // field banks
                    return SpriteLibrary.Shade(furrow ? Soil : Color32.Lerp(c, crop, 0.6f), f);
                }

                case Terrain.Lava:
                {
                    // A dark crust broken into plates, molten light in the seams.
                    float cr = Crack(seed + 24u, wx, wy, 7);
                    if (cr < 1.1f) return Color32.Lerp(Glow, c, cr / 1.1f);
                    return SpriteLibrary.Shade(Color32.Lerp(c, Crust, 0.55f), f);
                }

                case Terrain.DeepOcean:
                case Terrain.Ocean:
                {
                    // Short wave crests catching the light.
                    if ((wy + (wx >> 2)) % 9 == 0 && ((n >> 8) & 3) == 0) f *= 1.07f;
                    if (((n >> 8) & 1023) < 2) f *= 1.25f;
                    return SpriteLibrary.Shade(c, f);
                }

                case Terrain.Shallow:
                {
                    // Caustics over the sandy bottom.
                    float v = Value(seed + 25u, wx, wy, 2);
                    if (Mathf.Abs(v - 0.5f) < 0.06f) f *= 1.1f;
                    return SpriteLibrary.Shade(c, f);
                }

                case Terrain.River:
                {
                    if (((wx + wy) % 5 == 0) && ((n >> 8) & 3) == 0) f *= 1.1f;
                    return SpriteLibrary.Shade(c, f);
                }
            }
            return SpriteLibrary.Shade(c, f);
        }

        // ---------------------------------------------------------------- vết tích

        static readonly Color32[] ScarColor =
        {
            new Color32(0, 0, 0, 0),
            new Color32(34, 28, 26, 255),    // cháy sém
            new Color32(170, 166, 160, 255), // tro núi lửa
            new Color32(56, 46, 42, 255),    // hố
            new Color32(66, 52, 46, 255),    // khe nứt
            new Color32(60, 58, 68, 255),    // đá nham
            new Color32(112, 70, 56, 255),   // chiến trường
            new Color32(118, 94, 62, 255),   // phù sa
            new Color32(198, 170, 110, 255), // đất nứt nẻ
            new Color32(138, 110, 78, 255),  // giày xéo
        };

        static readonly float[] ScarAmount = { 0f, 0.88f, 0.65f, 0.85f, 0.35f, 0.85f, 0.5f, 0.55f, 0.55f, 0.45f };
        static readonly Color32 Bone = new Color32(234, 228, 208, 255);
        static readonly Color32 Rust = new Color32(132, 76, 52, 255);
        static readonly Color32 Blood = new Color32(96, 30, 28, 255);
        static readonly Color32 Chasm = new Color32(26, 20, 18, 255);
        static readonly Color32 Rubble = new Color32(152, 128, 104, 255);

        // One pixel of scarred ground. The cell's own mark, or a neighbour's bleeding in over the last two pixels
        // toward it; the strength jitters per pixel so a fading scar breaks up instead of dimming evenly.
        public static Color32 Scarred(Color32 c, byte own, byte l, byte r, byte b, byte t, int px, int py, int wx, int wy, uint n, uint seed)
        {
            byte s = own;
            if (px < 2) s = Bleed(s, l, px, n);
            else if (px > 5) s = Bleed(s, r, 7 - px, n);
            if (py < 2) s = Bleed(s, b, py, n >> 2);
            else if (py > 5) s = Bleed(s, t, 7 - py, n >> 2);
            if (s == 0) return c;
            var kind = ScarInfo.Kind(s);
            int str = Mathf.Min(15, ScarInfo.Strength(s) + (int)((n >> 4) & 3) - 1);
            if (str <= 0) return c;
            return PaintScar(kind, str, ScarTint(kind, str, c), wx, wy, n, seed);
        }

        static byte Bleed(byte own, byte nb, int dist, uint n)
        {
            if (nb == 0) return own;
            int sn = ScarInfo.Strength(nb) - 3 - dist * 3;
            if (sn <= ScarInfo.Strength(own) || (int)((n >> 6) & 3) <= dist) return own;
            return ScarInfo.Pack(ScarInfo.Kind(nb), sn);
        }

        // The scar's colour over the whole cell (also what the far map shows).
        public static Color32 ScarTint(ScarKind kind, int strength, Color32 c) =>
            Color32.Lerp(c, ScarColor[(int)kind], ScarAmount[(int)kind] * strength / 15f);

        // Its detail up close. Details thin out as the scar fades: a pixel keeps its speck while its hash is
        // under the remaining strength.
        public static Color32 PaintScar(ScarKind kind, int s, Color32 c, int wx, int wy, uint n, uint seed)
        {
            bool on = ((n >> 24) & 15) < s;
            switch (kind)
            {
                case ScarKind.Scorch:
                    if (s >= 12 && ((n >> 12) & 255) < 2) return Color32.Lerp(c, Ember, 0.8f); // still smouldering
                    if (!on) return c;
                    if (((n >> 12) & 15) == 0) return SpriteLibrary.Shade(c, 0.6f);  // burnt stubble
                    if (((n >> 16) & 31) == 0) return SpriteLibrary.Shade(c, 1.35f); // flakes of ash
                    return c;

                case ScarKind.Ash:
                    if (Value(seed + 50u, wx, wy, 3) > 0.62f) c = SpriteLibrary.Shade(c, 1.1f); // drifts
                    return on && ((n >> 12) & 31) == 0 ? SpriteLibrary.Shade(c, 0.75f) : c;

                case ScarKind.Crater:
                    if (s >= 13 && Crack(seed + 51u, wx, wy, 4) < 0.8f) return SpriteLibrary.Shade(c, 0.6f); // shattered floor
                    if (s <= 11) return Color32.Lerp(c, Rubble, ((n >> 12) & 3) == 0 ? 0.75f : 0.5f); // the thrown-up rim
                    return c;

                case ScarKind.Fissure:
                    if (Crack(seed + 52u, wx, wy, 4) < 0.25f + s * 0.06f) return Color32.Lerp(c, Chasm, 0.5f + s * 0.03f);
                    return c;

                case ScarKind.Basalt:
                    if (Crack(seed + 53u, wx, wy, 5) < 0.7f) return SpriteLibrary.Shade(c, 1.25f); // seams between the columns
                    return SpriteLibrary.Shade(c, 0.92f + Value(seed + 54u, wx, wy, 2) * 0.14f);

                case ScarKind.Battlefield:
                {
                    uint h = (n >> 12) & 255;
                    if (on && h < 3) return Bone;
                    if (on && h < 6) return Rust; // broken blades
                    float v = Value(seed + 55u, wx, wy, 2);
                    return v > 0.72f ? Color32.Lerp(c, Blood, 0.45f * s / 15f) : c;
                }

                case ScarKind.Silt:
                    if ((wy + (int)(Value(seed + 56u, wx, wy, 3) * 4f)) % 4 == 0) return SpriteLibrary.Shade(c, 0.9f);
                    return on && ((n >> 12) & 63) == 0 ? SpriteLibrary.Shade(c, 1.25f) : c; // a puddle catching the light

                case ScarKind.Parched:
                    return on && Crack(seed + 57u, wx, wy, 5) < 0.7f ? SpriteLibrary.Shade(c, 0.7f) : c;

                case ScarKind.Trampled:
                    return on && ((n >> 12) & 15) == 0 ? SpriteLibrary.Shade(c, 0.75f) : c; // hoof prints
            }
            return c;
        }

        // ---------------------------------------------------------------- per cell

        // Broad patches at cell scale, so the textures still read on the far map (one pixel per cell).
        public static float Macro(Terrain t, int x, int y, uint seed)
        {
            float v = Value(seed + 40u, x, y, 3);
            switch (t)
            {
                case Terrain.Desert:
                {
                    float d = y + x * 0.4f + v * 8f;
                    float ph = d - Mathf.Floor(d / 5f) * 5f;
                    return ph < 1.5f ? 1.06f : ph < 3f ? 0.95f : 1f;
                }
                case Terrain.Badlands:
                {
                    int band = Mathf.FloorToInt((y + v * 4f) / 2f);
                    return 0.9f + (Rand(seed + 41u, band, 0) & 255) / 255f * 0.18f;
                }
                case Terrain.Ashland: return 0.88f + v * 0.2f;
                case Terrain.Forest:
                case Terrain.Jungle: return 0.9f + v * 0.16f;
                case Terrain.Grass:
                case Terrain.Savanna:
                case Terrain.Tundra: return 0.94f + v * 0.12f;
                case Terrain.Mountain:
                case Terrain.Hills: return 0.9f + Value(seed + 42u, x, y, 1) * 0.2f;
                default: return 1f;
            }
        }

        // ---------------------------------------------------------------- noise

        // Smooth value noise over a lattice of 2^shift pixels; 0..1.
        static float Value(uint seed, int x, int y, int shift)
        {
            int size = 1 << shift, mask = size - 1;
            int gx = x >> shift, gy = y >> shift;
            float fx = ((x & mask) + 0.5f) / size, fy = ((y & mask) + 0.5f) / size;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Lat(seed, gx, gy), b = Lat(seed, gx + 1, gy);
            float c = Lat(seed, gx, gy + 1), d = Lat(seed, gx + 1, gy + 1);
            float top = a + (b - a) * fx, bottom = c + (d - c) * fx;
            return top + (bottom - top) * fy;
        }

        static float Lat(uint seed, int x, int y) => (Rand(seed, x, y) & 1023) * (1f / 1023f);

        // Lattice randomness from a fixed 256×256 table instead of hashing: a chunk samples noise ~10 times per
        // pixel, and hashing made each chunk cost ~34 ms. The seed only shifts the window, so the table repeats
        // every 256 lattice steps (at least 128 cells for the finest noise), which the eye never catches.
        static readonly uint[] Table = BuildTable();

        static uint[] BuildTable()
        {
            var t = new uint[256 * 256];
            for (int i = 0; i < t.Length; i++) t[i] = Hash.U32(0x7E87u, i & 255, i >> 8);
            return t;
        }

        static uint Rand(uint seed, int x, int y)
        {
            uint s = seed * 0x9E3779B1u; // a different window per pattern and per world
            return Table[((x + (int)(s >> 24)) & 255) | (((y + (int)(s >> 16)) & 255) << 8)] ^ s;
        }

        // Cheap Worley F2−F1 over a jittered grid of `size` pixels: near 0 on the cracks between plates.
        static float Crack(uint seed, int x, int y, int size)
        {
            int gx = x / size, gy = y / size;
            float best = 1e9f, second = 1e9f;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                uint h = Rand(seed, gx + dx, gy + dy);
                float px = (gx + dx) * size + (h & 255) / 255f * size;
                float py = (gy + dy) * size + ((h >> 8) & 255) / 255f * size;
                float ex = px - x - 0.5f, ey = py - y - 0.5f;
                float d = ex * ex + ey * ey;
                if (d < best) { second = best; best = d; }
                else if (d < second) second = d;
            }
            return Mathf.Sqrt(second) - Mathf.Sqrt(best);
        }

        // A two-pixel blade of grass rooted at a hashed pixel; `tip` is its lit upper pixel.
        static bool Tuft(uint seed, int wx, int wy, uint n, uint rarity, out bool tip)
        {
            tip = false;
            if (((n >> 8) & rarity) == 0) return true;
            if ((Hash.U32(seed, wx, wy - 1) >> 8 & rarity) == 0) { tip = true; return true; }
            return false;
        }
    }
}
