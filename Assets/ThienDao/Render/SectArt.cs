using ThienDao.Core;
using ThienDao.Sim;
using UnityEngine;

namespace ThienDao.Render
{
    // Sơn môn drawn (devlog 38): the whole complex of a sect painted onto one canvas, 8 px a cell, top down with the
    // fronts of the buildings showing (roof above, wall, red pillars and door below), on one north–south axis. Only
    // the modules that stand are painted; those in ruin are bleached, broken and grown over with moss. A righteous
    // sect has jade roofs, cream walls and gold ridges; a ma môn blood-dark roofs and grey stone; a thánh địa's main
    // hall is roofed in gold, a tiểu môn phái's in plain slate. Pixel art only: flat colours, hard edges.
    public static class SectArt
    {
        public const int Px = 8;
        public static int Width => (FactionSystem.LayoutX1 - FactionSystem.LayoutX0 + 1) * Px;
        public static int Height => (FactionSystem.LayoutY1 - FactionSystem.LayoutY0 + 1) * Px;

        struct Pal
        {
            public Color32 Roof, RoofDark, RoofLight, Ridge, Wall, WallShade, Pillar, Door, Pave, PaveLine, WallRed, Step;
        }

        static Color32 C(byte r, byte g, byte b) => new Color32(r, g, b, 255);

        static Pal Palette(bool demonic, byte tier)
        {
            if (demonic)
                return new Pal
                {
                    Roof = C(116, 30, 42), RoofDark = C(66, 14, 24), RoofLight = C(156, 48, 58), Ridge = C(206, 196, 176),
                    Wall = C(92, 82, 92), WallShade = C(64, 56, 66), Pillar = C(34, 26, 32), Door = C(18, 10, 14),
                    Pave = C(100, 92, 100), PaveLine = C(74, 66, 74), WallRed = C(70, 44, 54), Step = C(120, 112, 118)
                };
            var p = new Pal
            {
                Roof = C(60, 158, 148), RoofDark = C(34, 108, 102), RoofLight = C(112, 204, 188), Ridge = C(238, 198, 92),
                Wall = C(240, 226, 198), WallShade = C(206, 188, 158), Pillar = C(182, 52, 44), Door = C(74, 42, 30),
                Pave = C(218, 210, 190), PaveLine = C(188, 178, 158), WallRed = C(190, 66, 54), Step = C(232, 226, 212)
            };
            if (tier == FactionSystem.TierMinor) { p.Roof = C(104, 122, 140); p.RoofDark = C(70, 84, 100); p.RoofLight = C(140, 158, 176); p.Ridge = C(200, 186, 150); }
            return p;
        }

        static Pal Golden(Pal p)
        {
            p.Roof = C(226, 176, 66);
            p.RoofDark = C(176, 124, 38);
            p.RoofLight = C(252, 214, 118);
            p.Ridge = C(255, 244, 196);
            return p;
        }

        // The whole complex, bottom row first (Texture2D order).
        public static Color32[] Compose(uint built, uint ruined, byte tier, bool demonic, uint seed)
        {
            int w = Width, h = Height;
            var px = new Color32[w * h];
            var pal = Palette(demonic, tier);
            var layout = FactionSystem.Layout;
            // Rings first, then grounds, then buildings, so the buildings sit on top.
            int[] order = { 15, 4, 8, 9, 16, 6, 10, 11, 2, 3, 0, 1, 5, 7, 13, 14, 12 };
            foreach (int m in order)
            {
                if (m >= layout.Length || !FactionSystem.Has(built, m)) continue;
                var mod = layout[m];
                var hall = m == 0 && tier == FactionSystem.TierHoly && !demonic ? Golden(pal) : pal;
                Paint(px, w, h, mod, m, hall, pal, seed);
                if (FactionSystem.Has(ruined, m)) Ruin(px, w, h, mod, seed + (uint)m * 977u);
            }
            return px;
        }

        // ---------------------------------------------------------------- modules

        static void Paint(Color32[] px, int w, int h, SectModule mod, int index, Pal p, Pal plain, uint seed)
        {
            int x0 = (mod.X0 - FactionSystem.LayoutX0) * Px, y0 = (mod.Y0 - FactionSystem.LayoutY0) * Px;
            int x1 = (mod.X1 - FactionSystem.LayoutX0 + 1) * Px - 1, y1 = (mod.Y1 - FactionSystem.LayoutY0 + 1) * Px - 1;
            var cv = new Cv { P = px, W = w, H = h, Seed = seed };
            switch (mod.Part)
            {
                case SectPart.Hall:
                    cv.Pave(x0, y0, x1, y1, plain);
                    // The main hall: double eaves on a raised base, the incense burner and two steles before it.
                    cv.Building(x0 + 2 * Px, y0 + 5 * Px, x1 - 2 * Px, y1 - Px / 2, p, true);
                    cv.Burner((x0 + x1) / 2, y0 + 2 * Px + 2);
                    cv.Stele(x0 + 3 * Px, y0 + 3 * Px, plain);
                    cv.Stele(x1 - 3 * Px - 4, y0 + 3 * Px, plain);
                    break;
                case SectPart.Gate:
                    // The road from the gate to the hall, and the gate itself with its guardian lions.
                    cv.Pave((x0 + x1) / 2 - 10, y0 + 3 * Px, (x0 + x1) / 2 + 10, y1, plain);
                    cv.Building(x0 + 4, y0, x1 - 4, y0 + 4 * Px - 1, p, false, true);
                    cv.Lion(x0, y0 + 2);
                    cv.Lion(x1 - 5, y0 + 2);
                    break;
                case SectPart.Dorm:
                    cv.Pave(x0, y0, x1, y1, plain);
                    for (int hy = y0 + 4; hy + 2 * Px <= y1; hy += 3 * Px)
                    for (int hx = x0 + 4; hx + 2 * Px + 4 <= x1; hx += 3 * Px - 2)
                        cv.Building(hx, hy, hx + 2 * Px + 2, hy + 2 * Px, p, false);
                    break;
                case SectPart.InnerWall:
                case SectPart.OuterWall:
                    cv.WallRing(x0, y0, x1, y1, p, mod.Part == SectPart.OuterWall);
                    break;
                case SectPart.Scripture:
                    cv.Pave(x0, y0, x1, y1, plain);
                    cv.Building(x0 + Px, y0 + Px / 2, x1 - Px, y1, p, true);
                    break;
                case SectPart.Training:
                    cv.Training(x0, y0, x1, y1, plain);
                    break;
                case SectPart.Alchemy:
                    cv.Pave(x0, y0, x1, y1, plain);
                    cv.Building(x0 + Px / 2, y0 + 3 * Px, x1 - Px / 2, y1, p, false);
                    cv.Cauldron((x0 + x1) / 2, y0 + Px + 2);
                    break;
                case SectPart.Field:
                    cv.Field(x0, y0, x1, y1);
                    break;
                case SectPart.Formation:
                    cv.Formation((x0 + x1) / 2, (y0 + y1) / 2, Mathf.Min(x1 - x0, y1 - y0) / 2 - 1, plain);
                    break;
                case SectPart.Pagoda:
                    cv.Pagoda(x0, y0, x1, y1, p);
                    break;
                case SectPart.Elder:
                    cv.Pave(x0, y0, x1, y1 - 2 * Px, plain);
                    cv.Building(x0 + Px / 2, y0 + 2 * Px, x1 - Px / 2, y1, p, false);
                    cv.Tree(x0 + 6, y0 + 4, true);
                    cv.Tree(x1 - 12, y0 + 4, true);
                    break;
                case SectPart.Pond:
                    cv.Pond(x0, y0, x0 + 5 * Px - 1, y1);
                    cv.Pond(x1 - 5 * Px + 1, y0, x1, y1);
                    cv.Tree((x0 + x1) / 2 - 4, y0 + 4, false);
                    break;
            }
        }

        // Bleached and darkened, holes in the roofs, moss in the cracks.
        static void Ruin(Color32[] px, int w, int h, SectModule mod, uint seed)
        {
            int x0 = (mod.X0 - FactionSystem.LayoutX0) * Px, y0 = (mod.Y0 - FactionSystem.LayoutY0) * Px;
            int x1 = (mod.X1 - FactionSystem.LayoutX0 + 1) * Px - 1, y1 = (mod.Y1 - FactionSystem.LayoutY0 + 1) * Px - 1;
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(h - 1, y1); y++)
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(w - 1, x1); x++)
            {
                var c = px[y * w + x];
                if (c.a == 0) continue;
                uint r = Hash.U32(seed, x >> 1, y >> 1) % 23;
                if (r == 0) { px[y * w + x] = default; continue; }           // fallen away: the ground shows
                if (r < 4) { px[y * w + x] = C(84, 110, 64); continue; }     // moss and grass
                if (r == 4) { px[y * w + x] = C(36, 32, 30); continue; }     // a hole, soot
                byte g = (byte)((c.r + c.g + c.b) / 3);
                var grey = new Color32(g, g, g, 255);
                px[y * w + x] = SpriteLibrary.Shade(Color32.Lerp(c, grey, 0.6f), 0.78f);
            }
        }

        // ---------------------------------------------------------------- drawing primitives

        sealed class Cv
        {
            public Color32[] P;
            public int W, H;
            public uint Seed;

            void Set(int x, int y, Color32 c)
            {
                if ((uint)x < (uint)W && (uint)y < (uint)H) P[y * W + x] = c;
            }

            void Rect(int x0, int y0, int x1, int y1, Color32 c)
            {
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    Set(x, y, c);
            }

            // Flagstones: pale slabs in a staggered grid.
            public void Pave(int x0, int y0, int x1, int y1, Pal p)
            {
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int row = (y - y0) / 4;
                    bool line = (y - y0) % 4 == 0 || ((x - x0 + (row & 1) * 3) % 6) == 0;
                    var c = line ? p.PaveLine : p.Pave;
                    if (!line && Hash.U32(Seed, x, y) % 11 == 0) c = SpriteLibrary.Shade(c, 0.94f);
                    Set(x, y, c);
                }
            }

            // A hall seen from the front and above: steps, a cream wall with red pillars, a door and lattice windows,
            // then the tiled roof with dark eaves, a gold ridge and upturned corners; a double-eaved one has a second,
            // narrower roof above. A gate has a wide dark arch for a door.
            public void Building(int x0, int y0, int x1, int y1, Pal p, bool doubleEaves, bool gate = false)
            {
                int wpx = x1 - x0 + 1, hpx = y1 - y0 + 1;
                int wallH = Mathf.Max(5, hpx * 38 / 100), steps = 2;
                // Wall
                Rect(x0 + 2, y0, x1 - 2, y0 + steps - 1, p.Step);
                Rect(x0 + 3, y0 + steps, x1 - 3, y0 + wallH, p.Wall);
                Rect(x0 + 3, y0 + steps, x1 - 3, y0 + steps, p.WallShade);
                for (int x = x0 + 3; x <= x1 - 3; x += 6) Rect(x, y0 + steps, x, y0 + wallH, p.Pillar);
                int door = gate ? Mathf.Max(8, wpx / 3) : Mathf.Max(4, wpx / 7), dx = (x0 + x1) / 2 - door / 2;
                Rect(dx, y0 + steps, dx + door - 1, y0 + Mathf.Min(wallH, steps + (gate ? wallH - 1 : 6)), p.Door);
                if (gate) Rect(dx, y0 + wallH - 1, dx + door - 1, y0 + wallH, p.Pillar); // the lintel
                for (int x = x0 + 6; x < x1 - 5; x += 6)
                    if (x + 2 < dx || x > dx + door) Rect(x, y0 + steps + 3, x + 2, y0 + steps + 4, p.Door); // lattice windows
                // Roof
                int ry0 = y0 + wallH + 1, ry1 = doubleEaves ? y0 + wallH + (y1 - y0 - wallH) * 55 / 100 : y1;
                Roof(x0, ry0, x1, ry1, p);
                if (doubleEaves)
                {
                    int inset = wpx / 6;
                    Rect(x0 + inset + 2, ry1 + 1, x1 - inset - 2, ry1 + 2, p.Wall);
                    for (int x = x0 + inset + 2; x <= x1 - inset - 2; x += 5) Set(x, ry1 + 1, p.Pillar);
                    Roof(x0 + inset, ry1 + 3, x1 - inset, y1, p);
                }
            }

            void Roof(int x0, int y0, int x1, int y1, Pal p)
            {
                if (y1 <= y0) return;
                for (int y = y0; y <= y1; y++)
                {
                    int t = y - y0, span = y1 - y0;
                    int inset = t * 2 / 3; // the roof narrows toward the ridge
                    for (int x = x0 + inset; x <= x1 - inset; x++)
                    {
                        Color32 c = t <= 1 ? p.RoofDark : (t & 1) == 0 ? p.Roof : p.RoofLight;
                        if (((x - x0) % 3) == 0 && t > 1) c = SpriteLibrary.Shade(c, 0.9f);
                        if (t == span) c = p.Ridge;
                        Set(x, y, c);
                    }
                }
                // Upturned gold corners on the eaves, and a gold ornament at each end of the ridge.
                Set(x0, y0 + 1, p.Ridge);
                Set(x0 - 1, y0 + 2, p.Ridge);
                Set(x1, y0 + 1, p.Ridge);
                Set(x1 + 1, y0 + 2, p.Ridge);
                int top = (y1 - y0) * 2 / 3;
                Set(x0 + top - 1, y1 + 1, p.Ridge);
                Set(x1 - top + 1, y1 + 1, p.Ridge);
            }

            public void WallRing(int x0, int y0, int x1, int y1, Pal p, bool outer)
            {
                int t = outer ? 5 : 4;
                void Segment(int ax0, int ay0, int ax1, int ay1)
                {
                    Rect(ax0, ay0, ax1, ay1, p.Wall);
                    Rect(ax0, ay0, ax1, ay0 + 1, p.WallRed);
                    Rect(ax0, ay1, ax1, ay1, p.RoofDark);
                    Rect(ax0, ay1 - 1, ax1, ay1 - 1, p.Roof);
                }
                int mid = (x0 + x1) / 2, gap = 4 * Px; // the gate stands in the south wall's gap
                Segment(x0, y0, mid - gap, y0 + t);
                Segment(mid + gap, y0, x1, y0 + t);
                Segment(x0, y1 - t, x1, y1);
                Segment(x0, y0, x0 + t, y1);
                Segment(x1 - t, y0, x1, y1);
                // Corner towers.
                foreach (var (cx, cy) in new[] { (x0, y0), (x1 - 2 * Px + 1, y0), (x0, y1 - 2 * Px + 1), (x1 - 2 * Px + 1, y1 - 2 * Px + 1) })
                    Building(cx, cy, cx + 2 * Px - 1, cy + 2 * Px - 1, p, false);
            }

            public void Training(int x0, int y0, int x1, int y1, Pal p)
            {
                Pave(x0, y0, x1, y1, p);
                Rect(x0, y0, x1, y0, p.WallRed);
                Rect(x0, y1, x1, y1, p.WallRed);
                Rect(x0, y0, x0, y1, p.WallRed);
                Rect(x1, y0, x1, y1, p.WallRed);
                int cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, r = Mathf.Min(x1 - x0, y1 - y0) / 3;
                for (int y = cy - r - 1; y <= cy + r + 1; y++)
                for (int x = cx - r - 1; x <= cx + r + 1; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (Mathf.Abs(d - r) < 0.6f) Set(x, y, C(250, 250, 244));
                }
                // Weapon racks along the north edge, straw dummies along the south.
                for (int x = x0 + 4; x < x1 - 4; x += 7)
                {
                    Rect(x, y1 - 5, x + 4, y1 - 5, C(120, 80, 50));
                    for (int k = 0; k < 4; k++) Rect(x + k, y1 - 4, x + k, y1 - 2, k % 2 == 0 ? C(200, 204, 214) : C(150, 100, 60));
                    Rect(x + 2, y0 + 3, x + 2, y0 + 6, C(120, 80, 50));
                    Rect(x + 1, y0 + 6, x + 3, y0 + 8, C(214, 186, 110));
                }
            }

            public void Cauldron(int cx, int y)
            {
                var bronze = C(170, 120, 60);
                var dark = C(110, 74, 36);
                Rect(cx - 6, y + 2, cx + 6, y + 7, bronze);
                Rect(cx - 6, y + 2, cx + 6, y + 2, dark);
                Rect(cx - 7, y + 7, cx + 7, y + 8, dark);
                Rect(cx - 5, y, cx - 4, y + 1, dark);
                Rect(cx + 4, y, cx + 5, y + 1, dark);
                Rect(cx - 3, y - 1, cx + 3, y, C(255, 140, 40)); // the fire under it
                Set(cx, y - 2, C(255, 220, 90));
                Rect(cx - 2, y + 4, cx + 2, y + 5, C(230, 180, 90)); // its inscription
            }

            public void Field(int x0, int y0, int x1, int y1)
            {
                var soil = C(110, 82, 56);
                var fence = C(140, 100, 64);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    bool row = ((y - y0) % 4) < 2;
                    var c = row ? C(86, 168, 80) : soil;
                    if (row && Hash.U32(Seed, x, y) % 9 == 0) c = C(120, 220, 110);
                    if (row && Hash.U32(Seed + 5u, x, y) % 41 == 0) c = C(150, 250, 240); // a spirit herb glowing
                    Set(x, y, c);
                }
                Rect(x0, y0, x1, y0, fence);
                Rect(x0, y1, x1, y1, fence);
                Rect(x0, y0, x0, y1, fence);
                Rect(x1, y0, x1, y1, fence);
                for (int y = y0 + 8; y < y1; y += 16) Rect(x0 - 1, y, x1 + 1, y, fence); // paths between the plots
            }

            public void Formation(int cx, int cy, int r, Pal p)
            {
                var stone = C(176, 176, 184);
                var rune = C(110, 230, 230);
                for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d > r) continue;
                    Color32 c = d > r - 1.2f ? C(120, 120, 130) : stone;
                    if (Mathf.Abs(d - r * 0.7f) < 0.6f || Mathf.Abs(d - r * 0.4f) < 0.6f) c = rune;
                    float a = Mathf.Atan2(y - cy, x - cx);
                    if (d < r * 0.7f && d > r * 0.4f && Mathf.Abs(Mathf.Repeat(a * 8f / (Mathf.PI * 2f), 1f) - 0.5f) < 0.06f) c = rune; // the eight trigrams
                    if (d < 2f) c = C(240, 240, 250);
                    Set(x, y, c);
                }
                for (int k = 0; k < 8; k++) // pillars round the platform
                {
                    float a = k * Mathf.PI / 4f;
                    int px = cx + Mathf.RoundToInt(Mathf.Cos(a) * (r + 2)), py = cy + Mathf.RoundToInt(Mathf.Sin(a) * (r + 2));
                    Rect(px - 1, py - 1, px + 1, py + 2, p.Pillar);
                    Set(px, py + 3, p.Ridge);
                }
            }

            public void Pagoda(int x0, int y0, int x1, int y1, Pal p)
            {
                int cx = (x0 + x1) / 2, hpx = y1 - y0 + 1;
                int tiers = 5, th = (hpx - 6) / tiers;
                for (int k = 0; k < tiers; k++)
                {
                    int half = (x1 - x0) / 2 - k * 3, ty = y0 + k * th;
                    Rect(cx - half + 3, ty, cx + half - 3, ty + th / 2, p.Wall);
                    Rect(cx - 1, ty, cx + 1, ty + th / 2 - 1, p.Door);
                    for (int x = cx - half + 3; x <= cx + half - 3; x += 4) Rect(x, ty, x, ty + th / 2, p.Pillar);
                    Roof(cx - half, ty + th / 2 + 1, cx + half, ty + th - 1, p);
                }
                Rect(cx, y0 + tiers * th, cx, y1, p.Ridge); // the finial
                Rect(cx - 1, y1 - 2, cx + 1, y1 - 1, p.Ridge);
            }

            public void Pond(int x0, int y0, int x1, int y1)
            {
                var rim = C(226, 218, 196);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    bool edge = x == x0 || x == x1 || y == y0 || y == y1;
                    var c = edge ? rim : (((x + y * 3) & 7) == 0 ? C(120, 200, 226) : C(70, 160, 200));
                    uint hh = Hash.U32(Seed + 9u, x >> 1, y >> 1);
                    if (!edge && hh % 9 == 0) c = C(74, 150, 80);  // lotus pads
                    if (!edge && hh % 37 == 0) c = C(255, 160, 196); // lotus flowers
                    Set(x, y, c);
                }
            }

            public void Tree(int x, int y, bool peach)
            {
                var trunk = C(110, 74, 50);
                var leaf = peach ? C(255, 170, 200) : C(70, 170, 120);
                var light = peach ? C(255, 214, 230) : C(120, 210, 160);
                Rect(x + 2, y, x + 3, y + 4, trunk);
                for (int dy = 0; dy < 7; dy++)
                for (int dx = 0; dx < 7; dx++)
                {
                    float d = Mathf.Sqrt((dx - 3) * (dx - 3) + (dy - 3) * (dy - 3));
                    if (d <= 3.4f) Set(x - 1 + dx, y + 4 + dy, d < 1.6f && dy > 3 ? light : leaf);
                }
            }

            public void Burner(int cx, int y)
            {
                var bronze = C(160, 112, 56);
                Rect(cx - 2, y, cx + 2, y + 3, bronze);
                Rect(cx - 3, y + 3, cx + 3, y + 3, C(110, 74, 36));
                Set(cx - 2, y - 1, bronze);
                Set(cx + 2, y - 1, bronze);
            }

            public void Stele(int x, int y, Pal p)
            {
                Rect(x, y, x + 3, y + 7, C(150, 150, 156));
                Rect(x + 1, y + 2, x + 2, y + 6, C(110, 110, 118));
                Rect(x - 1, y, x + 4, y, C(120, 120, 128));
            }

            public void Lion(int x, int y)
            {
                var s = C(170, 170, 176);
                Rect(x, y, x + 4, y + 2, C(120, 120, 128));
                Rect(x + 1, y + 3, x + 3, y + 6, s);
                Rect(x, y + 6, x + 4, y + 8, s);
                Set(x + 1, y + 7, C(40, 40, 46));
                Set(x + 3, y + 7, C(40, 40, 46));
            }
        }
    }
}
